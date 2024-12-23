using System;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors
{
    public abstract class RecordEditor<TRecordType, TIdType, TPickerEditor> : EditorWindow
        where TRecordType : class, new()
        where TPickerEditor : PickerEditor
    {
        private Vector2 _recordScroll;
        private bool _isInRequest;

        public TIdType CurrentEditedRecordId
        {
            get
            {
                var lastEditedId = EditorPrefs.GetString(GetEditedRecordIdStringKey(), null);

                TIdType o;
                try
                {
                    o = JsonConvert.DeserializeObject<TIdType>(lastEditedId);
                }
                catch (Exception)
                {
                    return default;
                }

                if (!string.IsNullOrEmpty(lastEditedId) && !o.Equals(default(TIdType)))
                    return o;
                return default;
            }
            set
            {
                if (value.Equals(default(TIdType))) EditorPrefs.DeleteKey(GetEditedRecordIdStringKey());
                else EditorPrefs.SetString(GetEditedRecordIdStringKey(), JsonConvert.SerializeObject(value));
            }
        }

        public abstract string Header { get; }

        public TRecordType CurrentEditedRecord { get; private set; }

        protected bool HasValidRecordId => CurrentEditedRecordId is not null && !CurrentEditedRecordId.Equals(default(TIdType));

        protected virtual void OnGUI()
        {
            MetaverseEditorUtils.Header(Header);

            if (MetaverseProgram.ApiClient == null)
                return;

            if (!MetaverseProgram.ApiClient.Account.IsLoggedIn)
            {
                MetaverseAccountWindow.LoginButton();
                return;
            }

            var isLoading = _isInRequest;
            MetaverseEditorUtils.DrawLoadingScreen(
                () =>
                {
                    DrawToolbar();

                    if (CurrentEditedRecord == null)
                    {
                        if (HasValidRecordId)
                        {
                            _isInRequest = true;
                            BeginRequestRecord(CurrentEditedRecordId);
                            return;
                        }

                        CurrentEditedRecord = new TRecordType();
                    }

                    MetaverseEditorUtils.Box(() =>
                    {
                        _recordScroll = EditorGUILayout.BeginScrollView(_recordScroll);

                        DrawRecord(CurrentEditedRecord);

                        MetaverseEditorUtils.Box(() =>
                        {
                            if (GUILayout.Button(HasValidRecordId ? "Update Metadata" : "Create", EditorStyles.toolbarButton))
                            {
                                _isInRequest = true;
                                SaveRecord(CurrentEditedRecord);
                            }

                            EditorGUILayout.Space();

                            if (HasValidRecordId && GUILayout.Button("Delete", EditorStyles.toolbarButton))
                            {
                                if (EditorUtility.DisplayDialog(
                                    "Delete",
                                    "DANGER: You are about to delete this. This cannot be undone. Are you sure you want to continue?", "Yes", "Cancel"))
                                {
                                    _isInRequest = true;
                                    DeleteRecord(CurrentEditedRecord);
                                }
                            }
                        });

                        OnAfterDrawRecord(CurrentEditedRecord);
                        
                        GUILayout.FlexibleSpace();
                        
                        EditorGUILayout.EndScrollView();
                    });
                },
                MetaverseEditorUtils.DrawDefaultLoadingScreen, isLoading);
        }

        private void DrawToolbar()
        {
            MetaverseEditorUtils.Box(() =>
            {
                if (!HasValidRecordId)
                    GUI.enabled = false;

                if (GUILayout.Button("New", EditorStyles.toolbarButton))
                {
                    New();
                }

                GUI.enabled = true;

                if (GUILayout.Button("Select", EditorStyles.toolbarButton))
                {
                    PickerEditor.Pick<TPickerEditor>(o =>
                    {
                        var rec = o as TRecordType;
                        UpdateRecord(rec);
                    });
                }
            }, vertical: false);
        }

        protected abstract void DrawRecord(TRecordType record);

        protected virtual void OnAfterDrawRecord(TRecordType record)
        {
        }

        protected abstract void BeginRequestRecord(TIdType id);

        protected abstract void DeleteRecord(TRecordType record);

        protected abstract void SaveRecord(TRecordType record);

        protected abstract TIdType GetRecordIdentifier(TRecordType record);

        protected virtual void UpdateRecord(TRecordType rec)
        {
            _isInRequest = false;
            CurrentEditedRecord = rec;
            CurrentEditedRecordId = GetRecordIdentifier(rec);
        }

        protected void New()
        {
            _isInRequest = false;
            CurrentEditedRecordId = default;
            CurrentEditedRecord = null;
        }

        protected void OnRequestErrorFatal(object e)
        {
            New();
            OnRequestError(e);
        }

        protected void OnRequestError(object e)
        {
            _isInRequest = false;
            EditorUtility.DisplayDialog("Error Occured", "Error: " + e.ToPrettyErrorString(), "Ok");
            Debug.LogError(e);
        }

        private string GetEditedRecordIdStringKey()
        {
            return GetType().FullName + "." + nameof(CurrentEditedRecordId);
        }
    }
}