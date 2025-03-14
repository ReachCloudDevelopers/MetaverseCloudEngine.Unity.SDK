using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using MetaverseCloudEngine.Unity.Async;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.SPUP
{
    [HideMonoScript]
    [DisallowMultipleComponent]
    public class MetaverseSerialPortAutoConnect : TriInspectorMonoBehaviour
    {
        [Flags]
        public enum DeviceField
        {
            SerialNumber = 1,
            Product = 2,
            PortName = 4,
            Vendor = 8
        }

        [Flags]
        public enum DeviceType
        {
            Usb = 1,
            Bluetooth = 2,
            Pci = 4
        }

        [Required]
        [SerializeField] private Component serialPortUtilityPro;
        [SerializeField] private bool onStart = true;
        [SerializeField] private bool saveLastDevice = true;

        [ShowIf(nameof(saveLastDevice))]
        [SerializeField]
        private string saveKey = Guid.NewGuid().ToString()[..6].ToUpper();

        [InfoBox("You can use https://regexr.com/ to test your regex.")]
        [SerializeField]
        private string regexSearchString = "<Enter Search String>";

        [SerializeField] private DeviceField searchField = (DeviceField)(-1);
        [SerializeField] private DeviceType searchType = (DeviceType)(-1);
        [SerializeField] private bool debugLog = true;

        private FieldInfo _isAutoOpenField;
        private bool _triedToOpenSavedDevice;
        private readonly MetaverseSerialPortDeviceAPI _deviceAPI = new();

        private void OnValidate()
        {
            MetaverseSerialPortUtilityInterop.EnsureComponent(ref serialPortUtilityPro, gameObject);
        }

        private void Reset()
        {
            MetaverseSerialPortUtilityInterop.EnsureComponent(ref serialPortUtilityPro, gameObject);
        }

        private void Awake()
        {
            MetaverseSerialPortUtilityInterop.SetField(serialPortUtilityPro, ref _isAutoOpenField, MetaverseSerialPortUtilityInterop.SettableFieldID.IsAutoOpen, false);
            _deviceAPI.OnDeviceOpen.AddListener(() =>
            {
                if (saveLastDevice && !string.IsNullOrWhiteSpace(saveKey))
                    MetaverseProgram.Prefs.SetString(GetSaveKey(), _deviceAPI.DeviceString);
            });
        }

        private void OnDestroy()
        {
            _deviceAPI.Dispose();
        }

        private void Start()
        {
            if (onStart)
                AutoConnect();
        }

        private void OnEnable()
        {
            if (_deviceAPI.IsInitialized)
                Invoke(nameof(WatchConnection), 1f);
        }

        /// <summary>
        /// Automatically connect to the first device that matches the search criteria.
        /// </summary>
        public void AutoConnect()
        {
            MetaverseDispatcher.WaitForSeconds(0.5f, () =>
            {
                if (!this || !isActiveAndEnabled)
                {
                    if (this && debugLog)
                        MetaverseProgram.Logger.Log("AutoConnect cancelled because the component is not enabled.");
                    return;
                }

                if (serialPortUtilityPro is Behaviour { isActiveAndEnabled: false })
                {
                    if (debugLog)
                        MetaverseProgram.Logger.Log("AutoConnect cancelled because the SerialPortUtilityPro component is disabled.");
                    return;
                }
                
                if (!_triedToOpenSavedDevice)
                {
                    _triedToOpenSavedDevice = true;
                    if (!string.IsNullOrEmpty(saveKey))
                    {
                        var deviceInfoString = MetaverseProgram.Prefs.GetString(GetSaveKey(), string.Empty);
                        if (!string.IsNullOrEmpty(deviceInfoString) && 
                            MetaverseSerialPortUtilityInterop.DeviceInfo.TryParse(deviceInfoString, out var i) &&
                            i.ParsedOpenSystem is not null)
                        {
                            if (debugLog)
                                MetaverseProgram.Logger.Log(
                                    $"AutoConnect found a saved device: {i.SerialNumber}");

                            _deviceAPI.Initialize(
                                serialPortUtilityPro,
                                i.SerialNumber,
                                i,
                                i.ParsedOpenSystem.Value);
                            _deviceAPI.Open();
                            return;
                        }
                    }
                }

                var btDevices = MetaverseSerialPortUtilityInterop.GetConnectedDeviceList(
                    MetaverseSerialPortUtilityInterop.OpenSystem.BluetoothSsp);
                var usbDevices = MetaverseSerialPortUtilityInterop.GetConnectedDeviceList(
                    MetaverseSerialPortUtilityInterop.OpenSystem.Usb);
                var pciDevices = MetaverseSerialPortUtilityInterop.GetConnectedDeviceList(
                    MetaverseSerialPortUtilityInterop.OpenSystem.Pci);
                
                if (btDevices is null or { Length: 0 } &&
                    usbDevices is null or { Length: 0 } &&
                    pciDevices is null or { Length: 0 })
                {
                    if (debugLog)
                        MetaverseProgram.Logger.Log("AutoConnect cancelled because no devices are connected.");
                    return;
                }

                if (debugLog)
                    MetaverseProgram.Logger.Log(
                        $"Connected Bluetooth Devices: {(btDevices?.Length ?? 0)} | Connected USB Devices: {(usbDevices?.Length ?? 0)} | Connected PCI Devices: {(pciDevices?.Length ?? 0)}");

                var deviceInfo =
                    Array.Empty<(MetaverseSerialPortUtilityInterop.DeviceInfo,
                            MetaverseSerialPortUtilityInterop.OpenSystem)>()
                        .Concat(searchType.HasFlag(DeviceType.Bluetooth) && btDevices != null
                            ? btDevices.Select(x => (x, MetaverseSerialPortUtilityInterop.OpenSystem.BluetoothSsp))
                            : Array
                                .Empty<(MetaverseSerialPortUtilityInterop.DeviceInfo,
                                    MetaverseSerialPortUtilityInterop.OpenSystem)>())
                        .Concat(searchType.HasFlag(DeviceType.Usb) && usbDevices != null
                            ? usbDevices.Select(x => (x, MetaverseSerialPortUtilityInterop.OpenSystem.Usb))
                            : Array
                                .Empty<(MetaverseSerialPortUtilityInterop.DeviceInfo,
                                    MetaverseSerialPortUtilityInterop.OpenSystem)>())
                        .Concat(searchType.HasFlag(DeviceType.Pci) && pciDevices != null
                            ? pciDevices.Select(x => (x, MetaverseSerialPortUtilityInterop.OpenSystem.Pci))
                            : Array
                                .Empty<(MetaverseSerialPortUtilityInterop.DeviceInfo,
                                    MetaverseSerialPortUtilityInterop.OpenSystem)>())
                        .ToArray()
                        .FirstOrDefault(device =>
                            device.Item1 != null && (
                                (!string.IsNullOrWhiteSpace(device.Item1.SerialNumber) &&
                                 Regex.IsMatch(device.Item1.SerialNumber, regexSearchString.Replace("\\_", "_")) &&
                                 searchField.HasFlag(DeviceField.SerialNumber)) ||
                                (!string.IsNullOrWhiteSpace(device.Item1.Product) &&
                                 Regex.IsMatch(device.Item1.Product, regexSearchString.Replace("\\_", "_")) &&
                                 searchField.HasFlag(DeviceField.Product)) ||
                                (!string.IsNullOrWhiteSpace(device.Item1.PortName) &&
                                 Regex.IsMatch(device.Item1.PortName, regexSearchString.Replace("\\_", "_")) &&
                                 searchField.HasFlag(DeviceField.PortName)) ||
                                (!string.IsNullOrWhiteSpace(device.Item1.Vendor) &&
                                 Regex.IsMatch(device.Item1.Vendor, regexSearchString.Replace("\\_", "_")) &&
                                 searchField.HasFlag(DeviceField.Vendor))));

                if (deviceInfo.Item1 != null)
                {
                    if (debugLog)
                        MetaverseProgram.Logger.Log($"AutoConnect found a device: {deviceInfo.Item1.SerialNumber}");
                    _deviceAPI.Initialize(
                        serialPortUtilityPro,
                        deviceInfo.Item1.SerialNumber,
                        deviceInfo.Item1,
                        deviceInfo.Item2);
                    _deviceAPI.Open();
                }
                else
                {
                    if (debugLog)
                        MetaverseProgram.Logger.Log(
                            $"AutoConnect did not find a device for: {regexSearchString} | {searchField} | {searchType}");
                }

                if (!IsInvoking(nameof(WatchConnection)))
                    Invoke(nameof(WatchConnection), 1f);
            });
        }

        private void WatchConnection()
        {
            if (!_deviceAPI.IsThisDeviceOpened())
            {
                if (_deviceAPI.IsADeviceBeingOpened())
                {
                    if (!IsInvoking(nameof(WatchConnection)))
                        Invoke(nameof(WatchConnection), 1f);
                }
                else if (!IsInvoking(nameof(AutoConnect)))
                    Invoke(nameof(AutoConnect), 1f);
            }
            else if (!IsInvoking(nameof(WatchConnection)))
                Invoke(nameof(WatchConnection), 5f);
        }

        private string GetSaveKey()
        {
            return $"{nameof(MetaverseSerialPortAutoConnect)}_{saveKey}";
        }
    }
}