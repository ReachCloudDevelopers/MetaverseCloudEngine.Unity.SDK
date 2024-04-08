using System;

namespace MetaverseCloudEngine.Unity.Assets.LandPlots
{
    public class LandPlotOptionsObjectProperty : LandPlotObjectPropertyBase<int>
    {
        public LandPlotOptionsObjectPropertyOption[] options = Array.Empty<LandPlotOptionsObjectPropertyOption>();

        public override bool Validate(int value, out string error)
        {
            error = null;
            if (value >= 0 && value < options.Length)
                return true;

            error = "Value is not a known option.";
            return false;
        }

        protected override bool IsChanged(int oldValue, int newValue)
        {
            return oldValue != newValue;
        }

        protected override void OnValueChanged(int oldValue, int newValue)
        {
            var previousOption = options[oldValue];
            previousOption?.events.onNotChosen?.Invoke(previousOption.name);
            var option = options[newValue];
            option?.events.onChosen?.Invoke(option.name);
        }
    }
}
