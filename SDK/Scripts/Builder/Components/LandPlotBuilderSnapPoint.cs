using MetaverseCloudEngine.Common.Enumerations;
using MetaverseCloudEngine.Unity.Assets.Attributes;
using System.Collections.Generic;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Builder.Components
{
    public enum LandPlotBuilderSnapPointType
    {
        Left,
        Right,
        Top,
        Bottom,
        Front,
        Back,
    }

    [DisallowMultipleComponent]
    [HideMonoScript]
    [Experimental]
    public class LandPlotBuilderSnapPoint : TriInspectorMonoBehaviour
    {
        public LandPlotBuilderSnapPointType type = LandPlotBuilderSnapPointType.Left;
        [Range(-180, 0)] public float minRotation = -90;
        [Range(0, 180)] public float maxRotation = 90;

        [Header("Filtering")]
        public string requireString;
        public PrefabBuilderCategory requireCategories = 0;
        [MetaPrefabIdProperty] public List<string> requirePrefabs = new();
    }
}
