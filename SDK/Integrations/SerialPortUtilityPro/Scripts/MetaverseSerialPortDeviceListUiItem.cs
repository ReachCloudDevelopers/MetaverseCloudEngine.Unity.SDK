using System;
using System.Linq;
using System.Reflection;
using MetaverseCloudEngine.Unity.Async;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.SPUP
{
    public class MetaverseSerialPortDeviceListUiItem : MonoBehaviour
    {
        public UnityEvent<string> onDeviceName;
        public UnityEvent onStartedOpening;
        public UnityEvent onStoppedOpening;
        public UnityEvent onDeviceOpen;
        public UnityEvent onDeviceClosed;
        
        private Component _spup;
        private MetaverseSerialPortUtilityInterop.DeviceInfo _data;
        private MetaverseSerialPortUtilityInterop.OpenSystem _openSystem;

        private static bool _opening;
        private static FieldInfo _openMethodField;
        private static PropertyInfo _deviceNameProperty;
        private static PropertyInfo _vendorIdProperty;
        private static PropertyInfo _productIdProperty;
        private static PropertyInfo _serialNumberProperty;
        private static PropertyInfo _portProperty;
        private static MethodInfo _openMethod;
        private static MethodInfo _closeMethod;
        private static MethodInfo _isOpenProcessingMethod;

        private static MethodInfo _isOpenedMethod;

        public void Repaint(
            Component spu,
            string device, 
            MetaverseSerialPortUtilityInterop.DeviceInfo data, 
            MetaverseSerialPortUtilityInterop.OpenSystem openSystem)
        {
            _spup = spu;
            _data = data;
            _openSystem = openSystem;
            onDeviceName?.Invoke(device);
            RepaintOpenedState();
        }

        public void RepaintOpenedState()
        {
            if (!this)
                return;
            
            if (_data == null)
            {
                onDeviceClosed?.Invoke();
                return;
            }

            if (IsThisDeviceOpened())
                onDeviceOpen?.Invoke();
            else onDeviceClosed?.Invoke();
        }

        public void Open()
        {
            if (!this)
                return;

            if (_opening)
            {
                AlreadyOpening();
                return;
            }

            if (!_spup)
            {
                SpupDestroyed();
                return;
            }

            _opening = true;
            onStartedOpening?.Invoke();

            try
            {
                CloseSerial();
                MetaverseProgram.Logger.Log("Closing device...");
                RepaintOpenedState();
                
                var timeout = DateTime.UtcNow.AddSeconds(15);
                MetaverseDispatcher.WaitUntil(() => !this || !_spup || !_opening || DateTime.UtcNow > timeout || (!IsOpened() && !IsThisDeviceOpened()), () =>
                {
                    if (!this || !_spup || !_opening)
                    {
                        if (this && _opening)
                            OpenFailed();
                        _opening = false;
                        return;
                    }
                    
                    if (DateTime.UtcNow > timeout)
                    {
                        MetaverseProgram.Logger.Log("Device close timeout");
                        OpenFailed();
                        return;
                    }
                    
                    RepaintOpenedState();
                    MetaverseProgram.Logger.Log("Device closed");
                    OpenInternal();
                });
            }
            catch (Exception e)
            {
                if (this && _opening)
                    OpenFailed();
                _opening = false;
                MetaverseProgram.Logger.Log("Device close error: " + e.Message);
            }
        }

        private static void SpupDestroyed()
        {
#if METAVERSE_CLOUD_ENGINE_INTERNAL
            MetaverseProgram.RuntimeServices.InternalNotificationManager.ShowDialog(
                "Device Open Error", 
                "The device serial communication was destroyed.", 
                "OK");
#endif
        }

        private static void AlreadyOpening()
        {
#if METAVERSE_CLOUD_ENGINE_INTERNAL
            MetaverseProgram.RuntimeServices.InternalNotificationManager.ShowDialog(
                "Device Open Error", 
                "A device serial communication is already being opened.", 
                "OK");
#endif
        }

        private void OpenInternal()
        {
            if (!this || !_spup || !_opening)
            {
                MetaverseProgram.Logger.Log("Device open cancelled");
                if (this && _opening)
                    OpenFailed();
                _opening = false;
                return;
            }

            try
            {
                static bool IsHexString(string s) => !string.IsNullOrEmpty(s) && s.All(Uri.IsHexDigit);

                MetaverseSerialPortUtilityInterop.SetField(_spup, ref _openMethodField, "OpenMethod", (int)_openSystem);
                MetaverseSerialPortUtilityInterop.SetProperty(_spup, ref _vendorIdProperty, "VendorID", _data.Vendor);
                MetaverseSerialPortUtilityInterop.SetProperty(_spup, ref _portProperty, "Port", _data.PortName ?? "");
                MetaverseSerialPortUtilityInterop.SetProperty(_spup, ref _productIdProperty, "ProductID", _data.Product);
                if (_openSystem is MetaverseSerialPortUtilityInterop.OpenSystem.Usb or MetaverseSerialPortUtilityInterop.OpenSystem.Pci)
                {
                    if (!IsHexString(_data.Product)) MetaverseSerialPortUtilityInterop.SetProperty(_spup, ref _productIdProperty, "ProductID", "");
                    if (!IsHexString(_data.Vendor)) MetaverseSerialPortUtilityInterop.SetProperty(_spup, ref _vendorIdProperty, "VendorID", "");
                }
                MetaverseSerialPortUtilityInterop.SetProperty(_spup, ref _serialNumberProperty, "SerialNumber", _data.SerialNumber);
                MetaverseSerialPortUtilityInterop.SetProperty(_spup, ref _deviceNameProperty, "DeviceName", string.IsNullOrEmpty(_data.SerialNumber) 
                    ? _data.Vendor
                    : _data.SerialNumber);
            }
            catch (Exception e)
            {
                MetaverseProgram.Logger.Log("Device open error: " + e.Message);
                OpenFailed();
                return;
            }

            MetaverseProgram.Logger.Log("Specified device to open: VID:" + _data.Vendor + " PID:" + _data.Product + " SER:" + _data.SerialNumber + " PORT:" + _data.PortName);

            var timeout = DateTime.UtcNow.AddSeconds(15);
            MetaverseDispatcher.WaitUntil(
                () => !this || !_spup || 
                      !IsOpenProcessing()
                      || !_opening || DateTime.UtcNow > timeout, 
                () =>
                {
                    if (!this)
                    {
                        _opening = false;
                        return;
                    }

                    if (!_opening)
                    {
                        OpenFailed(false);
                        return;
                    }
                
                    if (DateTime.UtcNow > timeout)
                    {
                        OpenFailed();
                        return;
                    }

                    MetaverseProgram.Logger.Log("Opening device");

                    OpenSerial();
                    
                    timeout = DateTime.UtcNow.AddSeconds(15);
                    MetaverseDispatcher.WaitUntil(() =>
                        !this ||
                        !_spup ||
                        IsThisDeviceOpened() ||
                        IsOpenProcessing() ||
                        !_opening ||
                        DateTime.UtcNow > timeout, () =>
                    {
                        if (!this)
                        {
                            _opening = false;
                            return;
                        }
                        
                        if (!_opening)
                        {
                            OpenFailed(false);
                            return;
                        }
                        
                        if (DateTime.UtcNow > timeout)
                        {
                            MetaverseProgram.Logger.Log("Device open timeout");
                            OpenFailed();
                            return;
                        }
                        
                        MetaverseProgram.Logger.Log("Device processing finished");
                        
                        timeout = DateTime.UtcNow.AddSeconds(15);
                        MetaverseDispatcher.WaitUntil(
                            () => !this || 
                                  !_spup ||
                                  !IsOpenProcessing() || 
                                  !_opening ||
                                  DateTime.UtcNow > timeout ||
                                  IsThisDeviceOpened(),
                            () =>
                            {
                                if (!this)
                                {
                                    _opening = false;
                                    return;
                                }

                                if (!_opening)
                                {
                                    OpenFailed(false);
                                    return;
                                }
                    
                                if (DateTime.UtcNow > timeout)
                                {
                                    MetaverseProgram.Logger.Log("Device open timeout");
                                    OpenFailed();
                                    return;
                                }

                                if (IsThisDeviceOpened())
                                {
                                    onStoppedOpening?.Invoke();
                                    onDeviceOpen?.Invoke();
                                    MetaverseProgram.Logger.Log("Device opened.");
                                    _opening = false;
                                }
                                else
                                {
                                    MetaverseProgram.Logger.Log("Serial Number: " + GetSerialNumber() + " Expected Serial Number: " + _data.SerialNumber + " Opened: " + IsOpened());
                                    OpenFailed();
                                }
                            });
                    });
                });
        }

        private void OpenSerial()
        {
            MetaverseSerialPortUtilityInterop.CallInstanceMethod(_spup, ref _openMethod, "Open");
        }

        private void CloseSerial()
        {
            MetaverseSerialPortUtilityInterop.CallInstanceMethod(_spup, ref _closeMethod, "Close");
        }

        private void OpenFailed(bool showDialog = true)
        {
            if (this && _opening)
                onStoppedOpening?.Invoke();
            RepaintOpenedState();
            _opening = false;
#if METAVERSE_CLOUD_ENGINE_INTERNAL
            if (showDialog && !IsThisDeviceOpened())
                MetaverseProgram.RuntimeServices.InternalNotificationManager.ShowDialog(
                    "Device Open Error", 
                    "Failed to open device", 
                    "OK");
#endif
        }

        private bool IsThisDeviceOpened()
        {
            return GetSerialNumber()?.ToLower() == _data.SerialNumber?.ToLower() && IsOpened();
        }

        private bool IsOpenProcessing()
        {
            return _spup &&
                   MetaverseSerialPortUtilityInterop.CallInstanceMethod<bool>(_spup, ref _isOpenProcessingMethod,
                       "IsOpenProcessing");
        }

        private string GetSerialNumber()
        {
            return !_spup
                ? null
                : MetaverseSerialPortUtilityInterop.GetProperty<string>(_spup, ref _serialNumberProperty,
                    "SerialNumber");
        }

        private bool IsOpened()
        {
            return _spup &&
                   MetaverseSerialPortUtilityInterop.CallInstanceMethod<bool>(_spup, ref _isOpenedMethod, "IsOpened");
        }
    }
}