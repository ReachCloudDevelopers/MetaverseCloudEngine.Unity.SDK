using UnityEngine;

namespace MetaverseCloudEngine.Unity.Services.Options
{
    public interface IPlayerSpawnOptions
    {
        GameObject DefaultPlayerPrefab { get; }
        GameObject[] Addons { get; }
        bool AutoSpawnPlayer { get; }
    }
}