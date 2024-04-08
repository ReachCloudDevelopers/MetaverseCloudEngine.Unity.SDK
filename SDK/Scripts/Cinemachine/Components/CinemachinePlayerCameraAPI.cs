using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Cinemachine.Components
{
    [HideMonoScript]
    [DisallowMultipleComponent]
    public class CinemachinePlayerCameraAPI : TriInspectorMonoBehaviour
    {
        [Header("First Person")]
        public UnityEvent onFirstPersonActive;
        public UnityEvent onFirstPersonInactive;
        [Header("Third Person")]
        public UnityEvent onThirdPersonActive;
        public UnityEvent onThirdPersonInactive;

        private void OnEnable()
        {
            CinemachinePlayerCamera.CameraActive += OnCameraActive;
            CinemachinePlayerCamera.CameraInactive += OnCameraInactive;

            if (CinemachinePlayerCamera.FirstPerson && CinemachinePlayerCamera.FirstPerson.IsLive)
            {
                OnCameraActive(CinemachinePlayerCamera.PlayerCameraType.FirstPerson);
                OnCameraInactive(CinemachinePlayerCamera.PlayerCameraType.ThirdPerson);
            }
            else if (CinemachinePlayerCamera.ThirdPerson && CinemachinePlayerCamera.ThirdPerson.IsLive)
            {
                OnCameraInactive(CinemachinePlayerCamera.PlayerCameraType.FirstPerson);
                OnCameraActive(CinemachinePlayerCamera.PlayerCameraType.ThirdPerson);
            }
            else
            {
                OnCameraInactive(CinemachinePlayerCamera.PlayerCameraType.FirstPerson);
                OnCameraInactive(CinemachinePlayerCamera.PlayerCameraType.ThirdPerson);
            }
        }

        private void OnDisable()
        {
            CinemachinePlayerCamera.CameraActive -= OnCameraActive;
            CinemachinePlayerCamera.CameraInactive -= OnCameraInactive;
        }

        private void OnCameraActive(CinemachinePlayerCamera.PlayerCameraType type)
        {
            if (type == CinemachinePlayerCamera.PlayerCameraType.FirstPerson)
                onFirstPersonActive?.Invoke();
            else
                onThirdPersonActive?.Invoke();
        }

        private void OnCameraInactive(CinemachinePlayerCamera.PlayerCameraType type)
        {
            if (type == CinemachinePlayerCamera.PlayerCameraType.FirstPerson)
                onFirstPersonInactive?.Invoke();
            else
                onThirdPersonInactive?.Invoke();
        }

        public void SwitchToFirstPerson()
        {
            if (CinemachinePlayerCamera.FirstPerson)
                CinemachinePlayerCamera.FirstPerson.RequestLive();
        }
        
        public void SwitchToThirdPerson()
        {
            if (CinemachinePlayerCamera.ThirdPerson)
                CinemachinePlayerCamera.ThirdPerson.RequestLive();
        }
    }
}