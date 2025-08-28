using System;
using System.Linq;
using MetaverseCloudEngine.Unity.Assets.MetaSpaces;
using MetaverseCloudEngine.Unity.Attributes;
using MetaverseCloudEngine.Unity.Avatar.Components;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Components
{
    [AddComponentMenu(MetaverseConstants.ProductName + "/Gameplay/Player Groups/Player Group Avatar Override")]
    [HideMonoScript]
    public class PlayerGroupAvatarOverride : MetaSpaceBehaviour
    {
        [Tooltip("The player group ID to override avatars for.")]
        [PlayerGroupId] public string playerGroupId = string.Empty;
        [Tooltip("The avatars to override the existing ones. Avatars will be overridden by index, so if you want to keep an existing avatar, set that index to null.")]
        public PlayerGroupAvatar[] playerGroupAvatars = Array.Empty<PlayerGroupAvatar>();
        [Tooltip("If true, the avatars will be overridden in Awake. If false, you must call TryOverrideAvatars() manually.")]
        public bool overrideOnAwake = true;

        protected override void Awake()
        {
            base.Awake();
            
            if (overrideOnAwake)
                TryOverrideAvatars();
        }
        
        /// <summary>
        /// Tries to override the avatars for the specified player group ID with the provided avatars.
        /// If the player group ID is not set or no avatars are provided, the method does nothing.
        /// If the player group is found, it merges the existing avatars with the provided ones,
        /// overriding by index where a non-null avatar is provided.
        /// </summary>
        public void TryOverrideAvatars()
        {
            if (string.IsNullOrWhiteSpace(playerGroupId)) return;
            if (playerGroupAvatars.Length == 0) return;
            
            if (!MetaSpace.PlayerGroupOptions.PlayerGroups.TryGetFirstOrDefault(
                    x => x.identifier == playerGroupId, out var playerGroup)) return;
            
            var didOverride = false;
            var mergedAvatars = new PlayerGroupAvatar[Mathf.Max(playerGroup.playerAvatars.Length, playerGroupAvatars.Length)];
            for (int i = 0; i < mergedAvatars.Length; i++)
            {
                var overrideAvatar = i < playerGroupAvatars.Length ? playerGroupAvatars[i] : null;
                var originalAvatar = i < playerGroup.playerAvatars.Length ? playerGroup.playerAvatars[i] : null;
                if (overrideAvatar != null && overrideAvatar.avatarPrefab)
                {
                    mergedAvatars[i] = overrideAvatar;
                    if (originalAvatar == null || !Equals(overrideAvatar.avatarPrefab, originalAvatar.avatarPrefab))
                        didOverride = true;
                }
                else
                {
                    mergedAvatars[i] = originalAvatar;
                }
            
                var newOverrideAnimator = overrideAvatar != null && overrideAvatar.overrideAnimator
                    ? overrideAvatar.overrideAnimator
                    : originalAvatar?.overrideAnimator;
            
                if (mergedAvatars[i] != null && !Equals(mergedAvatars[i].overrideAnimator, newOverrideAnimator))
                    didOverride = true;
            
                if (mergedAvatars[i] != null)
                    mergedAvatars[i].overrideAnimator = newOverrideAnimator;
            }

            if (didOverride)
            {
                mergedAvatars = mergedAvatars.Where(x => x != null).ToArray();
                playerGroup.playerAvatars = mergedAvatars;
                
                var playerAvatarContainers = FindObjectsOfType<PlayerAvatarContainer>();
                foreach (var container in playerAvatarContainers)
                {
                    if (container.Avatar && string.IsNullOrEmpty(container.AvatarUrl))
                        container.LoadAvatar();
                }
            }
        }
    }
}