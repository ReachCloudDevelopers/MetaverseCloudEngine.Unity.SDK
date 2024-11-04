﻿using System;
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
        [InfoBox("You can use https://regexr.com/ to test your regex.")]
        [SerializeField] private string regexSearchString = "<Enter Search String>";
        [SerializeField] private DeviceField searchField = (DeviceField)(-1);
        [SerializeField] private DeviceType searchType = (DeviceType)(-1);

        private FieldInfo _isAutoOpenField;
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
            MetaverseSerialPortUtilityInterop.SetField(serialPortUtilityPro, ref _isAutoOpenField, "IsAutoOpen", true);
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
            MetaverseDispatcher.AtEndOfFrame(() =>
            {
                if (!this || !isActiveAndEnabled) return;
                var btDevices = MetaverseSerialPortUtilityInterop.GetConnectedDeviceList(
                    MetaverseSerialPortUtilityInterop.OpenSystem.BluetoothSsp);
                var usbDevices = MetaverseSerialPortUtilityInterop.GetConnectedDeviceList(
                    MetaverseSerialPortUtilityInterop.OpenSystem.Usb);
                var pciDevices = MetaverseSerialPortUtilityInterop.GetConnectedDeviceList(
                    MetaverseSerialPortUtilityInterop.OpenSystem.Pci);

                var deviceInfo =
                    Array.Empty<(MetaverseSerialPortUtilityInterop.DeviceInfo,
                            MetaverseSerialPortUtilityInterop.OpenSystem)>()
                        .Concat(searchType.HasFlag(DeviceType.Bluetooth)
                            ? btDevices.Select(x => (x, MetaverseSerialPortUtilityInterop.OpenSystem.BluetoothSsp))
                            : Array
                                .Empty<(MetaverseSerialPortUtilityInterop.DeviceInfo,
                                    MetaverseSerialPortUtilityInterop.OpenSystem)>())
                        .Concat(searchType.HasFlag(DeviceType.Usb)
                            ? usbDevices.Select(x => (x, MetaverseSerialPortUtilityInterop.OpenSystem.Usb))
                            : Array
                                .Empty<(MetaverseSerialPortUtilityInterop.DeviceInfo,
                                    MetaverseSerialPortUtilityInterop.OpenSystem)>())
                        .Concat(searchType.HasFlag(DeviceType.Pci)
                            ? pciDevices.Select(x => (x, MetaverseSerialPortUtilityInterop.OpenSystem.Pci))
                            : Array
                                .Empty<(MetaverseSerialPortUtilityInterop.DeviceInfo,
                                    MetaverseSerialPortUtilityInterop.OpenSystem)>())
                        .ToArray()
                        .FirstOrDefault(device =>
                            (Regex.IsMatch(device.Item1.SerialNumber, regexSearchString) &&
                             searchField.HasFlag(DeviceField.SerialNumber)) ||
                            (Regex.IsMatch(device.Item1.Product, regexSearchString) &&
                             searchField.HasFlag(DeviceField.Product)) ||
                            (Regex.IsMatch(device.Item1.PortName, regexSearchString) &&
                             searchField.HasFlag(DeviceField.PortName)) ||
                            (Regex.IsMatch(device.Item1.Vendor, regexSearchString) &&
                             searchField.HasFlag(DeviceField.Vendor)));

                if (deviceInfo.Item1 != null)
                {
                    _deviceAPI.Initialize(
                        serialPortUtilityPro,
                        deviceInfo.Item1.SerialNumber,
                        deviceInfo.Item1,
                        deviceInfo.Item2);
                    _deviceAPI.Open();

                    if (!IsInvoking(nameof(WatchConnection)))
                        Invoke(nameof(WatchConnection), 1f);
                }
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
                {
                    Invoke(nameof(AutoConnect), 1f);
                }
            }
            else
            {
                if (!IsInvoking(nameof(WatchConnection)))
                    Invoke(nameof(WatchConnection), 5f);
            }
        }
    }
}