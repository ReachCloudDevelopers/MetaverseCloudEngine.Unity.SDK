using System;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Components
{
    public class PermissionsAPI : TriInspectorMonoBehaviour
    {
        [Flags]
        public enum PermissionType
        {
            Microphone = 1,
            Camera = 2,
        }

        public bool requestOnStart = true;
        public PermissionType permissionType = (PermissionType)~0;

        public UnityEvent onMicrophoneGranted;
        public UnityEvent onMicrophoneDenied;

        public UnityEvent onCameraGranted;
        public UnityEvent onCameraDenied;

        private PermissionCallbacks _callbacks;

        private void Awake()
        {
            _callbacks = new PermissionCallbacks();
            _callbacks.PermissionGranted += OnPermissionGranted;
            _callbacks.PermissionDenied += OnPermissionDenied;
            _callbacks.PermissionDeniedAndDontAskAgain += OnPermissionDenied;
        }

        private void Start()
        {
            Init();

            if (requestOnStart)
                Request();
        }

        private void Init()
        {
            if (Permission.HasUserAuthorizedPermission(Permission.Microphone))
                onMicrophoneGranted?.Invoke();
            else onMicrophoneDenied?.Invoke();

            if (Permission.HasUserAuthorizedPermission(Permission.Camera))
                onCameraGranted?.Invoke();
            else onCameraDenied?.Invoke();
        }

        private void Request()
        {
            if (permissionType.HasFlag(PermissionType.Microphone) && !Permission.HasUserAuthorizedPermission(Permission.Microphone))
                Permission.RequestUserPermission(Permission.Microphone, _callbacks);
            if (permissionType.HasFlag(PermissionType.Camera) && !Permission.HasUserAuthorizedPermission(Permission.Camera))
                Permission.RequestUserPermission(Permission.Camera, _callbacks);
        }

        private void OnPermissionGranted(string permission)
        {
            switch (permission)
            {
                case Permission.Microphone:
                    onMicrophoneGranted?.Invoke();
                    break;
                case Permission.Camera:
                    onCameraGranted?.Invoke();
                    break;
            }
        }

        private void OnPermissionDenied(string permission)
        {
            switch (permission)
            {
                case Permission.Microphone:
                    onMicrophoneDenied?.Invoke();
                    break;
                case Permission.Camera:
                    onCameraDenied?.Invoke();
                    break;
            }
        }
    }
}
