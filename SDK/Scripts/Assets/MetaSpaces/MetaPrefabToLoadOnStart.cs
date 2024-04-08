using System;
using MetaverseCloudEngine.Unity.Assets.Attributes;
using TriInspectorMVCE;

namespace MetaverseCloudEngine.Unity.Assets.MetaSpaces
{
    [Serializable]
    public class MetaPrefabToLoadOnStart
    {
        public enum SpawnMode
        {
            MasterClient,
            Local,
            PreloadOnly,
        }

        public bool disabled;
        [MetaPrefabIdProperty] public string prefab;
        [LabelText("Mode")] public SpawnMode spawnAuthority;
    }
}