using System;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Unity.Async;
using MetaverseCloudEngine.Unity.Attributes;
using UnityEditor;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors
{
    [CustomPropertyDrawer(typeof(OrganizationIdPropertyAttribute))]
    public class OrganizationIdPropertyDrawer : RecordIdPropertyDrawer<OrganizationDto, string, OrganizationPicker>
    {
        protected override string ParseRecordId(string str) => str;
        protected override string GetRecordId(OrganizationDto record) => record.Id.ToString();
        protected override string GetRecordIdStringValue(string id) => id;
        protected override GUIContent GetRecordLabel(OrganizationDto record) => new (record.Name, EditorGUIUtility.IconContent(EditorGUIUtility.isProSkin ? "d_BlendTree Icon" : "BlendTree Icon").image, record.Description);
        protected override void RequestRecord(string id, Action<OrganizationDto> onSuccess, Action onFailed = null)
        {
            MetaverseProgram.OnInitialized(() =>
            {
                if (!Guid.TryParse(id, out var guid))
                {
                    onFailed?.Invoke();
                    return;
                }
            
                MetaverseProgram.ApiClient.Organizations.FindAsync(guid)
                    .ResponseThen(onSuccess, _ => onFailed?.Invoke());
            });
        }
    }
}