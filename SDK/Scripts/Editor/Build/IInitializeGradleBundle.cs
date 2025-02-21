using UnityEditor.Build;

namespace MetaverseCloudEngine.Unity.Editors.Builds
{
    public interface IInitializeGradleBundle : IOrderedCallback
    {
        void InitializeGradleBundle(string path);
    }
}