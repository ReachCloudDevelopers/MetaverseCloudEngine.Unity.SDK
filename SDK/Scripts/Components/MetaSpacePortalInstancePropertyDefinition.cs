using UnityEngine;
using UnityEngine.Serialization;

namespace MetaverseCloudEngine.Unity.Components
{
    [RequireComponent(typeof(MetaSpacePortal))]
    public partial class MetaSpacePortalInstancePropertyDefinition : MonoBehaviour
    {
        public string propertyName;
        public string value;

        [Tooltip("If the current instance contains this property the value of that property will be used.")]
        [FormerlySerializedAs("getFromCurrentInstance")]
        public bool getValueFromCurrentInstance;
        [Tooltip("Append this property to the instance ID.")]
        public bool appendToInstanceID;

        public void SetIntValue(int v)
        {
            this.value = v.ToString();
        }

        public void SetFloatValue(float v)
        {
            this.value = v.ToString();
        }

        public void SetBoolValue(bool v)
        {
            this.value = v.ToString();
        }

        public void SetStringValue(string v)
        {
            this.value = v;
        }

        public object GetObjectValue()
        {
            if (getValueFromCurrentInstance)
            {
                string v = null;
                GetObjectValueInternal(ref v);
                if (!string.IsNullOrEmpty(v))
                    return v;
            }

            if (string.IsNullOrEmpty(value))
                return string.Empty;

            if (int.TryParse(value, out int intValue))
                return intValue;

            if (float.TryParse(value, out float floatValue))
                return floatValue;

            if (bool.TryParse(value, out bool boolValue))
                return boolValue;

            return value;
        }

        partial void GetObjectValueInternal(ref string value);
    }
}