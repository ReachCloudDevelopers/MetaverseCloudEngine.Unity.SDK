using System;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using UnityEngine;

#if UNITY_IOS || UNITY_EDITOR
namespace MetaverseCloudEngine.Unity.SPUP
{
    /// <summary>
    /// iOS-specific Bluetooth serial manager.
    /// Uses native iOS functions to connect to a device, write data, and read data.
    /// </summary>
    public class IOSBluetoothSerialManager : MonoBehaviour
    {
        private static IOSBluetoothSerialManager _instance;
        /// <summary>
        /// Singleton instance of IOSBluetoothSerialManager.
        /// </summary>
        public static IOSBluetoothSerialManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("IOSBluetoothSerialManager");
                    _instance = go.AddComponent<IOSBluetoothSerialManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        /// <summary>
        /// Indicates whether a Bluetooth device is currently connected.
        /// </summary>
        [UsedImplicitly]
        public bool IsConnected { get; private set; }
        [UsedImplicitly]
        public string SerialNumber { get; set; }
        [UsedImplicitly]
        public string DeviceName { get; set; }
        [UsedImplicitly]
        public string Port { get; set; }
        [UsedImplicitly]
        public string VendorID { get; set; }
        [UsedImplicitly]
        public string ProductID { get; set; }

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
        /// Connects to the specified Bluetooth device.
        /// Uses the device's SerialNumber for connecting.
        /// </summary>
        /// <param name="device">Device information used for connection.</param>
        public void ConnectToDevice()
        {
            if (string.IsNullOrEmpty(SerialNumber))
            {
                MetaverseProgram.Logger.LogError("IOSBluetoothSerialManager: Serial number is empty.");
                return;
            }
            
            if (IsConnected)
            {
                Disconnect();
                IsConnected = false;
            }

            // Attempt connection via native call.
            int result = ios_connectToDevice(SerialNumber);
            if (result == 0) // Assuming 0 indicates success.
            {
                IsConnected = true;
                ConnectedDevice = new MetaverseSerialPortUtilityInterop.DeviceInfo
                {
                    SerialNumber = SerialNumber,
                    PortName = Port,
                    Vendor = VendorID,
                    Product = ProductID
                };
                MetaverseProgram.Logger.Log(
                    $"IOSBluetoothSerialManager: Successfully connected to device: {ConnectedDevice}");
            }
            else
            {
                MetaverseProgram.Logger.LogError(
                    $"IOSBluetoothSerialManager: Failed to connect to device: {ConnectedDevice}");
            }
        }

        /// <summary>
        /// Disconnects from the currently connected Bluetooth device.
        /// </summary>
        public void Disconnect()
        {
            if (!IsConnected)
            {
                MetaverseProgram.Logger.LogWarning("IOSBluetoothSerialManager: No device connected to disconnect.");
                return;
            }

            ios_disconnect();
            MetaverseProgram.Logger.Log("IOSBluetoothSerialManager: Disconnected from device: " + ConnectedDevice.ToString());
            IsConnected = false;
            ConnectedDevice = null;
        }

        /// <summary>
        /// Writes bytes to the connected Bluetooth device.
        /// Calls the native function to send data.
        /// </summary>
        /// <param name="data">Byte array to be sent.</param>
        public void WriteBytes(byte[] data)
        {
            if (!IsConnected)
                return;
            if (data == null || data.Length == 0)
                return;
            int bytesWritten = ios_writeData(data, data.Length);
            MetaverseProgram.Logger.Log("IOSBluetoothSerialManager: Wrote " + bytesWritten + " bytes.");
        }

        /// <summary>
        /// Reads bytes received from the Bluetooth device.
        /// Calls the native function to retrieve data.
        /// </summary>
        /// <returns>Byte array containing the received data.</returns>
        public byte[] ReadBytes()
        {
            if (!IsConnected)
                return Array.Empty<byte>();

            const int bufferSize = 1024;
            byte[] buffer = new byte[bufferSize];
            int bytesRead = ios_readData(buffer, bufferSize);
            if (bytesRead <= 0)
            {
                return Array.Empty<byte>();
            }
            byte[] result = new byte[bytesRead];
            Array.Copy(buffer, result, bytesRead);
            MetaverseProgram.Logger.Log("IOSBluetoothSerialManager: Read " + bytesRead + " bytes.");
            return result;
        }
    }
}
#endif