using System.Collections.Generic;
using System.Threading.Tasks;
using MetaverseCloudEngine.ApiClient;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Common.Models.QueryParams;

namespace MetaverseCloudEngine.Unity.Editors
{
    public class PublicMetaSpacePickerEditor : MetaSpacePickerEditor
    {
        protected override bool WriteableOnly => false;
    }
    
    public class MetaSpacePickerEditor : AssetPickerEditor<MetaSpaceDto, MetaSpaceQueryParams>
    {
        public override string Title => "Meta Space";

        protected override bool AmUserOnly => false;

        protected override Task<ApiResponse<IEnumerable<MetaSpaceDto>>> QueryAssetsAsync(MetaSpaceQueryParams queryParams)
        {
            return MetaverseProgram.ApiClient.MetaSpaces.GetAllAsync(queryParams);
        }
    }
}
