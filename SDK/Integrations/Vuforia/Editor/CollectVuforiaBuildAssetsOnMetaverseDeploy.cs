using MetaverseCloudEngine.Unity.Assets.MetaSpaces;
using MetaverseCloudEngine.Unity.Editors;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vuforia;

namespace MetaverseCloudEngine.Unity.Vuforia.Editors
{
    public class CollectVuforiaBuildAssetsOnMetaverseDeploy : IMetaversePrefabBuildProcessor, IMetaverseSceneBuildProcessor
    {
        public int callbackOrder { get; }
        
        public void OnPreProcessBuild(GameObject prefab)
        {
            VuforiaStreamingAssets.Collect(prefab);
            if (!VuforiaStreamingAssets.Instance.HasData()) 
                return;
            
            var streamingAssetsLoader = prefab.GetOrAddComponent<VuforiaStreamingAssetsLoader>();
            streamingAssetsLoader.vuforiaStreamingAssets = VuforiaStreamingAssets.Instance;

            AddVuforiaAreaTargetConfigurationHelper();
        }

        public void OnPreProcessBuild(Scene scene)
        {
            VuforiaStreamingAssets.Collect(AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path));
            if (!VuforiaStreamingAssets.Instance.HasData())
                return;
            
            var metaSpace = Object.FindObjectOfType<MetaSpace>(true);
            if (!metaSpace)
                throw new System.Exception("No MetaSpace found in scene.");

            var streamingAssetsLoader = metaSpace.gameObject.GetOrAddComponent<VuforiaStreamingAssetsLoader>();
            streamingAssetsLoader.vuforiaStreamingAssets = VuforiaStreamingAssets.Instance;

            AddVuforiaAreaTargetConfigurationHelper();
        }

        public void OnPostProcessBuild(GameObject prefab)
        {
        }

        public void OnPostProcessBuild(Scene scene)
        {
        }

        private static void AddVuforiaAreaTargetConfigurationHelper()
        {
            var existingHelpers = Resources.FindObjectsOfTypeAll<VuforiaAreaTargetConfigurationHelper>();
            foreach (var helper in existingHelpers)
            {
                if (!helper.TryGetComponent(out AreaTargetBehaviour _))
                    continue;
                Object.DestroyImmediate(helper);
            }
            
            var allBehaviours = Resources.FindObjectsOfTypeAll<AreaTargetBehaviour>();
            foreach (var behaviour in allBehaviours)
            {
                behaviour.gameObject.GetOrAddComponent<VuforiaAreaTargetConfigurationHelper>();
            }
        }
    }
}