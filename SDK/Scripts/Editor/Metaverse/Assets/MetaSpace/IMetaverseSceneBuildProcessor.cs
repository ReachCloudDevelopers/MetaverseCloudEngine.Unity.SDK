using UnityEditor.Build;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MetaverseCloudEngine.Unity.Editors
{
    public interface IMetaverseSceneBuildProcessor : IOrderedCallback
    {
        void OnPreProcessBuild(Scene scene);
        void OnPostProcessBuild(Scene scene);
    }

    public interface IMetaversePrefabBuildProcessor : IOrderedCallback
    {
        void OnPreProcessBuild(GameObject prefab);
        void OnPostProcessBuild(GameObject prefab);
    }
}