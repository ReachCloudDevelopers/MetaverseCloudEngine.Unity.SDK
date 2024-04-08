using UnityEngine;
using UnityEditor;
using MetaverseCloudEngine.Unity.Labels;

namespace MetaverseCloudEngine.Unity.Editors
{
    [CustomPropertyDrawer(typeof(Label))]
    public class LabelPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var valueProperty = property.FindPropertyRelative("value");
            EditorGUI.PropertyField(position, valueProperty, label);
        }
    }
}
