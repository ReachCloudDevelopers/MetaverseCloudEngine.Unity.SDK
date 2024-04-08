using UnityEngine;

namespace MetaverseCloudEngine.Unity.Assets.LandPlots
{
    [System.Serializable]
    public class LandPlotLoadRequirements
    {
        public bool loadOnStart = true;
        public ObjectLoadRange loadRange = new();
        
        public void DrawGizmos(Transform transform) => loadRange.DrawGizmos(transform);
        public void Validate() => loadRange.Validate();
    }
}