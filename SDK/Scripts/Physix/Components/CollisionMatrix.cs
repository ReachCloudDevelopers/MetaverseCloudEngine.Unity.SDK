using System.Collections.Generic;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Physix.Components
{
    [HideMonoScript]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Physics/Collision Matrix")]
    public class CollisionMatrix : TriInspectorMonoBehaviour
    {
        [InfoBox("This component is NOT a replacement for Unity's built-in collision matrix.")]
        public string physicsLayer;
        public List<string> collisionIgnoreLayers;
        public bool invertMask;

        private static readonly Dictionary<string, List<CollisionMatrix>> PhysicsObjectPool = new();

        private void OnEnable()
        {
            Register(this);
        }

        private void OnDisable()
        {
            UnRegister(this);
        }

        private static void Register(CollisionMatrix obj)
        {
            if (!PhysicsObjectPool.TryGetValue(obj.physicsLayer, out List<CollisionMatrix> pool))
                PhysicsObjectPool[obj.physicsLayer] = pool = new List<CollisionMatrix>();
            pool.Add(obj);

            Collider[] objColliders = obj.gameObject.GetTopLevelComponentsInChildrenOrdered<Collider, CollisionMatrix>();
            if (objColliders.Length == 0)
                return;

            if (!obj.invertMask)
            {
                foreach (string ignore in obj.collisionIgnoreLayers)
                {
                    if (!PhysicsObjectPool.TryGetValue(ignore, out List<CollisionMatrix> ignorePool))
                        continue;

                    foreach (CollisionMatrix objToIgnore in ignorePool)
                    {
                        Collider[] collidersToIgnore = objToIgnore.gameObject.GetTopLevelComponentsInChildrenOrdered<Collider, CollisionMatrix>();
                        foreach (Collider col1 in collidersToIgnore)
                            foreach (Collider col2 in objColliders)
                                Physics.IgnoreCollision(col1, col2);
                    }
                }
            }
            else
            {
                foreach (string layer in PhysicsObjectPool.Keys)
                {
                    if (obj.collisionIgnoreLayers.Contains(layer))
                        continue;

                    List<CollisionMatrix> ignorePool = PhysicsObjectPool[layer];
                    foreach (CollisionMatrix objToIgnore in ignorePool)
                    {
                        Collider[] collidersToIgnore = objToIgnore.gameObject.GetTopLevelComponentsInChildrenOrdered<Collider, CollisionMatrix>();
                        foreach (Collider col1 in collidersToIgnore)
                            foreach (Collider col2 in objColliders)
                                Physics.IgnoreCollision(col1, col2);
                    }
                }
            }
        }

        private static void UnRegister(CollisionMatrix obj)
        {
            if (!PhysicsObjectPool.TryGetValue(obj.physicsLayer, out List<CollisionMatrix> pool))
                return;

            pool.Remove(obj);
            if (pool.Count == 0)
                PhysicsObjectPool.Remove(obj.physicsLayer);
        }
    }
}