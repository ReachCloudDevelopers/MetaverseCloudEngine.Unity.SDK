using MetaverseCloudEngine.Unity.Attributes;
using UnityEditor;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors
{
    [CustomPropertyDrawer(typeof(DisallowNullAttribute))]
    public class RequiredAttributePropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var color = GUI.backgroundColor;
            if (!IsValid(property)) GUI.backgroundColor = Color.red;
            try { EditorGUI.PropertyField(position, property, label); }
            finally { GUI.backgroundColor = color; }
        }

        private bool IsValid(SerializedProperty property)
        {
            if (property.propertyType == SerializedPropertyType.String)
                return !string.IsNullOrEmpty(property.stringValue);
            if (property.propertyType == SerializedPropertyType.ObjectReference)
                return property.objectReferenceValue != null;
            if (property.propertyType == SerializedPropertyType.ExposedReference)
                return property.exposedReferenceValue != null;

            return true;
        }
    }
}