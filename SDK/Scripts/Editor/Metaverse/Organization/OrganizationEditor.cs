using System;
using System.IO;
using System.Threading.Tasks;
using MetaverseCloudEngine.ApiClient;
using MetaverseCloudEngine.ApiClient.Abstract;
using MetaverseCloudEngine.ApiClient.Extensions;
using MetaverseCloudEngine.Common.Enumerations;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Common.Models.Forms;
using MetaverseCloudEngine.Common.Models.QueryParams;
using MetaverseCloudEngine.Unity.Async;
using MetaverseCloudEngine.Unity.UI.Components;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MetaverseCloudEngine.Unity.Editors
{
    public class OrganizationEditor : RecordEditor<OrganizationDto, Guid, OrganizationPicker>
    {
        public enum EditorTabs
        {
            Users,
            MetaSpaces,
            DataSources,
        }

        private PaginatedEditor<OrganizationUserDto> _usersPager;
        private PaginatedEditor<MetaSpaceDto> _metaSpacesPager;
        private CloudDataSourceListEditor _dataSourceListEditor;

        private BaseTheme _theme;
        private Editor _themeEditor;
        private bool _themeExpanded;
        private EditorTabs _currentEditorTab;
        private static string[] _editorTabNames;
        
        private bool _fetchedLogo;
        private string _logoError;
        private Texture2D _organizationLogo;

        public override string Header => "Organizations";

        [MenuItem(MetaverseConstants.MenuItems.WindowsMenuRootPath + "Organizations")]
        public static void Open()
        {
            var window = GetWindow<OrganizationEditor>();
            window.Show();
        }

        private void Update()
        {
            Repaint();
        }

        private void OnEnable()
        {
            Initialize();
            titleContent = new GUIContent(Header, MetaverseEditorUtils.EditorIcon);
        }

        private void Initialize()
        {
            _usersPager = new PaginatedEditor<OrganizationUserDto>("Users");
            _usersPager.DrawRecord += OnUsersEditorDrawRecord;
            _usersPager.BeginRequest += OnUsersEditorBeginRequest;
            _usersPager.AddButtonClicked += OnUsersEditorAddButtonClicked;

            _metaSpacesPager = new PaginatedEditor<MetaSpaceDto>("Dashboard Meta Spaces");
            _metaSpacesPager.DrawRecord += OnMetaSpacesEditorDrawRecord;
            _metaSpacesPager.BeginRequest += OnMetaSpacesEditorBeginRequest;
            _metaSpacesPager.AddButtonClicked += OnMetaSpacesEditorAddButtonClicked;

            _dataSourceListEditor = new CloudDataSourceListEditor(CloudDataSourceHost.Organization);
        }

        protected override Guid GetRecordIdentifier(OrganizationDto record)
        {
            return record.Id;
        }

        protected override void BeginRequestRecord(Guid id)
        {
            MetaverseProgram.ApiClient.Organizations.FindAsync(id).ResponseThen(UpdateRecord, OnRequestErrorFatal);
        }

        protected override void DeleteRecord(OrganizationDto record)
        {
            TypeToConfirmEditorWindow.Open(
                $"Are you sure you want to delete the organization '{record.Name}'? THIS CANNOT BE UNDONE!",
                "DELETE " + record.Name + " FOREVER",
                "Delete",
                "Cancel",
                () => MetaverseProgram.ApiClient.Organizations.DeleteAsync(record.Id).ResponseThen(New, OnRequestError),
                () => { OnRequestError("Canceled Deletion"); });
            GUIUtility.ExitGUI();
        }

        protected override void SaveRecord(OrganizationDto record)
        {
            MetaverseProgram.ApiClient.Organizations.UpsertAsync(new OrganizationUpsertForm
            {
                Id = !HasValidRecordId ? null : record.Id,
                Name = record.Name,
                Description = record.Description,
                Private = record.Private,
                SupportsCrypto = record.SupportsCrypto,
            })
            .ResponseThen(UpdateRecord, OnRequestError);
        }

        protected override void UpdateRecord(OrganizationDto record)
        {
            base.UpdateRecord(record);
            _theme = null;
            _themeEditor = null;
            if (_organizationLogo)
                DestroyImmediate(_organizationLogo);
            _organizationLogo = null;
            _fetchedLogo = false;
            Initialize();
        }

        protected override void DrawRecord(OrganizationDto record)
        {
            MetaverseEditorUtils.Disabled(() => MetaverseEditorUtils.TextField("ID", !HasValidRecordId ? null : record.Id.ToString()));
            record.Name = MetaverseEditorUtils.TextField("Name", record.Name);
            record.Description = MetaverseEditorUtils.TextArea("Description", record.Description);
            if (HasValidRecordId)
            {
                if (!string.IsNullOrEmpty(MetaverseConstants.Urls.DashboardUrl) &&
                    GUILayout.Button("View Online"))
                {
                    var url = MetaverseConstants.Urls.DashboardUrl + "?organizationId=" + record.Id;
                    Application.OpenURL(url);
                }

                if (!string.IsNullOrEmpty(MetaverseConstants.Urls.DeepLink) &&
                    !string.IsNullOrEmpty(MetaverseConstants.Urls.WebGLHost) &&
                    GUILayout.Button("View In Desktop App"))
                {
                    var deepLinkUrl = MetaverseConstants.Urls.DeepLink + MetaverseConstants.Urls.WebGLHost + "?organizationId=" + record.Id;
                    Application.OpenURL(deepLinkUrl);
                }
            }
            record.Private = EditorGUILayout.Toggle("Private", record.Private);
            record.SupportsCrypto = EditorGUILayout.Toggle("Blockchain Support", record.SupportsCrypto);
            _themeExpanded = EditorGUILayout.Foldout(_themeExpanded, "Theme");
            if (_themeExpanded)
            {
                try
                {
                    RenderThumbnail(record);
                }
                catch
                {
                    /* ignored */
                }

                MetaverseEditorUtils.Box(() =>
                {
                    var labelWidth = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = labelWidth * 1.5f;
                    try
                    {
                        Editor.CreateCachedEditor(_theme, null, ref _themeEditor);

                        if (_theme == null)
                            _theme = BaseTheme.FromOrganizationTheme<BaseTheme>(record.Theme);

                        if (_themeEditor)
                        {
                            foreach (var target in _themeEditor.targets)
                            {
                                var serializedObject = new SerializedObject(target);
                                serializedObject.UpdateIfRequiredOrScript();
                                try
                                {
                                    var iterator = serializedObject.GetIterator();
                                    // Draw all excluding m_Script and Logo
                                    for (var enterChildren = true;
                                         iterator.NextVisible(enterChildren);
                                         enterChildren = false)
                                    {
                                        if (iterator.propertyPath is "m_Script" or nameof(BaseTheme.Logo))
                                            continue;
                                        EditorGUILayout.PropertyField(iterator, true);
                                    }
                                }
                                finally
                                {
                                    serializedObject.ApplyModifiedProperties();
                                }
                            }
                        }
                    }
                    finally
                    {

                        EditorGUIUtility.labelWidth = labelWidth;
                    }
                });
            }
        }

        private void RenderThumbnail(OrganizationDto record)
        {
            if (!_fetchedLogo)
            {
                EditorUtility.DisplayProgressBar("Fetching Logo", "Fetching organization logo...", 0.5f);
                if (_organizationLogo)
                    DestroyImmediate(_organizationLogo);
                _organizationLogo = null;
                _fetchedLogo = true;
                try
                {
                    if (record.Theme?.Logo != null)
                    {
                        MetaverseProgram.ApiClient.Organizations.DownloadLogoAsync(record.Id).ResponseThen(
                            r =>
                            {
                                r.GetResultAsAsync<Texture2D>().Then(t =>
                                {
                                    _organizationLogo = t;
                                });
                            }, e =>
                            {
                                _logoError = e.ToString();
                            });
                    }
                    else
                    {
                        _organizationLogo = null;
                        _logoError = "No organization logo.";
                    }
                }
                catch (Exception e)
                {
                    _logoError = e.Message;
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
            }
            else
            {
                if (_organizationLogo)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.Space();

                    EditorGUILayout.LabelField(
                        new GUIContent(_organizationLogo), EditorStyles.centeredGreyMiniLabel, GUILayout.MinHeight(128), GUILayout.ExpandWidth(true));

                    EditorGUILayout.Space();

                    if (GUILayout.Button("Delete Logo") && EditorUtility.DisplayDialog("Delete Logo", "Are you sure you want to delete the organization logo?", "Yes", "No"))
                    {
                        DeleteLogo(record);
                        GUIUtility.ExitGUI();
                    }
                    if (GUILayout.Button("Download Logo"))
                    {
                        var path = EditorUtility.SaveFilePanel("Download Logo (PNG)", "", "logo.png", "png");
                        if (!string.IsNullOrEmpty(path))
                        {
                            var bytes = _organizationLogo.EncodeToPNG();
                            File.WriteAllBytes(path, bytes);
                            
                            var open = EditorUtility.DisplayDialog("Logo Downloaded", "The organization logo has been downloaded successfully.", "Open", "Ok");
                            if (open)
                            {
                                var fullPath = Path.GetFullPath(path);
                                EditorUtility.RevealInFinder(fullPath);
                                GUIUtility.ExitGUI();
                            }
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(_logoError))
                    EditorGUILayout.HelpBox(_logoError, MessageType.Error);
                else
                    EditorGUILayout.HelpBox("Downloading...", MessageType.Info);

                if (GUILayout.Button("Upload Logo"))
                {
                    UploadLogo(record);
                }
            }
        }

        private void DeleteLogo(OrganizationDto record)
        {
            var delete = Task.Run(() => MetaverseProgram.ApiClient.Organizations.DeleteLogoAsync(record.Id)).Result;
            if (!delete.Succeeded)
            {
                var err = Task.Run(() => delete.GetErrorAsync()).Result;
                OnRequestError(err);
                return;
            }

            record.Theme.Logo = null;
            UpdateRecord(record);
        }

        private void UploadLogo(OrganizationDto record)
        {
            string fileName = null;
            var thumbnailPath = EditorUtility.OpenFilePanel("Select Logo", "", "png,jpg,jpeg");
            if (string.IsNullOrEmpty(thumbnailPath))
                return;
            
            if (!string.IsNullOrEmpty(thumbnailPath))
                fileName = Path.GetFileName(thumbnailPath);

            var bytes = File.ReadAllBytes(thumbnailPath);
            var memStream = new MemoryStream(bytes);
            var upsertLogo = Task.Run(async () => await MetaverseProgram.ApiClient.Organizations.UpsertLogoAsync(record.Id, memStream, fileName ?? "logo.png")).Result;
            if (!upsertLogo.Succeeded)
            {
                var err = Task.Run(async () => await upsertLogo.GetErrorAsync()).Result;
                OnRequestError(err);
                return;
            }

            record.Theme ??= new OrganizationThemeDto();
            record.Theme.Logo = Task.Run(async () => await upsertLogo.GetResultAsync()).Result;
            
            EditorUtility.DisplayDialog("Logo Uploaded", "The organization logo has been uploaded successfully.", "Ok");
            
            _fetchedLogo = false;
            _logoError = null;
            if (_organizationLogo)
                DestroyImmediate(_organizationLogo);
            _organizationLogo = null;
        }

        protected override void OnAfterDrawRecord(OrganizationDto record)
        {
            if (!HasValidRecordId) return;

            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.Space();

            MetaverseEditorUtils.Box(() =>
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

                EditorGUILayout.LabelField("Manage Organization Content", EditorStyles.boldLabel);

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.HelpBox("From here you can manage all of your organization content.", MessageType.Info);

                _editorTabNames ??= Enum.GetNames(typeof(EditorTabs));
                _currentEditorTab = (EditorTabs)GUILayout.Toolbar((int)_currentEditorTab, _editorTabNames, EditorStyles.toolbarButton);

                switch (_currentEditorTab)
                {
                    case EditorTabs.Users:
                        _usersPager.Draw(150);
                        break;
                    case EditorTabs.MetaSpaces:
                        _metaSpacesPager.Draw(150);
                        break;
                    case EditorTabs.DataSources:
                        _dataSourceListEditor.HostId = CurrentEditedRecordId;
                        _dataSourceListEditor.Draw();
                        break;
                }
            });
        }

        private bool OnUsersEditorDrawRecord(OrganizationUserDto record)
        {
            MetaverseEditorUtils.Box(() =>
            {
                if (GUILayout.Button("...", EditorStyles.miniButton, GUILayout.Width(23)))
                    OrganizationUserFormEditor.Edit(record, _ => _usersPager.Refresh());

                GUILayout.Space(10);

                EditorGUILayout.LabelField(record.SystemUser.UserName + (!record.HasAcceptedInvite ? " (Invite Sent)" : ""));
                MetaverseEditorUtils.Disabled(() => EditorGUILayout.EnumPopup(record.Role, GUILayout.Width(100)));
            }, vertical: false);
            return true;
        }

        private void OnUsersEditorAddButtonClicked()
        {
            if (HasValidRecordId)
                OrganizationUserFormEditor.New(CurrentEditedRecordId, _ => { _usersPager.Refresh(); });
        }

        private bool OnUsersEditorBeginRequest(int offset, int count, string filter)
        {
            if (!HasValidRecordId)
                return false;

            MetaverseProgram.ApiClient.Organizations.GetUsersAsync(new OrganizationUsersQueryParams
            {
                OrganizationId = CurrentEditedRecordId,
                Count = (uint)count,
                Offset = (uint)offset,
                EmailFilter = filter
            })
                .ResponseThen(_usersPager.EndRequest, e => _usersPager.RequestError = e.ToString());

            return true;
        }

        private bool OnMetaSpacesEditorDrawRecord(MetaSpaceDto record)
        {
            MetaverseEditorUtils.Box(() =>
            {
                if (GUILayout.Button("\u00D7", EditorStyles.miniButton, GUILayout.Width(21)) &&
                    EditorUtility.DisplayDialog("Remove Meta Space", $"Are you sure you want to remove '{record.Name}' from {record.Name}?", "Yes", "No"))
                {
                    var currentRecordId = CurrentEditedRecordId;
                    var remove = Task.Run(async () => await MetaverseProgram.ApiClient.Organizations.RemoveMetaSpaceAsync(currentRecordId, record.Id)).Result;
                    if (!remove.Succeeded)
                    {
                        var error = remove.GetErrorAsync().Result;
                        EditorUtility.DisplayDialog("Remove Meta Space Failed",
                            "Failed to remove the selected meta space from the organization: " + error, "Ok");
                        Debug.LogError("Remove meta space from organization <color=red><b>failed</b></color>: " + error);
                    }

                    MetaverseDispatcher.AtEndOfFrame(_metaSpacesPager.Refresh);
                }

                GUILayout.Space(10);
                EditorGUILayout.LabelField($"{record.Name} [{record.RepresentativeName ?? ""}] ({record.Id.ToString()[..5]}...)");
            }, vertical: false);
            return true;
        }

        private bool OnMetaSpacesEditorBeginRequest(int offset, int count, string filter)
        {
            if (!HasValidRecordId)
                return false;

            MetaverseProgram.ApiClient.MetaSpaces.GetAllAsync(new MetaSpaceQueryParams
            {
                OrganizationId = CurrentEditedRecordId,
                Count = (uint)count,
                Offset = (uint)offset,
                NameFilter = filter,
            })
            .ResponseThen(_metaSpacesPager.EndRequest, e => _usersPager.RequestError = e.ToString());

            return true;
        }

        private void OnMetaSpacesEditorAddButtonClicked()
        {
            if (HasValidRecordId)
                PickerEditor.Pick<PublicMetaSpacePickerEditor>(o =>
                {
                    var metaSpace = (MetaSpaceDto)o;
                    if (!EditorUtility.DisplayDialog("Add Meta Space", $"Are you sure you want to add the meta space '{metaSpace.Name}' to the organization?", "Yes", "No"))
                        return;

                    var recordId = CurrentEditedRecordId;
                    var upsert = Task.Run(async () =>
                        await MetaverseProgram.ApiClient.Organizations.UpsertMetaSpaceAsync(
                            new OrganizationMetaSpaceUpsertForm(recordId, metaSpace.Id))).Result;

                    if (!upsert.Succeeded)
                    {
                        var error = upsert.GetErrorAsync().Result;
                        EditorUtility.DisplayDialog("Add Meta Space Failed",
                            "Failed to add the selected meta space to the organization: " + error, "Ok");
                        Debug.LogError("Add meta space to organization <color=red><b>failed</b></color>: " + error);
                    }

                    MetaverseDispatcher.AtEndOfFrame(_metaSpacesPager.Refresh);
                });
        }
    }
}
