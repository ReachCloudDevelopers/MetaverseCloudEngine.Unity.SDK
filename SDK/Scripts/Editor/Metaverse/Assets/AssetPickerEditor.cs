using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MetaverseCloudEngine.ApiClient;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Common.Models.QueryParams;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors
{
    public abstract class AssetPickerEditor<TAssetDto, TAssetQueryParams> : QueryablePickerEditor<TAssetDto, TAssetQueryParams>
        where TAssetDto : AssetDto
        where TAssetQueryParams : AssetQueryParams, new()
    {
        protected virtual bool WriteableOnly => true;
        protected virtual bool AmUserOnly => true;

        protected abstract Task<ApiResponse<IEnumerable<TAssetDto>>> QueryAssetsAsync(TAssetQueryParams queryParams);

        protected override GUIContent GetPickableContent(object pickable)
        {
            var asset = (TAssetDto)pickable;
            return new GUIContent($"{asset.Name} ({asset.Id.ToString()[..5]}...)", asset.Description);
        }

        protected override Task<IEnumerable<TAssetDto>> QueryAsync(TAssetQueryParams queryParams)
        {
            return Task.Run(async () =>
            {
                var response = await QueryAssetsAsync(queryParams);
                if (response.Succeeded)
                    return await response.GetResultAsync();
                Debug.LogError($"Failed to query assets: {response.GetErrorAsync().Result}");
                return Array.Empty<TAssetDto>();
            });
        }

        protected override TAssetQueryParams GetQueryParams(int count, int offset, string filter)
        {
            return new TAssetQueryParams
            {
                UserId = AmUserOnly ? MetaverseProgram.ApiClient.Account.CurrentUser.Id : null,
                Offset = (uint)offset,
                Count = (uint)count,
                NameFilter = filter,
                Writeable = WriteableOnly,
                AdvancedSearch = !string.IsNullOrEmpty(filter),
            };
        }
    }
}
