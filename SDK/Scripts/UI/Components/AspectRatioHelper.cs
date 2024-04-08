using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.UI.Components
{
    [ExecuteAlways]
    public class AspectRatioHelper : MonoBehaviour
    {
        [SerializeField] private float minAspect = 0.5625f;
        [SerializeField] private UnityEvent onMinAspect;
        [SerializeField] private UnityEvent onMaxAspect;

        private bool _isMin;

        private void Awake()
        {
            _isMin = GetAspectRatio() < minAspect;
            OnChanged();
        }

        private void Update()
        {
            if (GetAspectRatio() < minAspect)
            {
                if (_isMin)
                {
                    _isMin = false;
                    OnChanged();
                }
            }
            else
            {
                if (!_isMin)
                {
                    _isMin = true;
                    OnChanged();
                }
            }
        }

        [ContextMenu("Capture Current Aspect")]
        private void CaptureCurrentAspect()
        {
            minAspect = GetAspectRatio();
        }

        private void OnChanged()
        {
            if (_isMin) onMinAspect?.Invoke();
            else onMaxAspect?.Invoke();
        }

        private static float GetAspectRatio()
        {
            return (float)UnityEngine.Device.Screen.width / UnityEngine.Device.Screen.height;
        }
    }
}