#if PLAYMAKER && METAVERSE_CLOUD_ENGINE

using HutongGames.PlayMaker;
using MetaverseCloudEngine.Unity.Components;

using UnityEngine;

namespace MetaverseCloudEngine.Unity.Integrations.PlayMaker.Actions
{
    [ActionCategory(MetaverseConstants.ProductName)]
    public class SpawnPrefab : FsmStateAction
    {
        private const string SpawnerName = "Spawner";

        [RequiredField] 
        [CheckForComponent(typeof(SpawnablePrefab))]
        public FsmGameObject prefab;
        public FsmGameObject spawnPoint;
        public FsmVector3 position;
        public FsmVector3 rotation;
        public FsmGameObject parent;
        [UIHint(UIHint.Variable)]
        public FsmGameObject storeObject;
        [UIHint(UIHint.Variable)]
        public FsmBool storeSpawned;
        public FsmEvent onSpawned;
        public FsmEvent onFailed;

        private Component _spawner;

        public override void OnEnter()
        {
            if (!prefab.Value)
            {
                Fsm.Event(onFailed);
                Finish();
                return;
            }

            if (!prefab.Value.TryGetComponent<SpawnablePrefab>(out _))
            {
                ConfigureLegacySpawner();
                return;
            }

            ConfigureNewSpawner();
        }

        private void ConfigureNewSpawner()
        {
            var spawner = new GameObject(SpawnerName)
                {
                    hideFlags = HideFlags.HideInHierarchy
                }
                .AddComponent<UnityPrefabSpawner>();
            
            _spawner = spawner;

            spawner.SpawnOnStart = true;
            spawner.Prefab = prefab.Value;
            
            if (!spawner.Prefab)
            {
                Fsm.Event(onFailed);
                Finish();
                return;
            }

            spawner.Parent = parent.Value ? parent.Value.transform : null;

            var spawnerTransform = spawner.transform;
            if (spawner.Parent)
            {
                spawnerTransform.SetParent(spawner.Parent);
                spawnerTransform.ResetLocalTransform();
            }

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

            if (spawner.LastSpawnedObject)
            {
                storeObject.Value = spawner.LastSpawnedObject;
                Fsm.Event(onSpawned);
                Finish();
                return;
            }

            spawner.onSpawned ??= new UnityEngine.Events.UnityEvent<GameObject>();
            spawner.onSpawned.AddListener(go =>
            {
                if (!go)
                {
                    storeSpawned.Value = false;
                    Fsm.Event(onFailed);
                    Finish();
                    return;
                }
                
                storeObject.Value = go;
                storeSpawned.Value = true;
                Fsm.Event(onSpawned);
                Finish();
            });
            
            spawner.onFailed.AddListener(() =>
            {
                storeSpawned.Value = false;
                Fsm.Event(onFailed);
                Finish();
            });
        }

        private void ConfigureLegacySpawner()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            var spawner = new GameObject(SpawnerName) { hideFlags = HideFlags.HideInHierarchy }.AddComponent<PrefabSpawner>();
#pragma warning restore CS0618 // Type or member is obsolete
            _spawner = spawner;

            spawner.Prefab = prefab.Value;
            if (!spawner.Prefab)
            {
                Finish();
                return;
            }

            spawner.Parent = parent.Value ? parent.Value.transform : null;

            var spawnerTransform = spawner.transform;
            if (spawner.Parent)
            {
                spawnerTransform.SetParent(spawner.Parent);
                spawnerTransform.ResetLocalTransform();
            }

            if (spawnPoint.Value)
                spawnerTransform.SetPositionAndRotation(spawnPoint.Value.transform.TransformPoint(position.Value), spawnPoint.Value.transform.rotation * Quaternion.Euler(rotation.Value));
            else
            {
                spawnerTransform.localPosition = position.Value;
                spawnerTransform.localEulerAngles = rotation.Value;
            }

            spawner.onSpawned.AddListener(go =>
            {
                storeObject.Value = go;
                Fsm.Event(onSpawned);
                Finish();
            });
        }

        public override void OnExit()
        {
            base.OnExit();

            if (_spawner)
            {
                Object.Destroy(_spawner.gameObject);
                _spawner = null;
            }
        }
    }
}
#endif