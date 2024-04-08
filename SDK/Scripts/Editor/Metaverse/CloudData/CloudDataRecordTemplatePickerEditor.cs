using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Common.Models.QueryParams;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors
{
    public class CloudDataRecordTemplatePickerEditor : QueryablePickerEditor<CloudDataRecordTemplateDto, CloudDataRecordTemplateQueryParams>
    {
        protected override async Task<IEnumerable<CloudDataRecordTemplateDto>> QueryAsync(CloudDataRecordTemplateQueryParams queryParams)
        {
            var result = await MetaverseProgram.ApiClient.CloudData.GetAllTemplatesAsync(queryParams);
            if (!result.Succeeded) return Array.Empty<CloudDataRecordTemplateDto>();
            return await result.GetResultAsync();
        }

        protected override GUIContent GetPickableContent(object pickable)
        {
            var recordTemplate = (CloudDataRecordTemplateDto)pickable;
            return new GUIContent($"{recordTemplate.Name} ({recordTemplate.Id.ToString()[..4]}...) [Source: '{recordTemplate.DataSource.Name}' ({recordTemplate.DataSource.Id.ToString()[..4]}...)]");
        }

        protected override CloudDataRecordTemplateQueryParams GetQueryParams(int count, int offset, string filter)
        {
            return new CloudDataRecordTemplateQueryParams
            {
                Count = (uint)count,
                Offset = (uint)offset,
                NameFilter = filter,
            };
        }
    }
}