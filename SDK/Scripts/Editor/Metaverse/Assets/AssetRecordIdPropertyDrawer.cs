using System;
using MetaverseCloudEngine.ApiClient;
using MetaverseCloudEngine.ApiClient.Controllers;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Common.Models.QueryParams;
using MetaverseCloudEngine.Unity.Async;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors
{
    public abstract class AssetRecordIdPropertyDrawer<TAssetDto, TAssetQueryParams, TAssetPicker> : RecordIdPropertyDrawer<TAssetDto, Guid, TAssetPicker>
        where TAssetPicker : AssetPickerEditor<TAssetDto, TAssetQueryParams>
        where TAssetDto : AssetDto
        where TAssetQueryParams : AssetQueryParams, new()
    {
        protected abstract IAssetController<TAssetDto> Controller { get; }
        protected override Guid ParseRecordId(string str) => Guid.TryParse(str, out var id) ? id : default;
        protected override Guid GetRecordId(TAssetDto record) => record.Id;
        protected override string GetRecordIdStringValue(Guid id) => id.ToString();
        protected override GUIContent GetRecordLabel(TAssetDto record) => new GUIContent(record.Name + " (" + record.Id.ToString()[..5] + "...)", GetIcon(), record.Id + Environment.NewLine + record.Description);
        protected virtual Texture GetIcon() => null;
        protected override void RequestRecord(Guid id, Action<TAssetDto> onSuccess, Action onFailed = null)
        {
            MetaverseProgram.OnInitialized(() =>
            {
                Controller.FindAsync(id).ResponseThen(onSuccess, e => onFailed?.Invoke());
            });
        }
    }
}
