using System;
using MetaverseCloudEngine.Unity.Networking.Enumerations;
using MetaverseCloudEngine.Unity.Services.Abstract;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Networking.Abstract
{
    /// <summary>
    /// A delegate that represents a network event callback.
    /// </summary>
    /// <param name="eventID">The ID of the event that was called.</param>
    /// <param name="sendingPlayerID">The ID of the player who sent the event.</param>
    /// <param name="content">The content of the current event.</param>
    public delegate void MetaSpaceNetworkEventDelegate(short eventID, int sendingPlayerID, object content);

    /// <summary>
    /// A delegate that represents something triggered by a player.
    /// </summary>
    /// <param name="playerID">The ID of the player.</param>
    public delegate void MetaSpaceNetworkPlayerDelegate(int playerID);

    /// <summary>
    /// An interface that describes a contract for a networking service for MetaSpaces.
    /// </summary>
    public interface IMetaSpaceNetworkingService : IMetaSpaceService
    {
        /// <summary>
        /// Should be invoked when the networking system is ready (this is arbitrary, but basically means that you should only do networking stuff after <see cref="IsReady"/>).
        /// </summary>
        event Action Ready;

        /// <summary>
        /// Should be invoked when the networking system is no longer ready.
        /// </summary>
        event Action UnReady;

        /// <summary>
        /// Should be invoked if the host has migrated.
        /// </summary>
        event Action HostChanged;

        /// <summary>
        /// Should be invoked when a player joins the meta space.
        /// </summary>
        event MetaSpaceNetworkPlayerDelegate PlayerJoined;

        /// <summary>
        /// Should be invoked when a player leaves the meta space.
        /// </summary>
        event MetaSpaceNetworkPlayerDelegate PlayerLeft;

        /// <summary>
        /// Gets or sets a value indicating whether the meta space can be joined. This value can only be set by the host.
        /// </summary>
        bool CanJoin { get; set; }
        
        /// <summary>
        /// Gets a value indicating whether the current meta space instance is private (single-player).
        /// </summary>
        bool Private { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the current client is operating in offline mode.
        /// </summary>
        bool IsOfflineMode { get; }

        /// <summary>
        /// Gets a value indicating whether the networking system is ready.
        /// </summary>
        bool IsReady { get; }

        /// <summary>
        /// Gets the host's player ID.
        /// </summary>
        int HostID { get; }

        /// <summary>
        /// Gets a value indicating whether this is the host client.
        /// </summary>
        bool IsHost { get; }

        /// <summary>
        /// Gets the local player identifier.
        /// </summary>
        int LocalPlayerID { get; }

        /// <summary>
        /// Gets the player count within this space.
        /// </summary>
        int PlayerCount { get; }

        /// <summary>
        /// Gets the instance ID of the space.
        /// </summary>
        string InstanceID { get; }

        /// <summary>
        /// Invokes a global network event.
        /// </summary>
        /// <param name="eventID">The event's unique ID.</param>
        /// <param name="receivers">The receivers of this event.</param>
        /// <param name="buffered">Whether to buffer this message for late joiners.</param>
        /// <param name="content">The network content of the event (must be serializable).</param>
        void InvokeEvent(short eventID, NetworkMessageReceivers receivers, bool buffered = false, object content = null);

        /// <summary>
        /// Invokes a global network event.
        /// </summary>
        /// <param name="eventID">The event's unique ID.</param>
        /// <param name="playerID">The receivers of this event.</param>
        /// <param name="content">The network content of the event (must be serializable).</param>
        void InvokeEvent(short eventID, int playerID, object content = null);
        
        /// <summary>
        /// Removes an event from the buffer.
        /// </summary>
        /// <param name="eventID">The ID of the event to remove.</param>
        /// <param name="content">The content of the event to remove.</param>
        void RemoveEventFromBuffer(short eventID, object content = null);

        /// <summary>
        /// Adds an event listener for the event with the given ID.
        /// </summary>
        /// <param name="eventID">The ID of the event.</param>
        /// <param name="handler">The callback function.</param>
        void AddEventHandler(short eventID, MetaSpaceNetworkEventDelegate handler);

        /// <summary>
        /// Removes an event listener from the event with the given ID.
        /// </summary>
        /// <param name="eventID">The ID of the event.</param>
        /// <param name="handler">The callback function.</param>
        void RemoveEventHandler(short eventID, MetaSpaceNetworkEventDelegate handler);

        /// <summary>
        /// Spawns a particular prefab over the network.
        /// </summary>
        /// <param name="prefab">The prefab to spawn on the network.</param>
        /// <param name="position">The position of the prefab that is being requested to spawn.</param>
        /// <param name="rotation">The rotation of the prefab to spawn.</param>
        /// <param name="serverOwned">Whether the prefab should be owned by the server/host client.</param>
        /// <param name="channel">The network channel that should be used for spawning this object.</param>
        /// <param name="instantiationData">Instantiation data that will be available on this object upon spawn.</param>
        /// <returns>The object that was spawned on the network.</returns>
        [Obsolete(
        "Please use the version of this method that takes an Action<NetworkSpawnedObject> instead. This method " +
              "may not work in all cases and may return null.")]
        GameObject SpawnGameObject(GameObject prefab, Vector3 position, Quaternion rotation, bool serverOwned, byte channel = 0, params object[] instantiationData);
        
        /// <summary>
        /// Spawns a particular prefab over the network.
        /// </summary>
        /// <param name="prefab">The prefab to spawn on the network.</param>
        /// <param name="onSpawned">Invoked when the object has been spawned.</param>
        /// <param name="position">The position of the prefab that is being requested to spawn.</param>
        /// <param name="rotation">The rotation of the prefab to spawn.</param>
        /// <param name="serverOwned">Whether the prefab should be owned by the server/host client.</param>
        /// <param name="channel">The network channel that should be used for spawning this object.</param>
        /// <param name="instantiationData">Instantiation data that will be available on this object upon spawn.</param>
        void SpawnGameObject(GameObject prefab, Action<NetworkSpawnedObject> onSpawned, Vector3 position, Quaternion rotation, bool serverOwned, byte channel = 0, params object[] instantiationData);

        /// <summary>
        /// Fetches the name of the player with the given <paramref name="playerID"/>.
        /// </summary>
        /// <param name="playerID">The ID of the player who's name you want to fetch.</param>
        /// <param name="onName">Invoked when the name was received.</param>
        /// <param name="onFailed">Invoked if fetching the name has failed.</param>
        void GetPlayerName(int playerID, Action<string> onName, Action onFailed = null);

        /// <summary>
        /// Gets an array of all the player IDs in the current network scene.
        /// </summary>
        /// <returns>The player IDs.</returns>
        int[] GetPlayerIDs();

        /// <summary>
        /// Gets a value indicating whether this player is the host player or not.
        /// </summary>
        /// <param name="playerID">The ID of the player.</param>
        bool IsHostPlayer(int playerID);

        /// <summary>
        /// Performs a round trip to the server.
        /// </summary>
        /// <param name="then">The action to perform after the round trip has been completed.</param>
        void RoundTrip(Action then);
    }
}