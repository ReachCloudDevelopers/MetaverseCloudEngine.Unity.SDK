using System.Collections.Generic;
using System.Threading.Tasks;
using MetaverseCloudEngine.ApiClient;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Common.Models.QueryParams;

namespace MetaverseCloudEngine.Unity.Editors
{
    public class LandPlotPickerEditor : AssetPickerEditor<LandPlotDto, LandPlotQueryParams>
    {
        protected override Task<ApiResponse<IEnumerable<LandPlotDto>>> QueryAssetsAsync(LandPlotQueryParams queryParams)
        {
            return MetaverseProgram.ApiClient.Land.GetAllAsync(queryParams);
        }
    }
}