using System;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Blockchain.Components
{
    [Serializable]
    public abstract partial class BlockchainMetaDataCheck<T>
    {
        public string metaDataKey;

        [Header("Events")]
        public UnityEvent<T> onValue;
        public UnityEvent onValueFailed;

        internal abstract bool TryParse(string stringValue, Action<T> onData, Action onFailed);
        internal virtual bool IsValid(T data) => true;
    }

    [Serializable]
    public class BlockchainStringMetaDataCheck : BlockchainMetaDataCheck<string>
    {
        [Header("Validation - Contains")]
        public bool checkContains;
        public bool ignoreContainsCase;
        public string containsString;

        [Header("Validation - Equals")]
        public bool checkEquals;
        public bool ignoreEqualsCase;
        public string equalsString;

        internal override bool TryParse(string stringValue, Action<string> onData, Action onFailed)
        {
            if (string.IsNullOrEmpty(stringValue))
            {
                onFailed?.Invoke();
                return false;
            }

            onData?.Invoke(stringValue);
            return true;
        }

        internal override bool IsValid(string data)
        {
            bool isTrue = !checkContains && !checkEquals;
            if (checkContains) isTrue |= data.Contains(containsString, ignoreContainsCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
            if (checkEquals) isTrue |= data.Equals(equalsString, ignoreEqualsCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
            return isTrue;
        }
    }

    [Serializable]
    public class BlockchainBoolMetaDataCheck : BlockchainMetaDataCheck<bool>
    {
        [Header("Validation")]
        public bool checkValue;
        public bool requiredValue = true;

        internal override bool TryParse(string stringValue, Action<bool> onData, Action onFailed)
        {
            if (bool.TryParse(stringValue, out bool data))
            {
                onData?.Invoke(data);
                return true;
            }

            onFailed?.Invoke();
            return false;
        }

        internal override bool IsValid(bool data) => !checkValue || data == requiredValue;
    }

    [Serializable]
    public class BlockchainFloatMetaDataCheck : BlockchainMetaDataCheck<float>
    {
        [Header("Validation")]
        public bool checkRange;
        public float minValue;
        public float maxValue = float.MaxValue;

        internal override bool TryParse(string stringValue, Action<float> onData, Action onFailed)
        {
            if (float.TryParse(stringValue, out float data))
            {
                onData?.Invoke(data);
                return true;
            }

            onFailed?.Invoke();
            return false;
        }

        internal override bool IsValid(float data)
        {
            if (!checkRange) return true;
            return data > minValue && data < maxValue;
        }
    }

    [Serializable]
    public class BlockchainIntMetaDataCheck : BlockchainMetaDataCheck<int>
    {
        [Header("Validation")]
        public bool checkRange;
        public int minValue;
        public int maxValue = int.MaxValue;

        internal override bool TryParse(string stringValue, Action<int> onData, Action onFailed)
        {
            if (int.TryParse(stringValue, out int data))
            {
                onData?.Invoke(data);
                return true;
            }

            onFailed?.Invoke();
            return false;
        }

        internal override bool IsValid(int data)
        {
            if (!checkRange) return true;
            return data > minValue && data < maxValue;
        }
    }

    [Serializable]
    public class BlockchainImageMetaDataCheck : BlockchainMetaDataCheck<Sprite>
    {
        public UnityEvent<Texture2D> onTextureValue;

        internal override bool TryParse(string stringValue, Action<Sprite> onData, Action onFailed)
        {
            if (onTextureValue.GetPersistentEventCount() > 0)
                return MetaverseIpfsAPI.FetchImage(stringValue, t => onTextureValue?.Invoke(t), onData, onFailed);
            return false;
        }
    }

    [Serializable]
    public class BlockchainMetaDataEvents
    {
        public UnityEvent onBeginCheckMetaData;
        public UnityEvent onEndCheckMetaData;
    }

    [Serializable]
    public partial class BlockchainMetaData
    {
        public BlockchainStringMetaDataCheck[] stringMetaDataChecks = Array.Empty<BlockchainStringMetaDataCheck>();
        public BlockchainIntMetaDataCheck[] intMetaDataChecks = Array.Empty<BlockchainIntMetaDataCheck>();
        public BlockchainBoolMetaDataCheck[] boolMetaDataChecks = Array.Empty<BlockchainBoolMetaDataCheck>();
        public BlockchainFloatMetaDataCheck[] floatMetaDataChecks = Array.Empty<BlockchainFloatMetaDataCheck>();
        public BlockchainImageMetaDataCheck[] imageMetaDataChecks = Array.Empty<BlockchainImageMetaDataCheck>();
        public BlockchainMetaDataEvents metaDataEvents = new();

        public void BeginCheck()
        {
            metaDataEvents.onBeginCheckMetaData?.Invoke();
        }

        public void PerformCheck(string metaData)
        {
            PlatformCheckInternal(metaData);
        }

        partial void PlatformCheckInternal(string metaData);

        public void EndCheck()
        {
            metaDataEvents.onEndCheckMetaData?.Invoke();
        }
        
        public bool HasAnyBlockchainMetaDataChecks()
        {
            return stringMetaDataChecks.Length > 0 ||
                   intMetaDataChecks.Length > 0 ||
                   boolMetaDataChecks.Length > 0 ||
                   floatMetaDataChecks.Length > 0 ||
                   imageMetaDataChecks.Length > 0;
        }
    }
}