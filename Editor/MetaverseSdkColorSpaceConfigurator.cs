using UnityEditor;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Installer.Editor
{
    public static class MetaverseSdkColorSpaceConfigurator
    {
        [InitializeOnLoadMethod]
        public static void ConfigureProject()
        {
            if (PlayerSettings.colorSpace == ColorSpace.Uninitialized)
            {
                EditorApplication.update += OnEditorUpdate;
                return;
            }

            CheckColorSpace();
        }

        private static void CheckColorSpace()
        {
            if (PlayerSettings.colorSpace != ColorSpace.Linear)
            {
                EditorUtility.DisplayDialog(
                    "Color Space",
                    $"The '{PlayerSettings.colorSpace}' color space is not supported. " +
                    "Only the 'Linear' color space is supported in the Metaverse Cloud SDK. " +
                    "Your project will now be switched to the 'Linear' Color space. This may " +
                    "take quite some time depending on the size of your project.",
                    "Ok");
                PlayerSettings.colorSpace = ColorSpace.Linear;
            }
        }

        private static void OnEditorUpdate()
        {
            if (PlayerSettings.colorSpace == ColorSpace.Uninitialized)
                return;

            CheckColorSpace();
            EditorApplication.update -= OnEditorUpdate;
        }
    }
}
