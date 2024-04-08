using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Common.Models.Forms;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.CloudData.Components
{
    /// <summary>
    /// Values that can be stored in a Cloud Data Record.
    /// </summary>
    [System.Serializable]
    public class CloudDataValues
    {
        [Tooltip("A string value that can be stored in the cloud.")]
        public string stringValue;
        [Tooltip("A boolean value that can be stored in the cloud.")]
        public bool boolValue;
        [Tooltip("A number value that can be stored in the cloud.")]
        public float numberValue;

        [Tooltip("An event that is invoked when the string value is changed.")]
        public UnityEvent<string> onStringValue;
        [Tooltip("An event that is invoked when the boolean value is changed.")]
        public UnityEvent<bool> onBoolValue;
        [Tooltip("An event that is invoked when the number value is changed.")]
        public UnityEvent<float> onFloatValue;
        [Tooltip("An event that is invoked when the number value is changed.")]
        public UnityEvent<int> onIntValue;
    }

    /// <summary>
    /// A Cloud Data Record is a component that can be attached to any game object to store data in the cloud.
    /// </summary>
    public class CloudDataRecord : CloudDataRecordBase<CloudDataValues>
    {
        [Header("Cloud Data")]
        [Tooltip("The values that can be stored in this record.")]
        public CloudDataValues values = new();

        /// <summary>
        /// Gets or sets the string value of this record.
        /// </summary>
        public string StringValue
        {
            get => values.stringValue;
            set
            {
                values.stringValue = value;
                values.onStringValue?.Invoke(value);
            }
        }

        /// <summary>
        /// Gets or sets the boolean value of this record.
        /// </summary>
        public bool BoolValue
        {
            get => values.boolValue;
            set
            {
                values.boolValue = value;
                values.onBoolValue?.Invoke(value);
            }
        }

        /// <summary>
        /// Gets or sets the float value of this record.
        /// </summary>
        public float FloatValue
        {
            get => values.numberValue;
            set
            {
                values.numberValue = value;
                values.onFloatValue?.Invoke(value);
                values.onIntValue?.Invoke((int)value);
            }
        }

        /// <summary>
        /// Gets or sets the int value of this record.
        /// </summary>
        public int IntValue
        {
            get => (int) FloatValue;
            set => FloatValue = value;
        }

        /// <summary>
        /// Parses the data from the record into the values.
        /// </summary>
        /// <param name="record">The record to parse.</param>
        /// <returns>The values.</returns>
        public override CloudDataValues ParseData(CloudDataRecordDto record)
        {
            var cloudDataValues = new CloudDataValues
            {
                stringValue = record.StringValue,
                boolValue = record.BoolValue,
                numberValue = record.NumberValue
            };
            StringValue = record.StringValue;
            BoolValue = record.BoolValue;
            FloatValue = record.NumberValue;
            return cloudDataValues;
        }

        /// <summary>
        /// Writes the data from the values into the record.
        /// </summary>
        /// <param name="form">The record to write to.</param>
        public override void WriteData(CloudDataRecordUpsertForm form)
        {
            form.StringValue = StringValue;
            form.BoolValue = BoolValue;
            form.NumberValue = FloatValue;
        }
    }
}