using System;
using System.Linq;
using MetaverseCloudEngine.Common.Enumerations;
using MetaverseCloudEngine.Common.Models;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Common.Models.Forms;
using MetaverseCloudEngine.Unity.Assets;
using MetaverseCloudEngine.Unity.Assets.Attributes;
using MetaverseCloudEngine.Unity.Async;
using UnityEditor;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors
{
    public class CloudDataRecordTemplateFormEditor : EditorWindow
    {
        [Serializable]
        public class AuthorizedUserClass
        {
            public string userNameRegexFilter;
            public string emailRegexFilter;
        }

        public class Fields : ScriptableObject
        {
            [Tooltip("Whether the records with this template can be queried publicly.")]
            public CloudDataRecordQueryAuthorization queryAuthorization = CloudDataRecordQueryAuthorization.Public;
            [Tooltip("The user upload authorization level. If private, then the user must have an account and be specified in the 'Authorized User' list or match the Regex filters.")]
            public CloudDataRecordUploadAuthorization uploadAuthorization = CloudDataRecordUploadAuthorization.Public;
            [Tooltip("The maximum amount of records that can be created. NOTE: This is per key + requirements. (Please consult documentation if confused)")]
            public bool limitRecordCount;
            [Tooltip("How much binary data (in bytes) can be included in the record. This is maximum of 2 GB.")]
            public bool limitBinarySize = true;
            [Tooltip("If private, then the user must own the proposed blockchain asset when trying to create/update a particular record. Otherwise the user can specify a blockchain source that they do not own.")]
            public CloudDataRecordUploadAuthorization blockchainAuthorization;
            
            public BlockchainReferenceAsset[] validAssets;
            public BlockchainReferenceCategory[] validCategories;
            public AuthorizedUserClass[] authorizedUsers;

            [MetaSpaceIdProperty] public string[] validMetaSpaces;
            [LandPlotIdProperty] public string[] validLandPlots;
        }

        private CloudDataRecordTemplateUpsertForm _form;
        private Fields _fields;
        private SerializedObject _fieldsSerializedObject;
        private bool _inRequest;
        private string _requestError;
        private Action _onUpdated;

        public static void Edit(Guid dataSourceId, Action onUpdated = null)
        {
            Edit(new CloudDataRecordTemplateDto
            {
                DataSource = new CloudDataSourceDto { Id = dataSourceId }
            }, onUpdated);
        }

        public static void Edit(CloudDataRecordTemplateDto dto = null, Action onUpdated = null)
        {
            var window = GetWindow<CloudDataRecordTemplateFormEditor>(true, "Edit Record Template");
            window._onUpdated = onUpdated;
            UpdateDto(dto, window);
        }

        private static void UpdateDto(CloudDataRecordTemplateDto dto, CloudDataRecordTemplateFormEditor window)
        {
            window._requestError = null;

            if (dto != null)
            {
                window._form = new CloudDataRecordTemplateUpsertForm
                {
                    Id = dto.Id == Guid.Empty ? null : dto.Id,
                    Name = dto.Name,
                    DataSourceId = dto.DataSource.Id,
                    Updatable = dto.Updatable,
                    Deletable = dto.Deletable,
                    MaximumRecords = dto.MaximumRecords,
                    MaximumBinarySize = dto.MaximumBinarySize,
                    LandPlotRequirement = dto.LandPlotRequirement,
                    RequiredBlockchainAssets = dto.RequiredBlockchainAssets.Select(x => new BlockchainReferenceAssetModel { Asset = x.Asset, Type = x.Type }).ToArray(),
                    RequiredBlockchainCategories = dto.RequiredBlockchainPolicies.Select(x => new BlockchainReferenceCategoryModel { Category = x.Category, Type = x.Type }).ToArray(),
                    RequiredLandPlots = dto.RequiredLandPlots.Select(x => x.LandPlotId).ToArray(),
                    RequiredMetaSpaces = dto.RequiredMetaSpaces.Select(x => x.MetaSpaceId).ToArray(),
                    BlockchainRequirement = dto.BlockchainRequirement,
                    BlockchainUploadAuthorization = dto.BlockchainUploadAuthorization,
                    UserRequirement = dto.UserRequirement,
                    MetaSpaceRequirement = dto.MetaSpaceRequirement,
                    AuthorizedUsers = dto.AuthorizedUsers.Select(x => new CloudDataRecordTemplateUpsertFormAuthorizedUser { EmailRegexFilter = x.EmailRegexFilter, UserNameRegexFilter = x.UserNameRegexFilter }).ToArray(),
                    UploadAuthorization = dto.UploadAuthorization,
                    QueryAuthorization = dto.QueryAuthorization,
                };
            }
            else
                window._form = new CloudDataRecordTemplateUpsertForm();

            window._fields = CreateInstance<Fields>();
            window._fieldsSerializedObject = new SerializedObject(window._fields);
            window._fields.limitRecordCount = window._form.MaximumRecords != null;
            window._fields.limitBinarySize = window._form.MaximumBinarySize != null;
            window._fields.validAssets = window._form.RequiredBlockchainAssets.Select(x => new BlockchainReferenceAsset { asset = x.Asset, type = x.Type }).ToArray();
            window._fields.validCategories = window._form.RequiredBlockchainCategories.Select(x => new BlockchainReferenceCategory { category = x.Category, type = x.Type }).ToArray();
            window._fields.validMetaSpaces = window._form.RequiredMetaSpaces?.Select(x => x.ToString()).ToArray();
            window._fields.validLandPlots = window._form.RequiredLandPlots?.Select(x => x.ToString()).ToArray();
            window._fields.authorizedUsers = window._form.AuthorizedUsers?.Select(x => new AuthorizedUserClass { emailRegexFilter = x.EmailRegexFilter, userNameRegexFilter = x.UserNameRegexFilter }).ToArray();
            window._fields.uploadAuthorization = window._form.UploadAuthorization;
            window._fields.queryAuthorization = window._form.QueryAuthorization;
        }

        private void OnGUI()
        {
            if (_form == null)
            {
                Close();
                return;
            }

            if (!string.IsNullOrEmpty(_requestError))
            {
                EditorGUILayout.HelpBox(_requestError, MessageType.Error);
            }

            MetaverseEditorUtils.DrawLoadingScreen(() =>
            {
                MetaverseEditorUtils.Box(() =>
                {
                    _fieldsSerializedObject.Update();

                    if (_form.Id != null)
                        MetaverseEditorUtils.Disabled(() => EditorGUILayout.TextField("Id", _form.Id.ToString()));

                    _form.Name = EditorGUILayout.TextField("Name", _form.Name);
                    _form.Updatable = EditorGUILayout.Toggle("Updatable", _form.Updatable);
                    _form.Deletable = EditorGUILayout.Toggle("Deletable", _form.Deletable);

                    EditorGUI.BeginChangeCheck();

                    MetaverseEditorUtils.Box(() =>
                    {
                        EditorGUILayout.BeginVertical("toolbar");
                        EditorGUILayout.LabelField("Record Validation", EditorStyles.boldLabel);
                        EditorGUILayout.EndVertical();

                        if (_form.UserRequirement == CloudDataSourceTemplateFieldRequirement.Required)
                        {
                            _fields.uploadAuthorization = CloudDataRecordUploadAuthorization.Private;
                            EditorGUILayout.HelpBox("Upload authorization set to private because User Requirement is 'Required'.", MessageType.Info);
                            GUI.enabled = false;
                        }
                        EditorGUILayout.PropertyField(_fieldsSerializedObject.FindProperty(nameof(Fields.uploadAuthorization)));
                        GUI.enabled = true;

                        if (_fields.uploadAuthorization == CloudDataRecordUploadAuthorization.Private)
                            EditorGUILayout.PropertyField(_fieldsSerializedObject.FindProperty(nameof(Fields.authorizedUsers)));

                        _form.UploadAuthorization = _form.UserRequirement == CloudDataSourceTemplateFieldRequirement.Required ? CloudDataRecordUploadAuthorization.Private : _fields.uploadAuthorization;
                        _form.AuthorizedUsers = _fields.authorizedUsers.Select(x => new CloudDataRecordTemplateUpsertFormAuthorizedUser { EmailRegexFilter = x.emailRegexFilter, UserNameRegexFilter = x.userNameRegexFilter }).ToArray();

                        EditorGUILayout.PropertyField(_fieldsSerializedObject.FindProperty(nameof(Fields.queryAuthorization)));
                        _form.QueryAuthorization = _fields.queryAuthorization;

                        EditorGUILayout.PropertyField(_fieldsSerializedObject.FindProperty(nameof(Fields.limitRecordCount)));
                        if (_fields.limitRecordCount) _form.MaximumRecords = EditorGUILayout.IntField("Max Records", _form.MaximumRecords ?? 1);
                        else _form.MaximumRecords = null;

                        EditorGUILayout.PropertyField(_fieldsSerializedObject.FindProperty(nameof(Fields.limitBinarySize)));
                        if (_fields.limitBinarySize) _form.MaximumBinarySize = EditorGUILayout.IntField("Max Binary Size (bytes)", _form.MaximumBinarySize ?? 0);
                        else _form.MaximumBinarySize = null;

                        GUI.enabled = _form.Id == null;
                        _form.UserRequirement = (CloudDataSourceTemplateFieldRequirement) EditorGUILayout.EnumPopup("User", _form.UserRequirement);
                        GUI.enabled = true;

                        GUI.enabled = _form.Id == null;
                        _form.BlockchainRequirement = (CloudDataSourceTemplateFieldRequirement) EditorGUILayout.EnumPopup("Blockchain", _form.BlockchainRequirement);
                        GUI.enabled = true;

                        if (_form.BlockchainRequirement != CloudDataSourceTemplateFieldRequirement.Exclude)
                            MetaverseEditorUtils.Box(() =>
                            {
                                EditorGUILayout.PropertyField(_fieldsSerializedObject.FindProperty(nameof(Fields.blockchainAuthorization)));
                                _form.BlockchainUploadAuthorization = _fields.blockchainAuthorization;

                                EditorGUILayout.PropertyField(_fieldsSerializedObject.FindProperty(nameof(Fields.validAssets)));
                                EditorGUILayout.PropertyField(_fieldsSerializedObject.FindProperty(nameof(Fields.validCategories)));
                            });

                        GUI.enabled = _form.Id == null;
                        _form.MetaSpaceRequirement = (CloudDataSourceTemplateFieldRequirement) EditorGUILayout.EnumPopup("Meta Space", _form.MetaSpaceRequirement);
                        GUI.enabled = true;

                        if (_form.MetaSpaceRequirement != CloudDataSourceTemplateFieldRequirement.Exclude)
                            MetaverseEditorUtils.Box(() => EditorGUILayout.PropertyField(_fieldsSerializedObject.FindProperty(nameof(Fields.validMetaSpaces))));

                        GUI.enabled = _form.Id == null;
                        _form.LandPlotRequirement = (CloudDataSourceTemplateFieldRequirement) EditorGUILayout.EnumPopup("Land Plot", _form.LandPlotRequirement);
                        GUI.enabled = true;

                        if (_form.LandPlotRequirement != CloudDataSourceTemplateFieldRequirement.Exclude)
                            MetaverseEditorUtils.Box(() => EditorGUILayout.PropertyField(_fieldsSerializedObject.FindProperty(nameof(Fields.validLandPlots))));
                    });

                    _fieldsSerializedObject.ApplyModifiedProperties();

                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button(_form.Id == null ? "Create New" : "Update Metadata"))
                    {
                        _form.RequiredLandPlots = _form.LandPlotRequirement != CloudDataSourceTemplateFieldRequirement.Exclude && _fields.validLandPlots != null
                            ? _fields.validLandPlots.Where(x => !string.IsNullOrWhiteSpace(x) && Guid.TryParse(x, out _)).Select(Guid.Parse).ToArray()
                            : Array.Empty<Guid>();
                        _form.RequiredMetaSpaces = _form.MetaSpaceRequirement != CloudDataSourceTemplateFieldRequirement.Exclude && _fields.validMetaSpaces != null
                            ? _fields.validMetaSpaces.Where(x => !string.IsNullOrWhiteSpace(x) && Guid.TryParse(x, out _)).Select(Guid.Parse).ToArray()
                            : Array.Empty<Guid>();
                        _form.RequiredBlockchainAssets = _form.BlockchainRequirement != CloudDataSourceTemplateFieldRequirement.Exclude && _fields.validAssets != null
                            ? _fields.validAssets.Where(x => !string.IsNullOrWhiteSpace(x.asset)).Select(x => new BlockchainReferenceAssetModel { Asset = x.asset, Type = x.type }).ToArray()
                            : Array.Empty<BlockchainReferenceAssetModel>();
                        _form.RequiredBlockchainCategories = _form.BlockchainRequirement != CloudDataSourceTemplateFieldRequirement.Exclude && _fields.validCategories != null
                            ? _fields.validCategories.Where(x => !string.IsNullOrWhiteSpace(x.category)).Select(x => new BlockchainReferenceCategoryModel { Category = x.category, Type = x.type }).ToArray()
                            : Array.Empty<BlockchainReferenceCategoryModel>();

                        _inRequest = true;
                        _requestError = null;
                        MetaverseProgram.ApiClient.CloudData.UpsertTemplateAsync(_form)
                            .ResponseThen(dto =>
                            {
                                UpdateDto(dto, this);
                                _onUpdated?.Invoke();
                                _inRequest = false;
                                
                            }, e =>
                            {
                                _requestError = e.ToString();
                                _inRequest = false;
                            });
                    }

                    if (_form.Id != null && GUILayout.Button("Delete"))
                    {
                        TypeToConfirmEditorWindow.Open(
                            "Are you sure you want to delete this record template? " +
                            "This will also delete ALL records associated with this template. You CANNOT undo this action.", 
                            "DELETE", 
                            "Delete", 
                            "Cancel", () =>
                        {
                            _inRequest = true;
                            _requestError = null;
                            MetaverseProgram.ApiClient.CloudData.DeleteTemplateAsync(_form.Id.Value)
                                .ResponseThen(() =>
                                {
                                    UpdateDto(null, this);
                                    _onUpdated?.Invoke();
                                    _inRequest = false;
                                
                                }, e =>
                                {
                                    _requestError = e.ToString();
                                    _inRequest = false;
                                });
                        });
                        GUIUtility.ExitGUI();
                    }
                });
            }, MetaverseEditorUtils.DrawDefaultLoadingScreen, _inRequest);
        }
    }
}