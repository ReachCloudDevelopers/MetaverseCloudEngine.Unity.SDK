using UnityEngine;

namespace MetaverseCloudEngine.Unity.Cinemachine.Editor.Components
{
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public class MetaSpaceCameraCinemachineWarningIgnore : MonoBehaviour
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
