using System;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MetaverseCloudEngine.Unity.Services.Implementation
{
    internal static class PrefsSessionUtility
    {
        // Removed session suffix logic to ensure credentials persist across edit/play modes
        internal static string GetSessionSuffix() => string.Empty;
    }
}
