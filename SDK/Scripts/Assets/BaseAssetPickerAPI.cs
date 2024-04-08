using System;
using UnityEngine;
using UnityEngine.Events;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using TriInspectorMVCE;

namespace MetaverseCloudEngine.Unity.Assets
{
    public abstract partial class BaseAssetPickerAPI<TAssetDto, TQueryParams> : TriInspectorMonoBehaviour 
        where TAssetDto : AssetDto
        where TQueryParams : AssetPickerQueryParams, new()
    {
        public bool showOnEnabled;
        public bool cancelOnDisable;
        
        public TQueryParams queryParams = new();
        
        [Space]
        public UnityEvent<Guid> onPickedID = new();
        public UnityEvent<string> onPickedIDString = new();
        public UnityEvent<TAssetDto> onPickedData = new();
        public UnityEvent onCancelled;

        private void OnEnable()
        {
            if (showOnEnabled)
                Show();
        }

        private void OnDisable()
        {
            if (cancelOnDisable)
                Cancel();
        }

        public void Cancel()
        {
            CancelInternal();
        }

        partial void CancelInternal();

        public virtual void Show()
        {
            ShowInternal();
        }

        partial void ShowInternal();
    }
}