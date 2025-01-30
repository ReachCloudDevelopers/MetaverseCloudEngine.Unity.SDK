using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MetaverseCloudEngine.ApiClient.Controllers;
using MetaverseCloudEngine.Common.Models.Forms;
using MetaverseCloudEngine.Common.Models.QueryParams;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Unity.Assets;
using MetaverseCloudEngine.Unity.Assets.MetaPrefabs;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using MetaverseCloudEngine.Common.Enumerations;
#pragma warning disable CS0162 // Unreachable code detected

namespace MetaverseCloudEngine.Unity.Editors
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(MetaPrefab))]
    public class MetaPrefabEditor : AssetEditor<MetaPrefab, MetaPrefabMetadata, PrefabDto, PrefabQueryParams, PrefabUpsertForm, MetaPrefabPickerEditor>
    {
        private static bool _askedToRevertPrefabs;
        private static bool _revertPrefabs;

        protected override object GetMainAsset(MetaPrefab asset) => asset ? asset.gameObject : null;
        protected override bool AllowUpdateMetadata => true;
        public override AssetController<PrefabDto, PrefabQueryParams, PrefabUpsertForm> Controller => MetaverseProgram.ApiClient.Prefabs;

        protected override Texture2D AutoCaptureThumbnail(MetaPrefab asset)
        {
            if (asset == null || (asset.MetaData.Listings == AssetListings.Unlisted && !asset.MetaData.isAvatar))
                return base.AutoCaptureThumbnail(asset);

            var assetGameObject = asset.gameObject;
            var prefab = PrefabUtility.GetCorrespondingObjectFromSource(assetGameObject);
            var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(assetGameObject);
            Texture2D texture;

            if (!string.IsNullOrEmpty(path) && (texture = TryLoadAssetPreviewSynchronously(AssetDatabase.LoadAssetAtPath<GameObject>(path))))
                return texture;

            if (prefab != null && (texture = TryLoadAssetPreviewSynchronously(prefab)) != null)
                return texture;

            if (EditorUtility.IsPersistent(assetGameObject))
                return TryLoadAssetPreviewSynchronously(assetGameObject);

            return base.AutoCaptureThumbnail(asset);
        }

        private static Texture2D TryLoadAssetPreviewSynchronously(UnityEngine.Object o)
        {
            int counter = 0;
            Texture2D thumbnail = null;
            while ((thumbnail == null && counter < 75) || AssetPreview.IsLoadingAssetPreviews())
            {
                thumbnail = AssetPreview.GetAssetPreview(o);
                counter++;
                Thread.Sleep(15);
            }

            return thumbnail;
        }

        protected override void DrawUploadControls()
        {
            var sourceMetaPrefab = Target.transform.parent ? Target.transform.root.GetComponents<MetaPrefab>().FirstOrDefault() ?? Target : Target;
            if (!EditorUtility.IsPersistent(sourceMetaPrefab))
            {
                EditorGUILayout.HelpBox("Meta prefabs can only be uploaded when selected from the project window. If this is a sub-prefab then select the root object of the hierarchy.", MessageType.Warning);

                GameObject prefab = null;

                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage != null && File.Exists(stage.assetPath))
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(stage.assetPath);

                if (prefab == null)
                    prefab = PrefabUtility.GetCorrespondingObjectFromSource(sourceMetaPrefab.gameObject);

                if (prefab != null && GUILayout.Button("Select Source Prefab"))
                {
                    EditorGUIUtility.PingObject(prefab);
                    Selection.activeObject = prefab;
                }

                return;
            }

            base.DrawUploadControls();
        }

        protected override PrefabUpsertForm GetUpsertForm(Guid? id, MetaPrefab asset, bool willUpload)
        {
            if (!EditorUtility.IsPersistent(asset.gameObject))
                throw new Exception("The object you're trying to upload is not a prefab.");

            var form = base.GetUpsertForm(id, asset, willUpload);
            
            form.PrefabBuildingCategory = asset.MetaData.builderCategories;
            form.IsBuildable = asset.MetaData.isBuildable;
            form.IsAvatar = asset.MetaData.isAvatar;
            form.PrefabLoadDistance = asset.MetaData.loadRange.loadDistance;
            form.PrefabUnloadDistance = asset.MetaData.loadRange.unloadDistance;

            if (willUpload)
                AssignChildPrefabForms(asset, form, true);

            return form;
        }

        protected override void OnUpdateMetaDataInternal(MetaPrefab asset, PrefabDto assetDto, SerializedObject assetSerializedObject)
        {
            var metaDataProperty = assetSerializedObject.FindProperty("metaData");

            var isBuildableProperty = metaDataProperty.FindPropertyRelative(nameof(MetaPrefabMetadata.isBuildable));
            if (isBuildableProperty.boolValue != assetDto.IsBuildable)
                isBuildableProperty.boolValue = assetDto.IsBuildable;

            var categoriesProperty = metaDataProperty.FindPropertyRelative(nameof(MetaPrefabMetadata.builderCategories));
            if (categoriesProperty.enumValueFlag != (int)assetDto.PrefabBuildingCategory)
                categoriesProperty.enumValueFlag = (int)assetDto.PrefabBuildingCategory;

            var isAvatar = metaDataProperty.FindPropertyRelative(nameof(MetaPrefabMetadata.isAvatar));
            if (isAvatar.boolValue != assetDto.IsAvatar)
                isAvatar.boolValue = assetDto.IsAvatar;

            var loadRangeProperty = metaDataProperty.FindPropertyRelative(nameof(MetaPrefabMetadata.loadRange));
            var loadDistanceProperty = loadRangeProperty.FindPropertyRelative(nameof(ObjectLoadRange.loadDistance));
            var unloadDistanceProperty = loadRangeProperty.FindPropertyRelative(nameof(ObjectLoadRange.unloadDistance));

            if (!Mathf.Approximately(loadDistanceProperty.floatValue, assetDto.PrefabLoadDistance))
                loadDistanceProperty.floatValue = assetDto.PrefabLoadDistance;
            if (!Mathf.Approximately(unloadDistanceProperty.floatValue, assetDto.PrefabUnloadDistance))
                unloadDistanceProperty.floatValue = assetDto.PrefabUnloadDistance;

            var childPrefabs = GetChildPrefabs(asset);
            if (childPrefabs.Length == 0)
                return;

            foreach (var childPrefab in childPrefabs)
            {
                var prefab = childPrefab;
                var childPrefabDto = assetDto.PrefabChildren.FirstOrDefault(
                    x => (prefab.ID == null && x.Name == prefab.name) || prefab.ID == x.Id);

                if (childPrefabDto == null)
                    continue;

                var childPrefabSerializedObject = new SerializedObject(childPrefab);
                ApplyMetaData(childPrefabSerializedObject, childPrefabDto);
            }
        }

        protected override void OnClearMetaDataInternal(MetaPrefab asset)
        {
            var childPrefabs = GetChildPrefabs(asset);
            if (childPrefabs.Length == 0)
                return;

            if (!EditorUtility.DisplayDialog("Unlink Asset ID", "Would you also like to unlink the child Meta Prefab IDs?", "Yes", "No"))
                return;

            foreach (var childPrefab in childPrefabs)
            {
                var childPrefabSerializedObject = new SerializedObject(childPrefab);
                ApplyMetaData(childPrefabSerializedObject, null);
            }
        }

        protected override void OnDeleteInternal(MetaPrefab asset)
        {
            var childPrefabs = GetChildPrefabs(asset);
            if (childPrefabs.Length == 0)
                return;

            foreach (var childPrefab in childPrefabs)
            {
                var childPrefabSerializedObject = new SerializedObject(childPrefab);
                ApplyMetaData(childPrefabSerializedObject, null);
            }
        }

        protected override bool UpsertThumbnail(Texture2D thumbnail, MetaPrefab asset, PrefabDto assetDto, bool allowDelete = true)
        {
            if (!base.UpsertThumbnail(thumbnail, asset, assetDto, allowDelete))
                return false;

            var childPrefabs = GetChildPrefabs(asset);
            foreach (var childPrefab in childPrefabs)
            {
                var childPrefabDto = assetDto.PrefabChildren.FirstOrDefault(x => x.Id == childPrefab.ID);
                if (childPrefabDto == null) continue;
                base.UpsertThumbnail(thumbnail, asset, childPrefabDto, allowDelete);
            }

            return true;
        }

        protected override void OnBeforeBuildPrefab(GameObject rootPrefab)
        {
            return;
            
            base.OnBeforeBuildPrefab(rootPrefab);

            // If the prefab contains child MetaPrefabs, ask the user
            // if they want to revert modifications. This is to fix
            // a bug in Unity 2022.2.20+ where prefabs are mangled
            // on project save.
            var childMetaPrefabs = rootPrefab.GetTopLevelComponentsInChildrenOrdered<MetaPrefab>()
                .Where(x => PrefabUtility.IsPartOfPrefabInstance(x) && x.gameObject != rootPrefab)
                .ToArray();
            
            if (childMetaPrefabs.Length == 0)
                return;

            if (!_askedToRevertPrefabs)
            {
                var wantsToRevertPrefabs = EditorUtility.DisplayDialog(
                    "Revert Child Meta Prefabs",
                    "Would you like to revert any changes made to child Meta Prefabs?",
                    "Yes (recommended)",
                    "No");

                _askedToRevertPrefabs = true;

                if (!wantsToRevertPrefabs)
                {
                    _revertPrefabs = false;
                    return;
                }
            }
            else if (!_revertPrefabs)
            {
                return;
            }

            _revertPrefabs = true;
            
            foreach (var childMetaPrefab in childMetaPrefabs)
            {
                var childObj = childMetaPrefab.gameObject;
                if (childObj.gameObject == rootPrefab) continue;
                PrefabUtility.RevertPrefabInstance(childObj, InteractionMode.AutomatedAction);
            }
        }

        protected override void OnAfterBuildPrefab(GameObject prefab)
        {
            base.OnAfterBuildPrefab(prefab);

            _revertPrefabs = false;
            _askedToRevertPrefabs = false;
        }

        private void AssignChildPrefabForms(MetaPrefab asset, PrefabUpsertForm sourceForm, bool willUpload)
        {
            if (asset.transform.parent || asset.transform.root != asset.transform)
                return;

            var childPrefabs = GetChildPrefabs(asset);
            if (asset.ID is not null && willUpload)
            {
                var assetID = asset.ID.Value;
                var getAsset = Task.Run(() => Controller.FindAsync(assetID)).Result;
                if (getAsset.Succeeded)
                {
                    var existingAssetDto = Task.Run(() => getAsset.GetResultAsync()).Result;
                    var supportedPlatforms = existingAssetDto.Platforms.Aggregate(0, (platform, doc) => platform | (int)doc.Platform);
                    var missingOldPlatforms = supportedPlatforms != 0 && !asset.SupportedPlatforms.HasFlag((Platform)supportedPlatforms);
                    var hasChildren = childPrefabs.Length > 0 || existingAssetDto.PrefabChildren.Count > 0;
                    if (missingOldPlatforms && hasChildren)
                    {
                        var dialog = EditorUtility.DisplayDialogComplex(
                            "Platform Misconfiguration",
                            "Prefabs with children require you to upload for all original platforms associated with the prefab.",
                            "Upload for Appropriate Platforms (Recommended)",
                            "Proceed Anyway",
                            "Cancel Upload");
                        switch (dialog)
                        {
                            case 0:
                                asset.SupportedPlatforms |= (Platform)supportedPlatforms;
                                EditorUtility.SetDirty(asset);
                                break;
                            case 2:
                                throw new Exception("Canceled upload.");
                        }
                    }
                }
            }

            if (childPrefabs.Length == 0)
                return;

            foreach (var childPrefab in childPrefabs)
            {
                if (childPrefab.ID is not null) continue;
                childPrefab.ID = Guid.NewGuid();
                EditorUtility.SetDirty(childPrefab);
            }

            if (asset.ID != null && childPrefabs.Any(x => x.ID == asset.ID))
                throw new InvalidOperationException("The child prefab ID cannot be the same as the root.");

            var childPrefabForms = childPrefabs.Select(x => GetUpsertForm(x.ID, x, willUpload)).ToArray();
            var names = childPrefabForms.Select(x => x.Name).ToArray();
            if (childPrefabs.Any(x => names.Count(y => y == x.name) > 1))
                throw new InvalidOperationException("All child prefab names must be unique!");

            sourceForm.ChildPrefabs = childPrefabForms;

            foreach (var form in childPrefabForms)
            {
                sourceForm.PrefabBuildingCategory |= form.PrefabBuildingCategory;
                sourceForm.Listings |= form.Listings;
                sourceForm.IsBuildable |= form.IsBuildable;
            }
        }

        private static MetaPrefab[] GetChildPrefabs(MetaPrefab source)
        {
            return source.GetComponentsInChildrenOrdered<MetaPrefab>().Where(x => x != source).ToArray();
        }
    }
}