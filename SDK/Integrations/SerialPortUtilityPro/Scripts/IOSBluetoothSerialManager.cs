using System;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using UnityEngine;

#if UNITY_IOS || UNITY_EDITOR
namespace MetaverseCloudEngine.Unity.SPUP
{
    /// <summary>
    /// iOS-specific Wi-Fi serial manager.
    /// Uses native iOS functions to connect to a device over TCP/IP, write data, and read data.
    /// </summary>
    public class IOSWiFiSerialManager : MonoBehaviour
    {
        private static IOSWiFiSerialManager _instance;

        /// <summary>
        /// Singleton instance of IOSWiFiSerialManager.
        /// </summary>
        public static IOSWiFiSerialManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("IOSWiFiSerialManager");
                    _instance = go.AddComponent<IOSWiFiSerialManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        /// <summary>
        /// Indicates whether a device is currently connected.
        /// </summary>
        [UsedImplicitly] public bool IsConnected { get; private set; }
        [UsedImplicitly] public string SerialNumber { get; set; } // IP address of the ESP32
        [UsedImplicitly] public string DeviceName { get; set; }
        [UsedImplicitly] public string Port { get; set; }
        [UsedImplicitly] public string VendorID { get; set; }
        [UsedImplicitly] public string ProductID { get; set; }

        /// <summary>
        /// Information about the currently connected device.
        /// </summary>
        public MetaverseSerialPortUtilityInterop.DeviceInfo ConnectedDevice { get; private set; }

        // Native function imports. These functions must be implemented in your iOS plugin.
        [DllImport("__Internal", EntryPoint = "ios_connectToDevice")]
        private static extern int ios_connectToDevice(string serialNumber);

        [DllImport("__Internal", EntryPoint = "ios_disconnect")]
        private static extern void ios_disconnect();

        [DllImport("__Internal", EntryPoint = "ios_writeData")]
        private static extern int ios_writeData(byte[] data, int length);

        [DllImport("__Internal", EntryPoint = "ios_readData")]
        private static extern int ios_readData(byte[] buffer, int bufferSize);

        /// <summary>
        /// Connects to the specified Wi-Fi device (by IP).
        /// </summary>
        public void ConnectToDevice()
        {
            if (string.IsNullOrEmpty(SerialNumber))
            {
                MetaverseProgram.Logger.LogError("IOSWiFiSerialManager: Serial number (IP) is empty.");
                return;
            }

            if (IsConnected)
            {
                Disconnect();
                IsConnected = false;
            }

            int result = ios_connectToDevice(SerialNumber);
            if (result == 0) // 0 = success
            {
                IsConnected = true;
                ConnectedDevice = new MetaverseSerialPortUtilityInterop.DeviceInfo
                {
                    SerialNumber = SerialNumber,
                    PortName = Port,
                    Vendor = VendorID,
                    Product = ProductID
                };
                MetaverseProgram.Logger.Log($"IOSWiFiSerialManager: Connected to device: {ConnectedDevice}");
            }
            else
            {
                MetaverseProgram.Logger.LogError($"IOSWiFiSerialManager: Failed to connect to device with IP {SerialNumber}");
            }
        }

        /// <summary>
        /// Disconnects from the currently connected Wi-Fi device.
        /// </summary>
        public void Disconnect()
        {
            if (!IsConnected)
            {
                MetaverseProgram.Logger.LogWarning("IOSWiFiSerialManager: No device connected to disconnect.");
                return;
            }

            ios_disconnect();
            MetaverseProgram.Logger.Log("IOSWiFiSerialManager: Disconnected from device: " + ConnectedDevice?.ToString());
            IsConnected = false;
            ConnectedDevice = null;
        }

        /// <summary>
        /// Sends bytes to the connected device over Wi-Fi (TCP).
        /// </summary>
        /// <param name="data">Byte array to send.</param>
        public void WriteBytes(byte[] data)
        {
            if (!IsConnected || data == null || data.Length == 0)
                return;

            int bytesWritten = ios_writeData(data, data.Length);
            MetaverseProgram.Logger.Log($"IOSWiFiSerialManager: Wrote {bytesWritten} bytes.");
        }

        /// <summary>
        /// Reads bytes received from the device over Wi-Fi (TCP).
        /// </summary>
        /// <returns>Byte array containing received data.</returns>
        public byte[] ReadBytes()
        {
            if (!IsConnected)
                return Array.Empty<byte>();

            const int bufferSize = 1024;
            byte[] buffer = new byte[bufferSize];
            int bytesRead = ios_readData(buffer, bufferSize);

            if (bytesRead <= 0)
                return Array.Empty<byte>();

            byte[] result = new byte[bytesRead];
            Array.Copy(buffer, result, bytesRead);
            MetaverseProgram.Logger.Log($"IOSWiFiSerialManager: Read {bytesRead} bytes.");
            return result;
        }
    }
}
#endif
