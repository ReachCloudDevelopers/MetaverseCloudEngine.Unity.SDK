using MetaverseCloudEngine.Common.Enumerations;
using TriInspectorMVCE;

namespace MetaverseCloudEngine.Unity.Assets.MetaPrefabs
{
    [System.Serializable]
    public class MetaPrefabMetadata : AssetMetadata
    {
        [Title("LOD")]
        [DisableIf(nameof(isAvatar))]
        public ObjectLoadRange loadRange;

        [Title("Avatar")]
        [DisableIf(nameof(isBuildable))]
        public bool isAvatar;

        [Title("Builder")]
        [DisableIf(nameof(isAvatar))]
        public bool isBuildable;
        [EnableIf(nameof(isBuildable))]
        public PrefabBuilderCategory builderCategories = 0;
    }
}