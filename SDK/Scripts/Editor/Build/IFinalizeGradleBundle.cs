using UnityEditor.Build;

namespace MetaverseCloudEngine.Unity.Editors.Builds
{
    public interface IFinalizeGradleBundle : IOrderedCallback
    {
        void FinalizeGradleBundle(string path);
    }
}