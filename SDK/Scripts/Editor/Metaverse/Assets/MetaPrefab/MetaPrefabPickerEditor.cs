using System.Collections.Generic;
using System.Threading.Tasks;
using MetaverseCloudEngine.ApiClient;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Common.Models.QueryParams;

namespace MetaverseCloudEngine.Unity.Editors
{
    public class MetaPrefabPickerEditor : AssetPickerEditor<PrefabDto, PrefabQueryParams>
    {
        protected override bool AmUserOnly => false;

        protected override Task<ApiResponse<IEnumerable<PrefabDto>>> QueryAssetsAsync(PrefabQueryParams queryParams)
        {
            queryParams.AdvancedSearch = false;
            return MetaverseProgram.ApiClient.Prefabs.GetAllAsync(queryParams);
        }
    }
}
