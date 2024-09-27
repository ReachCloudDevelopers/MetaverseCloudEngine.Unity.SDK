using System;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Assets.LandPlots
{
    public class LandPlotFloatObjectProperty : LandPlotObjectPropertyBase<float>
    {
        [Header("Range")]
        public bool hasMin;
        public float minValue = -999;
        public bool hasMax;
        public float maxValue = 999;

        /// <summary>
        /// Determines whether this property is a scale property. 
        /// "Scale" is a special property that is used to scale the object.
        /// </summary>
        public bool IsScale => this.DisplayName == "Scale";

        protected override bool IsChanged(float oldValue, float newValue)
        {
            return Math.Abs(oldValue - newValue) > Mathf.Epsilon;
        }

        public override bool Validate(float value, out string error)
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
