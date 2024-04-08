using System;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Services.Abstract
{
    /// <summary>
    /// This service is responsible for spawning and de-spawning players in the scene.
    /// </summary>
    public interface IPlayerSpawnService : IMetaSpaceService
    {
        /// <summary>
        /// The currently spawned player object on the local client.
        /// </summary>
        GameObject SpawnedPlayerObject { get; }
        /// <summary>
        /// The spawn point for the local player, this may be null if no spawn point is set.
        /// </summary>
        Transform LocalPlayerSpawnPoint { get; }

        /// <summary>
        /// This event is fired when a player is spawned on the local client.
        /// </summary>
        event Action<GameObject> LocalPlayerSpawned;
        /// <summary>
        /// This event is fired when a player is de-spawned on the local client.
        /// </summary>
        event Action LocalPlayerDeSpawned;

        /// <summary>
        /// Attempts to spawn a player on the local client.
        /// </summary>
        /// <param name="playerID">The ID of the player to spawn.</param>
        /// <param name="specificPoint">The specific spawn point to spawn the player at, if null a spawn point will be searched for.</param>
        void TrySpawnPlayer(int playerID, Transform specificPoint = null);
        /// <summary>
        /// Attempts to de-spawn a player on the local client.
        /// </summary>
        /// <param name="playerID">The ID of the player to de-spawn.</param>
        /// <returns>True if the player was de-spawned, false if the player was not de-spawned.</returns>
        bool TryDeSpawnPlayer(int playerID);
    }
}