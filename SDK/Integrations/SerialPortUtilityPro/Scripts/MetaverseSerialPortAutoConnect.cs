using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using MetaverseCloudEngine.Unity.Async;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.SPUP
{
    [HideMonoScript]
    [DisallowMultipleComponent]
    public class MetaverseSerialPortAutoConnect : TriInspectorMonoBehaviour
    {
        private const float WATCH_CONNECTION_INTERVAL = 5;
        private const float MAX_CONNECTION_TIMEOUT = 20;
        private const float AUTO_CONNECT_DELAY = 0.5f;
        
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
        [SerializeField]
        private Component serialPortUtilityPro;
        [SerializeField]
        private bool onStart = true;
        [SerializeField]
        private bool saveLastDevice = true;

        [ShowIf(nameof(saveLastDevice))]
        [SerializeField]
        private string saveKey = Guid.NewGuid().ToString()[..6].ToUpper();

        [InfoBox("You can use https://regexr.com/ to test your regex.")]
        [SerializeField]
        private string regexSearchString;

        [SerializeField]
        private DeviceField searchField = (DeviceField)(-1);
        [SerializeField]
        private DeviceType searchType = (DeviceType)(-1);
        [SerializeField]
        private bool debugLog = true;

        [Header("Events")]
        [SerializeField]
        private UnityEvent onHasSavedDevice = new();
        [SerializeField]
        private UnityEvent onNoSavedDevice = new();
        [SerializeField]
        private UnityEvent onDeviceOpened = new();
        [SerializeField]
        private UnityEvent onDeviceClosed = new();

        private FieldInfo _isAutoOpenField;
        private FieldInfo _onSystemEventField;
        private UnityAction<object, string> _onSystemEventCallback;
        private bool _opening;
        private readonly MetaverseSerialPortDeviceAPI _deviceAPI = new();
        private static MetaverseSerialPortAutoConnect _currentAutoConnect;

        private float _lastConnectionAttemptTime;

        public string SaveKey
        {
            get => saveKey;
            set => saveKey = value;
        }

        public string RegexSearchString
        {
            get => regexSearchString;
            set => regexSearchString = value;
        }

        public UnityEvent OnHasSavedDevice
        {
            get => onHasSavedDevice;
            set => onHasSavedDevice = value;
        }

        public UnityEvent OnNoSavedDevice
        {
            get => onNoSavedDevice;
            set => onNoSavedDevice = value;
        }

        public UnityEvent OnDeviceOpened
        {
            get => onDeviceOpened;
            set => onDeviceOpened = value;
        }

        public UnityEvent OnDeviceClosed
        {
            get => onDeviceClosed;
            set => onDeviceClosed = value;
        }

        public bool HasSavedDevice => 
            !string.IsNullOrEmpty(saveKey) &&
                MetaverseProgram.Prefs.GetString(GetSaveKey(), null) != null;

        private void OnValidate()
        {
            MetaverseSerialPortUtilityInterop.EnsureComponent(ref serialPortUtilityPro, gameObject);
        }

        private void Reset()
        {
            MetaverseSerialPortUtilityInterop.EnsureComponent(ref serialPortUtilityPro, gameObject);
        }

        private void OnDestroy()
        {
            _deviceAPI.Dispose();
            if (_currentAutoConnect == this)
                _currentAutoConnect = null;
            MetaverseSerialPortUtilityInterop.RemoveSystemEventCallback(
                serialPortUtilityPro,
                ref _onSystemEventField,
                OnSystemEventCallback);
        }

        private void Start()
        {
            if (!serialPortUtilityPro) return;
            MetaverseSerialPortUtilityInterop.SetField(serialPortUtilityPro, ref _isAutoOpenField,
                MetaverseSerialPortUtilityInterop.SettableFieldID.IsAutoOpen, false);
            MetaverseSerialPortUtilityInterop.AddSystemEventCallback(serialPortUtilityPro, ref _onSystemEventField,
                OnSystemEventCallback, ref _onSystemEventCallback);
            if (!string.IsNullOrWhiteSpace(saveKey) && MetaverseProgram.Prefs.GetString(GetSaveKey(), null) != null)
                onHasSavedDevice?.Invoke();
            else onNoSavedDevice?.Invoke();
            if (onStart) AutoConnect();
        }

        private void OnSystemEventCallback(object sender, string e)
        {
            if (!this) return;
            OnSerialPortMessage(e);
        }

        private void OnSerialPortMessage(string e)
        {
            MetaverseDispatcher.AtEndOfFrame(() =>
            {
                if (!this) return;
                if (_currentAutoConnect == this)
                    _currentAutoConnect = null;
                if (debugLog)
                    MetaverseProgram.Logger.Log($"[SPUP AutoConnect] {SaveKey} Event ID: {e.ToUpperInvariant().Trim()}");
                switch (e.ToUpperInvariant().Trim())
                {
                    case "CLOSED":
                    case "OPEN_ERROR":
                    case "BT_DISCONNECT_TO_SERVERMODE":
                    case "LICENSE_ERROR":
                        _opening = false;
                        onDeviceClosed?.Invoke();
                        if (_currentAutoConnect == this)
                            _currentAutoConnect = null;
                        StopAllCoroutines();
                        if (HasSavedDevice && !IsInvoking(nameof(WatchConnection)))
                            Invoke(nameof(WatchConnection), WATCH_CONNECTION_INTERVAL);
                        break;
                    case "OPENED":
                        _opening = false;
                        onDeviceOpened?.Invoke();
                        if (_currentAutoConnect == this)
                            _currentAutoConnect = null;
                        FieldInfo openMethodField = null;
                        PropertyInfo serialNumberProperty = null;
                        PropertyInfo vendorIdProperty = null;
                        PropertyInfo portProperty = null;
                        PropertyInfo productIdProperty = null;
                        var openMethod = MetaverseSerialPortUtilityInterop.GetField<int>(serialPortUtilityPro,
                            ref openMethodField, MetaverseSerialPortUtilityInterop.GettableFieldID.OpenMethod);
                        var vendorId = MetaverseSerialPortUtilityInterop.GetProperty<string>(serialPortUtilityPro,
                            ref vendorIdProperty, MetaverseSerialPortUtilityInterop.GettablePropertyID.VendorID);
                        var port = MetaverseSerialPortUtilityInterop.GetProperty<string>(serialPortUtilityPro,
                            ref portProperty, MetaverseSerialPortUtilityInterop.GettablePropertyID.Port);
                        var productId = MetaverseSerialPortUtilityInterop.GetProperty<string>(serialPortUtilityPro,
                            ref productIdProperty, MetaverseSerialPortUtilityInterop.GettablePropertyID.ProductID);
                        var serialNumber = MetaverseSerialPortUtilityInterop.GetProperty<string>(serialPortUtilityPro,
                            ref serialNumberProperty,
                            MetaverseSerialPortUtilityInterop.GettablePropertyID.SerialNumber);
                        var deviceInfo = new MetaverseSerialPortUtilityInterop.DeviceInfo
                        {
                            SerialNumber = serialNumber,
                            Vendor = vendorId,
                            PortName = port,
                            Product = productId,
                            ParsedOpenSystem = (MetaverseSerialPortUtilityInterop.OpenSystem)openMethod
                        };
                        if (saveLastDevice && !string.IsNullOrEmpty(saveKey))
                        {
                            MetaverseProgram.Prefs.SetString(GetSaveKey(), deviceInfo.ToString());
                            onHasSavedDevice?.Invoke();
                            if (debugLog)
                                MetaverseProgram.Logger.Log($"[SPUP AutoConnect] Saved device info: {deviceInfo}");
                        }
                        else if (debugLog)
                        {
                            MetaverseProgram.Logger.Log(
                                $"[SPUP AutoConnect] Device opened: {deviceInfo} | SaveLastDevice: {saveLastDevice} | SaveKey: {saveKey}");
                        }

                        break;
                }
            });
        }

        private void OnEnable()
        {
            if (_deviceAPI.IsInitialized)
                WatchConnection();
        }

        private void OnDisable()
        {
            Close();
        }

        /// <summary>
        /// Disconnects from the device and clears the saved device info.
        /// </summary>
        public void DisconnectAndForget()
        {
            if (debugLog)
                MetaverseProgram.Logger.Log($"[SPUP AutoConnect] {saveKey}->DisconnectAndForget()");
            Close();
            if (!saveLastDevice || string.IsNullOrEmpty(saveKey)) return;
            MetaverseProgram.Prefs.DeleteKey(GetSaveKey());
            if (debugLog)
                MetaverseProgram.Logger.Log($"[SPUP AutoConnect] Cleared saved device info for: {saveKey}");
        }

        /// <summary>
        /// Closes the serial port connection.
        /// </summary>
        public void Close()
        {
            if (_currentAutoConnect == this)
                _currentAutoConnect = null;
            _opening = false;
            MethodInfo closeMethod = null;
            MetaverseSerialPortUtilityInterop.CallInstanceMethod(serialPortUtilityPro, ref closeMethod,
                MetaverseSerialPortUtilityInterop.InstanceMethodID.Close);
        }

        /// <summary>
        /// Automatically connect to the first device that matches the search criteria.
        /// </summary>
        public void AutoConnect()
        {
            MetaverseDispatcher.WaitForSeconds(AUTO_CONNECT_DELAY, () =>
            {
                if (!this || !isActiveAndEnabled)
                {
                    if (this && debugLog)
                        MetaverseProgram.Logger.Log(
                            "[SPUP AutoConnect] Canceled because the component is not enabled.");
                    return;
                }

                if (_currentAutoConnect != this && _currentAutoConnect)
                {
                    if (debugLog && !string.IsNullOrWhiteSpace(saveKey))
                        MetaverseProgram.Logger.Log(
                            $"[SPUP AutoConnect] {saveKey} Waiting to auto connect...");
                    MetaverseDispatcher.WaitUntil(
                        () => !this || !isActiveAndEnabled || !_currentAutoConnect || _currentAutoConnect == this, () =>
                        {
                            if (this && isActiveAndEnabled)
                                AutoConnect();
                        });
                    return;
                }

                if (serialPortUtilityPro is Behaviour { isActiveAndEnabled: false })
                {
                    if (!IsInvoking(nameof(WatchConnection)))
                        Invoke(nameof(WatchConnection), WATCH_CONNECTION_INTERVAL);
                    return;
                }

                if (debugLog && !string.IsNullOrEmpty(saveKey))
                    MetaverseProgram.Logger.Log($"[SPUP AutoConnect] {saveKey}->AutoConnect()");

                if (!_opening)
                {
                    if (!string.IsNullOrEmpty(saveKey))
                    {
                        var deviceInfoString = MetaverseProgram.Prefs.GetString(GetSaveKey(), string.Empty);
                        if (debugLog)
                        {
                            MetaverseProgram.Logger.Log(
                                !string.IsNullOrEmpty(deviceInfoString)
                                    ? $"[SPUP AutoConnect] Trying to open the saved device: {deviceInfoString}"
                                    : $"[SPUP AutoConnect] No saved device found for: {saveKey}");
                        }

                        if (!string.IsNullOrEmpty(deviceInfoString) &&
                            MetaverseSerialPortUtilityInterop.DeviceInfo.TryParse(deviceInfoString, out var dev) &&
                            dev.ParsedOpenSystem is not null)
                        {
                            if (debugLog)
                                MetaverseProgram.Logger.Log(
                                    $"[SPUP AutoConnect] Found a saved device OPEN: {dev.SerialNumber}");

                            _currentAutoConnect = this;
                            _opening = true;

                            _deviceAPI.Initialize(
                                serialPortUtilityPro,
                                dev.SerialNumber,
                                dev,
                                dev.ParsedOpenSystem.Value);
                            _deviceAPI.OnStoppedOpening.RemoveAllListeners();
                            _deviceAPI.OnStoppedOpening.AddListener(() =>
                            {
                                if (!_deviceAPI.IsThisDeviceOpened() && _opening)
                                    OnSerialPortMessage("OPEN_ERROR");
                                if (_currentAutoConnect == this)
                                    _currentAutoConnect = null;
                                _opening = false;
                            });
                            _deviceAPI.Open();
                            _lastConnectionAttemptTime = Time.unscaledTime;
                            return;
                        }
                    }
                }
                else if (!_deviceAPI.IsThisDeviceOpened())
                {
                    if (debugLog)
                    {
                        MetaverseProgram.Logger.Log($"[SPUP AutoConnect] Awaiting Open: {_lastConnectionAttemptTime:N2}(s)");
                        if (Time.unscaledTime - _lastConnectionAttemptTime > MAX_CONNECTION_TIMEOUT)
                        {
                            MetaverseProgram.Logger.Log(
                                $"[SPUP AutoConnect] Open timeout after {MAX_CONNECTION_TIMEOUT:N2}(s).");
                            Close();
                        }
                    }
                }
                
                if (string.IsNullOrEmpty(regexSearchString))
                {
                    if (!IsInvoking(nameof(WatchConnection)) && HasSavedDevice)
                        Invoke(nameof(WatchConnection), WATCH_CONNECTION_INTERVAL);
                    return;
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
                        MetaverseProgram.Logger.Log("[SPUP AutoConnect] Canceled because no devices are connected.");
                    if (!IsInvoking(nameof(WatchConnection)))
                        Invoke(nameof(WatchConnection), WATCH_CONNECTION_INTERVAL);
                    return;
                }

                if (debugLog)
                    MetaverseProgram.Logger.Log(
                        $"Connected Bluetooth Devices: {btDevices?.Length ?? 0} | " +
                        $"Connected USB Devices: {usbDevices?.Length ?? 0} | " +
                        $"Connected PCI Devices: {pciDevices?.Length ?? 0}");

                var reg = regexSearchString?.Replace("\\_", "_") ?? string.Empty;
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
                                 Regex.IsMatch(device.Item1.SerialNumber, reg) &&
                                 searchField.HasFlag(DeviceField.SerialNumber)) ||
                                (!string.IsNullOrWhiteSpace(device.Item1.Product) &&
                                 Regex.IsMatch(device.Item1.Product, reg) &&
                                 searchField.HasFlag(DeviceField.Product)) ||
                                (!string.IsNullOrWhiteSpace(device.Item1.PortName) &&
                                 Regex.IsMatch(device.Item1.PortName, reg) &&
                                 searchField.HasFlag(DeviceField.PortName)) ||
                                (!string.IsNullOrWhiteSpace(device.Item1.Vendor) &&
                                 Regex.IsMatch(device.Item1.Vendor, reg) &&
                                 searchField.HasFlag(DeviceField.Vendor))));

                if (deviceInfo.Item1 != null)
                {
                    if (debugLog)
                        MetaverseProgram.Logger.Log($"AutoConnect found a device: {deviceInfo.Item1.SerialNumber}");
                    _currentAutoConnect = this;
                    _deviceAPI.Initialize(
                        serialPortUtilityPro,
                        deviceInfo.Item1.SerialNumber,
                        deviceInfo.Item1,
                        deviceInfo.Item2);
                    _deviceAPI.Open();
                }
                else if (debugLog)
                    MetaverseProgram.Logger.Log(
                        $"AutoConnect did not find a device for: {regexSearchString} | {searchField} | {searchType}");

                if (!IsInvoking(nameof(WatchConnection)))
                    Invoke(nameof(WatchConnection), WATCH_CONNECTION_INTERVAL); 
            });
        }

        private void WatchConnection()
        {
            if (!_deviceAPI.IsThisDeviceOpened())
            {
                if (_deviceAPI.IsADeviceBeingOpened())
                {
                    if (!IsInvoking(nameof(WatchConnection)))
                        Invoke(nameof(WatchConnection), WATCH_CONNECTION_INTERVAL);
                }
                else if (!IsInvoking(nameof(AutoConnect)))
                    Invoke(nameof(AutoConnect), WATCH_CONNECTION_INTERVAL);
            }
            else if (!IsInvoking(nameof(WatchConnection)))
                Invoke(nameof(WatchConnection), WATCH_CONNECTION_INTERVAL);
        }

        private string GetSaveKey()
        {
            return $"{nameof(MetaverseSerialPortAutoConnect)}_{saveKey}";
        }
    }
}