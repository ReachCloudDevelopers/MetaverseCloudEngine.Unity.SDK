using System;
using System.IO;
using MetaverseCloudEngine.Unity.Encryption;
using UnityEngine;
using IDeviceIdProvider = MetaverseCloudEngine.ApiClient.Controllers.IDeviceIdProvider;

namespace MetaverseCloudEngine.Unity
{
    public sealed class UnityDeviceIdProvider : IDeviceIdProvider
    {
        private const string PlayerPrefsKey = "MetaverseCloudEngine.DeviceId";

        private readonly AES _encryptor = new();
        private string _cachedDeviceId;

        public string DeviceId
        {
            get
            {
                if (!string.IsNullOrEmpty(_cachedDeviceId))
                    return _cachedDeviceId;

#if UNITY_EDITOR
                var stored = UnityEditor.EditorPrefs.GetString(PlayerPrefsKey, null);
#else
                var stored = PlayerPrefs.GetString(PlayerPrefsKey, null);
#endif
                if (!string.IsNullOrWhiteSpace(stored))
                {
                    _cachedDeviceId = stored;
                    return stored;
                }

                var seed = GenerateSeed();
                if (string.IsNullOrWhiteSpace(seed))
                    seed = Guid.NewGuid().ToString("N");

                var encrypted = _encryptor.EncryptString(seed);
                _cachedDeviceId = encrypted;

                TryPersistDeviceId(encrypted);

                return encrypted;
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    _cachedDeviceId = null;
#if UNITY_EDITOR
                    UnityEditor.EditorPrefs.DeleteKey(PlayerPrefsKey);
#else
                    PlayerPrefs.DeleteKey(PlayerPrefsKey);
                    PlayerPrefs.Save();
#endif
                    return;
                }

                _cachedDeviceId = value;
                TryPersistDeviceId(value);
            }
        }

        public void TryPersistDeviceId(string deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
                return;

#if UNITY_EDITOR
            if (UnityEditor.EditorPrefs.GetString(PlayerPrefsKey, null) == deviceId)
                return;

            UnityEditor.EditorPrefs.SetString(PlayerPrefsKey, deviceId);
#else
            if (PlayerPrefs.GetString(PlayerPrefsKey, null) == deviceId)
                return;

            PlayerPrefs.SetString(PlayerPrefsKey, deviceId);
            PlayerPrefs.Save();
#endif
        }

        private static string GenerateSeed()
        {
            var identifier = Application.identifier;
            if (string.IsNullOrWhiteSpace(identifier))
                identifier = $"{Application.companyName}.{Application.productName}";

#if UNITY_EDITOR
            var projectPath = GetProjectPath();
            if (!string.IsNullOrWhiteSpace(projectPath))
                return identifier + projectPath;
#endif

            return identifier;
        }

#if UNITY_EDITOR
        private static string GetProjectPath()
        {
            try
            {
                var dataPath = Application.dataPath;
                if (string.IsNullOrWhiteSpace(dataPath))
                    return null;

                var directory = new DirectoryInfo(dataPath);
                var projectDirectory = directory.Parent;
                return projectDirectory?.FullName ?? dataPath;
            }
            catch (Exception)
            {
                return null;
            }
        }
#endif
    }
}
