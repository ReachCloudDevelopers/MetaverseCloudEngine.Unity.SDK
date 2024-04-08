using UnityEditor;
using UnityEngine;

namespace TriInspectorUnityInternalBridgeMVCE
{
    public static class ScriptAttributeUtilityProxy
    {
        public static PropertyHandlerProxy GetHandler(SerializedProperty property) 
        {
            var type = typeof(EditorApplication).Assembly.GetType("UnityEditor.ScriptAttributeUtility", true);
            var handler = type.GetMethod("GetHandler", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, new object[] { property });
            return new PropertyHandlerProxy(handler);
        }
    }

    public readonly struct PropertyHandlerProxy
    {
        private readonly object _handler;

        public PropertyHandlerProxy(object handler)
        {
            _handler = handler;
        }

        // ReSharper disable once InconsistentNaming
        public bool hasPropertyDrawer => (bool)_handler.GetType().GetProperty("hasPropertyDrawer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.GetProperty).GetValue(_handler);

        public float GetHeight(SerializedProperty property, GUIContent label, bool includeChildren)
        {
            var height = (float)_handler.GetType().GetMethod("GetHeight", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).Invoke(_handler, new object[] { property, label, includeChildren });
            return height;
        }

        public bool OnGUI(Rect position, SerializedProperty property, GUIContent label, bool includeChildren)
        {
            return (bool)_handler.GetType().GetMethod("OnGUI", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).Invoke(_handler, new object[] { position, property, label, includeChildren });
        }
    }
}