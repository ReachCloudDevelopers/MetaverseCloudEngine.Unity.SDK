using UnityEngine;

namespace MetaverseCloudEngine.Unity.Assets.MetaPrefabs
{
    [System.Serializable]
    public class MetaPrefabLoadRequirements
    {
        [Tooltip("If this value is 0, the prefab will always load.")]
        public float loadDistance;
        [Tooltip("If this value is 0, the prefab will never unload.")]
        public float unloadDistance;

        public void DrawGizmos(Transform transform)
        {
            Vector3 pos = transform.position;
            Color oldColor = Gizmos.color;
            
            if (loadDistance > 0)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(pos, loadDistance);
            }

            if (unloadDistance > 0)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(pos, unloadDistance);
            }
            
            Gizmos.color = oldColor;
        }
    }
}