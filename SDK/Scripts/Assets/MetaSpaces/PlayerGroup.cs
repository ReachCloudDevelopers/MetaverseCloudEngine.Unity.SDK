using System;
using System.Linq;

using MetaverseCloudEngine.Unity.Components;

using TriInspectorMVCE;

using UnityEngine;
using Object = UnityEngine.Object;

namespace MetaverseCloudEngine.Unity.Assets.MetaSpaces
{
    [Serializable]
    public class PlayerGroupAvatar
    {
        public Animator avatarPrefab;
        public AnimatorOverrideController overrideAnimator;
    }

    [Serializable]
    [DeclareFoldoutGroup("foldout", Title = "$" + nameof(displayName))]
    public partial class PlayerGroup
    {
        public enum SelectionOrder
        {
            Random,
            Sequential
        }

        [Title("ID")]
        [Group("foldout")][ReadOnly] public string identifier;
        [Group("foldout")] public string displayName = "Default";

        [Title("Player")]
        [Group("foldout")] public GameObject playerPrefab;
        [Group("foldout")] public GameObject[] playerAddons;

        [Title("Rules")]
        [Group("foldout")][Min(0)] public int minPlayers;
        [Group("foldout")][Min(-1)] public int maxPlayers = -1;

        [Title("Avatar")]
        [Group("foldout")] public bool allowUserAvatars = true;
        [Group("foldout")][HideInInspector, SerializeField, Obsolete] private Animator[] customAvatars;
        [Group("foldout")] public PlayerGroupAvatar[] playerAvatars = Array.Empty<PlayerGroupAvatar>();
        [Group("foldout")] public AnimatorOverrideController overrideAnimator;

        [Title("Spawning")]
        [Group("foldout")] public SelectionOrder spawnOrder;

        public void Initialize()
        {
            Upgrade();

            if (string.IsNullOrEmpty(identifier))
                identifier = "Fallback";
        }

        public void Validate()
        {
            Upgrade();

#if UNITY_EDITOR

            if (UnityEditor.BuildPipeline.isBuildingPlayer)
                playerAvatars = playerAvatars?.Where(x => x.avatarPrefab != null).ToArray();

            if (string.IsNullOrEmpty(identifier))
                identifier = Guid.NewGuid().ToString();
#endif
        }

        public void Upgrade()
        {
#pragma warning disable CS0612 // Type or member is obsolete
            if (customAvatars != null && customAvatars.Length > 0)
            {
                playerAvatars = customAvatars.Select(x => new PlayerGroupAvatar { avatarPrefab = x }).ToArray();
                customAvatars = Array.Empty<Animator>();
            }
#pragma warning restore CS0612 // Type or member is obsolete
        }

        partial void GetSpawnPointsInternal(ref bool querying, Action<Transform[]> points);

        public void FindSpawnPoint(int playerID, Action<Transform> success, Action failed)
        {
            var queryingInternally = false;
            GetSpawnPointsInternal(ref queryingInternally, OnQueryFinished);

            if (!queryingInternally)
                OnQueryFinished(null);
            return;

            void OnQueryFinished(Transform[] points)
            {
                if (points == null || points.Length == 0)
                {
                    points = Object.FindObjectsOfType<MetaSpaceSpawnPoint>()
                        .Where(x => x.CanUseSpawnPoint(this) && x.Blockchain.IsEmpty() && !x.SpawnPointIDLabel.HasReference && string.IsNullOrEmpty((string)x.SpawnPointIDLabel))
                        .OrderByDescending(x => x.priority)
                        .Select(x => x.transform)
                        .ToArray();
                }

                if (points.Length == 0)
                {
                    failed?.Invoke();
                    return;
                }

                var spawn = spawnOrder switch
                {
                    SelectionOrder.Random => points[UnityEngine.Random.Range(0, points.Length - 1)].transform,
                    SelectionOrder.Sequential => points[playerID % points.Length].transform,
                    _ => null
                };

                if (spawn)
                {
                    OnSpawnPointSelectedInternal(spawn);
                    success?.Invoke(spawn);
                    return;
                }

                failed?.Invoke();
                return;
            }
        }

        partial void OnSpawnPointSelectedInternal(Transform spawnPoint);

        public PlayerGroupAvatar GetAvatar(int avatarID)
        {
            Upgrade();
            return playerAvatars.Length == 0 ? null : playerAvatars[avatarID % playerAvatars.Length];
        }

        public override string ToString()
        {
            var idString = identifier;
            if (identifier.Length >= 7)
                idString = identifier[..5] + "...";

            return $"{displayName} (ID: {idString})";
        }
    }
}