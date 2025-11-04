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

        static EncryptedPrefs()
        {
            PrimePathCache();
        }

        private static string _cachedConfigPath;
        private static bool _pathCached = false;

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

                // Attempt to recover from backup
                if (TryRecoverFromBackup(configPath))
                {
                    MetaverseProgram.Logger.Log($"Successfully recovered config from backup for {configPath}");
                }
                else
                {
                    MetaverseProgram.Logger.LogWarning($"Failed to recover config from backup for {configPath}. Starting with empty config.");
                }
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
                // Create backup before writing
                CreateBackup(configPath);

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
            if (_pathCached)
                return _cachedConfigPath;

#if UNITY_EDITOR
            UnityEngine.Debug.LogWarning("EncryptedPrefs: Uncached path access in editor - this should not happen post-prime. Check init order.");
#endif

            var basePrefix = MetaverseKioskModeAPI.Config ?? string.Empty;
            var path = $"{Application.persistentDataPath}/{basePrefix}_prefs";

#if UNITY_EDITOR
            path += "_editor_editmode";
#endif

            _cachedConfigPath = path;
            _pathCached = true;

            return path;
        }

        internal static void PrimePathCache()
        {
            if (!_pathCached)
            {
                _ = GetConfigPath();
            }
        }

        private static string GetBackupPath(string configPath)
        {
            return configPath + ".backup";
        }

        private void CreateBackup(string configPath)
        {
            try
            {
                if (UsePlayerPrefs())
                {
                    // For PlayerPrefs (WebGL), copy the encrypted value to a backup key
                    var encryptedKey = _encryptor.EncryptString(configPath);
                    var backupKey = _encryptor.EncryptString(GetBackupPath(configPath));

                    if (PlayerPrefs.HasKey(encryptedKey))
                    {
                        var currentValue = PlayerPrefs.GetString(encryptedKey);
                        PlayerPrefs.SetString(backupKey, currentValue);
                        PlayerPrefs.Save();
                    }
                }
                else
                {
                    // For file-based storage, copy the file
                    if (System.IO.File.Exists(configPath))
                    {
                        var backupPath = GetBackupPath(configPath);
                        System.IO.File.Copy(configPath, backupPath, overwrite: true);
                    }
                }
            }
            catch (Exception e)
            {
                MetaverseProgram.Logger.LogWarning($"Failed to create backup for {configPath}: {e}");
            }
        }

        private bool TryRecoverFromBackup(string configPath)
        {
            try
            {
                var backupPath = GetBackupPath(configPath);

                if (UsePlayerPrefs())
                {
                    // For PlayerPrefs (WebGL), try to restore from backup key
                    var backupKey = _encryptor.EncryptString(backupPath);

                    if (PlayerPrefs.HasKey(backupKey))
                    {
                        var backupValue = PlayerPrefs.GetString(backupKey);
                        var json = _encryptor.DecryptString(backupValue);

                        if (!string.IsNullOrEmpty(json))
                        {
                            // Validate the backup by attempting to deserialize
                            _config = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

                            // Restore the main config from backup
                            var encryptedKey = _encryptor.EncryptString(configPath);
                            PlayerPrefs.SetString(encryptedKey, backupValue);
                            PlayerPrefs.Save();

                            return true;
                        }
                    }
                }
                else
                {
                    // For file-based storage, try to restore from backup file
                    if (System.IO.File.Exists(backupPath))
                    {
                        var json = System.IO.File.ReadAllText(backupPath);

                        if (!string.IsNullOrEmpty(json))
                        {
                            // Validate the backup by attempting to deserialize
                            _config = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

                            // Restore the main config from backup
                            System.IO.File.Copy(backupPath, configPath, overwrite: true);

                            return true;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MetaverseProgram.Logger.LogWarning($"Failed to recover from backup for {configPath}: {e}");
            }

            return false;
        }
    }
}
