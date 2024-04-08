using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MetaverseCloudEngine.Unity.UI.Components
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(Toggle))]
    public class ExtraToggleEvents : MonoBehaviour
    {
        public UnityEvent onToggleOn;
        public UnityEvent onToggleOff;

        private Toggle _toggle;

        private void Awake() => _toggle = GetComponent<Toggle>();

        private void OnEnable()
        {
            _toggle.onValueChanged.AddListener(OnToggleValueChanged);
            OnToggleValueChanged(_toggle.isOn);
        }

        private void OnDisable()
        {
            if (_toggle)
                _toggle.onValueChanged.RemoveListener(OnToggleValueChanged);
        }

        private void OnToggleValueChanged(bool value)
        {
            if (value) onToggleOn?.Invoke();
            else onToggleOff?.Invoke();
        }
    }
}
