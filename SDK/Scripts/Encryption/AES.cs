using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Encryption
{
    public class AES : IEncryptor
    {
        public AES(string keySalt = null, string ivSalt = null)
        {
            Key = keySalt ?? GetKeySalt();
            IV = ivSalt ?? GetDefaultSalt();
        }

        /// <summary>
        /// Gets or sets the AES key.
        /// </summary>
        public string Key { get; set; }
        
        /// <summary>
        /// Gets or sets the IV key.
        /// </summary>
        public string IV { get; set; }

        public string EncryptString(string plainString)
        {
            // Obfuscate the string using AES encryption.
            var key = GetKeyBytes();
            var iv = GetIvBytes();
            var plainBytes = Encoding.UTF8.GetBytes(plainString);
            
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            
            using var encryptor = aes.CreateEncryptor();
            using var ms = new MemoryStream();
            using var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
            
            cs.Write(plainBytes, 0, plainBytes.Length);
            cs.FlushFinalBlock();
            return Convert.ToBase64String(ms.ToArray());
        }

        public string DecryptString(string encryptedString)
        {
            try
            {
                // Now decrypt the string.
                var key = GetKeyBytes();
                var iv = GetIvBytes();
                var encryptedBytes = Convert.FromBase64String(encryptedString);

                using var ms = new MemoryStream(encryptedBytes);
                using var aes = new AesCryptoServiceProvider();
                using var cs = new CryptoStream(ms, aes.CreateDecryptor(key, iv), CryptoStreamMode.Read);

                var plainBytes = new byte[encryptedBytes.Length];
                var decryptedByteCount = cs.Read(plainBytes, 0, plainBytes.Length);

                return Encoding.UTF8.GetString(plainBytes, 0, decryptedByteCount);
            }
            catch (Exception)
            {
                // If any exception occurs, we don't care
                // just return null.
                return null;
            }
        }

        private byte[] GetKeyBytes()
        {
            byte[] identifier = new byte[16];
            var deviceIDBytes = Encoding.UTF8.GetBytes(Key);
            Buffer.BlockCopy(deviceIDBytes, 0, identifier, 0, Math.Min(deviceIDBytes.Length, identifier.Length));
            return identifier;
        }

        private byte[] GetIvBytes()
        {
            var iv = new byte[16];
            var ivStringBytes = Encoding.UTF8.GetBytes(IV);
            Buffer.BlockCopy(ivStringBytes, 0, iv, 0, Math.Min(ivStringBytes.Length, iv.Length));
            return iv;
        }

        private static string GetKeySalt()
        {
            var deviceID = SystemInfo.deviceUniqueIdentifier;
            if (deviceID == SystemInfo.unsupportedIdentifier)
                deviceID = GetDefaultSalt();
            return deviceID;
        }

        private static string GetDefaultSalt()
        {
            return SystemInfo.deviceName + "_" + SystemInfo.deviceModel;
        }
    }
}