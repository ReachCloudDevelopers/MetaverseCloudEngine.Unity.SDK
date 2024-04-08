using System;
using MetaverseCloudEngine.Common.Enumerations;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Assets.MetaPrefabs
{
    [Serializable]
    public class MetaPrefabPickerQueryParams : AssetPickerQueryParams
    {
        [Header("Prefabs")]
        public bool canChangeCategory = true;
        public PrefabBuilderCategory builderCategory = (PrefabBuilderCategory)(~0);
        [Space]
        public bool queryIsBuildable;
        public bool isBuildable;
        [Space]
        public bool queryIsAvatar;
        public bool isAvatar;
    }
}