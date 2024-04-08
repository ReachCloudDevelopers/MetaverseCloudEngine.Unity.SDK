using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MetaverseCloudEngine.Unity.Attributes
{
    public class ProtectedFieldAttribute : PropertyAttribute
    {
        
    }
    
#if UNITY_EDITOR

    [CustomPropertyDrawer(typeof(ProtectedFieldAttribute))]
    public class ProtectedFieldDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            property.stringValue = EditorGUI.PasswordField(position, label, property.stringValue);
        }
    }
    
#endif
}