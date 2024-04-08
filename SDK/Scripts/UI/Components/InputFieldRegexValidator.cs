using TMPro;
using UnityEngine;
using System.Text.RegularExpressions;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.UI.Components
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_InputField))]
    public class InputFieldRegexValidator : MonoBehaviour
    {
        public string pattern;

        [Header("Events")]
        public UnityEvent onIsValid;
        public UnityEvent onIsNotValid;

        private TMP_InputField _inputField;

        private void Awake()
        {
            _inputField = GetComponent<TMP_InputField>();
            Validate(_inputField.text);
            AddListener();
        }

        private void OnDestroy()
        {
            RemoveListener();
        }

        private void AddListener()
        {
            _inputField.onValueChanged.AddListener(OnValueChanged);
        }

        private void RemoveListener()
        {
            _inputField.onValueChanged.RemoveListener(OnValueChanged);
        }

        private void OnValueChanged(string str)
        {
            RemoveListener();
            Validate(str);
            AddListener();
        }

        private void Validate(string str)
        {
            Match match = Regex.Match(str, pattern);
            if (!match.Success)
            {
                _inputField.ReleaseSelection();
                _inputField.text = string.Empty;
                _inputField.LayoutComplete();
                onIsNotValid?.Invoke();
                return;
            }

            onIsValid?.Invoke();
        }
    }
}
