using System;
using MetaverseCloudEngine.Common.Enumerations;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Common.Models.QueryParams;
using MetaverseCloudEngine.Unity.Async;
using UnityEditor;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors
{
    public class CloudDataSourceListEditor
    {
        private readonly CloudDataSourceHost _host;
        private readonly PaginatedEditor<CloudDataSourceDto> _dataSourceEditor;

        public CloudDataSourceListEditor(CloudDataSourceHost host, bool collapsable = true)
        {
            _host = host;
            _dataSourceEditor = new PaginatedEditor<CloudDataSourceDto>("Data Sources", collapsable);
            _dataSourceEditor.AddButtonClicked += DataSourceEditorOnAddButtonClicked;
            _dataSourceEditor.BeginRequest += DataSourceEditorOnBeginRequest;
            _dataSourceEditor.DrawRecord += DataSourceEditorOnDrawRecord;
        }

        public Guid? HostId { get; set; }

        private bool DataSourceEditorOnBeginRequest(int offset, int count, string filter)
        {
            var query = new CloudDataSourceQueryParams
            {
                Count = (uint) count,
                Offset = (uint) offset,
                NameFilter = filter,
            };
            switch (_host)
            {
                case CloudDataSourceHost.Asset: query.HostAssetId = HostId; break;
                case CloudDataSourceHost.Organization: query.HostOrganizationId = HostId; break;
                case CloudDataSourceHost.User: query.HostUserId = HostId; break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            MetaverseProgram.ApiClient.CloudData.GetAllSourcesAsync(query)
                .ResponseThen(_dataSourceEditor.EndRequest, e => _dataSourceEditor.RequestError = e.ToString());
            return true;
        }

        private void DataSourceEditorOnAddButtonClicked()
        {
            if (HostId == null) return;
            CloudDataSourceFormEditor.Edit(HostId.Value, _host, _dataSourceEditor.Refresh);
        }

        private bool DataSourceEditorOnDrawRecord(CloudDataSourceDto record)
        {
            MetaverseEditorUtils.Box(() =>
            {
                if (GUILayout.Button("...", EditorStyles.miniButton, GUILayout.Width(23)))
                    CloudDataSourceFormEditor.Edit(record, _dataSourceEditor.Refresh);

                EditorGUILayout.LabelField($"{record.Name} ({record.Id.ToString()[..4]}...)");
            }, vertical: false);
            return true;
        }

        public void Draw()
        {
            if (HostId == null) return;
            _dataSourceEditor.Draw();
        }
    }
}
