using System;
using System.Linq;
using MetaverseCloudEngine.ApiClient.Controllers;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Common.Models.Forms;
using MetaverseCloudEngine.Common.Models.QueryParams;
using MetaverseCloudEngine.Unity.Assets.MetaSpaces;
using UnityEditor;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors
{
    [CustomEditor(typeof(MetaSpace))]
    public class MetaSpaceEditor : AssetEditor<MetaSpace, MetaSpaceMetadata, MetaSpaceDto, MetaSpaceQueryParams, MetaSpaceUpsertForm, MetaSpacePickerEditor>
    {
        //private MetaSpaceExternalServicesListEditor _metaSpaceListEditor;

        protected override object GetMainAsset(MetaSpace asset) => asset ? asset.gameObject.scene : null;
        public override AssetController<MetaSpaceDto, MetaSpaceQueryParams, MetaSpaceUpsertForm> Controller => MetaverseProgram.ApiClient.MetaSpaces;

        protected override Texture2D AutoCaptureThumbnail(MetaSpace asset)
        {
            var thumb = base.AutoCaptureThumbnail(asset);
            if (thumb) return thumb;

            var mainCamera = Camera.main;
            if (mainCamera == null) return thumb;

            var rt = RenderTexture.GetTemporary(900, 900);
            try
            {
                mainCamera.targetTexture = rt;
                mainCamera.Render();
                return rt.Copy2D();
            }
            finally
            {
                mainCamera.targetTexture = null;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        public override MetaSpaceUpsertForm GetUpsertForm(Guid? id, MetaSpace asset, bool willUpload)
        {
            var form = base.GetUpsertForm(id, asset, willUpload);
            form.VRSupport = asset.MetaData.VRSupport;
            form.ARRequired = asset.MetaData.ARRequired;
            form.CryptoRelated = asset.MetaData.CryptoRelated;
            form.LoadOnStartPrefabs = asset.MetaData.LoadOnStartPrefabs
                .Where(x => Guid.TryParse(x.prefab, out _))
                .Select(x => new MetaSpaceUpsertFormLoadOnStartPrefab
                {
                    RequireMasterClient = x.spawnAuthority == MetaPrefabToLoadOnStart.SpawnMode.MasterClient,
                    DontSpawn = x.spawnAuthority == MetaPrefabToLoadOnStart.SpawnMode.PreloadOnly,
                    PrefabId = Guid.Parse(x.prefab),
                    Enabled = !x.disabled,
                    
                }).ToArray();
            form.SceneJoinBehavior = asset.MetaData.JoinBehavior;
            form.SceneJoinRequirements = asset.MetaData.JoinRequirements;
            form.RequiredUserTrackingDetails = asset.MetaData.RequiredTrackingDetails;
            form.AllowConcurrentLogins = asset.MetaData.AllowConcurrentLogins;
            form.Tags = asset.MetaData.Tags;
            return form;
        }

        protected override void OnUpdateMetaDataInternal(MetaSpace asset, MetaSpaceDto assetDto, SerializedObject assetSerializedObject)
        {
            var metaData = assetSerializedObject.FindProperty("metaData");
            UpdateLoadOnStartMetaPrefabs(assetDto, metaData);
            var vrSupport = metaData.FindPropertyRelative("vrSupport");
            if (vrSupport.enumValueIndex != (int)assetDto.VRSupport)
                vrSupport.enumValueFlag = (int)assetDto.VRSupport;
            var arRequired = metaData.FindPropertyRelative("arRequired");
            if (arRequired.boolValue != assetDto.ARRequired)
                arRequired.boolValue = assetDto.ARRequired;
            var requiredTrackingDetails = metaData.FindPropertyRelative("requiredTrackingDetails");
            if (requiredTrackingDetails.enumValueIndex != (int)assetDto.RequiredUserTrackingDetails)
                requiredTrackingDetails.enumValueFlag = (int)assetDto.RequiredUserTrackingDetails;
            var joinBehavior = metaData.FindPropertyRelative("joinBehavior");
            if (joinBehavior.enumValueIndex != (int)assetDto.SceneJoinBehavior)
                joinBehavior.enumValueFlag = (int)assetDto.SceneJoinBehavior;
            var joinRequirements = metaData.FindPropertyRelative("joinRequirements");
            if (joinRequirements.enumValueIndex != (int)assetDto.SceneJoinRequirements)
                joinRequirements.enumValueFlag = (int)assetDto.SceneJoinRequirements;
            var allowConcurrentLogins = metaData.FindPropertyRelative("allowConcurrentLogins");
            if (allowConcurrentLogins.boolValue != assetDto.AllowConcurrentLogins)
                allowConcurrentLogins.boolValue = assetDto.AllowConcurrentLogins;
            var tags = metaData.FindPropertyRelative("tags");
            if (tags.enumValueIndex != (int)assetDto.Tags)
                tags.enumValueFlag = (int)assetDto.Tags;
        }

        protected override void DrawUploadControls()
        {
            if (Target.gameObject.IsPrefab())
            {
                EditorGUILayout.HelpBox("You can only upload meta spaces from an open scene.", MessageType.Warning);
                return;
            }

            base.DrawUploadControls();
        }

        private static void UpdateLoadOnStartMetaPrefabs(MetaSpaceDto dto, SerializedProperty metaDataProperty)
        {
            var loadOnStartPrefabsProperty = metaDataProperty.FindPropertyRelative("loadOnStartPrefabs");
            var isDifferent = dto.LoadOnStartPrefabs.Count != loadOnStartPrefabsProperty.arraySize;
            if (!isDifferent)
            {
                foreach (var loadOnStartPrefab in dto.LoadOnStartPrefabs)
                {
                    for (var i = 0; i < loadOnStartPrefabsProperty.arraySize; i++)
                    {
                        var prop = loadOnStartPrefabsProperty.GetArrayElementAtIndex(i);
                        if (prop.FindPropertyRelative(nameof(MetaPrefabToLoadOnStart.prefab)).stringValue == loadOnStartPrefab.PrefabId.ToString())
                        {
                            var spawnAuth = loadOnStartPrefab.RequireMasterClient
                                ? (int)MetaPrefabToLoadOnStart.SpawnMode.MasterClient
                                : loadOnStartPrefab.DontSpawn
                                    ? (int)MetaPrefabToLoadOnStart.SpawnMode.PreloadOnly 
                                    : (int)MetaPrefabToLoadOnStart.SpawnMode.Local;
                            isDifferent = 
                                prop.FindPropertyRelative(nameof(MetaPrefabToLoadOnStart.spawnAuthority)).enumValueIndex != spawnAuth ||
                                prop.FindPropertyRelative(nameof(MetaPrefabToLoadOnStart.disabled)).boolValue != !loadOnStartPrefab.Enabled;
                            if (isDifferent)
                                break;
                        }
                    }
                }
            }
            
            if (!isDifferent)
                return;
            
            loadOnStartPrefabsProperty.ClearArray();

            foreach (var loadOnStartPrefabs in dto.LoadOnStartPrefabs)
            {
                loadOnStartPrefabsProperty.InsertArrayElementAtIndex(0);
                var prop = loadOnStartPrefabsProperty.GetArrayElementAtIndex(0);
                prop.FindPropertyRelative(nameof(MetaPrefabToLoadOnStart.prefab)).stringValue = loadOnStartPrefabs.PrefabId.ToString();
                prop.FindPropertyRelative(nameof(MetaPrefabToLoadOnStart.spawnAuthority)).enumValueIndex = loadOnStartPrefabs.RequireMasterClient 
                    ? (int)MetaPrefabToLoadOnStart.SpawnMode.MasterClient
                    : loadOnStartPrefabs.DontSpawn 
                        ? (int)MetaPrefabToLoadOnStart.SpawnMode.PreloadOnly
                        : (int)MetaPrefabToLoadOnStart.SpawnMode.Local;
                prop.FindPropertyRelative(nameof(MetaPrefabToLoadOnStart.disabled)).boolValue = !loadOnStartPrefabs.Enabled;
            }
        }

        protected override void DrawListEditors()
        {
            base.DrawListEditors();

            if (Target.ID == null)
                return;
            
            //_metaSpaceListEditor ??= new MetaSpaceExternalServicesListEditor(Target);
            //_metaSpaceListEditor.Draw();
        }
    }
}
