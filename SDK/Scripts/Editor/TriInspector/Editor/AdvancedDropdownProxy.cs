using UnityEditor.IMGUI.Controls;

namespace TriInspectorUnityInternalBridgeMVCE
{
    internal class AdvancedDropdownProxy
    {
        public static void SetShowHeader(AdvancedDropdown dropdown, bool showHeader)
        {
            var windowInstance = dropdown.GetType().GetField("m_WindowInstance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.GetField).GetValue(dropdown);
            windowInstance.GetType().GetProperty("showHeader", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.SetProperty).SetValue(windowInstance, showHeader);
        }
    }
}