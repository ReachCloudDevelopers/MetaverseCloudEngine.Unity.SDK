using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MetaverseCloudEngine.ApiClient;
using MetaverseCloudEngine.ApiClient.Controllers;
using MetaverseCloudEngine.Common.Enumerations;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Common.Models.QueryParams;
using MetaverseCloudEngine.Unity.Async;
using UnityEditor;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors
{
    public class AssetPaginatedEditor<TAssetDto, TAssetQueryParams>
        where TAssetDto : AssetDto
        where TAssetQueryParams : AssetQueryParams, new()
    {
        private readonly IQueryAssets<TAssetDto, TAssetQueryParams> _queryController;
        private readonly IAssetController<TAssetDto> _assetController;
        private readonly PaginatedEditor<TAssetDto> _paginatedEditor;
        private readonly Dictionary<Guid, bool> _assetPlatformsExpanded = new();
        private readonly Dictionary<Guid, bool> _assetChildPrefabsExpanded = new();

        private static GUIContent _deleteIconContent;
        private static GUIContent _clipboardIconContent;
        private static GUIContent _fileIconContent;

        public AssetPaginatedEditor(
            string title,
            IQueryAssets<TAssetDto, TAssetQueryParams> queryController,
            IAssetController<TAssetDto> assetController)
        {
            _queryController = queryController;
            _assetController = assetController;
            _paginatedEditor = new PaginatedEditor<TAssetDto>(title);
            _paginatedEditor.AddButtonClicked += OnAssetViewAddButtonClicked;
            _paginatedEditor.BeginRequest += OnAssetViewBeginRequest;
            _paginatedEditor.DrawRecord += OnAssetViewDrawRecord;
            _paginatedEditor.DisplayAddButton = false;
            _paginatedEditor.Refresh();
        }

        public bool? WriteableAssetsOnly { get; set; } = true;
        public Texture Icon { get; set; }
        public Action<TAssetQueryParams> QueryParamsModifier { get; set; }

        public void Refresh()
        {
            _paginatedEditor.Refresh();
        }

        public void Draw(float? maxHeight = null)
        {
            _paginatedEditor.Draw(maxHeight);
        }

        private void OnAssetViewAddButtonClicked()
        {
            // TODO
        }

        private bool OnAssetViewBeginRequest(int offset, int count, string filter)
        {
            var qParams = new TAssetQueryParams
            {
                Count = (uint)count,
                Offset = (uint)offset,
                NameFilter = filter,
                Writeable = WriteableAssetsOnly,
                AdvancedSearch = false,
            };

            if (qParams is PrefabQueryParams pqParams && string.IsNullOrEmpty(filter))
                pqParams.ChildPrefabs = false;
            QueryParamsModifier?.Invoke(qParams);

            _queryController.GetAllAsync(qParams).ResponseThen(
                _paginatedEditor.EndRequest,
                e => _paginatedEditor.RequestError = e.ToString());

            return true;
        }

        private bool OnAssetViewDrawRecord(TAssetDto record)
        {
            if (record == null)
                return true;

            _deleteIconContent ??= EditorGUIUtility.IconContent("TreeEditor.Trash");
            _clipboardIconContent ??= EditorGUIUtility.IconContent("Clipboard", "Copy ID");
            _fileIconContent ??=
                EditorGUIUtility.IconContent(EditorGUIUtility.isProSkin ? "d_DefaultAsset Icon" : "DefaultAsset Icon");

            var recordDeleted = false;

            MetaverseEditorUtils.Box(() =>
            {
                EditorGUILayout.BeginHorizontal();

                try
                {
                    EditorGUIUtility.SetIconSize(Vector2.one * 16);

                    try
                    {
                        EditorGUILayout.LabelField(new GUIContent(record.Name, Icon, record.Name),
                            GUILayout.MinWidth(350));

                        GUILayout.FlexibleSpace();

                        if (record.Platforms.Count > 0)
                            EditorGUILayout.LabelField($"Total Size: {GetRecordTotalSize(record):F2}" + " MB",
                                EditorStyles.miniLabel, GUILayout.Width(125));
                    }
                    finally
                    {
                        EditorGUIUtility.SetIconSize(Vector2.zero);
                    }

                    EditorGUILayout.BeginHorizontal();
                    try
                    {
                        EditorGUILayout.LabelField(new GUIContent($"({record.Id.ToString()[..5]}...)"),
                            EditorStyles.miniBoldLabel, GUILayout.Width(60));
                        if (GUILayout.Button(_clipboardIconContent, GUILayout.Width(30)))
                        {
                            EditorGUIUtility.systemCopyBuffer = record.Id.ToString();
                        }
                    }
                    finally
                    {
                        EditorGUILayout.EndHorizontal();
                    }

                    if (GUILayout.Button(_deleteIconContent, EditorStyles.miniButton, GUILayout.Width(30)))
                    {
                        TypeToConfirmEditorWindow.Open(
                            $"Are you sure you want to delete '{record.Name}'? This action cannot be undone.",
                            "DELETE",
                            "Delete Forever",
                            "No",
                            () =>
                            {
                                var delete = Task.Run(async () => await _assetController.DeleteAsync(record.Id)).Result;
                                if (!delete.Succeeded)
                                {
                                    var error = Task.Run(async () => await delete.GetErrorAsync()).Result;
                                    EditorUtility.DisplayDialog("Delete Failed", error, "Ok");
                                    Debug.LogError(error);
                                    return;
                                }

                                EditorUtility.DisplayDialog("Delete Succeeded", $"{record.Name} was deleted successfully!",
                                    "Ok");
                                Debug.Log($"<b><color=green>Successfully</color></b> deleted asset '{record.Name}'.");
                                _paginatedEditor.RemoveRecord(record);
                                recordDeleted = true;
                            },
                            () => { });
                        GUIUtility.ExitGUI();
                    }
                }
                finally
                {
                    EditorGUILayout.EndHorizontal();
                }

                if (record.Platforms.Count > 0)
                {
                    MetaverseEditorUtils.Box(() =>
                    {
                        _assetPlatformsExpanded[record.Id] = EditorGUILayout.Foldout(
                            _assetPlatformsExpanded.TryGetValue(record.Id, out var expanded) && expanded,
                            "Platforms (" + record.Platforms.Count + ")");
                        if (_assetPlatformsExpanded[record.Id])
                        {
                            foreach (var platform in record.Platforms.ToArray())
                            {
                                if (platform.Document == null)
                                    continue;

                                EditorGUILayout.BeginHorizontal("box");
                                EditorGUILayout.LabelField(
                                    new GUIContent(platform.Platform.ToString(), _fileIconContent.image),
                                    GUILayout.ExpandWidth(true));
                                EditorGUILayout.LabelField($"{(double)platform.Document.Size / 1_000_000:F2}" + " MB",
                                    EditorStyles.miniLabel, GUILayout.Width(100));

                                if (GUILayout.Button(_deleteIconContent, EditorStyles.miniButton, GUILayout.Width(50)))
                                {
                                    TypeToConfirmEditorWindow.Open(
                                        $"Are you sure you want to delete '{record.Name} {platform.Platform}'? This action cannot be undone.",
                                        "DELETE",
                                        "Delete Forever",
                                        "No",
                                        () =>
                                        {
                                            var delete = Task.Run(async () =>
                                                await _assetController.DeletePlatformAsync(
                                                    record.Id,
                                                    platform.Platform)).Result;
                                            if (!delete.Succeeded)
                                            {
                                                var error = Task.Run(async () => await delete.GetErrorAsync()).Result;
                                                EditorUtility.DisplayDialog("Delete Platforms Failed", error, "Ok");
                                                Debug.LogError(error);
                                                GUIUtility.ExitGUI();
                                                return;
                                            }

                                            EditorUtility.DisplayDialog("Delete Platforms Succeeded",
                                                $"{record.Name} {platform.Platform} was deleted successfully! This action cannot be undone.",
                                                "Ok");
                                            Debug.Log(
                                                $"<b><color=green>Successfully</color></b> deleted asset platforms {platform.Platform} for '{record.Name}'.");
                                            record.Platforms.Remove(platform);
                                        },
                                        () => { });
                                    GUIUtility.ExitGUI();
                                }

                                EditorGUILayout.EndHorizontal();
                            }
                        }
                    });
                }

                if (record is PrefabDto prefab)
                {
                    if (prefab.PrefabChildren.Count > 0)
                    {
                        MetaverseEditorUtils.Box(() =>
                        {
                            _assetChildPrefabsExpanded[record.Id] = EditorGUILayout.Foldout(
                                _assetChildPrefabsExpanded.TryGetValue(record.Id, out var expanded) && expanded,
                                "Children (" + prefab.PrefabChildren.Count + ")");
                            if (_assetChildPrefabsExpanded[record.Id])
                            {
                                foreach (var child in prefab.PrefabChildren)
                                    OnAssetViewDrawRecord(child as TAssetDto);
                            }
                        });
                    }
                }
            });

            return !recordDeleted;
        }

        protected virtual double GetRecordTotalSize(AssetDto record)
        {
            return (record.Platforms.Sum(x => (double?)x.Document?.Size) ?? 0) / (1_000_000);
        }
    }
}