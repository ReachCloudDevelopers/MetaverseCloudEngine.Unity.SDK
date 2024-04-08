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
        private MetaSpaceExternalServicesListEditor _metaSpaceListEditor;

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

        protected override MetaSpaceUpsertForm GetUpsertForm(Guid? id, MetaSpace asset, bool willUpload)
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
            metaData.FindPropertyRelative("vrSupport").enumValueFlag = (int)assetDto.VRSupport;
            metaData.FindPropertyRelative("arRequired").boolValue = assetDto.ARRequired;
            metaData.FindPropertyRelative("requiredTrackingDetails").enumValueFlag = (int)assetDto.RequiredUserTrackingDetails;
            metaData.FindPropertyRelative("joinBehavior").enumValueFlag = (int)assetDto.SceneJoinBehavior;
            metaData.FindPropertyRelative("joinRequirements").enumValueFlag = (int)assetDto.SceneJoinRequirements;
            metaData.FindPropertyRelative("allowConcurrentLogins").boolValue = assetDto.AllowConcurrentLogins;
            metaData.FindPropertyRelative("tags").enumValueFlag = (int)assetDto.Tags;
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
            loadOnStartPrefabsProperty.ClearArray();

            foreach (var loadOnStartPrefabs in dto.LoadOnStartPrefabs)
            {
                loadOnStartPrefabsProperty.InsertArrayElementAtIndex(0);
                var prop = loadOnStartPrefabsProperty.GetArrayElementAtIndex(0);
                prop.FindPropertyRelative(nameof(MetaPrefabToLoadOnStart.prefab)).stringValue = loadOnStartPrefabs.PrefabId.ToString();
                prop.FindPropertyRelative(nameof(MetaPrefabToLoadOnStart.spawnAuthority)).enumValueIndex = loadOnStartPrefabs.RequireMasterClient ? 0 : loadOnStartPrefabs.DontSpawn ? 2 : 1; 
                prop.FindPropertyRelative(nameof(MetaPrefabToLoadOnStart.disabled)).boolValue = !loadOnStartPrefabs.Enabled;
            }
        }

        protected override void DrawListEditors()
        {
            base.DrawListEditors();

            if (Target.ID == null)
                return;
            
            _metaSpaceListEditor ??= new MetaSpaceExternalServicesListEditor(Target);
            _metaSpaceListEditor.Draw();
        }
    }
}
