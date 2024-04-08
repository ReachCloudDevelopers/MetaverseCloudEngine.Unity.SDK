using MetaverseCloudEngine.Unity.Services.Options;
using System;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Assets.MetaSpaces
{
    [Serializable]
    public class MetaSpacePlayerSpawnOptions : IPlayerSpawnOptions
    {
        [Tooltip("If true, players will automatically be spawned upon connection to the Meta Space.")]
        [SerializeField] private bool autoSpawnPlayer = true;

        [Tooltip("The default prefab that will be used to spawn players.")]
        [SerializeField] private GameObject defaultPlayerPrefab;

        [Tooltip("The override animator controller to use for the player. If none specified, will use the default animator controller.")]
        [SerializeField] private AnimatorOverrideController overrideAnimator;

        [Tooltip("Additional prefabs that will be added to the player object upon spawn.")]
        [SerializeField] private GameObject[] playerAddons;

        /// <summary>
        /// Gets a value indicating whether players should be automatically spawned upon connection to the Meta Space.
        /// </summary>
        public bool AutoSpawnPlayer => autoSpawnPlayer;

        /// <summary>
        /// Gets the default prefab that will be used to spawn players.
        /// </summary>
        public GameObject DefaultPlayerPrefab => defaultPlayerPrefab;

        /// <summary>
        /// Gets the additional prefabs that will be added to the player object upon spawn.
        /// </summary>
        public GameObject[] Addons => playerAddons;

        /// <summary>
        /// Overrides the runtime animator controller for all player avatars.
        /// </summary>
        public AnimatorOverrideController OverrideAnimator => overrideAnimator;
    }
}
