using UnityEngine;

namespace MetaverseCloudEngine.Unity.Attributes
{
    public class ReadOnlyAttribute : PropertyAttribute
    {
        public bool DuringPlayMode { get; set; }
    }

#if UNITY_EDITOR

    [UnityEditor.CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
    public class ReadOnlyDrawer : UnityEditor.PropertyDrawer
    {
        public override float GetPropertyHeight(UnityEditor.SerializedProperty property, GUIContent label)
        {
            return UnityEditor.EditorGUI.GetPropertyHeight(property, label, true);
        }

        public override void OnGUI(Rect position, UnityEditor.SerializedProperty property, GUIContent label)
        {
            GUI.enabled = false;
            if (attribute is ReadOnlyAttribute attributeReadOnly)
            {
                if (attributeReadOnly.DuringPlayMode && !Application.isPlaying)
                    GUI.enabled = true;

                UnityEditor.EditorGUI.PropertyField(position, property, label, true);
            }
            GUI.enabled = true;
        }
    }

#endif
}
