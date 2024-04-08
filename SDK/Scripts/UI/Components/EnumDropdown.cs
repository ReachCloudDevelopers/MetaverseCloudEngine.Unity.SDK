using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.UI.Components
{
    [RequireComponent(typeof(TMP_Dropdown))]
    public class EnumDropdown : MonoBehaviour
    {
        public string enumTypeName;
        public string[] ignoredValues;
        public bool populateOptions;

        public UnityEvent onNullValue;
        public UnityEvent<int> onEnumValue;

        private int[] _enumValues;
        private string[] _enumNames;
        
        private void Start()
        {
            Type enumType = Type.GetType(enumTypeName);
            if (enumType is not {IsEnum: true})
            {
                MetaverseProgram.Logger.LogWarning($"Enum type '{enumTypeName}' is not valid.");
                return;
            }

            _enumNames = Enum.GetNames(enumType);
            if (ignoredValues.Length > 0)
                _enumNames = _enumNames.Where(x => !ignoredValues.Contains(x)).ToArray();
            _enumValues = _enumNames.Select(x => (int)Enum.Parse(enumType, x)).ToArray();

            TMP_Dropdown dropDown = GetComponent<TMP_Dropdown>();
            if (populateOptions)
                dropDown.options = _enumNames.Select(x => new TMP_Dropdown.OptionData(x.CamelCaseToSpaces())).ToList(); 
            
            GetValue(dropDown, dropDown.value);
            dropDown.onValueChanged.AddListener(index => { GetValue(dropDown, index); });
        }

        private void GetValue(TMP_Dropdown dropDown, int index)
        {
            TMP_Dropdown.OptionData option = dropDown.options[index];
            int enumValueIndex = Array.IndexOf(_enumNames, option.text.Replace(" ", string.Empty));
            if (enumValueIndex != -1)
                onEnumValue?.Invoke(_enumValues[enumValueIndex]);
            else
                onNullValue?.Invoke();
        }
    }
}