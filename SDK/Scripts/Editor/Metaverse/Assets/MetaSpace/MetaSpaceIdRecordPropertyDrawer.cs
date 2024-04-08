using MetaverseCloudEngine.ApiClient;
using MetaverseCloudEngine.ApiClient.Controllers;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Common.Models.QueryParams;
using MetaverseCloudEngine.Unity.Assets.Attributes;
using UnityEditor;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors
{
    [CustomPropertyDrawer(typeof(MetaSpaceIdPropertyAttribute))]
    public class MetaSpaceIdRecordPropertyDrawer : AssetRecordIdPropertyDrawer<MetaSpaceDto, MetaSpaceQueryParams, MetaSpacePickerEditor>
    {
        protected override IAssetController<MetaSpaceDto> Controller => MetaverseProgram.ApiClient.MetaSpaces;
        protected override Texture GetIcon() => EditorGUIUtility.IconContent("d_ToolHandleGlobal").image;
    }
}