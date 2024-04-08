using System;
using UnityEngine;
using MetaverseCloudEngine.Unity.Services.Abstract;

namespace MetaverseCloudEngine.Unity.Services.Implementation
{
    public class UnityDebugLogger : IDebugLogger
    {
        private const string LogTag
#if UNITY_EDITOR
            = "<b><color=#34c6eb>[Metaverse]</color></b>";
#else
            = "[Metaverse]";
#endif
        
        private const string LogTagLog = 
#if UNITY_EDITOR
            "<b><color=#00ff00>[Log]</color></b>";
#else
            "[Log]";
#endif
        
        private const string LogTagWarning =
#if UNITY_EDITOR
            "<b><color=#ffcc00>[Warning]</color></b>";
#else
            "[Warning]";
#endif
        
        private const string LogTagError =
#if UNITY_EDITOR
            "<b><color=#ff0000>[Error]</color></b>";
#else
            "[Error]";
#endif

        public void Log(object content)
            => Debug.Log($"{LogTag} [{DateTime.Now:hh:mm:ss}] {LogTagLog} {content}");

        public void LogError(object content) =>
            Debug.Log($"{LogTag} [{DateTime.Now:hh:mm:ss}] {LogTagError} {content}");

        public void LogWarning(object content) 
            => Debug.Log($"{LogTag} [{DateTime.Now:hh:mm:ss}] {LogTagWarning} {content}");
    }
}