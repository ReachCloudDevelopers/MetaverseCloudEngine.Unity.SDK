using System.Collections.Generic;
using System.Threading.Tasks;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Common.Models.QueryParams;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors
{
    public class OrganizationPicker : QueryablePickerEditor<OrganizationDto, OrganizationQueryParams>
    {
        protected override GUIContent GetPickableContent(object pickable)
        {
            var asset = (OrganizationDto)pickable;
            return new GUIContent($"{asset.Name} ({asset.Id.ToString()[..5]}...)", asset.Description);
        }

        protected override OrganizationQueryParams GetQueryParams(int count, int offset, string filter)
        {
            return new OrganizationQueryParams
            {
                Count = (uint)count,
                Offset = (uint)offset,
                NameFilter = filter,
                MyOrganizationsOnly = true,
            };
        }

        protected override Task<IEnumerable<OrganizationDto>> QueryAsync(OrganizationQueryParams queryParams)
        {
            return Task.Run(async () =>
            {
                var organizations = await MetaverseProgram.ApiClient.Organizations.GetAllAsync(queryParams);
                if (organizations.Succeeded) return await organizations.GetResultAsync();
                Error = await organizations.GetErrorAsync();
                return new OrganizationDto[0];
            });
        }
    }
}
