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
    public class MetaSpaceExternalServiceFormEditor : EditorWindow
    {
        private string _requestError;
        private bool _isRequesting;
        private Action _onFinished;
        private string _apiKeyPreview;

        public MetaSpaceExternalServiceUpsertForm Form { get; set; } = new();

        public static void Edit(Guid metaSpaceId, Action onFinished = null)
        {
            Edit(new MetaSpaceExternalServiceDto
            {
                MetaSpaceId = metaSpaceId,
                IsEnabled = true,
                
            }, onFinished);
        }

        public static void Edit(MetaSpaceExternalServiceDto dto = null, Action onFinished = null)
        {
            var window = GetWindow<MetaSpaceExternalServiceFormEditor>(true, "External Service");
            window._onFinished = onFinished;
            window.maxSize = new Vector2(400, 200);
            window.minSize = window.maxSize;
            UpdateDto(window, dto);
        }

        private void OnDestroy()
        {
            _onFinished?.Invoke();
        }

        private static void UpdateDto(MetaSpaceExternalServiceFormEditor window, MetaSpaceExternalServiceDto dto = null)
        {
            window._requestError = null;

            if (dto != null)
            {
                window.Form.Id = dto.Id;
                window.Form.Domain = dto.Domain;
                window.Form.IsEnabled = dto.IsEnabled;
                window.Form.RequireLogin = dto.RequireLogin;
                window.Form.MetaSpaceId = dto.MetaSpaceId;
                window._apiKeyPreview = dto.ApiKeyTrimmed;
            }
            else
            {
                window.Form = new MetaSpaceExternalServiceUpsertForm();
                window._apiKeyPreview = null;
            }
        }

        private void OnGUI()
        {
            MetaverseEditorUtils.Box(() =>
            {
                MetaverseEditorUtils.DrawLoadingScreen(() =>
                {
                    if (!string.IsNullOrEmpty(_requestError))
                        EditorGUILayout.HelpBox(_requestError, MessageType.Error);

                    if (HasID())
                        MetaverseEditorUtils.Disabled(() => EditorGUILayout.TextField("ID", Form.Id.ToString()));

                    Form.Domain = EditorGUILayout.TextField("Domain", Form.Domain);
                    if (!string.IsNullOrEmpty(_apiKeyPreview))
                    {
                        GUI.enabled = false;
                        EditorGUILayout.TextField("API Key", _apiKeyPreview);
                        GUI.enabled = true;
                    }
                    Form.ApiKey = EditorGUILayout.PasswordField(
                        $"{(!string.IsNullOrEmpty(_apiKeyPreview) ? "New" : "")} API Key", Form.ApiKey);
                    Form.IsEnabled = EditorGUILayout.Toggle("Is Enabled", Form.IsEnabled ?? false);
                    Form.RequireLogin = EditorGUILayout.Toggle("Require Login", Form.RequireLogin ?? false);

                    if (GUILayout.Button(!HasID() ? "Create New" : "Update Metadata"))
                    {
                        _isRequesting = true;
                        _requestError = null;
                        
                        MetaverseProgram.ApiClient.MetaSpaces.UpsertExternalService(Form)
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

                    if (HasID())
                    {
                        if (GUILayout.Button("Delete"))
                        {
                            _isRequesting = true;
                            MetaverseProgram.ApiClient.MetaSpaces.DeleteExternalService(Form.Id.GetValueOrDefault())
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
                        }
                    }

                }, MetaverseEditorUtils.DrawDefaultLoadingScreen, _isRequesting);
            });
        }

        private bool HasID()
        {
            return Form.Id != null && Form.Id != Guid.Empty;
        }
    }
}