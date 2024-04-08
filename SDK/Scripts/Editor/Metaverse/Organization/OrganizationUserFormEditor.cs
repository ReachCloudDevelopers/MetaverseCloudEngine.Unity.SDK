using System;
using MetaverseCloudEngine.Common.Enumerations;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Common.Models.Forms;
using MetaverseCloudEngine.Unity.Async;
using UnityEditor;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors
{
    public class OrganizationUserFormEditor : EditorWindow
    {
        private Vector2 _scroll;
        private string _requestError;
        private bool _isRequesting;
        private OrganizationUserDto _editUser;
        private readonly OrganizationUserUpsertForm _upsertForm = new();

        public event Action<OrganizationUserDto> Done;

        public static void New(Guid organizationId, Action<OrganizationUserDto> completed)
        {
            var window = InitWindow(completed);
            window.InitNew(organizationId);
        }

        public static void Edit(OrganizationUserDto contributor, Action<OrganizationUserDto> completed)
        {
            var window = InitWindow(completed);
            window.InitEdit(contributor);

        }

        private void InitEdit(OrganizationUserDto user)
        {
            _editUser = user;
            _upsertForm.UserNameOrEmail = user.SystemUser.UserName;
            _upsertForm.Role = user.Role;
            _upsertForm.OrganizationId = user.OrganizationId;
        }

        private void InitNew(Guid organizationId)
        {
            _upsertForm.OrganizationId = organizationId;
        }

        private static OrganizationUserFormEditor InitWindow(Action<OrganizationUserDto> done)
        {
            var window = GetWindow<OrganizationUserFormEditor>(true, "Edit Organization User");
            window.maxSize = window.minSize = new Vector2(300, 150);
            window.Done = done;
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

                    if (_editUser != null) GUI.enabled = false;
                    _upsertForm.UserNameOrEmail = EditorGUILayout.TextField("Username Or Email", _upsertForm.UserNameOrEmail);
                    GUI.enabled = true;

                    _upsertForm.Role = (OrganizationRole)EditorGUILayout.EnumPopup("Role", _upsertForm.Role);

                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button(_editUser == null ? "Add" : "Update"))
                    {
                        _isRequesting = true;

                        MetaverseProgram.ApiClient.Organizations.UpsertUserAsync(_upsertForm)
                            .ResponseThen(r =>
                            {
                                _isRequesting = false;
                                Done?.Invoke(r);
                                Close();
                            },
                            e =>
                            {
                                _isRequesting = false;
                                _requestError = e.ToString();
                            });
                    }

                    if (_editUser == null || !GUILayout.Button("Remove") || !EditorUtility.DisplayDialog("Remove User",
                            $"Are you sure you want to remove {_editUser.SystemUser.UserName} from {_editUser.Organization.Name}?",
                            "Yes", "No")) return;
                    {
                        _isRequesting = true;
                        MetaverseProgram.ApiClient.Organizations.RemoveUserAsync(_upsertForm.OrganizationId, _upsertForm.UserNameOrEmail)
                            .ResponseThen(() =>
                                {
                                    _isRequesting = false;
                                    Done?.Invoke(null);
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
