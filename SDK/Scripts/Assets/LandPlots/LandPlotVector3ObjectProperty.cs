using UnityEngine;

namespace MetaverseCloudEngine.Unity.Assets.LandPlots
{
    public class LandPlotVector3ObjectProperty : LandPlotObjectPropertyBase<Vector3>
    {
        protected override bool IsChanged(Vector3 oldValue, Vector3 newValue)
        {
            return oldValue != newValue;
        }
    }
}
