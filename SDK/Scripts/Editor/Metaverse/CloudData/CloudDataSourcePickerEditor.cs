using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Common.Models.QueryParams;

namespace MetaverseCloudEngine.Unity.Editors
{
    public class CloudDataSourcePickerEditor : QueryablePickerEditor<CloudDataSourceDto, CloudDataSourceQueryParams>
    {
        protected override async Task<IEnumerable<CloudDataSourceDto>> QueryAsync(CloudDataSourceQueryParams queryParams)
        {
            var result = await MetaverseProgram.ApiClient.CloudData.GetAllSourcesAsync(queryParams);
            if (!result.Succeeded) return Array.Empty<CloudDataSourceDto>();
            return await result.GetResultAsync();
        }

        protected override CloudDataSourceQueryParams GetQueryParams(int count, int offset, string filter)
        {
            return new CloudDataSourceQueryParams
            {
                Count = (uint)count,
                Offset = (uint)offset,
                NameFilter = filter
            };
        }
    }
}