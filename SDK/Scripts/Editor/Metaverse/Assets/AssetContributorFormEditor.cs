using System;
using MetaverseCloudEngine.ApiClient;
using MetaverseCloudEngine.ApiClient.Controllers;
using MetaverseCloudEngine.Common.Enumerations;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Common.Models.Forms;
using MetaverseCloudEngine.Unity.Async;
using UnityEditor;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors
{
    public class AssetContributorFormEditor : EditorWindow
    {
        public enum ContributorType
        {
            User,
            Organization,
        }

        private Vector2 _scroll;
        private string _requestError;
        private bool _isRequesting;
        private AssetContributorDto _editContributor;
        private IAssetContributorController _controller;
        private ContributorType _contribType;
        private readonly AssetContributorUpsertForm _upsertForm = new();

        public event Action<AssetContributorDto> Completed;

        public static void New(Guid assetId, IAssetContributorController controller, Action<AssetContributorDto> completed)
        {
            var window = InitWindow(controller, completed);
            window.InitNew(assetId);
        }

        public static void Edit(AssetContributorDto contributor, IAssetContributorController controller, Action<AssetContributorDto> completed)
        {
            var window = InitWindow(controller, completed);
            window.InitEdit(contributor);

        }

        private void InitEdit(AssetContributorDto contributor)
        {
            _editContributor = contributor;
            _contribType = contributor.SystemUser != null ? ContributorType.User : ContributorType.Organization;
            _upsertForm.Id = contributor.AssetId;
            _upsertForm.Role = contributor.Role;
            _upsertForm.OrganizationId = contributor.Organization?.Id;
            _upsertForm.UserNameOrEmail = contributor.SystemUser?.UserName;
            _upsertForm.IsRepresentative = contributor.IsRepresentative;
            _requestError = null;
        }

        private void InitNew(Guid assetId)
        {
            _upsertForm.Id = assetId;
        }

        private static AssetContributorFormEditor InitWindow(IAssetContributorController controller, Action<AssetContributorDto> completed)
        {
            var window = GetWindow<AssetContributorFormEditor>(true, "Edit Contributor");
            window.maxSize = window.minSize = new Vector2(300, 150);
            window._controller = controller;
            window.Completed = completed;
            return window;
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            MetaverseEditorUtils.DrawLoadingScreen(() =>
            {
                MetaverseEditorUtils.Box(() =>
                {
                    MetaverseEditorUtils.Error(_requestError);

                    if (_editContributor != null)
                        GUI.enabled = false;

                    _contribType = (ContributorType)EditorGUILayout.EnumPopup("Contributor Type", _contribType);

                    switch (_contribType)
                    {
                        case ContributorType.User:
                            {
                                _upsertForm.UserNameOrEmail = EditorGUILayout.TextField("Username Or Email", _upsertForm.UserNameOrEmail);
                            }
                            break;
                        case ContributorType.Organization:
                            {
                                var organizationId = EditorGUILayout.TextField("Organization ID", _upsertForm.OrganizationId.ToString());
                                if (Guid.TryParse(organizationId, out var id))
                                {
                                    _upsertForm.OrganizationId = id;
                                }
                            }
                            break;
                    }

                    GUI.enabled = true;

                    _upsertForm.Role = (ContributionRole)EditorGUILayout.EnumPopup("Role", _upsertForm.Role);
                    
                    if (_editContributor is { HasAcceptedInvite: true })
                    {
                        if (_editContributor is {IsRepresentative: true})
                            GUI.enabled = false;

                        _upsertForm.IsRepresentative = EditorGUILayout.Toggle("Is Representative", _upsertForm.IsRepresentative);

                        GUI.enabled = true;
                    }
                    else if (_editContributor is { HasAcceptedInvite: false })
                    {
                        GUI.enabled = false;
                        EditorGUILayout.HelpBox("This contributor has not accepted their invite yet.", MessageType.Warning);
                        GUI.enabled = true;
                    }

                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button(_editContributor == null ? "Add" : "Update"))
                    {
                        _isRequesting = true;

                        _controller.UpsertContributorAsync(_upsertForm)
                            .ResponseThen(r =>
                            {
                                _isRequesting = false;
                                Completed?.Invoke(r);
                                Close();
                            },
                            e =>
                            {
                                _isRequesting = false;
                                _requestError = e.ToString();
                            });
                    }

                    if (_editContributor != null && GUILayout.Button("Remove"))
                    {
                        _isRequesting = true;
                        _controller.RemoveContributorAsync(new AssetContributorRemoveForm
                        {
                            Id = _upsertForm.Id,
                            OrganizationId = _upsertForm.OrganizationId,
                            UserNameOrEmail = _upsertForm.UserNameOrEmail
                        })
                        .ResponseThen(() =>
                        {
                            _isRequesting = false;
                            Completed?.Invoke(null);
                            Close();
                        },
                        e =>
                        {
                            _isRequesting = false;
                            _requestError = e.ToString();
                        });
                    }
                });

            }, MetaverseEditorUtils.DrawDefaultLoadingScreen, _isRequesting);

            EditorGUILayout.EndScrollView();
        }
    }
}
