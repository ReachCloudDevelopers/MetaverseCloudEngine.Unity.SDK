using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Video.Components
{
    [System.Serializable]
    public class VideoCameraEvents
    {
        public UnityEvent onVideoDisabled;
        public UnityEvent onVideoActive;

        public UnityEvent onScreenShareDisabled;
        public UnityEvent onScreenShareActive;

        public void Combine(VideoCameraEvents events)
        {
            onVideoDisabled.AddListener(() => events.onVideoDisabled?.Invoke());
            onVideoActive.AddListener(() => events.onVideoActive?.Invoke());

            onScreenShareDisabled.AddListener(() => events.onScreenShareDisabled?.Invoke());
            onScreenShareActive.AddListener(() => events.onScreenShareActive?.Invoke());
        }
    }
}
