#if MV_DOTNOW_SCRIPTING

using System;
using MetaverseCloudEngine.Unity.Scripting.Components;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors
{
    [InitializeOnLoad]
    internal static class MetaverseDotNetScriptTitleBarEditor
    {
        static MetaverseDotNetScriptTitleBarEditor()
        {
            ComponentTitlebarGUI.OnTitlebarGUI += OnTitlebarGUI;
        }

        private static void OnTitlebarGUI(Rect rect, UnityEngine.Object @object)
        {
            var script = @object as MetaverseDotNetScript;
            if (!script)
                return;

            var serializedObject = new SerializedObject(script);
            var assemblyProp = serializedObject.FindProperty("assemblyAsset");
            var classNameProp = serializedObject.FindProperty("className");

            var assemblyAsset = assemblyProp?.objectReferenceValue as TextAsset;
            var className = classNameProp?.stringValue;

            var displayTitle = BuildTitle(assemblyAsset, className);

            var fakeHeaderRect = new Rect(0, 0, rect.width, rect.height);

            // Draw header background to match MetaverseScript
            GUI.Box(fakeHeaderRect, GUIContent.none, EditorStyles.toolbar);

            // Layout mirrors MetaverseScriptTitleBarEditor
            var foldRect = new Rect(fakeHeaderRect.x + 4f, fakeHeaderRect.y, 16f, fakeHeaderRect.height - 4f);
            var iconRect = new Rect(foldRect.x + 14f, fakeHeaderRect.y - 2f, fakeHeaderRect.height - 4f, fakeHeaderRect.height);
            GUI.Label(iconRect, EditorGUIUtility.GetIconForObject(script));
            var toggleRect = new Rect(foldRect.xMax + 20f, fakeHeaderRect.y, 18f, fakeHeaderRect.height - 4f);
            var titleRect = new Rect(
                toggleRect.xMax + 2f,
                fakeHeaderRect.y - 2f,
                fakeHeaderRect.width - (toggleRect.xMax - fakeHeaderRect.x) - 8f,
                fakeHeaderRect.height);

            // Foldout and enabled toggle
            var isExpanded = InternalEditorUtility.GetIsInspectorExpanded(script);
            var newExpanded = EditorGUI.Foldout(foldRect, isExpanded, GUIContent.none, true);
            if (newExpanded != isExpanded)
                InternalEditorUtility.SetIsInspectorExpanded(script, newExpanded);

            var enabledProp = serializedObject.FindProperty("m_Enabled");
            if (enabledProp != null)
            {
                var newEnabled = EditorGUI.Toggle(toggleRect, enabledProp.boolValue);
                if (newEnabled != enabledProp.boolValue)
                {
                    enabledProp.boolValue = newEnabled;
                    serializedObject.ApplyModifiedProperties();
                }
            }

            // Title label showing class name + status
            GUI.Label(titleRect, displayTitle, EditorStyles.boldLabel);

            // Right-side icons (help, preset, menu) to match MetaverseScript header
            var iconSize = titleRect.height - 1;
            var rightMargin = 1f;
            var totalIconWidth = iconSize * 3;
            var startX = fakeHeaderRect.xMax - totalIconWidth - rightMargin;

            var helpIconRect = new Rect(startX, fakeHeaderRect.y, iconSize, iconSize);
            GUI.Label(helpIconRect, EditorGUIUtility.IconContent("_Help").image);

            var presetIconRect = new Rect(startX + iconSize - 1, fakeHeaderRect.y, iconSize, iconSize);
            GUI.Label(presetIconRect, EditorGUIUtility.IconContent("Preset.Context").image);

            var menuIconRect = new Rect(startX + (iconSize - 1) * 2, fakeHeaderRect.y, iconSize, iconSize);
            GUI.Label(menuIconRect, EditorGUIUtility.IconContent("_Menu").image);
        }

        private static string BuildTitle(TextAsset assemblyAsset, string className)
        {
            string baseTitle;

            if (assemblyAsset && !string.IsNullOrEmpty(className))
            {
                baseTitle = GetNiceTypeName(className);
            }
            else if (assemblyAsset)
            {
                baseTitle = ObjectNames.NicifyVariableName(assemblyAsset.name) + " (.NET Script)";
            }
            else
            {
                return "(No Script)";
            }

            var status = GetSecurityStatusSuffix(assemblyAsset);
            return string.IsNullOrEmpty(status) ? baseTitle : $"{baseTitle}  {status}";
        }

        private static string GetNiceTypeName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName))
                return fullName;

            // Normalise nested type separators and strip namespace
            fullName = fullName.Replace('+', '.');
            var lastDot = fullName.LastIndexOf('.');
            if (lastDot >= 0 && lastDot < fullName.Length - 1)
                return fullName[(lastDot + 1)..];

            return fullName;
        }

        private static string GetSecurityStatusSuffix(TextAsset assemblyAsset)
        {
            if (!assemblyAsset)
                return string.Empty;

            if (!MetaverseDotNetScriptSecurity.Enabled)
                return "(Security Disabled)";

            try
            {
                if (MetaverseDotNetScriptSecurity.ValidateAssemblyBytes(assemblyAsset.bytes, out var message))
                    return string.Empty;

                // We don't surface the full message here to keep the header compact;
                // detailed info is shown in the inspector body.
                return "[Security Validation Failed]";
            }
            catch (Exception)
            {
                return "[Security Validation Error]";
            }
        }
    }
}


#endif // MV_DOTNOW_SCRIPTING
