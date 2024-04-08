#if PLAYMAKER && METAVERSE_CLOUD_ENGINE

using HutongGames.PlayMaker;
using MetaverseCloudEngine.Unity.Assets.MetaPrefabs;
using System;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Integrations.PlayMaker.Actions
{
    [ActionCategory(MetaverseConstants.ProductName)]
    public class SpawnMetaPrefab : FsmStateAction
    {
        public FsmString prefabID;
        public FsmGameObject spawnPoint;
        public FsmVector3 position;
        public FsmVector3 rotation;
        public FsmGameObject parent;
        [UIHint(UIHint.Variable)]
        public FsmGameObject storeObject;
        public FsmEvent onSpawned;
        public FsmEvent onSpawnFailed;

        public override void OnEnter()
        {
            if (!Guid.TryParse(prefabID.Value, out var guid))
            {
                Fsm.Event(onSpawnFailed);
                Finish();
                return;
            }

            var spawner = MetaPrefabSpawner.CreateSpawner(
                guid, 
                position.Value, 
                Quaternion.Euler(rotation.Value), 
                parent.Value ? parent.Value.transform : null,
                parent.Value ? parent.Value.transform : null);

            var spawnerTransform = spawner.transform;
            if (spawnPoint.Value)
            {
                spawnerTransform.SetPositionAndRotation(
                    spawnPoint.Value.transform.TransformPoint(position.Value), 
                    spawnPoint.Value.transform.rotation * Quaternion.Euler(rotation.Value));
            }
            else
            {
                spawnerTransform.localPosition = position.Value;
                spawnerTransform.localEulerAngles = rotation.Value;
            }

            spawner.events.onSpawned.AddListener(go =>
            {
                storeObject.Value = go;
                Fsm.Event(onSpawned);
                Finish();
                UnityEngine.Object.Destroy(spawner.gameObject);
            });

            spawner.events.onFailed.AddListener(e =>
            {
                Fsm.Event(onSpawnFailed);
                Finish();
                UnityEngine.Object.Destroy(spawner.gameObject);
            });
        }
    }
}
#endif