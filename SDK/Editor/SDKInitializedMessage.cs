namespace MetaverseCloudEngine.Unity.Editors
{
    public static class SDKInitializedMessage
    {
        [UnityEditor.InitializeOnLoadMethod]
        private static void OnSDKInitialized()
        {
            MetaverseProgram.Logger.Log(MetaverseConstants.ProductName + " SDK Initialized");
        }
    }
}