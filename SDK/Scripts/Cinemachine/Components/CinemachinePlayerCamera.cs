using System;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Cinemachine.Components
{
    public class CinemachinePlayerCamera : CinemachineVirtualCameraEvents
    {
        public enum PlayerCameraType
        {
            ThirdPerson,
            FirstPerson,
        }

        public PlayerCameraType type = PlayerCameraType.ThirdPerson;
        public UnityEvent onRequestLive;
        
        public static CinemachinePlayerCamera FirstPerson { get; private set; }
        public static CinemachinePlayerCamera ThirdPerson { get; private set; }

        public static event Action<PlayerCameraType> CameraActive;
        public static event Action<PlayerCameraType> CameraInactive;

        protected override void OnEnable()
        {
            base.OnEnable();
            
            if (type == PlayerCameraType.ThirdPerson)
                ThirdPerson = this;
            else
                FirstPerson = this;
        }

        private void OnCameraActive() => CameraActive?.Invoke(type);
        private void OnCameraInactive() => CameraInactive?.Invoke(type);

        public void RequestLive()
        {
            onRequestLive?.Invoke();
        }

        protected override void OnFullyLive() => OnCameraActive();
        protected override void OnNotLive() => OnCameraInactive();
    }
}