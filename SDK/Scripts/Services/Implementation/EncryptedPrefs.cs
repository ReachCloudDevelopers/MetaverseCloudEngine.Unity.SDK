using MetaverseCloudEngine.Unity.Encryption;
using MetaverseCloudEngine.Unity.Services.Abstract;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Services.Implementation
{
    public class EncryptedPrefs : IPrefs
    {
        private Dictionary<string, object> _config;
        private readonly IEncryptor _encryptor = new AES();

        public void DeleteKey(string key)
        {
            Load();

            key = _encryptor.EncryptString(key);

            _config.Remove(key);

            Save();
        }

        public float GetFloat(string key, float defaultValue)
        {
            Load();

            key = _encryptor.EncryptString(key);

            return _config.TryGetValue(key, out var value) 
                ? Convert.ToSingle(value)
                : defaultValue;
        }

        public int GetInt(string key, int defaultValue)
        {
            Load();

            key = _encryptor.EncryptString(key);

            return _config.TryGetValue(key, out var value) 
                ? Convert.ToInt32(value) 
                : defaultValue;
        }

        public string GetString(string key, string defaultValue)
        {
            Load();

            key = _encryptor.EncryptString(key);

            return _config.TryGetValue(key, out var value) 
                ? _encryptor.DecryptString((string)value) 
                : defaultValue;
        }

        public void SetFloat(string key, float value)
        {
            Load();

            key = _encryptor.EncryptString(key);
            _config[key] = value;

            Save();
        }

        public void SetInt(string key, int value)
        {
            Load();

            key = _encryptor.EncryptString(key);
            _config[key] = value;

            Save();
        }

        public void SetString(string key, string value)
        {
            Load();

            key = _encryptor.EncryptString(key);
            value = _encryptor.EncryptString(value);
            _config[key] = value;

            Save();
        }

        private void Load()
        {
            if (_config != null)
                return;

            var configPath = GetConfigPath();

            if (!UsePlayerPrefs())
            {
                if (!System.IO.File.Exists(configPath))
                {
                    _config = new Dictionary<string, object>();
                    return;
                }
            }

            try
            {
                var json = ReadConfig(configPath);
                if (!string.IsNullOrEmpty(json))
                    _config = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
            }
            catch (Exception e)
            {
                MetaverseProgram.Logger.LogWarning($"Failed to load config from {configPath}: {e}");
            }
            finally
            {
                _config ??= new Dictionary<string, object>();
            }
        }

        private void Save()
        {
            var configPath = GetConfigPath();
            try
            {
                var json = JsonConvert.SerializeObject(_config);
                WriteConfig(configPath, json);
            }
            catch (Exception e)
            {
                MetaverseProgram.Logger.LogWarning($"Failed to save config to {configPath}: {e}");
            }
        }

        private string ReadConfig(string configPath)
        {
            return UsePlayerPrefs() 
                ? _encryptor.DecryptString(PlayerPrefs.GetString(_encryptor.EncryptString(configPath))) 
                : System.IO.File.ReadAllText(configPath);
        }

        private void WriteConfig(string configPath, string json)
        {
            if (UsePlayerPrefs())
            {
                // Use IndexDB
                PlayerPrefs.SetString(_encryptor.EncryptString(configPath), _encryptor.EncryptString(json));
                PlayerPrefs.Save();
                return;
            }
            
            System.IO.File.WriteAllText(configPath, json);
        }

        private static bool UsePlayerPrefs()
        {
            return Application.platform == RuntimePlatform.WebGLPlayer;
        }

        private static string GetConfigPath()
        {
            return 
                $"{Application.persistentDataPath}/{MetaverseKioskModeAPI.Config}_prefs"
#if UNITY_EDITOR
                + "_editor_editmode"
#endif
                ;
        }
    }
}
