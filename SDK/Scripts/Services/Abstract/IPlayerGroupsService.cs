using System.Collections.Generic;
using MetaverseCloudEngine.Unity.Assets.MetaSpaces;

namespace MetaverseCloudEngine.Unity.Services.Abstract
{
    /// <summary>
    /// An event that is fired when a player joins or leaves a player group.
    /// </summary>
    public delegate void PlayerGroupPlayerEvent(PlayerGroup playerGroup, int playerID);

    /// <summary>
    /// A service that is responsible for managing player groups.
    /// </summary>
    public interface IPlayerGroupsService : IMetaSpaceService
    {
        /// <summary>
        /// This event is fired when a player joins a player group.
        /// </summary>
        event PlayerGroupPlayerEvent PlayerJoinedPlayerGroup;
        /// <summary>
        /// This event is fired when a player leaves a player group.
        /// </summary>
        event PlayerGroupPlayerEvent PlayerLeftPlayerGroup;

        /// <summary>
        /// The current player group that the local player is in.
        /// </summary>
        PlayerGroup CurrentPlayerGroup { get; }
        /// <summary>
        /// True if the minimum player group requirements are met (i.e. groups are filled at or above minimum).
        /// </summary>
        bool MeetsMinimumRequirements { get; }

        /// <summary>
        /// Gets the player group with the specified ID.
        /// </summary>
        /// <param name="id">The ID of the player group to get.</param>
        /// <returns>The player group with the specified ID.</returns>
        PlayerGroup GetPlayerGroup(string id);
        /// <summary>
        /// Gets all players and the player group they are in.
        /// </summary>
        /// <returns>A dictionary of player IDs and the player group they are in.</returns>
        IDictionary<int, PlayerGroup> GetPlayerGroups();
        /// <summary>
        /// Gets the number of players in the player group with the specified ID.
        /// </summary>
        /// <param name="id">The ID of the player group to get the player count of.</param>
        /// <returns>The number of players in the player group with the specified ID.</returns>
        int GetPlayerGroupPlayerCount(string id);
        /// <summary>
        /// Gets the total number of players in the specified player groups.
        /// </summary>
        /// <param name="groupIds">The IDs of the player groups to get the total player count of.</param>
        /// <returns>The total number of players in the specified player groups.</returns>
        int GetTotalNumPlayersInGroups(params string[] groupIds);
        /// <summary>
        /// Determines if the player with the specified ID is in the player group with the specified ID.
        /// </summary>
        /// <param name="playerID">The ID of the player to check.</param>
        /// <param name="id">The ID of the player group to check.</param>
        /// <returns>True if the player with the specified ID is in the player group with the specified ID, false otherwise.</returns>
        bool IsInPlayerGroup(int playerID, string id);
        bool IsPlayerGroupFull(string id);
        bool PlayerGroupExists(string id);
        bool TryGetPlayerPlayerGroup(int playerID, out PlayerGroup playerGroup);
        bool TryJoinNextAvailablePlayerGroup();
        bool TryJoinPlayerGroup(string id);
        bool TrySetPlayerPlayerGroup(int playerID, string id);
    }
}