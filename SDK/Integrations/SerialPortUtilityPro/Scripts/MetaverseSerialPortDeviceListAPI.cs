﻿using System;
using System.Reflection;
using MetaverseCloudEngine.Unity.Async;
using TriInspectorMVCE;
using UnityEngine;
#if UNITY_ANDROID || UNITY_EDITOR
using UnityEngine.Android;
#endif
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.SPUP
{
    public class MetaverseSerialPortDeviceListAPI : TriInspectorMonoBehaviour
    {
        [Required] [SerializeField] private Component serialPortUtilityPro;
        [SerializeField] private bool detectDevicesOnStart;
        public UnityEvent onAnyDeviceFound = new();
        public UnityEvent onNoDevicesFound = new();
        public UnityEvent onSerialPortClosed = new();
        public UnityEvent onSerialPortOpened = new();
        public UnityEvent<string, MetaverseSerialPortUtilityInterop.DeviceInfo, MetaverseSerialPortUtilityInterop.OpenSystem> onDeviceFound = new();

        private bool _listAfterPermissionsGranted;
        private MethodInfo _isOpenProcessingMethod;
        private UnityAction<object, string> _onSystemEventCallback;
        private FieldInfo _onSystemEventField;
        private string _bufferedEvent;
        private bool? _isOpened;

        private void Reset()
        {
            MetaverseSerialPortUtilityInterop.EnsureComponent(ref serialPortUtilityPro, gameObject);
        }

        private void OnValidate()
        {
            MetaverseSerialPortUtilityInterop.EnsureComponent(ref serialPortUtilityPro, gameObject);
        }

        private void Awake()
        {
            if (serialPortUtilityPro)
                MetaverseSerialPortUtilityInterop.AddSystemEventCallback(serialPortUtilityPro, ref _onSystemEventField, OnSystemEventCallback, ref _onSystemEventCallback);
        }

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(_bufferedEvent))
                return;
            var e = _bufferedEvent;
            _bufferedEvent = null;
            OnSystemEventCallback(serialPortUtilityPro, e);
        }

        private void Start()
        {
            if (detectDevicesOnStart)
                ListDevices();
        }

        private void OnDestroy()
        {
            if (serialPortUtilityPro)
                MetaverseSerialPortUtilityInterop.RemoveSystemEventCallback(serialPortUtilityPro, ref _onSystemEventField, _onSystemEventCallback);
        }

        private void OnSystemEventCallback(object sender, string e)
        {
            MetaverseDispatcher.AtEndOfFrame(() =>
            {
                if (!this || !isActiveAndEnabled)
                {
                    _bufferedEvent = e;
                    return;
                }
                switch (e.ToUpper())
                {
                    case "OPENED":
                        if (_isOpened is null || !_isOpened.Value)
                            onSerialPortOpened?.Invoke();
                        _isOpened = true;
                        break;
                    case "CLOSED":
                    case "OPEN_ERROR":
                    case "BT_DISCONNECT_TO_SERVERMODE":
                    case "LICENSE_ERROR":
                        if (_isOpened is null || _isOpened.Value)
                            onSerialPortClosed?.Invoke();
                        _isOpened = false;
                        break;
                }
            });
        }

        public void ListDevices()
        {
            if (!this)
                return;

            if (_listAfterPermissionsGranted)
                return;

#if UNITY_2020_2_OR_NEWER
#if UNITY_ANDROID || UNITY_EDITOR
            if (Application.platform == RuntimePlatform.Android && !MVUtils.IsVRCompatible())
            {
                MVUtils.RequestUsbPermissions();

                if (!Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_SCAN")
                    || !Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_ADVERTISE")
                    || !Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_CONNECT"))
                {
                    var callbacks = new PermissionCallbacks();
                    callbacks.PermissionGranted += OnCallbacksOnPermissionGranted;
                
                    Permission.RequestUserPermissions(new[] {
                        "android.permission.BLUETOOTH_SCAN",
                        "android.permission.BLUETOOTH_ADVERTISE",
                        "android.permission.BLUETOOTH_CONNECT"
                    }, callbacks);
                    return;
                }
            }
#endif
#endif
            
            MetaverseDispatcher.WaitForSeconds(0.1f, () =>
            {
                MetaverseDispatcher.WaitUntil(
                () => !MetaverseSerialPortUtilityInterop.CallInstanceMethod<bool>(serialPortUtilityPro, ref _isOpenProcessingMethod, MetaverseSerialPortUtilityInterop.InstanceMethodID.IsOpenProcessing),
                () =>
                {
                    _listAfterPermissionsGranted = false;
                    
                    var foundDevices = false;
                    var systems = (MetaverseSerialPortUtilityInterop.OpenSystem[])Enum.GetValues(typeof(MetaverseSerialPortUtilityInterop.OpenSystem));
                    foreach (var type in systems)
                    {
                        if (type != MetaverseSerialPortUtilityInterop.OpenSystem.BluetoothSsp &&
                            type != MetaverseSerialPortUtilityInterop.OpenSystem.Usb && 
                            type != MetaverseSerialPortUtilityInterop.OpenSystem.Pci)
                            continue;
                        
                        var devices = MetaverseSerialPortUtilityInterop.GetConnectedDeviceList(type);
                        if (devices is null || devices.Length == 0)
                            continue;

                        if (!foundDevices)
                        {
                            onAnyDeviceFound?.Invoke();
                            foundDevices = true;
                        }
                
                        foreach (var device in devices)
                        {
                            if (device is null)
                                continue;
                            
                            if (string.IsNullOrEmpty(device.Vendor) && 
                                string.IsNullOrEmpty(device.Product) && 
                                string.IsNullOrEmpty(device.SerialNumber) && 
                                string.IsNullOrEmpty(device.PortName))
                                continue;
                            
                            var deviceNameText = $"TYPE: {type}";
                            if (!string.IsNullOrEmpty(device.Vendor))
                                deviceNameText += $", VID: {device.Vendor} ";
                            if (!string.IsNullOrEmpty(device.Product))
                                deviceNameText += $", PID: {device.Product} ";
                            if (!string.IsNullOrEmpty(device.SerialNumber))
                                deviceNameText += $", SER: {device.SerialNumber}";
                            if (!string.IsNullOrEmpty(device.PortName))
                                deviceNameText += $", PORT: {device.PortName}";

                            if (/*!Application.isMobilePlatform && */!string.IsNullOrEmpty(device.Product) && int.TryParse(device.Product, out var productId))
                                device.Product = productId.ToString("X");

                            onDeviceFound?.Invoke(deviceNameText, device, type);
                        }
                    }
                    
                    if (!foundDevices)
                        onNoDevicesFound?.Invoke();
                });
            });
        }

        private void OnCallbacksOnPermissionGranted(string permission)
        {
            if (!this) return;
            MetaverseProgram.Logger.Log("Permissions Granted: " + permission);
            ListDevices();
            _listAfterPermissionsGranted = true;
        }
    }
}