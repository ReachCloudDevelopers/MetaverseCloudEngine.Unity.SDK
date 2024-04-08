using UnityEditor;
using UnityEngine;

namespace TriInspectorUnityInternalBridgeMVCE
{
    internal static class EditorGUIUtilityProxy
    {
        public static Texture2D GetHelpIcon(MessageType type)
        {
            return typeof(EditorGUIUtility).GetMethod("GetHelpIcon", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, new object[] { type }) as Texture2D;
        }
    }
}