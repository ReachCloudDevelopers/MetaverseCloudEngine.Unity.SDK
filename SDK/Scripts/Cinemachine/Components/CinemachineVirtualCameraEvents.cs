using Cinemachine;
using MetaverseCloudEngine.Unity.Async;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Cinemachine.Components
{
    /// <summary>
    /// This behaviour is intended to be attached to a Virtual Camera GameObject. It allows you
    /// to listen to the various CinemachineCamera events and do something in response.
    /// </summary>
    [HideMonoScript]
    [RequireComponent(typeof(CinemachineVirtualCameraBase))]
    public class CinemachineVirtualCameraEvents : TriInspectorMonoBehaviour
    {
        [Tooltip("Invoked when the virtual camera starts Live status.")]
        public UnityEvent onLive;
        [Tooltip("Invoked when the virtual camera stops Live status and all blending is complete.")]
        public UnityEvent onFullyLive;
        [Tooltip("Invoked when the virtual camera status changes to a non-live status.")]
        public UnityEvent onNotLive;
        
        private CinemachineVirtualCameraBase _vCam;
        private bool _wasLive;
        
        public bool IsLive => GetActiveBrain() && GetActiveBrain().IsLive(_vCam);
        
        protected virtual void OnEnable()
        {
            _vCam = GetComponent<CinemachineVirtualCameraBase>();
            UpdateCam(GetActiveBrain());
            CinemachineCore.CameraUpdatedEvent.AddListener(UpdateCam);
        }

        protected virtual void OnDisable()
        {
            CinemachineCore.CameraUpdatedEvent.RemoveListener(UpdateCam);
        }

        private static CinemachineBrain GetActiveBrain()
        {
            return CinemachineCore.Instance != null && CinemachineCore.Instance.BrainCount > 0 ? CinemachineCore.Instance.GetActiveBrain(0) : null;
        }

        private void UpdateCam(CinemachineBrain brain)
        {
            if (!brain)
                return;
            
            if (brain.IsLive(_vCam))
            {
                if (_wasLive) return;
                _wasLive = true;
                onLive?.Invoke();
                MetaverseDispatcher.WaitUntil(() => !this || !brain || !brain.IsBlending, () =>
                {
                    if (!this || !brain || !brain.IsLive(_vCam))
                        return;
                    if (!_wasLive)
                        return;
                    onFullyLive?.Invoke();
                    OnFullyLive();
                });
                return;
            }

            if (!_wasLive)
                return;
            onNotLive?.Invoke();
            _wasLive = false;
            OnNotLive();
        }

        protected virtual void OnFullyLive()
        {
        }

        protected virtual void OnNotLive()
        {
        }
    }
}