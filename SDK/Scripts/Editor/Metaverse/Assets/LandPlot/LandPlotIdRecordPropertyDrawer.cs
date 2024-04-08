using MetaverseCloudEngine.ApiClient;
using MetaverseCloudEngine.ApiClient.Controllers;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Common.Models.QueryParams;
using MetaverseCloudEngine.Unity.Assets.Attributes;
using UnityEditor;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors
{
    [CustomPropertyDrawer(typeof(LandPlotIdPropertyAttribute))]
    public class LandPlotIdRecordPropertyDrawer : AssetRecordIdPropertyDrawer<LandPlotDto, LandPlotQueryParams, LandPlotPickerEditor>
    {
        protected override IAssetController<LandPlotDto> Controller => MetaverseProgram.ApiClient.Land;
        protected override Texture GetIcon() => EditorGUIUtility.IconContent("TerrainInspector.TerrainToolSculpt On").image;
    }
}