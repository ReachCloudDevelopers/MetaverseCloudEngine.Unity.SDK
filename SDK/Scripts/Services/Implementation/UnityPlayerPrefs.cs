using MetaverseCloudEngine.Unity.Services.Abstract;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Services.Implementation
{
    public class UnityPlayerPrefs : IPrefs
    {
        private static string Prefix => MetaverseKioskModeAPI.Config;

        public void DeleteKey(string key)
        {
#if UNITY_EDITOR
            if (Application.isEditor)
            {
                UnityEditor.EditorPrefs.DeleteKey(Prefix + key);
                return;
            }
#endif
            PlayerPrefs.DeleteKey(Prefix + key);
        }

        public float GetFloat(string key, float defaultValue)
        {
#if UNITY_EDITOR
            if (Application.isEditor)
            {
                return UnityEditor.EditorPrefs.GetFloat(Prefix + key, defaultValue);
            }
#endif
            return PlayerPrefs.GetFloat(Prefix + key, defaultValue);
        }

        public int GetInt(string key, int defaultValue)
        {
#if UNITY_EDITOR
            if (Application.isEditor)
            {
                return UnityEditor.EditorPrefs.GetInt(Prefix + key, defaultValue);
            }
#endif
            return PlayerPrefs.GetInt(Prefix + key, defaultValue);
        }

        public string GetString(string key, string defaultValue)
        {
#if UNITY_EDITOR
            if (Application.isEditor)
            {
                return UnityEditor.EditorPrefs.GetString(Prefix + key, defaultValue);
            }
#endif
            return PlayerPrefs.GetString(Prefix + key, defaultValue);
        }

        public void SetFloat(string key, float value)
        {
#if UNITY_EDITOR
            if (Application.isEditor)
            {
                UnityEditor.EditorPrefs.SetFloat(Prefix + key, value);
                return;
            }
#endif
            PlayerPrefs.SetFloat(Prefix + key, value);
            PlayerPrefs.Save();
        }

        public void SetInt(string key, int value)
        {
#if UNITY_EDITOR
            if (Application.isEditor)
            {
                UnityEditor.EditorPrefs.SetInt(Prefix + key, value);
                return;
            }
#endif
            PlayerPrefs.SetInt(Prefix + key, value);
            PlayerPrefs.Save();
        }

        public void SetString(string key, string value)
        {
#if UNITY_EDITOR
            if (Application.isEditor)
            {
                if (string.IsNullOrEmpty(value))
                {
                    UnityEditor.EditorPrefs.DeleteKey(Prefix + key);
                    return;
                }

                UnityEditor.EditorPrefs.SetString(Prefix + key, value);
                return;
            }
#endif
            PlayerPrefs.SetString(Prefix + key, value);
            PlayerPrefs.Save();
        }
    }
}
