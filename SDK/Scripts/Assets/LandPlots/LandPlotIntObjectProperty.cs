using UnityEngine;

namespace MetaverseCloudEngine.Unity.Assets.LandPlots
{
    public class LandPlotIntObjectProperty : LandPlotObjectPropertyBase<int>
    {
        [Header("Range")]
        public bool hasMin;
        public int minValue = -999;
        public bool hasMax;
        public int maxValue = 999;

        protected override bool IsChanged(int oldValue, int newValue)
        {
            return oldValue != newValue;
        }

        public override bool Validate(int value, out string error)
        {
            if (hasMin && value < minValue)
            {
                error = "Value must be greater than " + minValue;
                return false;
            }

            if (hasMax && value > maxValue)
            {
                error = "Value must be less than " + maxValue;
                return false;
            }

            return base.Validate(value, out error);
        }
    }
}
