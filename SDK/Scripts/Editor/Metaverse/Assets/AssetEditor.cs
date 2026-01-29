using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Stopwatch = System.Diagnostics.Stopwatch;
using MetaverseCloudEngine.ApiClient;
using MetaverseCloudEngine.ApiClient.Controllers;
using MetaverseCloudEngine.ApiClient.Options;
using MetaverseCloudEngine.Common.Enumerations;
using MetaverseCloudEngine.Common.Models;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Common.Models.Forms;
using MetaverseCloudEngine.Common.Models.QueryParams;
using MetaverseCloudEngine.Unity.Assets;
using MetaverseCloudEngine.Unity.Assets.MetaSpaces;
using MetaverseCloudEngine.Unity.Async;
using Newtonsoft.Json;
using TriInspectorMVCE;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
// ReSharper disable StaticMemberInGenericType

namespace MetaverseCloudEngine.Unity.Editors
{
    public abstract class AssetEditor<TAsset, TAssetMetadata, TAssetDto, TAssetQueryParams, TAssetUpsertForm, TPickerEditor> : TriEditor
        where TAsset : Asset<TAssetMetadata>
        where TAssetMetadata : AssetMetadata, new()
        where TAssetDto : AssetDto
        where TAssetQueryParams : AssetQueryParams
        where TAssetUpsertForm : AssetUpsertForm, new()
        where TPickerEditor : PickerEditor
    {
        private GUIContent _deleteIconContent;
        private GUIContent _refreshIconContent;
        private GUIContent _detachIconContent;
        private static bool _foldoutPlatformOptions;
        private static readonly AssetBuildPlatform[] PlatformTabOrder =
        {
            AssetBuildPlatform.StandaloneWindows64,
            AssetBuildPlatform.StandaloneOSX,
            AssetBuildPlatform.StandaloneLinux64,
            AssetBuildPlatform.Android,
            AssetBuildPlatform.AndroidVR,
            AssetBuildPlatform.iOS,
            AssetBuildPlatform.WebGL,
        };

        private static readonly Dictionary<AssetBuildPlatform, (string dark, string light)> PlatformIconResourcePaths = new()
        {
            { AssetBuildPlatform.StandaloneWindows64, ("PlatformIcons/windows", "PlatformIcons/windows_light") },
            { AssetBuildPlatform.StandaloneOSX, ("PlatformIcons/apple", "PlatformIcons/apple_light") },
            { AssetBuildPlatform.StandaloneLinux64, ("PlatformIcons/linux", "PlatformIcons/linux_light") },
            { AssetBuildPlatform.Android, ("PlatformIcons/android", "PlatformIcons/android_light") },
            { AssetBuildPlatform.AndroidVR, ("PlatformIcons/meta", "PlatformIcons/meta_light") },
            { AssetBuildPlatform.iOS, ("PlatformIcons/ios", "PlatformIcons/ios_light") },
            { AssetBuildPlatform.WebGL, ("PlatformIcons/globe", "PlatformIcons/globe_light") },
        };

        private static readonly Dictionary<AssetBuildPlatform, Texture2D> PlatformIconCache = new();
        private static GUIContent[] _platformTabContents;
        private static GUIStyle _platformTabStyle;
        private static bool _lastProSkinState;

        private readonly struct PlatformPreset
        {
            internal PlatformPreset(string name, string tooltip, BundleMaxTextureResolution resolution, bool compressTextures, int textureQuality, ModelImporterMeshCompression meshCompression)
            {
                Name = name;
                Tooltip = tooltip;
                MaxTextureResolution = resolution;
                CompressTextures = compressTextures;
                TextureQuality = textureQuality;
                MeshCompression = meshCompression;
            }

            internal string Name { get; }
            internal string Tooltip { get; }
            internal BundleMaxTextureResolution MaxTextureResolution { get; }
            internal bool CompressTextures { get; }
            internal int TextureQuality { get; }
            internal ModelImporterMeshCompression MeshCompression { get; }
        }

        private static readonly PlatformPreset[] PlatformPresets =
        {
            new PlatformPreset("Low", "Optimized for quick iteration: 512px textures, high mesh compression.", BundleMaxTextureResolution._512, true, 25, ModelImporterMeshCompression.High),
            new PlatformPreset("Medium", "Balanced settings: 1024px textures, medium mesh compression.", BundleMaxTextureResolution._1024, true, 50, ModelImporterMeshCompression.Medium),
            new PlatformPreset("High", "High quality: 2048px textures, light mesh compression.", BundleMaxTextureResolution._2048, true, 75, ModelImporterMeshCompression.Low),
            new PlatformPreset("Max", "Maximum fidelity: 4096px textures, no mesh compression.", BundleMaxTextureResolution._4096, false, 100, ModelImporterMeshCompression.Off),
        };

        private SerializedProperty _idProperty;
        private SerializedProperty _blockchainTypeProperty;
        private SerializedProperty _blockchainSourceProperty;
        private SerializedProperty _supportedPlatformsProperty;
        private SerializedProperty _publishProperty;
        
        private AssetContributorEditor<TAssetDto> _contributorEditor;

        private static int _selectPlatformOption = (int)Platform.StandaloneWindows64;
        private static Dictionary<Platform, BundlePlatformOptions> _currentPlatformOptions;

        private bool _requestedThumbnail;
        private bool _hasThumbnail;
        private bool _requestingThumbnail;
        private Texture2D _thumbnailTexture;
        private static bool _foldoutThumbnail;
        private AssetDto _assetDto;
        private bool _requestedDto;
        private AssetDto _reviewVersionDto;
        private bool _requestedReviewVersionDto;

        private const string UploadSpeedPrefKey = "MetaverseCloudEngine_Unity_LastUploadSpeedBytesPerSecond";
        private const double DefaultSimulatedBytesPerSecond = 2.0 * 1024 * 1024;

        private static int _activeUploadCount;

        private const string PendingUploadSessionStateKey = "MetaverseCloudEngine_Unity_PendingBundleUpload";

        private static bool UploadInProgress => _activeUploadCount > 0;

        private static void IncrementUploadInProgress()
        {
            _activeUploadCount++;
        }

        private static void DecrementUploadInProgress()
        {
            _activeUploadCount = Math.Max(0, _activeUploadCount - 1);
        }

        public TAsset Target { get; private set; }

        public static bool AskToTurnOffGPUInstancing {
            get => EditorPrefs.GetBool(nameof(AskToTurnOffGPUInstancing), true);
            set => EditorPrefs.SetBool(nameof(AskToTurnOffGPUInstancing), value);
        }

        protected abstract object GetMainAsset(TAsset asset);
        protected virtual bool AllowUpdateMetadata => true;

        public abstract AssetController<TAssetDto, TAssetQueryParams, TAssetUpsertForm> Controller { get; }
        public override bool RequiresConstantRepaint() => true;

        public void Init()
        {
            if (!Target)
                Target = target as TAsset;

            _deleteIconContent ??= EditorGUIUtility.IconContent("TreeEditor.Trash");
            _refreshIconContent ??= EditorGUIUtility.IconContent("TreeEditor.Refresh");
            _detachIconContent ??= EditorGUIUtility.IconContent("UnLinked");
            _contributorEditor ??= new AssetContributorEditor<TAssetDto>(Target, Controller, Controller);

            if (serializedObject.targetObject)
            {
                _idProperty ??= serializedObject.FindProperty("id");
                _blockchainSourceProperty ??= serializedObject.FindProperty("blockchainSource");
                _blockchainTypeProperty ??= serializedObject.FindProperty("blockchainType");
                _supportedPlatformsProperty ??= serializedObject.FindProperty("supportedPlatforms");
                _publishProperty ??= serializedObject.FindProperty("publish");
            }
        }

        private void OnEnable()
        {
            _currentPlatformOptions = null;
            _assetDto = null;
            _reviewVersionDto = null;
            _requestedDto = false;
            _requestedReviewVersionDto = false;
        }

        public virtual TAssetUpsertForm GetUpsertForm(Guid? id, TAsset asset, bool willUpload)
        {
            return new TAssetUpsertForm
            {
                Id = id,
                BlockchainSource = asset.BlockchainSource,
                BlockchainSourceType = asset.BlockchainSourceType,
                Name = asset.MetaData.Name,
                Description = asset.MetaData.Description,
                Listings = asset.MetaData.Listings,
                Private = asset.MetaData.Private,
                BlockchainAssets = asset.MetaData.BlockchainReferences?.Assets?.Select(x => new BlockchainReferenceAssetModel { Asset = x.asset, Type = x.type }).ToArray(),
                BlockchainCategories = asset.MetaData.BlockchainReferences?.Categories?.Select(x => new BlockchainReferenceCategoryModel { Category = x.category, Type = x.type }).ToArray(),
                Publish = CanPublish() ? asset.Publish : null, 
            };
        }

        private static bool CanPublish()
        {
            return MetaverseProgram.ApiClient?.Account.CurrentUser?.UserName == "solla";
        }

        protected virtual ApiResponse<TAssetDto> Upsert(TAsset asset, TAssetUpsertForm form)
        {
            return Task.Run(async () => await Controller.UpsertAsync(form)).Result;
        }

        protected virtual void DrawInspectorGUI()
        {
            MetaverseEditorUtils.Box(() => base.OnInspectorGUI());
        }

        protected virtual void OnUpdateMetaDataInternal(TAsset asset, TAssetDto assetDto, SerializedObject assetSerializedObject)
        {
        }

        protected virtual void OnClearMetaDataInternal(TAsset asset)
        {
        }

        public override void OnInspectorGUI()
        {
            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("This component cannot be modified in play mode.", MessageType.Warning);
                return;
            }

            MetaverseEditorUtils.DrawLoadingScreen(() =>
            {
                Init();

                if (Target == null)
                {
                    EditorGUILayout.HelpBox("Target is null.", MessageType.Error);
                    return;
                }

                serializedObject.UpdateIfRequiredOrScript();

                FetchAssetDtoInternal();
                
                DrawHeaderGUI();

                DrawPendingUploadResumeUI();

                DrawID();

                DrawPublishControls();

                DrawInspectorGUI();

                DrawUploadControls();

                try { serializedObject.ApplyModifiedProperties(); }
                catch { /* ignored */ }

                EditorGUILayout.Separator();

                if (MetaverseProgram.ApiClient.Account.IsLoggedIn && 
                    targets.Length == 1 && 
                    Target.BlockchainSourceType == BlockchainType.None)
                {
                    DrawListEditors();
                }

            }, MetaverseEditorUtils.DrawDefaultLoadingScreen, !MetaverseProgram.Initialized);
        }

        protected virtual void DrawListEditors()
        {
            _contributorEditor?.Draw();
            //_dataSourceListEditor.HostId = Target.ID;
            //_dataSourceListEditor?.Draw();
        }

        private void DrawHeaderGUI()
        {
            MetaverseEditorUtils.Header(
                Target != null && !string.IsNullOrEmpty(Target.MetaData.Name)
                    ? Target.MetaData.Name
                    : ObjectNames.GetInspectorTitle(serializedObject.targetObject), displayIcon: false);
        }

        protected virtual void DrawID()
        {
            if (_idProperty == null)
                return;

            MetaverseEditorUtils.Box(() =>
            {
                MetaverseEditorUtils.Disabled(() => { EditorGUILayout.PropertyField(_idProperty, new GUIContent("ID")); });

                if (!string.IsNullOrEmpty(_idProperty.stringValue))
                {
                    DrawAssetModificationControls();
                }
                else
                {
                    if (GUILayout.Button("...", EditorStyles.miniButton, GUILayout.Width(25)))
                    {
                        PickerEditor.Pick<TPickerEditor>(asset =>
                        {
                            foreach (var t in targets)
                                ApplyMetaData(new SerializedObject(t), (TAssetDto)asset);
                        });
                    }
                }
            }, vertical: false);

            if (string.IsNullOrEmpty(_idProperty.stringValue) || Target.BlockchainSourceType != BlockchainType.None)
                MetaverseEditorUtils.Box(() =>
                {
                    if (string.IsNullOrEmpty(_idProperty.stringValue))
                        MetaverseEditorUtils.Info(
                            "Using the below fields you can tie this asset directly to the blockchain. " +
                            "Note that in order for this to work the token must be minted, and you must be the owner. " +
                            "Hosting fees still apply.");

                    MetaverseEditorUtils.Disabled(
                        () =>
                        {
                            _blockchainSourceProperty.serializedObject.UpdateIfRequiredOrScript();
                            using var check = new EditorGUI.ChangeCheckScope();
                            EditorGUILayout.PropertyField(_blockchainSourceProperty);
                            if (check.changed)
                                _blockchainSourceProperty.serializedObject.ApplyModifiedProperties();
                        },
                        !string.IsNullOrEmpty(_idProperty.stringValue));

                    MetaverseEditorUtils.Disabled(
                        () =>
                        {
                            _blockchainTypeProperty.serializedObject.UpdateIfRequiredOrScript();
                            using var check = new EditorGUI.ChangeCheckScope();
                            EditorGUILayout.PropertyField(_blockchainTypeProperty);
                            if (check.changed)
                                _blockchainTypeProperty.serializedObject.ApplyModifiedProperties();
                        },
                        !string.IsNullOrEmpty(_idProperty.stringValue));
                });
        }

        protected virtual void DrawAssetModificationControls()
        {
            if (GUILayout.Button(_detachIconContent, GUILayout.Width(40)))
            {
                if (EditorUtility.DisplayDialog(
                        "Detach",
                        "You are about to detach 1 or more assets. Are you sure you want to continue?", "Yes", "Cancel"))
                {
                    foreach (var obj in targets)
                        ApplyMetaData(new SerializedObject(obj), null);
                    
                    _thumbnailTexture = null;
                    _hasThumbnail = false;
                    _requestedThumbnail = false;
                    _requestingThumbnail = false;
                    
                    _contributorEditor = null;
                    //_dataSourceListEditor = null;
                }

                GUIUtility.ExitGUI();
            }

            if (Target.ID != null && 
                GUILayout.Button(_refreshIconContent, GUILayout.Width(40)) && 
                targets.Length == 1)
            {
                if (EditorUtility.DisplayDialog("Refresh",
                        "You are about to refresh this assets metadata, which will pull the most recent changes from " +
                        "the cloud. Are you sure you want to continue? You may lose local metadata changes.",
                        "Yes", "Cancel"))
                {
                    RefreshMetaData();
                }
            }

            if (GUILayout.Button(_deleteIconContent, GUILayout.Width(40)))
            {
                TypeToConfirmEditorWindow.Open(
                    "You are about to delete 1 or more assets. This cannot be undone. Are you sure you want to continue?",
                    "DELETE",
                    "Delete Asset(s)",
                    "Cancel",
                    () =>
                    {
                        foreach (var obj in targets)
                        {
                            var t = obj as TAsset;
                            Delete(t, Controller, () =>
                            {
                                ApplyMetaData(new SerializedObject(t), null);
                                OnDeleteInternal(t);

                            }, displaySuccessDialog: targets.Length == 1);
                        }
                        
                        GUIUtility.ExitGUI();
                    });
                
                GUIUtility.ExitGUI();
            }
        }

        private void RefreshMetaData()
        {
            if (!Target)
                return;
            
            // This pulls the most recent metadata from the server
            // and applies it to the target object.
            var find = Task.Run(async () => await Controller.FindAsync(Target.ID!.Value)).Result;
            if (find.Succeeded)
            {
                var result = Task.Run(async () => await find.GetResultAsync()).Result;
                ApplyMetaData(new SerializedObject(Target), result);
                OnUpdateMetaDataInternal(Target, result, new SerializedObject(Target));
                _assetDto = result;
                EditorUtility.DisplayDialog("Refresh Succeeded", "Your asset's metadata has been refreshed.", "Ok");
            }
            else
            {
                var error = Task.Run(async () => await find.GetErrorAsync()).Result;
                EditorUtility.DisplayDialog("Refresh Failed", "Something went wrong while refreshing your asset's metadata: " + error, "Ok");
            }
        }

        private static void Delete(TAsset target, IAssetController<TAssetDto> controller, Action onFinished, bool displaySuccessDialog = true)
        {
            var assetName = target.MetaData.Name;
            EditorUtility.DisplayProgressBar("Delete Metaverse Asset", $"Deleting asset '{assetName}'...", 1);

            try
            {
                if (target.ID == null)
                    return;

                var id = target.ID!.Value;
                var response = Task.Run(async () => await controller.DeleteAsync(id)).Result;
                if (response.Succeeded)
                {
                    if (displaySuccessDialog) EditorUtility.DisplayDialog("Delete Metaverse Asset", $"Deleted '{assetName}' successfully!", "Ok");
                    onFinished?.Invoke();
                }
                else
                {
                    DeleteError(response.GetErrorAsync().Result);
                }
            }
            catch (Exception e)
            {
                DeleteError(e);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        protected virtual void OnDeleteInternal(TAsset asset)
        {
        }

        private static void DeleteError(object e)
        {
            var error = "Failed to delete: " + e;
            EditorUtility.DisplayDialog("Delete", error, "Ok");
            Debug.LogError(e);
        }

        protected virtual void DrawUploadControls()
        {
            EditorGUILayout.Separator();

            if (!MetaverseProgram.ApiClient.Account.IsLoggedIn)
            {
                EditorGUILayout.HelpBox("Please log in to upload.", MessageType.Info);

                if (GUILayout.Button("Log In"))
                    MetaverseAccountWindow.Open();

                return;
            }

            if (UploadInProgress)
            {
                EditorGUILayout.HelpBox("An upload is currently in progress. Please wait for it to complete before starting another upload or metadata update.", MessageType.Info);
            }

            var wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && !UploadInProgress;
            try
            {
                MetaverseEditorUtils.Box(() =>
                {
                    var currentUser = MetaverseProgram.ApiClient.Account.CurrentUser;
                    var protectedEmailString = currentUser.Email[..1] + "****" + currentUser.Email[(currentUser.Email.IndexOf('@')-1)..];
                    EditorGUILayout.LabelField("You are logged in as: " + currentUser.UserName + " (" + protectedEmailString + ")");

                    if (targets.Length == 1)
                    {
                        var updateMetadataLanguageString = Target.ID == null ? "Create" : "Update Metadata";
                        if (AllowUpdateMetadata && (Target.ID != null || GetMainAsset(Target) == null) && GUILayout.Button(updateMetadataLanguageString) &&
                            EditorUtility.DisplayDialog(updateMetadataLanguageString, "You are about to update metadata. Are you sure you want to do that?", "Yes", "Cancel"))
                        {
                            Upload(GetMainAsset(Target), Target, serializedObject, true);
                            return;
                        }

#if MV_BUILD_PIPELINE
                        if (GetMainAsset(Target) != null)
                        {
                            if (GUILayout.Button("Build & Upload") && EditorUtility.DisplayDialog("Build & Upload", $"You are about to build the asset bundle(s) and upload. This could take a long time. Are you sure you want to do it?", "Yes", "Cancel"))
                            {
                                Upload(GetMainAsset(Target), Target, serializedObject, false);
                                GUIUtility.ExitGUI();
                            }

                            if (CanPublish() && _publishProperty != null)
                            {
                                EditorGUILayout.PropertyField(_publishProperty);
                            }
                        }
#else
                        EditorGUILayout.HelpBox("Please Install Scriptable Build Pipeline v1.21.22 or Newer from the Package Manager", MessageType.Warning);
                        GUI.enabled = false;
                        try
                        {
                            GUILayout.Button("Build & Upload");
                        }
                        finally
                        {
                            GUI.enabled = true;
                        }
#endif
                    }
                    else
                    {
                        var nullIDs = targets.All(x => ((TAsset)x).ID == null);
                        var nullMainAssets = targets.All(x => GetMainAsset((TAsset)x) == null);
                        var updateMetadataLanguageString = nullIDs ? "Create" : "Update Metadata";
                        if (AllowUpdateMetadata && (!nullIDs || GetMainAsset(Target) == null) && nullMainAssets && GUILayout.Button(updateMetadataLanguageString) &&
                            EditorUtility.DisplayDialog(updateMetadataLanguageString, "You are about to update metadata for the selected assets. Are you sure you want to do that?", "Yes", "Cancel"))
                        {
                            foreach (var t in targets)
                            {
                                var asset = (TAsset)t;
                                Upload(GetMainAsset(asset), asset, new SerializedObject(asset), true);
                            }
                        }
                    }

                    DrawPlatformControls();

                    DrawThumbnailControls();
                });
            }
            finally
            {
                GUI.enabled = wasEnabled;
            }
        }

        private void DrawPublishControls()
        {
            if (!Target || Target.ID == null) 
                return;

            GetPublishStatus(
                out var hasReviewVersion, 
                out var isReviewRequested, 
                out var reviewVersionPlatforms);

            if (reviewVersionPlatforms.Count == 0) 
                return;
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Un-Published Changes", EditorStyles.boldLabel);

            foreach (var platform in reviewVersionPlatforms)
            {
                EditorGUILayout.BeginHorizontal();
                var fileSizeBytes = platform.Document.Size;

                EditorGUILayout.LabelField(platform.Platform.ToString(), (fileSizeBytes / 1024.0 / 1024.0).ToString("N2") + " MB");

                if (GUILayout.Button("\u00d7", GUILayout.Width(20)))
                {
                    TypeToConfirmEditorWindow.Open(
                        $"You are about to delete the pending changes for {platform.Platform}. Are you sure you want to continue?",
                        "DELETE",
                        "Delete Pending Changes",
                        "Cancel",
                        () =>
                        {
                            var response = Task.Run(async () => await Controller.DeletePlatformAsync(_reviewVersionDto.Id, platform.Platform)).Result;
                            if (response.Succeeded)
                                EditorUtility.DisplayDialog("Delete Pending Changes", "Deleted pending changes for " + platform.Platform, "Ok");
                            else
                            {
                                var error = Task.Run(async () => await response.GetErrorAsync()).Result;
                                EditorUtility.DisplayDialog("Delete Pending Changes", "Failed to delete pending changes for " + platform.Platform + ": " + error, "Ok");
                            }

                            _assetDto = null;
                            _requestedDto = false;

                            _reviewVersionDto = null;
                            _requestedReviewVersionDto = false;
                        });
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();
            }
            
            if (hasReviewVersion || !_assetDto.Listings.HasFlag(AssetListings.Main) || !_assetDto.Listings.HasFlag(AssetListings.Blockchain))
            {
                if (GUILayout.Button("Publish (Request Review)") && 
                    EditorUtility.DisplayDialog(
                        "Publish Pending Changes",
                        "You are about to publish the pending changes. Are you sure you want to continue?", 
                        "Yes",
                        "Cancel"))
                {
                    var response = Task.Run(async () => await Controller.PublishAsync(
                        _reviewVersionDto.Id)).Result;

                    if (response.Succeeded)
                        EditorUtility.DisplayDialog("Publish Pending Changes", "Published pending changes successfully!", "Ok");
                    else
                    {
                        var error = Task.Run(async () => await response.GetErrorAsync()).Result;
                        EditorUtility.DisplayDialog("Publish Pending Changes", "Failed to publish pending changes: " + error, "Ok");
                    }

                    _assetDto = null;
                    _requestedDto = false;

                    _reviewVersionDto = null;
                    _requestedReviewVersionDto = false;

                    RefreshMetaData();
                }
            }
            else if (GUILayout.Button("Cancel Review"))
            {
                
            }
        }

        private void GetPublishStatus(out bool hasReviewVersion, out bool isReviewRequested, out ICollection<AssetPlatformDocumentDto> reviewVersionPlatforms)
        {
            if (_assetDto == null)
            {
                hasReviewVersion = false;
                isReviewRequested = false;
                reviewVersionPlatforms = Array.Empty<AssetPlatformDocumentDto>();
                return;
            }
            
            hasReviewVersion = _assetDto.HasReviewVersion;
            isReviewRequested = _assetDto.IsReviewRequested;

            GetReviewVersionDto();
            
            reviewVersionPlatforms = _reviewVersionDto is not null ? _reviewVersionDto.Platforms : Array.Empty<AssetPlatformDocumentDto>();
        }

        private TAssetDto FetchAssetDtoNow(Guid? id = null)
        {
            if (id == null)
            {
                if (Target.ID == null)
                    return null;
                id = Target.ID.Value;
            }
            
            var getAsset = Task.Run(async () => await Controller.FindAsync(id.Value)).Result;
            if (!getAsset.Succeeded)
                return null;

            var assetDto = Task.Run(async () => await getAsset.GetResultAsync()).Result;
            return assetDto;
        }

        private void FetchAssetDtoInternal()
        {
            if (_requestedDto || _assetDto != null || !Target || Target.ID is null) 
                return;
            
            _requestedDto = true;
            Task.Run(async () =>
            {
                var dto = await Controller.FindAsync(Target.ID!.Value);
                if (dto.Succeeded)
                    _assetDto = await dto.GetResultAsync();
            });
        }

        [Serializable]
        private sealed class PendingBundleUploadState
        {
            [Serializable]
            public sealed class PendingBuild
            {
                public int Platforms;
                public string OutputPath;
            }

            public int Version = 1;
            public string AssetType;
            public string AssetServerId;
            public string AssetName;
            public string BundlePath;
            public string AssetUpsertFormJson;
            public bool SuppressDialog;
            public PendingBuild[] Builds;
            public long TotalBytes;
            public string StartedUtc;
        }

        private static void SavePendingUploadState(PendingBundleUploadState state)
        {
            try
            {
                var json = JsonConvert.SerializeObject(state);
                SessionState.SetString(PendingUploadSessionStateKey, json);
            }
            catch
            {
                // ignored
            }
        }

        private static PendingBundleUploadState LoadPendingUploadState()
        {
            try
            {
                var json = SessionState.GetString(PendingUploadSessionStateKey, null);
                if (string.IsNullOrWhiteSpace(json))
                    return null;
                return JsonConvert.DeserializeObject<PendingBundleUploadState>(json);
            }
            catch
            {
                return null;
            }
        }

        private static void ClearPendingUploadState()
        {
            try
            {
                SessionState.EraseString(PendingUploadSessionStateKey);
            }
            catch
            {
                // ignored
            }
        }

        private static void ShowEditorNotification(string message, double seconds = 4d)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            EditorApplication.delayCall += () =>
            {
                try
                {
                    var window = EditorWindow.focusedWindow ?? SceneView.lastActiveSceneView;
                    if (window == null)
                        return;

                    window.ShowNotification(new GUIContent(message));

                    var start = EditorApplication.timeSinceStartup;
                    void Tick()
                    {
                        if (EditorApplication.timeSinceStartup - start < seconds)
                            return;
                        EditorApplication.update -= Tick;
                        try { window.RemoveNotification(); } catch { /* ignored */ }
                    }

                    EditorApplication.update += Tick;
                }
                catch
                {
                    // ignored
                }
            };
        }

        private static IEnumerable<Platform> ExpandPlatformFlags(Platform platforms)
        {
            foreach (Platform p in Enum.GetValues(typeof(Platform)))
            {
                if ((int)p == 0)
                    continue;
                if (platforms.HasFlag(p))
                    yield return p;
            }
        }

        private static string BuildPendingUploadVerificationKey(PendingBundleUploadState state)
        {
            return state == null
                ? null
                : $"{state.AssetType}|{state.AssetServerId}";
        }

        private static readonly Dictionary<string, Task<(bool completed, TAssetDto dto, string error)>> PendingUploadVerificationTasks = new();

        private void DrawPendingUploadResumeUI()
        {
            if (UploadInProgress)
                return;

            var state = LoadPendingUploadState();
            if (state == null)
                return;

            // Only surface the prompt for the matching asset type & server id.
            if (!string.Equals(state.AssetType, typeof(TAsset).AssemblyQualifiedName, StringComparison.Ordinal))
                return;

            var localServerId = _idProperty?.stringValue;
            if (!string.IsNullOrWhiteSpace(state.AssetServerId) &&
                !string.Equals(state.AssetServerId, localServerId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var buildCount = state.Builds?.Length ?? 0;
            if (buildCount <= 0)
            {
                ClearPendingUploadState();
                return;
            }

            // If the upload actually completed during an assembly/domain reload, we won't see the
            // success log/dialog. Do a one-time server verification and auto-clear if completed.
            if (Guid.TryParse(state.AssetServerId, out var verifyId) &&
                MetaverseProgram.ApiClient?.Account?.IsLoggedIn == true)
            {
                var verificationKey = BuildPendingUploadVerificationKey(state);
                if (!string.IsNullOrWhiteSpace(verificationKey))
                {
                    if (!PendingUploadVerificationTasks.TryGetValue(verificationKey, out var verificationTask))
                    {
                        PendingUploadVerificationTasks[verificationKey] = Task.Run(async () =>
                        {
                            try
                            {
                                var response = await Controller.FindAsync(verifyId);
                                if (!response.Succeeded)
                                    return (false, default, await response.GetErrorAsync());

                                var dto = await response.GetResultAsync();
                                var requiredPlatforms = state.Builds
                                    .SelectMany(b => ExpandPlatformFlags((Platform)b.Platforms))
                                    .Distinct()
                                    .ToArray();

                                var uploadedPlatforms = (dto?.Platforms ?? Array.Empty<AssetPlatformDocumentDto>())
                                    .Select(p => p.Platform)
                                    .Distinct()
                                    .ToHashSet();

                                var missing = requiredPlatforms.Where(rp => !uploadedPlatforms.Contains(rp)).ToArray();
                                return (missing.Length == 0, dto, missing.Length == 0 ? null : $"Missing platforms: {string.Join(", ", missing)}");
                            }
                            catch (Exception e)
                            {
                                return (false, default, e.Message);
                            }
                        });

                        verificationTask = PendingUploadVerificationTasks[verificationKey];
                    }

                    if (!verificationTask.IsCompleted)
                    {
                        EditorGUILayout.HelpBox("Checking server to see if the upload already finished...", MessageType.Info);
                    }
                    else
                    {
                        PendingUploadVerificationTasks.Remove(verificationKey);

                        if (verificationTask.IsFaulted)
                        {
                            // Fall through to resume UI.
                        }
                        else
                        {
                            var (completed, dto, error) = verificationTask.Result;
                            if (completed && dto != null)
                            {
                                MetaverseProgram.Logger.Log($"<b><color=green>Successfully</color></b> uploaded bundles for '{state.AssetName ?? dto.Name}'. (Recovered after assembly reload)");
                                ShowEditorNotification($"Upload complete: {state.AssetName ?? dto.Name}");

                                try
                                {
                                    ApplyMetaData(serializedObject, dto);
                                }
                                catch
                                {
                                    // ignored
                                }

                                ClearPendingUploadState();
                                GUIUtility.ExitGUI();
                                return;
                            }

                            if (!string.IsNullOrWhiteSpace(error))
                                EditorGUILayout.HelpBox($"Server status check: {error}", MessageType.Warning);
                        }
                    }
                }
            }

            var missingBuilds = state.Builds.Where(b => string.IsNullOrWhiteSpace(b?.OutputPath) || !File.Exists(b.OutputPath)).ToArray();
            var hasMissing = missingBuilds.Length > 0;

            var msg = new StringBuilder();
            msg.AppendLine("A bundle upload appears to have been interrupted (likely by an assembly/domain reload). ");
            msg.AppendLine($"Pending bundles: {buildCount}");
            if (hasMissing)
                msg.AppendLine($"Missing bundle files: {missingBuilds.Length} (rebuild may be required)");

            EditorGUILayout.HelpBox(msg.ToString().TrimEnd(), hasMissing ? MessageType.Warning : MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginDisabledGroup(hasMissing);
                if (GUILayout.Button("Resume Upload", GUILayout.Height(22)))
                {
                    try
                    {
                        var form = !string.IsNullOrWhiteSpace(state.AssetUpsertFormJson)
                            ? JsonConvert.DeserializeObject<TAssetUpsertForm>(state.AssetUpsertFormJson)
                            : GetUpsertForm(Guid.TryParse(localServerId, out var id) ? id : null, Target, willUpload: true);

                        var builds = state.Builds
                            .Select(b => new MetaverseAssetBundleAPI.BundleBuild
                            {
                                OutputPath = b.OutputPath,
                                Platforms = (Platform)b.Platforms,
                            })
                            .ToArray();

                        MetaverseProgram.Logger.Log($"Resuming interrupted bundle upload for '{state.AssetName ?? Target?.MetaData?.Name ?? Target?.name}'.");

                        // Clear first so a second reload doesn't cause repeated prompts.
                        ClearPendingUploadState();
                        UploadBundles(Controller, state.BundlePath, builds, form, suppressDialog: state.SuppressDialog);
                    }
                    catch (Exception e)
                    {
                        MetaverseProgram.Logger.Log($"Failed to resume upload: {e.Message}");
                    }

                    GUIUtility.ExitGUI();
                }
                EditorGUI.EndDisabledGroup();

                if (GUILayout.Button("Dismiss", GUILayout.Height(22)))
                {
                    ClearPendingUploadState();
                    GUIUtility.ExitGUI();
                }
            }
        }

        protected virtual Texture2D AutoCaptureThumbnail(TAsset asset)
        {
            return null;
        }

        private void DrawPlatformControls()
        {
            if (GetMainAsset(Target) != null && _supportedPlatformsProperty != null)
            {
                var targetPlatform = (AssetBuildPlatform)EditorGUILayout.EnumFlagsField(
                    "Target Platforms",
                    (AssetBuildPlatform)_supportedPlatformsProperty.intValue);

                if ((int)Target.SupportedBuildPlatforms != (int)targetPlatform)
                {
                    Target.SupportedPlatforms = (Platform)targetPlatform;
                    EditorUtility.SetDirty(Target);
                }

                if (targetPlatform == 0)
                {
                    EditorGUILayout.HelpBox("You must select at least one build platform.", MessageType.Error);
                }

                EditorGUILayout.Space();

                DrawPlatformOptions();

                return;
            }

            _currentPlatformOptions = null;
        }

        private void DrawThumbnailControls()
        {
            var showThumbnailControls = Target.ID != null && GetMainAsset(Target) != null;
            if (!showThumbnailControls)
                return;

            if (EditorGUILayout.DropdownButton(new GUIContent("Thumbnail"), FocusType.Passive))
                _foldoutThumbnail = !_foldoutThumbnail;

            if (!_foldoutThumbnail)
                return;

            if (!_thumbnailTexture && !_requestedThumbnail)
            {
                EditorGUILayout.HelpBox("Fetching thumbnail...", MessageType.Info);
                    
                if (_requestingThumbnail) 
                    return;
                    
                Task.Run(async () =>
                {
                    var thumbnailResponse = await Controller.DownloadThumbnailAsync(Target.ID!.Value);
                    try
                    {
                        if (thumbnailResponse.Succeeded)
                        {
                            _hasThumbnail = true;
                                
                            var result = await thumbnailResponse.GetResultAsync();
                            if (result != null)
                            {
                                var tex2D = await result.ImageData.GetResultAsAsync<Texture2D>();
                                if (tex2D != null)
                                    _thumbnailTexture = tex2D;
                            }
                        }
                    }
                    finally
                    {
                        _requestedThumbnail = true;
                    }
                });
                    
                _requestingThumbnail = true;
                return;
            }

            if (_thumbnailTexture)
            {
                EditorGUILayout.LabelField(
                    new GUIContent(_thumbnailTexture), EditorStyles.centeredGreyMiniLabel, GUILayout.MaxHeight(256), GUILayout.ExpandWidth(true));
            }

            if (GUILayout.Button("Upload Thumbnail"))
            {
                int thumbnailOption = EditorUtility.DisplayDialogComplex("Upload Thumbnail", "What is the source of the thumbnail?", "Automatic", "Select File", "Cancel");
                if (thumbnailOption == 0)
                {
                    Object[] selection;
                    if (Target is Assets.MetaPrefabs.MetaPrefab mp)
                        selection = mp.GetComponentsInChildren<Assets.MetaPrefabs.MetaPrefab>(true);
                    else selection = targets;
                    
                    foreach (var t in selection)
                    {
                        if (t is TAsset asset && asset.ID != null)
                        {
                            var thumbnail = AutoCaptureThumbnail(asset);
                            if (thumbnail != null)
                            {
                                try { UpsertThumbnail(thumbnail, asset, FetchAssetDtoNow(asset.ID.Value), false); } catch { /* ignored */ }
                            }
                        }
                    }
                }
                else if (thumbnailOption == 1)
                {
                    var thumbnailFile = EditorUtility.OpenFilePanelWithFilters("Select Thumbnail", "", new[] { "PNG", "png", "JPG", "jpg", "JPEG", "jpeg", "TIFF", "tiff" });
                    if (!string.IsNullOrEmpty(thumbnailFile) && File.Exists(thumbnailFile))
                    {
                        try
                        {
                            var tex = new Texture2D(2, 2);
                            tex.LoadImage(File.ReadAllBytes(thumbnailFile), false);
                            UpsertThumbnail(tex, Target, FetchAssetDtoNow(), false);
                        }
                        catch (Exception)
                        {
                            // ignored
                        }
                    }
                }
            }

            if (_hasThumbnail && GUILayout.Button("Delete Thumbnail"))
            {
                if (!EditorUtility.DisplayDialog("Delete Thumbnail", "Are you sure you want to delete the current thumbnail?", "Yes", "No"))
                    return;

                UpsertThumbnail(null, Target, FetchAssetDtoNow(), allowDelete: true);
                GUIUtility.ExitGUI();
            }
        }

        private void DrawPlatformOptions()
        {
            if (EditorGUILayout.DropdownButton(new GUIContent("Platform Optimization Options"), FocusType.Passive))
                _foldoutPlatformOptions = !_foldoutPlatformOptions;

            if (_foldoutPlatformOptions)
            {
                if (_currentPlatformOptions == null)
                {
                    LoadPlatformOptions();
                }
                
                EditorGUILayout.HelpBox("You can use these options to perform project-wide optimizations for the asset bundle build process. These options will be used when building the asset bundle(s) for the selected platforms.", MessageType.Info);

                var selectedPlatform = DrawPlatformTabs();
                var platformKey = (Platform)(int)selectedPlatform;
                var options = _currentPlatformOptions!.TryGetValue(platformKey, out var opt) ? opt : _currentPlatformOptions[platformKey] = new BundlePlatformOptions();

                var changed = false;

                var overrideDefaults = EditorGUILayout.Toggle("Override Defaults", options.overrideDefaults);
                if (overrideDefaults != options.overrideDefaults)
                {
                    options.overrideDefaults = overrideDefaults;
                    changed = true;
                }

                if (DrawPlatformPresets(ref options))
                    changed = true;

                using (new EditorGUI.DisabledScope(!options.overrideDefaults))
                {
                    if (DrawPlatformOptionFields(ref options))
                        changed = true;
                }

                if (changed)
                {
                    _currentPlatformOptions[platformKey] = options;
                    SavePlatformOptions(Guid.TryParse(_idProperty.stringValue, out var id) ? id : null);
                }
            }
        }

        private static AssetBuildPlatform DrawPlatformTabs()
        {
            var selected = (AssetBuildPlatform)_selectPlatformOption;
            var contents = GetPlatformTabContents();
            var index = Array.IndexOf(PlatformTabOrder, selected);
            if (index < 0)
            {
                index = 0;
                selected = PlatformTabOrder[0];
                _selectPlatformOption = (int)selected;
            }

            using (new EditorGUILayout.HorizontalScope(GUILayout.Height(32)))
            {
                EditorGUILayout.PrefixLabel("Target Platform");
                
                var buttonStyle = GetPlatformTabStyle();
                for (var i = 0; i < contents.Length; i++)
                {
                    var isSelected = i == index;
                    var pressed = GUILayout.Toggle(isSelected, contents[i], buttonStyle, GUILayout.Width(32), GUILayout.Height(32));
                    if (!isSelected && pressed)
                    {
                        selected = PlatformTabOrder[i];
                        _selectPlatformOption = (int)selected;
                        GUI.FocusControl(null);
                    }
                }
            }

            return selected;
        }

        private static bool DrawPlatformPresets(ref BundlePlatformOptions options)
        {
            var changed = false;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("Quick Presets");

                var buttonStyle = new GUIStyle(EditorStyles.miniButton)
                {
                    fixedHeight = 20f,
                    padding = new RectOffset(4, 4, 2, 2),
                    stretchWidth = true
                };

                foreach (var preset in PlatformPresets)
                {
                    if (!GUILayout.Button(new GUIContent(preset.Name, preset.Tooltip), buttonStyle))
                        continue;

                    options.overrideDefaults = true;
                    options.maxTextureResolution = preset.MaxTextureResolution;
                    options.compressTextures = preset.CompressTextures;
                    options.compressorQuality = preset.TextureQuality;
                    options.meshCompression = preset.MeshCompression;
                    GUI.FocusControl(null);
                    changed = true;
                }
            }

            return changed;
        }

        private static bool DrawPlatformOptionFields(ref BundlePlatformOptions options)
        {
            var changed = false;

            var maxTexture = (BundleMaxTextureResolution)EditorGUILayout.EnumPopup("Max Texture Size", options.maxTextureResolution);
            if (maxTexture != options.maxTextureResolution)
            {
                options.maxTextureResolution = maxTexture;
                changed = true;
            }

            var compressTextures = EditorGUILayout.Toggle("Compress Textures", options.compressTextures);
            if (compressTextures != options.compressTextures)
            {
                options.compressTextures = compressTextures;
                changed = true;
            }

            var textureQuality = EditorGUILayout.IntSlider("Texture Quality", options.compressorQuality, 0, 100);
            if (textureQuality != options.compressorQuality)
            {
                options.compressorQuality = textureQuality;
                changed = true;
            }

            var meshCompression = (ModelImporterMeshCompression)EditorGUILayout.EnumPopup("Mesh Compression", options.meshCompression);
            if (meshCompression != options.meshCompression)
            {
                options.meshCompression = meshCompression;
                changed = true;
            }

            return changed;
        }

        private static GUIContent[] GetPlatformTabContents()
        {
            if (EditorGUIUtility.isProSkin != _lastProSkinState)
            {
                _platformTabContents = null;
                PlatformIconCache.Clear();
                _lastProSkinState = EditorGUIUtility.isProSkin;
            }

            if (_platformTabContents != null)
                return _platformTabContents;

            _platformTabContents = new GUIContent[PlatformTabOrder.Length];
            for (var i = 0; i < PlatformTabOrder.Length; i++)
            {
                var platform = PlatformTabOrder[i];
                var icon = LoadPlatformIcon(platform);
                var tooltip = GetPlatformDisplayName(platform);
                _platformTabContents[i] = icon != null
                    ? new GUIContent(icon, tooltip)
                    : new GUIContent(tooltip);
            }

            return _platformTabContents;
        }

        private static Texture2D LoadPlatformIcon(AssetBuildPlatform platform)
        {
            if (PlatformIconCache.TryGetValue(platform, out var icon))
                return icon;

            // Use Unity's built-in icon for iOS
            if (platform == AssetBuildPlatform.iOS)
            {
                icon = EditorGUIUtility.FindTexture("BuildSettings.iPhone");
                if (icon)
                {
                    PlatformIconCache[platform] = icon;
                    return icon;
                }
            }

            if (!PlatformIconResourcePaths.TryGetValue(platform, out var paths))
            {
                PlatformIconCache[platform] = null;
                return null;
            }

            var resourcePath = EditorGUIUtility.isProSkin ? paths.light : paths.dark;
            icon = Resources.Load<Texture2D>(resourcePath);
            PlatformIconCache[platform] = icon;
            return icon;
        }

        private static GUIStyle GetPlatformTabStyle()
        {
            if (_platformTabStyle != null)
                return _platformTabStyle;

            _platformTabStyle = new GUIStyle(EditorStyles.toolbarButton)
            {
                fixedHeight = 32f,
                fixedWidth = 32f,
                stretchHeight = false,
                stretchWidth = false,
                alignment = TextAnchor.MiddleCenter,
                imagePosition = ImagePosition.ImageOnly,
                padding = new RectOffset(5, 5, 5, 5),
                margin = new RectOffset(1, 1, 0, 0)
            };

            if (_platformTabStyle.onNormal.background == null && _platformTabStyle.active.background != null)
                _platformTabStyle.onNormal.background = _platformTabStyle.active.background;

            if (_platformTabStyle.onHover.background == null && _platformTabStyle.hover.background != null)
                _platformTabStyle.onHover.background = _platformTabStyle.hover.background;

            if (_platformTabStyle.onActive.background == null && _platformTabStyle.active.background != null)
                _platformTabStyle.onActive.background = _platformTabStyle.active.background;

            if (_platformTabStyle.onFocused.background == null && _platformTabStyle.focused.background != null)
                _platformTabStyle.onFocused.background = _platformTabStyle.focused.background;

            return _platformTabStyle;
        }

        private static string GetPlatformDisplayName(AssetBuildPlatform platform)
        {
            return ObjectNames.NicifyVariableName(platform.ToString());
        }

        private static void SavePlatformOptions(Guid? newId)
        {
            EditorPrefs.SetString(newId.ToString(), JsonConvert.SerializeObject(_currentPlatformOptions));
        }

        private void LoadPlatformOptions()
        {
            var platformOptions = EditorPrefs.GetString(_idProperty.stringValue, string.Empty);
            try { _currentPlatformOptions = JsonConvert.DeserializeObject<Dictionary<Platform, BundlePlatformOptions>>(platformOptions) ?? new(); }
            catch { _currentPlatformOptions = new Dictionary<Platform, BundlePlatformOptions>(); }
        }

        private void Upload(object mainAsset, TAsset asset, SerializedObject serObj, bool metadataOnly)
        {
            AssetBundle.UnloadAllAssetBundles(true);

            EditorUtility.DisplayProgressBar("Updating Metaverse Asset Metadata", "Updating asset metadata...", 1);
            
            Dictionary<Platform, BundlePlatformOptions> originalPlatformOptions = null;
            
            AssetDto updatedAssetDto = null;

            try
            {
                LoadPlatformOptions();

                originalPlatformOptions = _currentPlatformOptions;
                
                var form = GetUpsertForm(asset.ID, asset, !metadataOnly);

                if (metadataOnly)
                {
                    // First we want to upsert the asset object, this needs to be done before we upload to
                    // it, in case it doesn't exist.
                    var response = Upsert(asset, form);

                    // We have to do this synchronously to ensure stuff doesn't get garbage collected
                    // after we rebuild. 
                    if (response.Succeeded)
                    {
                        var assetDto = response.GetResultAsync().Result;
                        
                        ApplyMetaData(serObj, assetDto);
                        
                        EditorUtility.DisplayDialog(
                            "Update Succeeded",
                            "Your asset metadata has successfully updated.",
                            "Ok");
                        Debug.Log(
                            $"<b><color=green>Successfully</color></b> updated metadata for {assetDto.Name} ({assetDto.Id}).");
                        EditorUtility.ClearProgressBar();
                        
                        updatedAssetDto = assetDto;
                    }
                    else
                    {
                        UploadFailure(Task.Run(async () => await response.GetErrorAsync()).Result);
                    }

                    return;
                }

                _currentPlatformOptions = originalPlatformOptions;
                
                BeginBuildAndUpload(mainAsset, asset, form, Controller, (dto, _) =>
                {
                    updatedAssetDto = dto;
                });
            }
            catch (AggregateException e)
            {
                UploadFailure(e.GetBaseException());
            }
            catch (Exception e)
            {
                UploadFailure(e);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                
                if (originalPlatformOptions is not null && updatedAssetDto is not null)
                {
                    _currentPlatformOptions = originalPlatformOptions;
                    SavePlatformOptions(updatedAssetDto.Id);
                }
                
                _requestedDto = false;
                _requestedReviewVersionDto = false;
            }
        }

        private void GetReviewVersionDto()
        {
            if (_assetDto is null)
                return;
            
            if (_requestedReviewVersionDto)
                return;

            if (_assetDto.IsReviewVersion)
            {
                _reviewVersionDto = _assetDto;
                _requestedReviewVersionDto = true; 
                return;
            }

            if (_assetDto.ReviewVersionId is null)
            {
                _requestedReviewVersionDto = true;
                return;
            }
            
            _requestedReviewVersionDto = true;
            Task.Run(async () =>
            {
                var dto = await Controller.FindAsync(_assetDto.ReviewVersionId!.Value);
                if (dto.Succeeded)
                {
                    _reviewVersionDto = await dto.GetResultAsync();
                }
            });
        }

        protected virtual bool UpsertThumbnail(Texture2D thumbnail, TAsset asset, TAssetDto assetDto, bool allowDelete = true)
        {
            try
            {
                if (thumbnail)
                {
                    try
                    {
                        var thumbnailPath = AssetDatabase.GetAssetPath(thumbnail);
                        var fileName = !string.IsNullOrEmpty(thumbnailPath) ? Path.GetFileName(thumbnailPath) : null;
                        if (string.IsNullOrEmpty(Path.GetExtension(fileName)))
                            fileName += ".png";

                        var data = thumbnail.Copy2D().EncodeToBytes();
                        var uploadResponse = Task.Run(async () => await Controller.UpsertThumbnailAsync(assetDto.Id, new MemoryStream(data), fileName ?? "thumbnail.png")).Result;
                        if (!uploadResponse.Succeeded)
                        {
                            var errorAsync = uploadResponse.GetErrorAsync();
                            UploadFailure(errorAsync.Result);
                            return false;
                        }

                        _hasThumbnail = true;
                        _thumbnailTexture = thumbnail;
                        Debug.Log("<b><color=green>Successfully</color></b> updated thumbnail for '" + assetDto.Name + "' (" + assetDto.Id.ToString()[..4] + "...)");
                    }
                    catch (Exception e)
                    {
                        UploadFailure(e.Message);
                        return false;
                    }
                }
                else if (assetDto.Thumbnail != null && allowDelete)
                {
                    var deleteResponse = Task.Run(async () => await Controller.DeleteThumbnailAsync(assetDto.Id)).Result;
                    if (!deleteResponse.Succeeded && deleteResponse.Message.StatusCode != HttpStatusCode.NotFound)
                    {
                        UploadFailure(deleteResponse.GetErrorAsync().Result);
                        return false;
                    }

                    _hasThumbnail = false;
                    _thumbnailTexture = null;
                    
                    Debug.Log("<b><color=green>Successfully</color></b> deleted thumbnail.");
                }

                return true;
            }
            finally
            {
                GUIUtility.ExitGUI();
            }
        }

        private void BeginBuildAndUpload(
            object mainAsset,
            TAsset asset,
            TAssetUpsertForm assetUpsertForm,
            IUpsertAssets<TAssetDto, TAssetUpsertForm> upsertController,
            Action<AssetDto, IEnumerable<MetaverseAssetBundleAPI.BundleBuild>> onBuildSuccess = null)
        {
            // Store build context for potential retry
            _lastBuildMainAsset = mainAsset;
            _lastBuildAsset = asset;
            _lastBuildAssetUpsertForm = assetUpsertForm;
            _lastBuildUpsertController = upsertController;
            _lastBuildOnSuccess = onBuildSuccess;

            TryDisableGPUInstancingOnWebGLIfUserWantsTo(mainAsset, asset);

            switch (mainAsset)
            {
                case Scene scene:
                {
                    var scenePath = scene.path;
                    scene.BuildStreamedScene(asset.SupportedPlatforms, builds =>
                        {
                            UploadBundles(
                                upsertController,
                                scenePath,
                                builds,
                                assetUpsertForm,
                                onBuildSuccess);
                        },
                        _currentPlatformOptions,
                        OnBuildError);
                    break;
                }
                case GameObject go:
                {
                    var mainAssetPath = AssetDatabase.GetAssetPath(go);
                    if (string.IsNullOrEmpty(mainAssetPath))
                    {
                        OnBuildError("Asset does not exist within this project.");
                        return;
                    }

                    try
                    {
                        go.BuildPrefab(
                            asset.SupportedPlatforms,
                            builds => UploadBundles(
                                upsertController, 
                                mainAssetPath,
                                builds,
                                assetUpsertForm,
                                onBuildSuccess),
                            onPreProcessBuild: () => OnBeforeBuildPrefab(go),
                            platformOptions: _currentPlatformOptions, 
                            failed: OnBuildError);
                    }
                    finally
                    {
                        OnAfterBuildPrefab(go);
                    }

                    break;
                }
            }
        }

        protected virtual void OnBeforeBuildPrefab(GameObject prefab)
        {
        }

        protected virtual void OnAfterBuildPrefab(GameObject prefab)
        {
        }

        private static void TryDisableGPUInstancingOnWebGLIfUserWantsTo(object mainAsset, TAsset target)
        {
            try
            {
                if (!AskToTurnOffGPUInstancing)
                    return;

                var materials = AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(mainAsset is Scene s ? AssetDatabase.LoadAssetAtPath<SceneAsset>(s.path) : mainAsset as Object), true)
                    .Select(path => AssetDatabase.LoadAssetAtPath(path, AssetDatabase.GetMainAssetTypeAtPath(path)))
                    .Where(x => x is Material { enableInstancing: true });

                var materialsArray = materials as Object[] ?? materials.ToArray();
                if (materialsArray.Any() && target.SupportedPlatforms.HasFlag(Platform.WebGL))
                {
                    var response = EditorUtility.DisplayDialogComplex(
                        "Turn Off GPU Instancing",
                        "WebGL does not have great support for GPU instancing. Would you like to turn off GPU instancing on all your materials now?",
                        "Yes", "No", "No. Don't ask again.");

                    if (response == 0)
                    {
                        AssetDatabase.StartAssetEditing();
                        try
                        {
                            foreach (var material in materialsArray)
                            {
                                var mat = material as Material;
                                if (!mat) continue;
                                mat.enableInstancing = false;
                                AssetDatabase.SaveAssetIfDirty(mat);
                            }
                        }
                        finally
                        {
                            AssetDatabase.StopAssetEditing();
                        }
                    }

                    if (response == 2)
                        AskToTurnOffGPUInstancing = false;
                }
            }
            catch (Exception e) { Debug.LogException(e); /* ignored */ }
        }

        // Store build context for retry
        private object _lastBuildMainAsset;
        private TAsset _lastBuildAsset;
        private TAssetUpsertForm _lastBuildAssetUpsertForm;
        private IUpsertAssets<TAssetDto, TAssetUpsertForm> _lastBuildUpsertController;
        private Action<AssetDto, IEnumerable<MetaverseAssetBundleAPI.BundleBuild>> _lastBuildOnSuccess;
        private SerializedObject _lastBuildSerializedObject;

        private void OnBuildError(object e)
        {
            EditorUtility.ClearProgressBar();

            // Show dialog with retry option
            bool retry = EditorUtility.DisplayDialog(
                "Build Failed",
                $"Asset bundle building process failed. {e.ToPrettyErrorString()}\n\nWould you like to retry the build?",
                "Retry",
                "Cancel");

            // If user clicked "Retry", attempt retry immediately
            if (retry)
            {
                RetryBuild();
            }
        }

        private void RetryBuild()
        {
            if (_lastBuildMainAsset == null || _lastBuildAsset == null || _lastBuildAssetUpsertForm == null || _lastBuildUpsertController == null)
            {
                EditorUtility.DisplayDialog("Retry Failed", "Build information is incomplete or corrupted.", "Ok");
                return;
            }

            // Retry the build
            BeginBuildAndUpload(_lastBuildMainAsset, _lastBuildAsset, _lastBuildAssetUpsertForm, _lastBuildUpsertController, _lastBuildOnSuccess);
        }

        public void UploadBundles(
            IUpsertAssets<TAssetDto, TAssetUpsertForm> controller,
            string bundlePath,
            IEnumerable<MetaverseAssetBundleAPI.BundleBuild> builds,
            TAssetUpsertForm assetUpsertForm,
            Action<AssetDto, IEnumerable<MetaverseAssetBundleAPI.BundleBuild>> onBuildSuccess = null,
            Action<object> onError = null,
            int tries = 0,
            bool suppressDialog = false)
        {
            UploadBundlesInternal(controller, bundlePath, builds, assetUpsertForm, onBuildSuccess, onError, tries, suppressDialog);
        }

        private void UploadBundlesInternal(
            IUpsertAssets<TAssetDto, TAssetUpsertForm> controller,
            string bundlePath,
            IEnumerable<MetaverseAssetBundleAPI.BundleBuild> builds,
            TAssetUpsertForm assetUpsertForm,
            Action<AssetDto, IEnumerable<MetaverseAssetBundleAPI.BundleBuild>> onBuildSuccess,
            Action<object> onError,
            int tries,
            bool suppressDialog)
        {
            var buildsArray = builds as MetaverseAssetBundleAPI.BundleBuild[] ?? builds?.ToArray() ?? Array.Empty<MetaverseAssetBundleAPI.BundleBuild>();
            IncrementUploadInProgress();
            try
            {
                var context = new UploadBundlesContext(
                    this,
                    controller,
                    bundlePath,
                    buildsArray,
                    assetUpsertForm,
                    onBuildSuccess,
                    onError,
                    tries,
                    suppressDialog);
                context.Start();
            }
            catch
            {
                DecrementUploadInProgress();
                throw;
            }
        }

        private sealed class UploadBundlesContext
        {
            private enum Phase
            {
                Uploading,
                HandleUploadResponse,
                WaitingForDto,
                WaitingForError,
                WaitingForSessionRefresh,
                Completed
            }

            private readonly AssetEditor<TAsset, TAssetMetadata, TAssetDto, TAssetQueryParams, TAssetUpsertForm, TPickerEditor> _owner;
            private readonly IUpsertAssets<TAssetDto, TAssetUpsertForm> _controller;
            private readonly string _bundlePath;
            private readonly MetaverseAssetBundleAPI.BundleBuild[] _builds;
            private readonly TAssetUpsertForm _assetUpsertForm;
            private readonly Action<AssetDto, IEnumerable<MetaverseAssetBundleAPI.BundleBuild>> _onBuildSuccess;
            private readonly Action<object> _onError;
            private readonly int _tries;
            private readonly bool _suppressDialog;

            private readonly List<Stream> _openStreams = new();
            private CancellationTokenSource _uploadCancellation;
            private bool _lockedAssemblies;

            private Task<ApiResponse<TAssetDto>> _uploadTask;
            private ApiResponse<TAssetDto> _uploadResponse;
            private Task<TAssetDto> _dtoTask;
            private Task<string> _errorTask;
            private Task<MetaverseCloudEngine.ApiClient.Controllers.AccountController.SessionValidationResult> _sessionTask;

            private Phase _phase;

            private long _totalBytes;
            private double _lastUiUpdate;
            private double _estimatedSeconds;
            private Stopwatch _stopwatch;
            private double _uploadDurationSeconds;

            public UploadBundlesContext(
                AssetEditor<TAsset, TAssetMetadata, TAssetDto, TAssetQueryParams, TAssetUpsertForm, TPickerEditor> owner,
                IUpsertAssets<TAssetDto, TAssetUpsertForm> controller,
                string bundlePath,
                MetaverseAssetBundleAPI.BundleBuild[] builds,
                TAssetUpsertForm assetUpsertForm,
                Action<AssetDto, IEnumerable<MetaverseAssetBundleAPI.BundleBuild>> onBuildSuccess,
                Action<object> onError,
                int tries,
                bool suppressDialog)
            {
                _owner = owner;
                _controller = controller;
                _bundlePath = bundlePath;
                _builds = builds;
                _assetUpsertForm = assetUpsertForm;
                _onBuildSuccess = onBuildSuccess;
                _onError = onError;
                _tries = tries;
                _suppressDialog = suppressDialog;
            }

            public void Start()
            {
                if (_builds == null || _builds.Length == 0)
                {
                    _onError?.Invoke("No bundles were available to upload.");
                    Complete();
                    return;
                }

                if (Application.internetReachability == NetworkReachability.NotReachable)
                {
                    var retry = EditorUtility.DisplayDialog(
                        "Internet Connection Required",
                        "Please check your internet connection and try again.",
                        "Retry",
                        "Cancel");

                    if (retry)
                    {
                        _owner.UploadBundles(
                            _controller,
                            _bundlePath,
                            _builds,
                            _assetUpsertForm,
                            _onBuildSuccess,
                            _onError,
                            _tries + 1,
                            _suppressDialog);
                    }
                    else
                    {
                        _onError?.Invoke("Upload cancelled.");
                    }

                    Complete();
                    return;
                }

                if (_tries >= 3)
                {
                    _owner.StoreBundleInfoForRetry(_builds);

                    var retry = !_suppressDialog && EditorUtility.DisplayDialog(
                        "Upload Failed",
                        "Uploading failed, please check your internet connection, or log-in and try again. If the issue persists, please restart Unity.",
                        "Retry",
                        "Cancel");

                    if (retry)
                    {
                        _owner.UploadBundles(
                            _controller,
                            _bundlePath,
                            _builds,
                            _assetUpsertForm,
                            _onBuildSuccess,
                            _onError,
                            0,
                            _suppressDialog);
                    }
                    else
                    {
                        _onError?.Invoke("Upload cancelled.");
                    }

                    Complete();
                    return;
                }

                AssetPlatformUpsertOptions[] platformOptions;
                try
                {
                    PersistPendingUploadState();

                    platformOptions = _builds.Select(x =>
                    {
                        var stream = File.OpenRead(x.OutputPath);
                        _openStreams.Add(stream);
                        return new AssetPlatformUpsertOptions
                        {
                            Stream = stream,
                            AssetPath = _bundlePath,
                            Platform = x.Platforms,
                        };
                    }).ToArray();

                    _totalBytes = _builds.Sum(x => new FileInfo(x.OutputPath).Length);
                }
                catch (Exception ex)
                {
                    HandleUploadException(ex);
                    return;
                }

                var uploadSizeMb = _totalBytes / 1024f / 1024f;
                MetaverseProgram.Logger.Log($"Uploading bundles for '{_assetUpsertForm.Name}' ({uploadSizeMb:N2} MB)...");

                var hasSavedUploadSpeed = EditorPrefs.HasKey(UploadSpeedPrefKey);
                var savedUploadSpeed = hasSavedUploadSpeed ? Math.Max(EditorPrefs.GetFloat(UploadSpeedPrefKey), 0f) : 0f;
                var bytesPerSecondForEstimate = hasSavedUploadSpeed && savedUploadSpeed > 0f
                    ? (double)savedUploadSpeed
                    : DefaultSimulatedBytesPerSecond;
                _estimatedSeconds = _totalBytes <= 0 || bytesPerSecondForEstimate <= 0
                    ? 0
                    : _totalBytes / bytesPerSecondForEstimate;

                _stopwatch = Stopwatch.StartNew();
                _lastUiUpdate = 0d;
                _uploadCancellation = new CancellationTokenSource();

                EditorApplication.LockReloadAssemblies();
                _lockedAssemblies = true;

                try
                {
                    _uploadTask = _controller.UpsertPlatformsAsync(
                        platformOptions,
                        form: _assetUpsertForm,
                        cancellationToken: _uploadCancellation.Token);
                }
                catch (Exception ex)
                {
                    HandleUploadException(ex);
                    return;
                }

                _phase = Phase.Uploading;

                Update();

                EditorApplication.update += Update;
            }

            private void PersistPendingUploadState()
            {
                try
                {
                    var ownerId = _owner?._idProperty?.stringValue;
                    var formId = _assetUpsertForm?.Id?.ToString();
                    var serverId = !string.IsNullOrWhiteSpace(formId) ? formId : ownerId;
                    var state = new PendingBundleUploadState
                    {
                        AssetType = typeof(TAsset).AssemblyQualifiedName,
                        AssetServerId = serverId,
                        AssetName = _assetUpsertForm?.Name,
                        BundlePath = _bundlePath,
                        AssetUpsertFormJson = JsonConvert.SerializeObject(_assetUpsertForm),
                        SuppressDialog = _suppressDialog,
                        Builds = _builds
                            ?.Select(b => new PendingBundleUploadState.PendingBuild
                            {
                                Platforms = (int)b.Platforms,
                                OutputPath = b.OutputPath,
                            })
                            .ToArray(),
                        TotalBytes = _builds?.Sum(b =>
                        {
                            try { return new FileInfo(b.OutputPath).Length; }
                            catch { return 0L; }
                        }) ?? 0,
                        StartedUtc = DateTime.UtcNow.ToString("O"),
                    };

                    SavePendingUploadState(state);
                }
                catch
                {
                    // ignored
                }
            }

            private void Update()
            {
                try
                {
                    if (_phase == Phase.Completed)
                        return;

                    if (_uploadCancellation is { IsCancellationRequested: false } &&
                        Application.internetReachability == NetworkReachability.NotReachable)
                    {
                        _uploadCancellation.Cancel();
                        MetaverseProgram.Logger.Log("Internet not reachable. Download cancelled.");
                    }

                    switch (_phase)
                    {
                        case Phase.Uploading:
                            UpdateUploadProgressBar();
                            if (_uploadTask is { IsCompleted: true })
                            {
                                _stopwatch?.Stop();
                                _uploadDurationSeconds = _stopwatch?.Elapsed.TotalSeconds ?? 0;
                                EditorUtility.ClearProgressBar();
                                UnlockAssemblies();
                                _phase = Phase.HandleUploadResponse;
                            }
                            break;

                        case Phase.HandleUploadResponse:
                            HandleUploadResponse();
                            break;

                        case Phase.WaitingForDto:
                            WaitForDto();
                            break;

                        case Phase.WaitingForError:
                            WaitForError();
                            break;

                        case Phase.WaitingForSessionRefresh:
                            WaitForSessionRefresh();
                            break;
                    }
                }
                catch (Exception ex)
                {
                    HandleUploadException(ex);
                }
            }

            private void UpdateUploadProgressBar()
            {
                if (_uploadTask == null)
                    return;

                var now = EditorApplication.timeSinceStartup;
                if (now - _lastUiUpdate < 0.1d)
                    return;

                _lastUiUpdate = now;

                var uploadSizeMB = _totalBytes / 1024f / 1024f;
                var elapsed = _stopwatch?.Elapsed.TotalSeconds ?? 0;

                var hasSavedUploadSpeed = EditorPrefs.HasKey(UploadSpeedPrefKey);
                var hasEstimate = _totalBytes > 0 && _estimatedSeconds > 0;
                var normalizedProgress = hasEstimate
                    ? Math.Min(elapsed / _estimatedSeconds, 0.95d)
                    : 0.25d + 0.5d * Math.Abs(Math.Sin(elapsed * 0.5d));

                var isVerifying = hasEstimate && elapsed >= _estimatedSeconds;
                double? etaSecondsRemaining = null;
                if (hasEstimate && hasSavedUploadSpeed)
                    etaSecondsRemaining = Math.Max(_estimatedSeconds - elapsed, 0);

                var progressMessage = !isVerifying
                    ? hasSavedUploadSpeed && hasEstimate
                        ? $"Uploading assets... ETA {FormatEta(etaSecondsRemaining)}"
                        : "Uploading assets..."
                    : elapsed >= _estimatedSeconds * 2
                        ? "Still uploading... (taking longer than expected)"
                        : "Verifying upload with server...";

                if (_suppressDialog)
                    progressMessage = "[Batch] " + progressMessage;

                var canceled = EditorUtility.DisplayCancelableProgressBar(
                    $"Uploading \"{_assetUpsertForm.Name}\" ({uploadSizeMB:N2} MB)",
                    progressMessage,
                    (float)normalizedProgress);

                if (canceled)
                    _uploadCancellation?.Cancel();
            }

            private void HandleUploadResponse()
            {
                if (_uploadCancellation is { IsCancellationRequested: true })
                {
                    MetaverseProgram.Logger.Log($"Upload cancelled for '{_assetUpsertForm.Name}'.");
                    if (_suppressDialog)
                        ShowEditorNotification($"Upload cancelled: {_assetUpsertForm.Name}");
                    _onError?.Invoke("Upload cancelled.");
                    Complete();
                    return;
                }

                if (_uploadTask == null)
                {
                    _onError?.Invoke("Unknown upload error.");
                    Complete();
                    return;
                }

                if (_uploadTask.IsFaulted)
                {
                    HandleUploadException(_uploadTask.Exception?.GetBaseException() ?? _uploadTask.Exception);
                    return;
                }

                if (_uploadTask.IsCanceled)
                {
                    if (_suppressDialog)
                        ShowEditorNotification($"Upload cancelled: {_assetUpsertForm.Name}");
                    _onError?.Invoke("Upload cancelled.");
                    Complete();
                    return;
                }

                _uploadResponse = _uploadTask.Result;
                if (_uploadResponse.Succeeded)
                {
                    ShowFinalizingProgress("Finalizing...");
                    _dtoTask = _uploadResponse.GetResultAsync();
                    _phase = Phase.WaitingForDto;
                }
                else
                {
                    ShowFinalizingProgress("Retrieving error...");
                    _errorTask = _uploadResponse.GetErrorAsync();
                    _phase = Phase.WaitingForError;
                }
            }

            private void WaitForDto()
            {
                if (_dtoTask == null)
                {
                    HandleUploadException("Unknown upload response.");
                    return;
                }

                if (!_dtoTask.IsCompleted)
                {
                    ShowFinalizingProgress("Finalizing...");
                    return;
                }

                if (_dtoTask.IsFaulted)
                {
                    HandleUploadException(_dtoTask.Exception?.GetBaseException() ?? _dtoTask.Exception);
                    return;
                }

                var dto = _dtoTask.Result;
                var platformString = "\n- " + string.Join("\n- ", _builds.Select(x => x.Platforms.ToString()));

                MetaverseProgram.Logger.Log(
                    $"<b><color=green>Successfully</color></b> uploaded bundles for '{_assetUpsertForm.Name}'.\n{platformString}");

                if (!_suppressDialog)
                    EditorUtility.DisplayDialog(
                        "Upload Successful",
                        $"\"{_assetUpsertForm.Name}\" was uploaded successfully!{platformString}",
                        "Ok");
                else
                    ShowEditorNotification($"Upload complete: {_assetUpsertForm.Name}");

                TryPersistUploadSpeed(_totalBytes, _uploadDurationSeconds);

                if (_assetUpsertForm.Listings != dto.Listings && !_suppressDialog)
                {
                    EditorUtility.DisplayDialog(
                        "Listings Notice",
                        $"Though you selected {_assetUpsertForm.Listings} listing(s) for your asset, the server only allowed \"{dto.Listings}\". This can " +
                        "happen if you are not a verified creator. Please check our documentation for publishing requirements.",
                        "Ok");
                }

                if (!_owner.Target)
                {
                    if (typeof(MetaSpace).IsAssignableFrom(typeof(TAsset)))
                    {
                        var asset = FindObjectOfType<TAsset>(true);
                        if (asset)
                        {
                            _owner.ApplyMetaData(new SerializedObject(asset), dto);
                            _owner.ClearBundleRetryInfo(asset);
                        }
                    }
                }
                else
                {
                    _owner.ApplyMetaData(_owner.serializedObject, dto);
                    _owner.ClearBundleRetryInfo(_owner.Target);
                }

                _onBuildSuccess?.Invoke(dto, _builds);
                Complete();
            }

            private void WaitForError()
            {
                if (_errorTask == null)
                {
                    HandleUploadException("Unknown upload error.");
                    return;
                }

                if (!_errorTask.IsCompleted)
                {
                    ShowFinalizingProgress("Retrieving error...");
                    return;
                }

                if (_errorTask.IsFaulted)
                {
                    HandleUploadException(_errorTask.Exception?.GetBaseException() ?? _errorTask.Exception);
                    return;
                }

                var error = (_errorTask.Result?.ToPrettyErrorString() ?? "Unknown error.").ToPrettyErrorString();
                MetaverseProgram.Logger.Log($"<b><color=red>Upload Failed</color></b> for '{_assetUpsertForm.Name}': {error}");

                if (_suppressDialog)
                {
                    var shortError = error;
                    if (!string.IsNullOrWhiteSpace(shortError) && shortError.Length > 140)
                        shortError = shortError[..140] + "...";
                    ShowEditorNotification($"Upload failed: {_assetUpsertForm.Name}\n{shortError}");
                }

                var isAuthError = error.Contains("Unauthorized") || error.Contains("401");
                if (isAuthError)
                {
                    ShowFinalizingProgress("Refreshing session...");
                    _sessionTask = MetaverseProgram.ApiClient.Account.EnsureValidSessionAsync();
                    _phase = Phase.WaitingForSessionRefresh;
                    return;
                }

                _owner.StoreBundleInfoForRetry(_builds);
                if (!_suppressDialog)
                {
                    _owner.ShowUploadFailureDialog(error,
                        () => _owner.UploadBundles(
                            _controller,
                            _bundlePath,
                            _builds,
                            _assetUpsertForm,
                            _onBuildSuccess,
                            _onError,
                            _tries + 1,
                            _suppressDialog),
                        () => _onError?.Invoke(error));
                }
                else
                {
                    _onError?.Invoke(error);
                }

                Complete();
            }

            private void WaitForSessionRefresh()
            {
                if (_sessionTask == null)
                {
                    _onError?.Invoke("Authentication error.");
                    Complete();
                    return;
                }

                if (!_sessionTask.IsCompleted)
                {
                    ShowFinalizingProgress("Refreshing session...");
                    return;
                }

                if (_sessionTask.IsFaulted)
                {
                    HandleUploadException(_sessionTask.Exception?.GetBaseException() ?? _sessionTask.Exception);
                    return;
                }

                var tokenResult = _sessionTask.Result;
                if (tokenResult.Succeeded && !tokenResult.RequiresReauthentication)
                {
                    MetaverseProgram.Logger.Log($"Authentication error detected. Retrying upload after session refresh (attempt {_tries + 1}/3)...");
                    _owner.UploadBundles(
                        _controller,
                        _bundlePath,
                        _builds,
                        _assetUpsertForm,
                        _onBuildSuccess,
                        _onError,
                        _tries + 1,
                        _suppressDialog);
                    Complete();
                    return;
                }

                if (tokenResult.RequiresReauthentication)
                {
                    MetaverseProgram.Logger.Log($"Authentication required. Opening login to retry upload (attempt {_tries + 1}/3)...");
                    MetaverseAccountWindow.Open(() =>
                    {
                        _owner.UploadBundles(
                            _controller,
                            _bundlePath,
                            _builds,
                            _assetUpsertForm,
                            _onBuildSuccess,
                            _onError,
                            _tries + 1,
                            _suppressDialog);
                    });
                    Complete();
                    return;
                }

                _onError?.Invoke("Authentication error.");
                Complete();
            }

            private void ShowFinalizingProgress(string message)
            {
                var uploadSizeMB = _totalBytes / 1024f / 1024f;
                EditorUtility.DisplayProgressBar(
                    $"Uploading \"{_assetUpsertForm.Name}\" ({uploadSizeMB:N2} MB)",
                    message,
                    1f);
            }

            private void HandleUploadException(object exception)
            {
                var ex = exception as Exception;
                ex = ex?.GetBaseException() ?? ex;
                var msg = ex?.ToString() ?? exception?.ToString() ?? "Unknown upload error.";

                MetaverseProgram.Logger.Log($"<b><color=red>Exception</color></b> during upload: {msg}");

                if (_suppressDialog)
                {
                    var shortError = msg;
                    if (!string.IsNullOrWhiteSpace(shortError) && shortError.Length > 140)
                        shortError = shortError[..140] + "...";
                    ShowEditorNotification($"Upload error: {_assetUpsertForm.Name}\n{shortError}");
                }

                if (_builds is { Length: > 0 })
                    _owner.StoreBundleInfoForRetry(_builds);

                if (!_suppressDialog)
                {
                    _owner.ShowUploadFailureDialog(msg,
                        () => _owner.UploadBundles(
                            _controller,
                            _bundlePath,
                            _builds,
                            _assetUpsertForm,
                            _onBuildSuccess,
                            _onError,
                            _tries + 1,
                            _suppressDialog),
                        () => _onError?.Invoke(ex ?? (object)msg));
                }
                else
                {
                    _onError?.Invoke(ex ?? (object)msg);
                }

                Complete();
            }

            private void UnlockAssemblies()
            {
                if (_lockedAssemblies)
                {
                    EditorApplication.UnlockReloadAssemblies();
                    _lockedAssemblies = false;
                }
            }

            private void Complete()
            {
                if (_phase == Phase.Completed)
                    return;

                _phase = Phase.Completed;
                EditorApplication.update -= Update;

                try { EditorUtility.ClearProgressBar(); } catch { /* ignored */ }
                UnlockAssemblies();

                _uploadCancellation?.Dispose();
                _uploadCancellation = null;

                foreach (var stream in _openStreams)
                    try { stream?.Dispose(); } catch { /* ignored */ }
                _openStreams.Clear();

                // If we reach completion normally, clear any pending-resume marker.
                ClearPendingUploadState();

                DecrementUploadInProgress();
            }
        }

        private void ShowUploadFailureDialog(string errorMessage, Action retryCallback = null, Action doneCallback = null)
        {
            EditorUtility.ClearProgressBar();

            var retry = EditorUtility.DisplayDialog(
                "Upload Failed",
                $"{errorMessage.ToPrettyErrorString()}\n\nWould you like to retry the upload?",
                "Retry",
                "Cancel");

            if (retry)
                retryCallback?.Invoke();
            else
                doneCallback?.Invoke();
        }

        private static void UploadFailure(object error)
        {
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("Upload Failed", $"{error.ToPrettyErrorString()}", "Ok");
            MetaverseProgram.Logger.Log(error?.ToString() ?? "Unknown upload error.");
        }

        private static string FormatEta(double? secondsRemaining)
        {
            if (secondsRemaining is not double remaining || double.IsNaN(remaining) || double.IsInfinity(remaining))
                return "--:--";

            remaining = Math.Max(remaining, 0);
            var timeSpan = TimeSpan.FromSeconds(remaining);

            if (timeSpan.TotalHours >= 1)
                return $"{(int)timeSpan.TotalHours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";

            return $"{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
        }

        private static void TryPersistUploadSpeed(long totalBytes, double uploadDurationSeconds)
        {
            if (totalBytes <= 0)
                return;

            if (uploadDurationSeconds <= 0)
                return;

            var bytesPerSecond = totalBytes / uploadDurationSeconds;
            if (double.IsNaN(bytesPerSecond) || double.IsInfinity(bytesPerSecond) || bytesPerSecond <= 0)
                return;

            var clampedBytesPerSecond = (float)Math.Min(bytesPerSecond, float.MaxValue);
            if (clampedBytesPerSecond <= 0)
                return;

            EditorPrefs.SetFloat(UploadSpeedPrefKey, clampedBytesPerSecond);
        }

        private void StoreBundleInfoForRetry(IEnumerable<MetaverseAssetBundleAPI.BundleBuild> builds)
        {
            if (Target == null)
                return;

            var buildsArray = builds as MetaverseAssetBundleAPI.BundleBuild[] ?? builds.ToArray();

            Target.LastUploadFailed = true;
            Target.LastBundlePlatforms = buildsArray.Select(x => x.Platforms.ToString()).ToArray();
            Target.LastBundlePaths = buildsArray.Select(x => x.OutputPath).ToArray();

            EditorUtility.SetDirty(Target);
            serializedObject.ApplyModifiedProperties();
            AssetDatabase.SaveAssetIfDirty(Target);
        }

        private void ClearBundleRetryInfo(TAsset asset)
        {
            if (asset == null)
                return;

            asset.LastUploadFailed = false;
            asset.LastBundlePlatforms = System.Array.Empty<string>();
            asset.LastBundlePaths = System.Array.Empty<string>();

            EditorUtility.SetDirty(asset);
            try
            {
                if (serializedObject?.targetObject == asset)
                    serializedObject.ApplyModifiedProperties();
            }
            catch
            {
                // ignored
            }
            AssetDatabase.SaveAssetIfDirty(asset);
        }

        protected void ApplyMetaData(SerializedObject obj, TAssetDto dto)
        {
            var idProperty = obj.FindProperty("id");
            if (idProperty.stringValue != dto?.Id.ToString())
                idProperty.stringValue = dto?.Id.ToString();

            var blockchainSourceProperty = obj.FindProperty("blockchainSource");
            var blockchainTypeProperty = obj.FindProperty("blockchainType");
            var objTargetObject = obj.targetObject as TAsset;
            
            if (dto != null)
            {
                if (blockchainSourceProperty.stringValue != dto.BlockchainSource)
                    blockchainSourceProperty.stringValue = dto.BlockchainSource;
                if (blockchainTypeProperty.enumValueIndex != (int)dto.BlockchainSourceType)
                    blockchainTypeProperty.enumValueIndex = (int)dto.BlockchainSourceType;

                var metaDataProperty = obj.FindProperty("metaData");
                if (metaDataProperty != null)
                {
                    var nameProperty = metaDataProperty.FindPropertyRelative("name");
                    if (nameProperty.stringValue != dto.Name)
                        nameProperty.stringValue = dto.Name;
                    var descriptionProperty = metaDataProperty.FindPropertyRelative("description");
                    if (descriptionProperty.stringValue != dto.Description)
                        descriptionProperty.stringValue = dto.Description;
                    var listingsProperty = metaDataProperty.FindPropertyRelative("listings");
                    if ((int)dto.Listings != listingsProperty.intValue)
                        listingsProperty.intValue = (int)dto.Listings;
                    var privateProperty = metaDataProperty.FindPropertyRelative("private");
                    if (privateProperty.boolValue != dto.Private)
                        privateProperty.boolValue = dto.Private;

                    UpdateBlockchainReferences(dto, obj);
                }
                
                if (obj.ApplyModifiedProperties())
                    EditorUtility.SetDirty(obj.targetObject);

                OnUpdateMetaDataInternal(objTargetObject, dto, obj);
            }
            else
            {
                OnClearMetaDataInternal(objTargetObject);
            }

            if (obj.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(obj.targetObject);
            }
        }

        private static void UpdateBlockchainReferences(AssetDto dto, SerializedObject serObj)
        {
            if (dto.BlockchainReferences == null)
                return;

            var blockchainCategoriesProp = serObj.FindProperty("metaData").FindPropertyRelative("blockchainReferences").FindPropertyRelative("categories");
            var isDifferent = blockchainCategoriesProp.arraySize != dto.BlockchainReferences.Categories.Count;
            if (!isDifferent)
            {
                foreach (var category in dto.BlockchainReferences.Categories)
                {
                    if (isDifferent)
                        break;
                    for (var i = 0; i < blockchainCategoriesProp.arraySize; i++)
                    {
                        var prop = blockchainCategoriesProp.GetArrayElementAtIndex(i);
                        if (prop.FindPropertyRelative("category").stringValue == category.Category &&
                            prop.FindPropertyRelative("type").enumValueIndex == (int)category.Type)
                            continue;

                        isDifferent = true;
                        break;
                    }
                }
            }

            if (isDifferent)
            {
                blockchainCategoriesProp.ClearArray();

                foreach (var category in dto.BlockchainReferences.Categories)
                {
                    blockchainCategoriesProp.InsertArrayElementAtIndex(0);

                    var prop = blockchainCategoriesProp.GetArrayElementAtIndex(0);
                    prop.FindPropertyRelative("category").stringValue = category.Category;
                    prop.FindPropertyRelative("type").enumValueIndex = (int)category.Type;
                }
            }

            var blockchainAssetsProp = serObj.FindProperty("metaData").FindPropertyRelative("blockchainReferences").FindPropertyRelative("assets");
            isDifferent = blockchainAssetsProp.arraySize != dto.BlockchainReferences.Assets.Count;
            
            if (!isDifferent)
            {
                foreach (var asset in dto.BlockchainReferences.Assets)
                {
                    if (isDifferent)
                        break;
                    for (var i = 0; i < blockchainAssetsProp.arraySize; i++)
                    {
                        var prop = blockchainAssetsProp.GetArrayElementAtIndex(i);
                        if (prop.FindPropertyRelative("asset").stringValue == asset.Asset &&
                            prop.FindPropertyRelative("type").enumValueIndex == (int)asset.Type)
                            continue;

                        isDifferent = true;
                        break;
                    }
                }
            }

            if (isDifferent)
            {
                blockchainAssetsProp.ClearArray();

                foreach (var asset in dto.BlockchainReferences.Assets)
                {
                    blockchainAssetsProp.InsertArrayElementAtIndex(0);

                    var prop = blockchainAssetsProp.GetArrayElementAtIndex(0);
                    prop.FindPropertyRelative("asset").stringValue = asset.Asset;
                    prop.FindPropertyRelative("type").enumValueIndex = (int)asset.Type;
                }
            }
        }
    }
}
