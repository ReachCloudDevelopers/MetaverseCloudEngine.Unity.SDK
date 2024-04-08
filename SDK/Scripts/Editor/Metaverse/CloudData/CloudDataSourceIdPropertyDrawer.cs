using System;
using UnityEngine;
using MetaverseCloudEngine.Unity.Async;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Unity.CloudData.Attributes;
using UnityEditor;

namespace MetaverseCloudEngine.Unity.Editors
{
    [CustomPropertyDrawer(typeof(CloudDataSourceIdAttribute))]
    public class CloudDataSourceIdPropertyDrawer : RecordIdPropertyDrawer<CloudDataSourceDto, Guid, CloudDataSourcePickerEditor>
    {
        protected override Guid ParseRecordId(string str) => Guid.TryParse(str, out var id) ? id : Guid.Empty;
        protected override Guid GetRecordId(CloudDataSourceDto record) => record.Id;
        protected override string GetRecordIdStringValue(Guid id) => id.ToString();
        protected override GUIContent GetRecordLabel(CloudDataSourceDto record) => new (record.Name + " (" + record.Id.ToString()[..5] + "...)");
        protected override void RequestRecord(Guid id, Action<CloudDataSourceDto> onSuccess, Action onFailed = null) => MetaverseProgram.ApiClient.CloudData.FindSourceAsync(id).ResponseThen(onSuccess, _ => onFailed?.Invoke());
    }
}