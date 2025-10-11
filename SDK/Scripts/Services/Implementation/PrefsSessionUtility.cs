using System;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MetaverseCloudEngine.Unity.Services.Implementation
{
    internal static class PrefsSessionUtility
    {
#if UNITY_EDITOR
        private const string SessionKey = "MetaverseCloudEngine.Prefs.SessionSuffix";

        internal static string GetSessionSuffix()
        {
            // Use EditorPrefs instead of SessionState to persist across editor restarts
            // Key is unique per project path to support multiple projects
            var projectPath = Path.GetFullPath(".");
            var projectHash = projectPath.GetHashCode().ToString("X8");
            var editorPrefsKey = $"{SessionKey}_{projectHash}";
            
            var suffix = EditorPrefs.GetString(editorPrefsKey, string.Empty);
            if (string.IsNullOrEmpty(suffix))
            {
                suffix = "_" + Guid.NewGuid().ToString("N");
                EditorPrefs.SetString(editorPrefsKey, suffix);
            }

            return suffix;
        }
#else
        internal static string GetSessionSuffix() => string.Empty;
#endif
    }
}
