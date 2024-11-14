#if !METAVERSE_CLOUD_ENGINE_INTERNAL
using JetBrains.Annotations;
using MetaverseCloudEngine.Unity.Installer;
using UnityEditor;

namespace MetaverseCloudEngine.Unity.Editors
{
    public static class MetaverseCheckForUpdatesEditorButton
    {
        [UsedImplicitly]
        [MenuItem(MetaverseConstants.ProductName + "Check for SDK Updates", false, 100)]
        public static void CheckForUpdates()
        {
            MetaverseRequiredPackageInstaller.ForceInstallPackages();
        }
    }
}
#endif