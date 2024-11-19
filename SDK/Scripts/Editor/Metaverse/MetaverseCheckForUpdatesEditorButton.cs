#if METAVERSE_CLOUD_ENGINE && !CLOUD_BUILD_PLATFORM
using JetBrains.Annotations;
using MetaverseCloudEngine.Unity.Installer;
using UnityEditor;

namespace MetaverseCloudEngine.Unity.Editors
{
    public static class MetaverseCheckForUpdatesEditorButton
    {
        [UsedImplicitly]
        [MenuItem(MetaverseConstants.ProductName + "/Check for SDK Updates", false, 100)]
        public static void CheckForUpdates()
        {
            MetaverseRequiredPackageInstaller.ForceInstallPackages();
        }
    }
}
#endif
