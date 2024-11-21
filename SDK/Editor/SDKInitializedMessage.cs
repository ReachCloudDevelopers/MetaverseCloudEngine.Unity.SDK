#if METAVERSE_CLOUD_ENGINE
namespace MetaverseCloudEngine.Unity.Editors
{
    public static class SDKInitializedMessage
    {
        [UnityEditor.InitializeOnLoadMethod]
        private static void OnSDKInitialized()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                MetaverseProgram.Logger.Log(MetaverseConstants.ProductName + " SDK Initialized");
            };
        }
    }
}
#endif
