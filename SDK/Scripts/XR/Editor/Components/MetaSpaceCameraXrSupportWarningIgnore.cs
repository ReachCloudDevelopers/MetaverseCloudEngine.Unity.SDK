using UnityEngine;

namespace MetaverseCloudEngine.Unity.XR.Editor.Components
{
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public class MetaSpaceCameraXrSupportWarningIgnore : MonoBehaviour
    {
        private void OnValidate()
        {
            Reset();
        }

        private void Reset()
        {
            hideFlags = HideFlags.HideInInspector;
        }
    }
}
