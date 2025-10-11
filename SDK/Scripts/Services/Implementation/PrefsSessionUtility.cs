using System;

namespace MetaverseCloudEngine.Unity.Services.Implementation
{
    internal static class PrefsSessionUtility
    {
#if UNITY_EDITOR
        private const string SessionKey = "MetaverseCloudEngine.Prefs.SessionSuffix";

        internal static string GetSessionSuffix()
        {
            var suffix = UnityEditor.SessionState.GetString(SessionKey, string.Empty);
            if (string.IsNullOrEmpty(suffix))
            {
                suffix = "_" + Guid.NewGuid().ToString("N");
                UnityEditor.SessionState.SetString(SessionKey, suffix);
            }

            return suffix;
        }
#else
        internal static string GetSessionSuffix() => string.Empty;
#endif
    }
}
