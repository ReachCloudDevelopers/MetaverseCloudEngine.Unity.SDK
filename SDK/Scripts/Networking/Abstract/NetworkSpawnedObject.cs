using UnityEngine;

namespace MetaverseCloudEngine.Unity.Networking.Abstract
{
    public class NetworkSpawnedObject
    {
        public NetworkSpawnedObject(GameObject obj)
        {
            GameObject = obj;
            Transform = obj.transform;
        }
        
        public GameObject GameObject { get; }
        public Transform Transform { get; }
        public bool IsStale { get; set; }
    }
}