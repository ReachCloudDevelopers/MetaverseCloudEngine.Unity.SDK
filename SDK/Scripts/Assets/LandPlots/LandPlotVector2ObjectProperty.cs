using UnityEngine;

namespace MetaverseCloudEngine.Unity.Assets.LandPlots
{
    public class LandPlotVector2ObjectProperty : LandPlotObjectPropertyBase<Vector2>
    {
        protected override bool IsChanged(Vector2 oldValue, Vector2 newValue)
        {
            return oldValue != newValue;
        }
    }
}
