using System.Linq;
using UnityEngine.SceneManagement;

namespace MetaverseCloudEngine.Unity.Assets.MetaSpaces
{
#if UNITY_EDITOR
    public class MetaSpaceResourceHelper : UnityEditor.Build.IProcessSceneWithReport
    {
        public int callbackOrder => -int.MaxValue;

        public void OnProcessScene(
            Scene scene,
            UnityEditor.Build.Reporting.BuildReport report)
        {
            System.Collections.Generic.IEnumerable<MetaSpace> metaSpaces = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<MetaSpace>());
            foreach (MetaSpace space in metaSpaces)
                space.NetworkOptions.ScanForSpawnables(scene);
        }
    }
#endif
}
