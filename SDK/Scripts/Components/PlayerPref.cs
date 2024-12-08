using MetaverseCloudEngine.Unity.Async;
using System;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Components
{
    public class PlayerPref : MonoBehaviour
    {
        [Serializable]
        public class PlayerPrefValue<T>
        {
            public T defaultValue;
            public UnityEvent<T> onGetValue;
        }

        [SerializeField] private string prefKey;
        [SerializeField] private bool getOnStart = true;

        [SerializeField] private PlayerPrefValue<string> playerPrefStringValue;
        [SerializeField] private PlayerPrefValue<bool> playerPrefBoolValue;
        [SerializeField] private PlayerPrefValue<int> playerPrefIntValue;
        [SerializeField] private PlayerPrefValue<float> playerPrefFloatValue;

        public string PrefKey { get => prefKey; set => prefKey = value; }

        public string StringValue {
            get {
                if (string.IsNullOrEmpty(PrefKey))
                    return playerPrefStringValue.defaultValue;
                return MetaverseProgram.Prefs.GetString($"{PrefKey}_string", playerPrefStringValue.defaultValue);
            }
        }

        public float FloatValue {
            get {
                if (string.IsNullOrEmpty(PrefKey))
                    return playerPrefFloatValue.defaultValue;
                return MetaverseProgram.Prefs.GetFloat($"{PrefKey}_float", playerPrefFloatValue.defaultValue);
            }
        }

        public int IntValue {
            get {
                if (string.IsNullOrEmpty(PrefKey))
                    return playerPrefIntValue.defaultValue;
                return MetaverseProgram.Prefs.GetInt($"{PrefKey}_int", playerPrefIntValue.defaultValue);
            }
        }

        public bool BoolValue {
            get {
                if (string.IsNullOrEmpty(PrefKey))
                    return playerPrefBoolValue.defaultValue;
                return MetaverseProgram.Prefs.GetInt($"{PrefKey}_bool", playerPrefBoolValue.defaultValue ? 1 : 0) == 1;
            }
        }

        private void Start()
        {
            if (getOnStart)
                Get();
        }

        public void SetStringValue(string value)
        {
            MetaverseProgram.OnInitialized(() =>
                MetaverseProgram.Prefs.SetString($"{PrefKey}_string", value));
        }

        public void SetFloatValue(float value)
        {
            MetaverseProgram.OnInitialized(() => 
                MetaverseProgram.Prefs.SetFloat($"{PrefKey}_float", value));
        }

        public void SetIntValue(int value)
        {
            MetaverseProgram.OnInitialized(() => 
                MetaverseProgram.Prefs.SetInt($"{PrefKey}_int", value));
        }

        public void SetBoolValue(bool value)
        {
            MetaverseProgram.OnInitialized(() =>
                MetaverseProgram.Prefs.SetInt($"{PrefKey}_bool", value ? 1 : 0));
        }

        public void Get()
        {
            MetaverseProgram.OnInitialized(() =>
            {
                MetaverseDispatcher.AtEndOfFrame(() =>
                {
#if METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED
                    MetaverseDispatcher.WaitUntil(() => 
                    !MetaverseDeepLinkAPI.IsActivating, () =>
                    {
#endif
                        playerPrefStringValue.onGetValue?.Invoke(StringValue);
                        playerPrefFloatValue.onGetValue?.Invoke(FloatValue);
                        playerPrefIntValue.onGetValue?.Invoke(IntValue);
                        playerPrefBoolValue.onGetValue?.Invoke(BoolValue);
#if METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED
                    });
#endif
                });
            });
        }
    }
}
