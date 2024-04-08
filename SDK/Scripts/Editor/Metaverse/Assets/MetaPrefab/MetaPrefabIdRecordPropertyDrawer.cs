using MetaverseCloudEngine.ApiClient;
using MetaverseCloudEngine.ApiClient.Controllers;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Common.Models.QueryParams;
using MetaverseCloudEngine.Unity.Assets.Attributes;
using UnityEditor;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors
{
    [CustomPropertyDrawer(typeof(MetaPrefabIdPropertyAttribute))]
    public class MetaPrefabIdRecordPropertyDrawer : AssetRecordIdPropertyDrawer<PrefabDto, PrefabQueryParams, MetaPrefabPickerEditor>
    {
        protected override IAssetController<PrefabDto> Controller => MetaverseProgram.ApiClient.Prefabs;
        protected override Texture GetIcon() => EditorGUIUtility.IconContent( EditorGUIUtility.isProSkin ? "d_Prefab Icon" : "Prefab Icon").image;
    }
}
