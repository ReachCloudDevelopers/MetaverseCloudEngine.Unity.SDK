using UnityEditor;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.FixingOtherPeoplesCode
{
    // Fixes OVRPlugin.cs compilation errors on iOS/Unsupported platforms.
    internal static class FixOVRPluginCompilationErrorsIOSMetaSDK83
    {
        [InitializeOnLoadMethod]
        public static void PatchCode()
        {
            var files = Directory.GetFiles("Library/PackageCache", "OVRPlugin.cs", SearchOption.AllDirectories);
            if (files.Length == 0) return;
            
            var path = files.FirstOrDefault(x => 
                x.Replace("\\", "/").Contains("com.meta.xr.sdk.core") && 
                x.Replace("\\", "/").EndsWith("OVRPlugin.cs"));
            
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) {
                return;
            }

            var text = File.ReadAllText(path);
            var originalText = text;
            
            // Fix 1: AcquireLayerSwapchain (Incorrect return type and missing out parameter assignment)
            var brokenAcquire = "public static Result AcquireLayerSwapchain(int layerId, out int acquiredIndex)\n    {\n#if OVRPLUGIN_UNSUPPORTED_PLATFORM\n        return false;\n#else";
            var fixedAcquire = "public static Result AcquireLayerSwapchain(int layerId, out int acquiredIndex)\n    {\n#if OVRPLUGIN_UNSUPPORTED_PLATFORM\n        acquiredIndex = 0;\n        return Result.Failure_Unsupported;\n#else";

            if (text.Contains(brokenAcquire)) {
                text = text.Replace(brokenAcquire, fixedAcquire);
            } else if (text.Contains(brokenAcquire.Replace("\n", "\r\n"))) {
                text = text.Replace(brokenAcquire.Replace("\n", "\r\n"), fixedAcquire.Replace("\n", "\r\n"));
            }

            // Fix 2: GetControllerParametricProperties (Missing out parameter assignment)
            var brokenHaptics = "public static bool GetControllerParametricProperties(Controller controllerMask, out HapticsParametricProperties hapticsProperties)\n    {\n#if OVRPLUGIN_UNSUPPORTED_PLATFORM\n        return false;\n#else";
            var fixedHaptics = "public static bool GetControllerParametricProperties(Controller controllerMask, out HapticsParametricProperties hapticsProperties)\n    {\n#if OVRPLUGIN_UNSUPPORTED_PLATFORM\n        hapticsProperties = default;\n        return false;\n#else";

            if (text.Contains(brokenHaptics)) {
                text = text.Replace(brokenHaptics, fixedHaptics);
            } else if (text.Contains(brokenHaptics.Replace("\n", "\r\n"))) {
                text = text.Replace(brokenHaptics.Replace("\n", "\r\n"), fixedHaptics.Replace("\n", "\r\n"));
            }

            if (text != originalText) {
                File.WriteAllText(path, text);
                UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
                Debug.Log("Fixed OVRPlugin.cs compilation errors for iOS/Unsupported platforms");
            }
        }
    }
}
