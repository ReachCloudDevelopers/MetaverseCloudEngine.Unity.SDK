using System;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Unity.Assets;
using MetaverseCloudEngine.Unity.Async;
using UnityEditor;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors
{
    public class MetaSpaceExternalServicesListEditor
    {
        private readonly PaginatedEditor<MetaSpaceExternalServiceDto> _paginatedEditor;
        private readonly IAssetReference _assetReference;

        public MetaSpaceExternalServicesListEditor(IAssetReference assetReference)
        {
            _paginatedEditor = new PaginatedEditor<MetaSpaceExternalServiceDto>("External Services")
             {
                 DisplayFilter = false,
                 DisplayPagers = false
             };
            _paginatedEditor.AddButtonClicked += OnAddButtonClicked;
            _paginatedEditor.BeginRequest += OnBeginRequest;
            _paginatedEditor.DrawRecord += OnDrawRecord;
            _assetReference = assetReference;
        }
        
        public void Draw()
        {
            _paginatedEditor.Draw();
        }

        private bool OnDrawRecord(MetaSpaceExternalServiceDto record)
        {
            MetaverseEditorUtils.Box(() =>
            {
                if (GUILayout.Button("...", EditorStyles.miniButton, GUILayout.Width(23)))
                    MetaSpaceExternalServiceFormEditor.Edit(record, _paginatedEditor.Refresh);
                EditorGUILayout.LabelField(record.Domain);
            }, vertical: false);
            return true;
        }

        private bool OnBeginRequest(int offset, int count, string filter)
        {
            if (_assetReference.ID == null) return false;
            if (!MetaverseProgram.Initialized) return false;
            if (!MetaverseProgram.ApiClient.Account.IsLoggedIn) return false;
            MetaverseProgram.ApiClient.MetaSpaces.GetExternalServices(_assetReference.ID.GetValueOrDefault())
                .ResponseThen(_paginatedEditor.EndRequest, e => _paginatedEditor.RequestError = e.ToString());
            return true;
        }

        private void OnAddButtonClicked()
        {
            MetaSpaceExternalServiceFormEditor.Edit(_assetReference.ID.GetValueOrDefault(), _paginatedEditor.Refresh);
        }
    }
}