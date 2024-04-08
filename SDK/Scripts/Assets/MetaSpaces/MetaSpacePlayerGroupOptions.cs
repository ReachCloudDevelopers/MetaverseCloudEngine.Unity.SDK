using MetaverseCloudEngine.Unity.Services.Options;
using System;
using System.Linq;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Assets.MetaSpaces
{
    /// <summary>
    /// The MetaSpacePlayerGroupOptions class holds options related to player groups within a Meta Space.
    /// </summary>
    [Serializable]
    public class MetaSpacePlayerGroupOptions : IPlayerGroupOptions
    {
        [Tooltip("Determines whether the player group should be automatically selected based on the options specified.")]
        [SerializeField] private bool autoSelectPlayerGroup = true;

        [Tooltip("The mode used to determine which player group the player will be assigned to.")]
        [SerializeField] private PlayerGroupSelectionMode playerGroupSelectionMode;

        [Tooltip("The available player groups in this Meta Space.")]
        [SerializeField] private PlayerGroup[] playerGroups = Array.Empty<PlayerGroup>();

        /// <summary>
        /// Determines whether the player group should be automatically selected based on the options specified.
        /// </summary>
        public bool AutoSelectPlayerGroup => autoSelectPlayerGroup;

        /// <summary>
        /// The mode used to determine which player group the player will be assigned to.
        /// </summary>
        public PlayerGroupSelectionMode PlayerGroupSelectionMode => playerGroupSelectionMode;

        /// <summary>
        /// The available player groups in this Meta Space.
        /// </summary>
        public PlayerGroup[] PlayerGroups => playerGroups;

        public void Initialize()
        {
            foreach (PlayerGroup group in playerGroups)
                group.Initialize();

            playerGroups = playerGroups
                .GroupBy(x => x.identifier)
                .Select(x => x.FirstOrDefault())
                .Where(x => x != null && !string.IsNullOrEmpty(x.identifier))
                .ToArray();
        }

        public void Validate()
        {
            if (Application.isPlaying)
                return;
            
            EnsurePlayerGroups();
            EnsureUniquePlayerGroupNames();

            foreach (PlayerGroup group in playerGroups)
                group.Validate();
        }

        private void EnsurePlayerGroups()
        {
            if (playerGroups == null || playerGroups.Length == 0)
                playerGroups = new [] { new PlayerGroup() };
        }

        private void EnsureUniquePlayerGroupNames()
        {
            if (playerGroups.Any(x => playerGroups.Any(y => x != y && x.identifier == y.identifier)))
            {
                System.Collections.Generic.IEnumerable<IGrouping<string, PlayerGroup>> grouping = playerGroups.GroupBy(x => x.identifier);
                foreach (IGrouping<string, PlayerGroup> group in grouping)
                {
                    PlayerGroup[] values = group.ToArray();
                    for (int i = 0; i < values.Length; i++)
                    {
                        if (i == 0)
                            continue;

                        int index = i;
                        string id;
                        do
                        {
                            id = Guid.NewGuid().ToString();
                            index++;
                        }
                        while (playerGroups.Any(x => x.identifier == id));
                        values[i].identifier = id;
                    }
                }
            }
        }
    }
}
