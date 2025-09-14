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

        private SerializedProperty _idProperty;
        private SerializedProperty _blockchainTypeProperty;
        private SerializedProperty _blockchainSourceProperty;
        private SerializedProperty _supportedPlatformsProperty;
        private SerializedProperty _publishProperty;
        
        private AssetContributorEditor<TAssetDto> _contributorEditor;
        private CloudDataSourceListEditor _dataSourceListEditor;

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

        public TAsset Target { get; private set; }
        
        public static bool AskToTurnOffGPUInstancing {
            get => EditorPrefs.GetBool(nameof(AskToTurnOffGPUInstancing), true);
            set => EditorPrefs.SetBool(nameof(AskToTurnOffGPUInstancing), value);
        }

        protected abstract object GetMainAsset(TAsset asset);
        protected virtual bool AllowUpdateMetadata => true;

        public abstract AssetController<TAssetDto, TAssetQueryParams, TAssetUpsertForm> Controller { get; }
        public override bool RequiresConstantRepaint() => true;

        private void Init()
        {
            if (!Target)
                Target = target as TAsset;

            _deleteIconContent ??= EditorGUIUtility.IconContent("TreeEditor.Trash");
            _refreshIconContent ??= EditorGUIUtility.IconContent("TreeEditor.Refresh");
            _detachIconContent ??= EditorGUIUtility.IconContent("UnLinked");
            _contributorEditor ??= new AssetContributorEditor<TAssetDto>(Target, Controller, Controller);
            _dataSourceListEditor ??= new CloudDataSourceListEditor(CloudDataSourceHost.Asset);

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

        protected virtual TAssetUpsertForm GetUpsertForm(Guid? id, TAsset asset, bool willUpload)
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
                BlockchainAssets = asset.MetaData.BlockchainReferences?.Assets.Select(x => new BlockchainReferenceAssetModel { Asset = x.asset, Type = x.type }).ToArray(),
                BlockchainCategories = asset.MetaData.BlockchainReferences?.Categories.Select(x => new BlockchainReferenceCategoryModel { Category = x.category, Type = x.type }).ToArray(),
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
            _dataSourceListEditor.HostId = Target.ID;
            _dataSourceListEditor?.Draw();
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
                    _dataSourceListEditor = null;
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

                _selectPlatformOption = (int)(AssetBuildPlatform)EditorGUILayout.EnumPopup("Platform", (AssetBuildPlatform)_selectPlatformOption);

                var platform = (Platform)_selectPlatformOption;
                var options = _currentPlatformOptions!.TryGetValue(platform, out var opt) ? opt : _currentPlatformOptions[platform] = new BundlePlatformOptions();

                EditorGUI.BeginChangeCheck();

                options.overrideDefaults = EditorGUILayout.Toggle("Override Defaults", options.overrideDefaults);

                if (!options.overrideDefaults)
                    GUI.enabled = false;

                options.maxTextureResolution = (BundleMaxTextureResolution)EditorGUILayout.EnumPopup("Max Texture Size", options.maxTextureResolution);
                options.compressTextures = EditorGUILayout.Toggle("Compress Textures", options.compressTextures);
                options.compressorQuality = EditorGUILayout.IntSlider("Texture Quality", options.compressorQuality, 0, 100);
                options.meshCompression = (ModelImporterMeshCompression)EditorGUILayout.EnumPopup("Mesh Compression", options.meshCompression);

                GUI.enabled = true;

                if (EditorGUI.EndChangeCheck())
                {
                    _currentPlatformOptions[platform] = options;
                    SavePlatformOptions(Guid.TryParse(_idProperty.stringValue, out var id) ? id : null);
                }
            }
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

        private void Upload(object mainAsset, TAsset asset, SerializedObject serObj, bool metadataOnly, bool allowUnauthorizedRetry = true)
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
                        if (response.StatusCode == HttpStatusCode.Unauthorized && allowUnauthorizedRetry)
                        {
                            var validate = Task.Run(async () => await MetaverseProgram.ApiClient.Account.ValidateTokenAsync()).Result;
                            if (validate.Succeeded)
                            {
                                Upload(mainAsset, asset, serObj, true, false);
                                return;
                            }

                            OnUnauthorizedUpload(() => Upload(mainAsset, asset, serObj, true, false));
                            return;
                        }

                        UploadFailure(Task.Run(async () => await response.GetErrorAsync()).Result);
                    }

                    return;
                }

                _currentPlatformOptions = originalPlatformOptions;
                
                BeginBuild(mainAsset, asset, form, Controller, (dto, _) =>
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

        private void BeginBuild(
            object mainAsset,
            TAsset asset,
            TAssetUpsertForm assetUpsertForm,
            IUpsertAssets<TAssetDto, TAssetUpsertForm> upsertController,
            Action<AssetDto, IEnumerable<MetaverseAssetBundleAPI.BundleBuild>> onBuildSuccess = null)
        {
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

        private static void OnBuildError(object e)
        {
            EditorUtility.DisplayDialog("Build Failed", $"Asset bundle building process failed. {e.ToPrettyErrorString()}", "Ok");
            EditorUtility.ClearProgressBar();
        }

        private void UploadBundles(
            IUpsertAssets<TAssetDto, TAssetUpsertForm> controller,
            string bundlePath,
            IEnumerable<MetaverseAssetBundleAPI.BundleBuild> builds,
            TAssetUpsertForm assetUpsertForm,
            Action<AssetDto, IEnumerable<MetaverseAssetBundleAPI.BundleBuild>> onBuildSuccess = null,
            int tries = 0)
        {
            if (tries >= 3)
            {
                EditorUtility.DisplayDialog(
                    "Upload Failed",
                    "Uploading failed, please check your internet connection, or log-in and try again. If the issue persists, please restart Unity.",
                    "Ok");
                return;
            }
            
            var buildsEnumerable = builds as MetaverseAssetBundleAPI.BundleBuild[] ?? builds.ToArray();
            var openStreams = new List<Stream>();
            var options = buildsEnumerable.Select(x =>
            {
                var stream = File.OpenRead(x.OutputPath);
                openStreams.Add(stream);
                return new AssetPlatformUpsertOptions
                {
                    Stream = stream,
                    AssetPath = bundlePath,
                    Platform = x.Platforms,
                };
            });

            try
            {
                var uploadCancellation = new CancellationTokenSource();
                var result = Task.Run(
                    async () =>
                        await controller
                            .UpsertPlatformsAsync(options, form: assetUpsertForm)
                            .WithCancellation(uploadCancellation.Token),
                    uploadCancellation.Token);

                try
                {
                    // Simulate 100 Mbps upload progress based on total file size
                    var totalBytes = buildsEnumerable.Sum(x => new FileInfo(x.OutputPath).Length);
                    var uploadSizeMB = totalBytes / 1024f / 1024f;

                    // 100 Mbps ~= 12.5 MB/s (MiB/s). Using 12.5 * 1024 * 1024 bytes/s
                    const double simulatedBytesPerSecond = 12.5 * 1024 * 1024;
                    var estimatedSeconds = totalBytes <= 0 ? 0 : totalBytes / simulatedBytesPerSecond;

                    var sw = Stopwatch.StartNew();
                    while (!result.IsCompleted)
                    {
                        double progress = 0;
                        if (totalBytes > 0 && estimatedSeconds > 0)
                        {
                            var elapsed = sw.Elapsed.TotalSeconds;
                            progress = Math.Min(elapsed / estimatedSeconds, 0.99); // cap until completion
                        }

                        if (EditorUtility.DisplayCancelableProgressBar(
                                $"Uploading \"{assetUpsertForm.Name}\" ({uploadSizeMB:N2} MB)",
                                "Uploading assets...",
                                (float)progress))
                        {
                            uploadCancellation.Cancel();
                            break;
                        }

                        Thread.Sleep(100);
                    }

                    // Ensure UI shows completion if task finished
                    if (result.IsCompleted)
                    {
                        EditorUtility.DisplayProgressBar(
                            $"Uploading \"{assetUpsertForm.Name}\" ({uploadSizeMB:N2} MB)",
                            "Finalizing...",
                            1f);
                    }
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }

                var exception = result.Exception?.InnerExceptions.FirstOrDefault();
                if (exception is not null)
                {
                    throw exception;
                }

                var platformsString = "\n- " + string.Join("\n- ", buildsEnumerable.Select(x => x.Platforms.ToString()));
                if (result.Result.Succeeded)
                {
                    var dto = result.Result.GetResultAsync().Result;
                    MetaverseProgram.Logger.Log(
                        $"<b><color=green>Successfully</color></b> uploaded bundles for '{assetUpsertForm.Name}'.\n" + 
                            platformsString);
                    EditorUtility.DisplayDialog("Upload Successful",
                        $"\"{assetUpsertForm.Name}\" was uploaded successfully!" + platformsString, "Ok");

                    if (assetUpsertForm.Listings != dto.Listings)
                    {
                        EditorUtility.DisplayDialog("Listings Notice",
                            $"Though you selected {assetUpsertForm.Listings} listing(s) for your asset, the server only allowed \"{dto.Listings}\". This can " +
                            "happen if you are not a verified creator. Please check our documentation for publishing requirements.",
                            "Ok");
                    }

                    if (!Target)
                    {
                        if (!typeof(MetaSpace).IsAssignableFrom(typeof(TAsset)))
                            return;
                        var asset = FindObjectOfType<TAsset>(true);
                        if (asset)
                            ApplyMetaData(new SerializedObject(asset), dto);
                    }
                    else
                        ApplyMetaData(serializedObject, dto);
                    
                    onBuildSuccess?.Invoke(dto, buildsEnumerable);
                }
                else
                {
                    MetaverseProgram.Logger.Log(
                        $"<b><color=red>Failed</color></b> to upload bundles for '{assetUpsertForm.Name}'." + platformsString);

                    if (uploadCancellation.IsCancellationRequested)
                        return;

                    if (result.Result.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        var validation = Task.Run(async () => 
                            await MetaverseProgram.ApiClient.Account.ValidateTokenAsync(), 
                            uploadCancellation.Token).Result;
                        if (validation.Succeeded)
                        {
                            UploadBundles(
                                controller, 
                                bundlePath, 
                                buildsEnumerable, 
                                assetUpsertForm, 
                                onBuildSuccess,
                                ++tries);
                            return;
                        }
                        
                        OnUnauthorizedUpload(() =>
                        {
                            EditorUtility.DisplayDialog(
                                "Retrying Upload", 
                                "You have successfully logged in. The upload will now be retried.", 
                                "Ok");
                            
                            UploadBundles(
                                controller, 
                                bundlePath, 
                                buildsEnumerable, 
                                assetUpsertForm, 
                                onBuildSuccess,
                                ++tries);
                        });
                        return;
                    }

                    var prettyErrorString = Task
                        .Run(async () => 
                            await result.Result.GetErrorAsync(), 
                            uploadCancellation.Token).Result
                        .ToPrettyErrorString();
                    
                    UploadFailure(prettyErrorString);
                }
            }
            finally
            {
                foreach (var stream in openStreams)
                    try { stream?.Dispose(); } catch { /* ignored */ }
            }
        }

        private static void OnUnauthorizedUpload(Action loginAction = null)
        {
            EditorUtility.ClearProgressBar();
            Task.Run(async () => await MetaverseProgram.ApiClient.Account
                .LogOutAsync()).Wait();
            if (!EditorUtility.DisplayDialog(
                "Upload Failed", 
                "Your session has expired or you are not authorized to modify the asset. " +
                "Please log in to an authorized account to continue uploading.", 
                "Log In", "Cancel Upload"))
                return;
            MetaverseAccountWindow.Open(loginAction);
        }

        private static void UploadFailure(object error)
        {
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("Upload Failed", $"{error.ToPrettyErrorString()}", "Ok");
            MetaverseProgram.Logger.Log(error.ToString());
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
