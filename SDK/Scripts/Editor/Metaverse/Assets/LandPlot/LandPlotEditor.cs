using System.Linq;
using MetaverseCloudEngine.ApiClient;
using MetaverseCloudEngine.ApiClient.Controllers;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Common.Models.Forms;
using MetaverseCloudEngine.Common.Models.QueryParams;
using MetaverseCloudEngine.Unity.Assets.LandPlots;
using UnityEditor;

namespace MetaverseCloudEngine.Unity.Editors
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(LandPlot))]
    public class LandPlotEditor : AssetEditor<LandPlot, LandPlotMetadata, LandPlotDto, LandPlotQueryParams, LandPlotUpsertForm, LandPlotPickerEditor>
    {
        protected override object GetMainAsset(LandPlot asset) => null;

        public override AssetController<LandPlotDto, LandPlotQueryParams, LandPlotUpsertForm> Controller => MetaverseProgram.ApiClient.Land;

        protected override void DrawID()
        {
            if (IsMetaSpaceSourceLandPlot())
                return;
            
            base.DrawID();
        }

        protected override void DrawUploadControls()
        {
            if (IsMetaSpaceSourceLandPlot())
            {
                EditorGUILayout.HelpBox("Land plot cannot be uploaded or updated because a 'Meta Space Source Land Plot' is attached.", MessageType.Info);
                return;
            }
            
            base.DrawUploadControls();
        }

        private bool IsMetaSpaceSourceLandPlot()
        {
            return targets.Any(x => ((LandPlot) x).GetComponent<MetaSpaceSourceLandPlot>());
        }
    }
}