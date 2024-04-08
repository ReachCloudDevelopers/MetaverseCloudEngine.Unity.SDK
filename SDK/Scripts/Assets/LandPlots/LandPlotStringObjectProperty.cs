using System.Text.RegularExpressions;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Assets.LandPlots
{
    public class LandPlotStringObjectProperty : LandPlotObjectPropertyBase<string>
    {
        [Tooltip("An optional regular expression that the value must match to be valid.")]
        [SerializeField] private string regexValidation;

        protected override bool IsChanged(string oldValue, string newValue)
        {
            return oldValue != newValue;
        }

        public override bool Validate(string value, out string error)
        {
            return base.Validate(value, out error) && RegexValidate(value, out error);
        }

        private bool RegexValidate(string value, out string error)
        {
            if (string.IsNullOrEmpty(regexValidation))
            {
                error = null;
                return true;
            }

            error = "Validation Failed";
            return Regex.IsMatch(value, regexValidation);
        }
    }
}
