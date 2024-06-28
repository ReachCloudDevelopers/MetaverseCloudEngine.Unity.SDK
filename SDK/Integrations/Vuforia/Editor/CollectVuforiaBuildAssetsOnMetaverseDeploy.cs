using MetaverseCloudEngine.Unity.Assets.MetaSpaces;
using MetaverseCloudEngine.Unity.Editors;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MetaverseCloudEngine.Unity.Vuforia.SDK.Integrations.Vuforia.Editor
{
    public class CollectVuforiaBuildAssetsOnMetaverseDeploy : IMetaversePrefabBuildProcessor, IMetaverseSceneBuildProcessor
    {
        public int callbackOrder { get; }
        
        public void OnPreProcessBuild(GameObject prefab)
        {
            VuforiaStreamingAssets.Collect();
        }

        public void OnPreProcessBuild(Scene scene)
        {
            VuforiaStreamingAssets.Collect();

            var metaSpace = Object.FindObjectOfType<MetaSpace>(true);
            if (!metaSpace)
                throw new System.Exception("No MetaSpace found in scene.");

            metaSpace.gameObject.AddComponent<VuforiaStreamingAssetsLoader>();
        }

        public void OnPostProcessBuild(GameObject prefab)
        {
        }

        public void OnPostProcessBuild(Scene scene)
        {
        }
    }
}