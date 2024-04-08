using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

using System;
using System.Linq;
using System.Collections.Generic;

using MetaverseCloudEngine.Unity.Assets;
using MetaverseCloudEngine.Unity.Assets.MetaSpaces;
using MetaverseCloudEngine.Unity.Attributes;
using MetaverseCloudEngine.Unity.Services.Abstract;
using MetaverseCloudEngine.Unity.Networking.Components;
using MetaverseCloudEngine.Unity.Networking.Abstract;

using JetBrains.Annotations;
using MetaverseCloudEngine.Unity.Async;
using MetaverseCloudEngine.Unity.Labels;

namespace MetaverseCloudEngine.Unity.Components
{
    public class MetaSpaceSpawnPoint : MonoBehaviour
    {
        [Serializable]
        public class BlockchainSceneDataRequirement
        {
            public List<BlockchainReferenceAsset> assets;
            public List<BlockchainReferenceCategory> categories;

            internal bool IsEmpty()
            {
                return (assets == null || assets.Count == 0) && (categories == null || categories.Count == 0);
            }
        }

        public int priority;

        [Header("Identifier (Optional)")]
        [SerializeField, HideInInspector] private string spawnPointID;
        [SerializeField] private Label pointID;

        [Header("Spawn Behavior")]
        [Tooltip("If we don't want it to be detected by the spawning system then this should be unchecked. Usually this is checked " +
                 "if you only want to use this spawner manually.")]
        [SerializeField] private bool detectedBySpawner = true;
        [SerializeField, PlayerGroupId] private string[] allowedPlayerGroups;
        [SerializeField] private NetworkTransform playerParent;

        [Header("Blockchain Scene Data Requirements")]
        [FormerlySerializedAs("cardano")]
        [SerializeField] private BlockchainSceneDataRequirement blockchain;

        [Header("Events")]
        public UnityEvent<GameObject> onPlayerSpawned;

        public string SpawnPointID {
            set => pointID.SetValue(value);
        }

        public Label SpawnPointIDLabel {
            get => pointID;
            set => pointID = value;
        }

        public NetworkTransform PlayerParent
        {
            get => playerParent;
            set => playerParent = value;
        }
        public BlockchainSceneDataRequirement Blockchain => blockchain ?? new BlockchainSceneDataRequirement();

        private void Awake()
        {
            Upgrade();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            Upgrade();

            if (!UnityEditor.BuildPipeline.isBuildingPlayer)
                return;

            allowedPlayerGroups = allowedPlayerGroups.Where(x => !string.IsNullOrEmpty(x)).ToArray();
        }
#endif

        private void Upgrade()
        {
            if (!string.IsNullOrEmpty(spawnPointID))
            {
                pointID = spawnPointID;
                spawnPointID = null;
            }
        }

        [UsedImplicitly]
        private void OnPlayerSpawned(GameObject player)
        {
            if (playerParent && playerParent.NetworkObject) player.transform.SetParent(playerParent.transform);
            onPlayerSpawned?.Invoke(player);
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem(MetaverseConstants.MenuItems.GameObjectMenuRootPath + "Spawn Point")]
        private static void CreateSpawnPoint()
        {
            var parent = UnityEditor.Selection.activeTransform;
            var go = new GameObject(nameof(MetaSpaceSpawnPoint));
            go.AddComponent<MetaSpaceSpawnPoint>();
            UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create Spawn Point");
            go.transform.SetParent(parent);
            UnityEditor.Selection.activeTransform = go.transform;
        }
#endif

        public void SpawnUsingThisPoint()
        {
            MetaSpace.OnReady(space =>
            {
                MetaverseDispatcher.WaitForSeconds(0.1f, () =>
                {
                    var spawnService = space.GetService<IPlayerSpawnService>();
                    var networkService = space.GetService<IMetaSpaceNetworkingService>();
                    spawnService.TrySpawnPlayer(networkService.LocalPlayerID, transform);
                });
            });
        }

        public bool CanUseSpawnPoint(PlayerGroup playerGroup = null)
        {
            return detectedBySpawner && IsPlayerGroupAllowed(playerGroup);
        }

        public bool IsPlayerGroupAllowed(PlayerGroup playerGroup)
        {
            if (playerGroup == null)
                return allowedPlayerGroups.Length == 0;
            return allowedPlayerGroups.Length == 0 ||
                   allowedPlayerGroups.Any(x => x == playerGroup?.identifier);
        }
    }
}