using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Components
{
    public class VersionNumberHelper : MonoBehaviour
    {
        public UnityEvent<string> onVersionNumber;

        private void OnEnable()
        {
            onVersionNumber?.Invoke(Application.version);
        }
    }
}