using System;
using System.Collections;
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
            int option = EditorUtility.DisplayDialogComplex(
                "Build Failed",
                $"Asset bundle building process failed. {e.ToPrettyErrorString()}\n\nWould you like to retry the build?",
                "Retry",
                "Cancel",
                "Ok");

            // If user clicked "Retry" (option 0), attempt retry immediately
            if (option == 0)
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
            IncrementUploadInProgress();
            try
            {
                if (Application.internetReachability == NetworkReachability.NotReachable)
                {
                    if (EditorUtility.DisplayDialog(
                            "Internet Connection Required",
                            "Please check your internet connection and try again.",
                            "Retry",
                            "Cancel"))
                    {
                        UploadBundles(
                            controller,
                            bundlePath,
                            builds,
                            assetUpsertForm,
                            onBuildSuccess,
                            onError,
                            tries + 1,
                            suppressDialog);
                    }
                    else
                    {
                        onError?.Invoke("Upload cancelled.");
                    }

                    return;
                }

                if (tries >= 3)
                {
                    StoreBundleInfoForRetry(builds);

                    var retry = !suppressDialog
                        ? EditorUtility.DisplayDialog(
                            "Upload Failed",
                            "Uploading failed, please check your internet connection, or log-in and try again. If the issue persists, please restart Unity.",
                            "Retry",
                            "Cancel")
                        : false;

                    if (retry)
                        UploadBundles(
                            controller,
                            bundlePath,
                            builds,
                            assetUpsertForm,
                            onBuildSuccess,
                            onError,
                            tries,
                            suppressDialog);
                    else
                        onError?.Invoke("Upload cancelled.");

                    return;
                }

                var buildsArray = builds as MetaverseAssetBundleAPI.BundleBuild[] ?? builds.ToArray();

                try
                {
                    ExecuteUpload(controller, bundlePath, buildsArray, assetUpsertForm, onBuildSuccess, onError, tries, suppressDialog);
                }
                catch (Exception ex)
                {
                    ex = ex.GetBaseException();
                    MetaverseProgram.Logger.Log($"<b><color=red>Exception</color></b> during upload: {ex}");

                    StoreBundleInfoForRetry(buildsArray);
                    if (!suppressDialog)
                    {
                        ShowUploadFailureDialog(ex.ToString(),
                            () => UploadBundles(
                                controller,
                                bundlePath,
                                buildsArray,
                                assetUpsertForm,
                                onBuildSuccess,
                                onError),
                            () => onError?.Invoke(ex));
                    }
                    else
                    {
                        // In batch/suppressed-dialog mode, don't silently auto-retry on exceptions.
                        // Surface the error so callers like BatchBuilderWindow can mark the
                        // asset as failed and continue/stop according to their policy.
                        onError?.Invoke(ex);
                    }
                }
            }
            finally
            {
                DecrementUploadInProgress();
            }
        }

        private void ExecuteUpload(
            IUpsertAssets<TAssetDto, TAssetUpsertForm> controller,
            string bundlePath,
            MetaverseAssetBundleAPI.BundleBuild[] builds,
            TAssetUpsertForm assetUpsertForm,
            Action<AssetDto, IEnumerable<MetaverseAssetBundleAPI.BundleBuild>> onBuildSuccess,
            Action<object> onError,
            int tries,
            bool suppressDialog)
        {
            var openStreams = new List<Stream>();
            var platformOptions = builds.Select(x =>
            {
                var stream = File.OpenRead(x.OutputPath);
                openStreams.Add(stream);
                return new AssetPlatformUpsertOptions
                {
                    Stream = stream,
                    AssetPath = bundlePath,
                    Platform = x.Platforms,
                };
            }).ToArray();

            var totalBytes = builds.Sum(x => new FileInfo(x.OutputPath).Length);
            double uploadDurationSeconds = 0;

            using var uploadCancellation = new CancellationTokenSource();
            var uploadTask = controller.UpsertPlatformsAsync(
                platformOptions,
                form: assetUpsertForm,
                cancellationToken: uploadCancellation.Token);

            try
            {
                MonitorUploadProgress(uploadTask, assetUpsertForm.Name, totalBytes, uploadCancellation, suppressDialog, duration => uploadDurationSeconds = duration);

                if (uploadCancellation.IsCancellationRequested)
                {
                    onError?.Invoke("Upload cancelled.");
                    return;
                }

                ApiResponse<TAssetDto> uploadResponse;
                try
                {
                    uploadResponse = uploadTask.GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                    onError?.Invoke("Upload cancelled.");
                    return;
                }

                var platformString = "\n- " + string.Join("\n- ", builds.Select(x => x.Platforms.ToString()));
                if (uploadResponse.Succeeded)
                {
                    var dto = uploadResponse.GetResultAsync().GetAwaiter().GetResult();

                    MetaverseProgram.Logger.Log(
                        $"<b><color=green>Successfully</color></b> uploaded bundles for '{assetUpsertForm.Name}'.\n{platformString}");

                    if (!suppressDialog)
                        EditorUtility.DisplayDialog(
                            "Upload Successful",
                            $"\"{assetUpsertForm.Name}\" was uploaded successfully!{platformString}",
                            "Ok");

                    TryPersistUploadSpeed(totalBytes, uploadDurationSeconds);

                    if (assetUpsertForm.Listings != dto.Listings && !suppressDialog)
                    {
                        EditorUtility.DisplayDialog(
                            "Listings Notice",
                            $"Though you selected {assetUpsertForm.Listings} listing(s) for your asset, the server only allowed \"{dto.Listings}\". This can " +
                            "happen if you are not a verified creator. Please check our documentation for publishing requirements.",
                            "Ok");
                    }

                    if (!Target)
                    {
                        // If the editor target has gone missing (for example, the scene was
                        // reloaded or the object was unloaded), we still want the upload to
                        // be treated as successful so that batch operations can continue.
                        // For MetaSpaces we make a best-effort attempt to find an instance
                        // in the currently loaded scenes and apply the metadata to it.
                        if (typeof(MetaSpace).IsAssignableFrom(typeof(TAsset)))
                        {
                            var asset = FindObjectOfType<TAsset>(true);
                            if (asset)
                            {
                                ApplyMetaData(new SerializedObject(asset), dto);
                                ClearBundleRetryInfo(asset);
                            }
                        }
                    }
                    else
                    {
                        ApplyMetaData(serializedObject, dto);
                        ClearBundleRetryInfo(Target);
                    }

                    onBuildSuccess?.Invoke(dto, builds);
                }
                else
                {
                    var error = uploadResponse.GetErrorAsync().GetAwaiter().GetResult().ToPrettyErrorString();
                    var isAuthError = error.Contains("Unauthorized") || error.Contains("401");
                    if (isAuthError && tries < 2)
                    {
                        var tokenResult = MetaverseProgram.ApiClient.Account.EnsureValidSessionAsync().GetAwaiter().GetResult();
                        if (tokenResult.RequiresReauthentication)
                        {
                            MetaverseProgram.Logger.Log($"Authentication error detected. Retrying upload after token refresh (attempt {tries + 1}/3)...");
                            MetaverseAccountWindow.Open(() =>
                            {
                                UploadBundles(
                                    controller,
                                    bundlePath,
                                    builds,
                                    assetUpsertForm,
                                    onBuildSuccess,
                                    onError,
                                    tries + 1,
                                    suppressDialog);
                            });
                            return;
                        }

                        Thread.Sleep(500);
                        onError?.Invoke("Authentication error.");
                        return;
                    }

                    StoreBundleInfoForRetry(builds);
                    if (!suppressDialog)
                    {
                        ShowUploadFailureDialog(error,
                            () => UploadBundles(
                                controller,
                                bundlePath,
                                builds,
                                assetUpsertForm,
                                onBuildSuccess,
                                onError),
                            () => onError?.Invoke(error));
                    }
                    else
                    {
                        // When dialogs are suppressed (e.g., batch building), we should not
                        // auto-retry indefinitely with no user feedback. Instead, surface the
                        // error so callers (like BatchBuilderWindow) can mark the asset as
                        // failed and continue or stop according to their own policy.
                        onError?.Invoke(error);
                    }
                }
            }
            finally
            {
                foreach (var stream in openStreams)
                    try { stream?.Dispose(); } catch { /* ignored */ }

                EditorUtility.ClearProgressBar();
            }
        }

        private void MonitorUploadProgress(
            Task uploadTask,
            string assetName,
            long totalBytes,
            CancellationTokenSource cancellationSource,
            bool suppressDialog,
            Action<double> durationCaptured)
        {
            try
            {
                EditorApplication.LockReloadAssemblies();

                var uploadSizeMB = totalBytes / 1024f / 1024f;
                var hasSavedUploadSpeed = EditorPrefs.HasKey(UploadSpeedPrefKey);
                var savedUploadSpeed = hasSavedUploadSpeed ? Math.Max(EditorPrefs.GetFloat(UploadSpeedPrefKey), 0f) : 0f;
                var bytesPerSecondForEstimate = hasSavedUploadSpeed && savedUploadSpeed > 0f
                    ? (double)savedUploadSpeed
                    : DefaultSimulatedBytesPerSecond;
                var estimatedSeconds = totalBytes <= 0 || bytesPerSecondForEstimate <= 0
                    ? 0
                    : totalBytes / bytesPerSecondForEstimate;

                var sw = Stopwatch.StartNew();
                while (!uploadTask.IsCompleted && !cancellationSource.IsCancellationRequested)
                {
                    double progress = 0;
                    double? etaSecondsRemaining = null;
                    if (totalBytes > 0 && estimatedSeconds > 0)
                    {
                        var elapsed = sw.Elapsed.TotalSeconds;
                        progress = Math.Min(elapsed / estimatedSeconds, 1);
                        if (hasSavedUploadSpeed)
                            etaSecondsRemaining = Math.Max(estimatedSeconds - elapsed, 0);
                    }

                    var progressMessage = progress < 1
                        ? hasSavedUploadSpeed && estimatedSeconds > 0
                            ? $"Uploading assets... ETA {FormatEta(etaSecondsRemaining)}"
                            : "Uploading assets..."
                        : "Verifying upload...";

                    if (suppressDialog)
                        progressMessage = "[Batch] " + progressMessage;

                    var canceled = EditorUtility.DisplayCancelableProgressBar(
                        $"Uploading \"{assetName}\" ({uploadSizeMB:N2} MB)",
                        progressMessage,
                        (float)progress);

                    if (canceled)
                    {
                        cancellationSource.Cancel();
                        break;
                    }

                    EditorApplication.QueuePlayerLoopUpdate();
                    EditorApplication.Step();
                    Thread.Sleep(100);
                }

                sw.Stop();
                durationCaptured?.Invoke(sw.Elapsed.TotalSeconds);

                if (uploadTask.IsCompleted && !cancellationSource.IsCancellationRequested)
                {
                    EditorUtility.DisplayProgressBar(
                        $"Uploading \"{assetName}\" ({uploadSizeMB:N2} MB)",
                        "Finalizing...",
                        1f);
                    DelayRealtimeSeconds(1f);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                EditorApplication.UnlockReloadAssemblies();
            }
        }

        private static void DelayRealtimeSeconds(float seconds)
        {
            var end = EditorApplication.timeSinceStartup + seconds;
            while (EditorApplication.timeSinceStartup < end)
            {
                EditorApplication.QueuePlayerLoopUpdate();
                EditorApplication.Step();
                Thread.Sleep(15);
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
                retryCallback();
            else
                doneCallback();
        }

        private static void UploadFailure(object error)
        {
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("Upload Failed", $"{error.ToPrettyErrorString()}", "Ok");
            MetaverseProgram.Logger.Log(error.ToString());
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
