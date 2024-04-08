using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MetaverseCloudEngine.Unity.UI.Components
{
    [RequireComponent(typeof(Slider))]
    [ExecuteInEditMode]
    public class ExtraSliderEvents : MonoBehaviour
    {
        public UnityEvent onMin;
        public UnityEvent onMax;
        public string outputStringFormat = "{0:P}%";
        public UnityEvent<string> onOutputString;

        private Slider _slider;
        
        private void Awake()
        {
            _slider = GetComponent<Slider>();
            _slider.onValueChanged.AddListener(OnValueChanged);
            OnValueChanged(_slider.value);
        }

        private void OnValueChanged(float value)
        {
            if (value <= _slider.minValue)
                onMin?.Invoke();
            else if (value >= _slider.maxValue)
                onMax?.Invoke();

            if (!string.IsNullOrWhiteSpace(outputStringFormat))
            {
                string format = string.Format(outputStringFormat, value);
                onOutputString?.Invoke(format);
            }
        }
    }
}
