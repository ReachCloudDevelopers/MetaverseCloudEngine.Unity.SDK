using System;
using MetaverseCloudEngine.Common.Enumerations;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Common.Models.Forms;
using MetaverseCloudEngine.Common.Models.QueryParams;
using MetaverseCloudEngine.Unity.Async;
using UnityEditor;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors
{
    public class CloudDataSourceFormEditor : EditorWindow
    {
        private string _requestError;
        private bool _isRequesting;
        private PaginatedEditor<CloudDataRecordTemplateDto> _recordTemplateEditor;
        private Action _onFinished;

        public CloudDataSourceUpsertForm Form { get; set; } = new ();

        public static void Edit(Guid hostId, CloudDataSourceHost host, Action onFinished = null)
        {
            Edit(new CloudDataSourceDto
            {
                HostAssetId = host == CloudDataSourceHost.Asset ? hostId : null,
                HostOrganizationId = host == CloudDataSourceHost.Organization ? hostId : null,
                HostUserId = host == CloudDataSourceHost.User ? hostId : null,
            }, onFinished);
        }
        
        public static void Edit(CloudDataSourceDto dto = null, Action onFinished = null)
        {
            var window = GetWindow<CloudDataSourceFormEditor>(true, "Data Source");
            window._onFinished = onFinished;
            UpdateDto(window, dto);
        }

        private void OnDestroy()
        {
            _onFinished?.Invoke();
        }

        private static void UpdateDto(CloudDataSourceFormEditor window, CloudDataSourceDto dto = null)
        {
            window._requestError = null;

            if (dto != null)
            {
                window.Form.Id = dto.Id != Guid.Empty ? dto.Id : null;
                window.Form.Name = dto.Name;
                window.Form.Private = dto.Private;
                window.Form.HostAssetId = dto.HostAssetId;
                window.Form.HostOrganizationId = dto.HostOrganizationId;
                if (dto.HostUserId != null) // TODO FIXME: The Username of the current user is used but that might not match the Host User ID. 
                    window.Form.HostUserNameOrEmail = MetaverseProgram.ApiClient.Account.CurrentUser?.UserName;
            }
            else
            {
                window.Form = new CloudDataSourceUpsertForm();
            }
        }

        private void OnGUI()
        {
            if (_recordTemplateEditor == null)
            {
                _recordTemplateEditor = new PaginatedEditor<CloudDataRecordTemplateDto>("Record Templates");
                _recordTemplateEditor.BeginRequest += RecordTemplateEditorOnBeginRequest;
                _recordTemplateEditor.AddButtonClicked += RecordTemplateEditorOnAddButtonClicked;
                _recordTemplateEditor.DrawRecord += RecordTemplateEditorOnDrawRecord;
            }
            
            MetaverseEditorUtils.Box(() =>
            {
                MetaverseEditorUtils.DrawLoadingScreen(() =>
                {
                    if (!string.IsNullOrEmpty(_requestError))
                        EditorGUILayout.HelpBox(_requestError, MessageType.Error);

                    if (HasID())
                        MetaverseEditorUtils.Disabled(() => EditorGUILayout.TextField("Id", Form.Id.ToString()));

                    Form.Name = EditorGUILayout.TextField("Name", Form.Name);
                    Form.Private = EditorGUILayout.Toggle("Private", Form.Private);
                
                    if (!HasID())
                        GUILayout.FlexibleSpace();

                    if (GUILayout.Button(!HasID() ? "Create New" : "Update Metadata"))
                    {
                        _isRequesting = true;
                        _requestError = null;
                        MetaverseProgram.ApiClient.CloudData.UpsertSourceAsync(Form)
                            .ResponseThen(r =>
                            {
                                UpdateDto(this, r);
                                _onFinished?.Invoke();
                                _isRequesting = false;

                            }, e =>
                            {
                                _requestError = e.ToString();
                                _isRequesting = false;
                            });
                    }

                    if (!HasID())
                        return;
                    
                    if (GUILayout.Button("Delete"))
                    {
                        TypeToConfirmEditorWindow.Open(
                            $"Are you sure you want to delete this data source ('{Form.Name}')?\nYOU WILL LOSE ALL DATA ASSOCIATED WITH THIS DATA SOURCE.\nThis action cannot be undone!",
                            "DELETE",
                            "Delete",
                            "Cancel",
                            () =>
                            {
                                _isRequesting = true;
                                MetaverseProgram.ApiClient.CloudData.DeleteSourceAsync(Form.Id!.Value)
                                    .ResponseThen(() =>
                                    {
                                        UpdateDto(this);
                                        _onFinished?.Invoke();
                                        _isRequesting = false;

                                    }, e =>
                                    {
                                        _requestError = e.ToString();
                                        _isRequesting = false;
                                    });
                            },
                            () => { });
                        GUIUtility.ExitGUI();
                    }

                    _recordTemplateEditor.Draw();

                }, MetaverseEditorUtils.DrawDefaultLoadingScreen, _isRequesting);
            });
        }

        private bool RecordTemplateEditorOnDrawRecord(CloudDataRecordTemplateDto record)
        {
            MetaverseEditorUtils.Box(() =>
            {
                if (GUILayout.Button("...", EditorStyles.miniButton, GUILayout.Width(23)))
                    CloudDataRecordTemplateFormEditor.Edit(record, _recordTemplateEditor.Refresh);
                EditorGUILayout.LabelField($"{record.Name} ({record.Id.ToString()[..4]}...)");
            }, vertical: false);
            return true;
        }

        private bool HasID()
        {
            return Form.Id != null && Form.Id != Guid.Empty;
        }

        private void RecordTemplateEditorOnAddButtonClicked()
        {
            if (Form.Id == null) return;
            CloudDataRecordTemplateFormEditor.Edit(Form.Id.Value, _recordTemplateEditor.Refresh);
        }

        private bool RecordTemplateEditorOnBeginRequest(int offset, int count, string filter)
        {
            MetaverseProgram.ApiClient.CloudData.GetAllTemplatesAsync(new CloudDataRecordTemplateQueryParams
            {
                DataSourceId = Form.Id,
                Count = (uint)count,
                Offset = (uint)offset,
                NameFilter = filter,
                
            }).ResponseThen(_recordTemplateEditor.EndRequest, e => _recordTemplateEditor.RequestError = e.ToString());

            return true;
        }
    }
}