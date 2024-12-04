using System;
using System.Linq;
using System.Reflection;
using MetaverseCloudEngine.Unity.Async;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.SPUP
{
    public class MetaverseSerialPortDeviceAPI : IDisposable
    {
        public readonly UnityEvent<string> OnDeviceName = new();
        public readonly UnityEvent OnStartedOpening = new();
        public readonly UnityEvent OnStoppedOpening = new();
        public readonly UnityEvent OnDeviceOpen = new();
        public readonly UnityEvent OnDeviceClosed = new();
        
        private Component _spup;
        private MetaverseSerialPortUtilityInterop.DeviceInfo _data;
        private MetaverseSerialPortUtilityInterop.OpenSystem _openSystem;
        private bool _isDisposed;

        private static MetaverseSerialPortDeviceAPI _opening;
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
        
        public bool IsInitialized => _spup && _data != null;

        /// <summary>
        /// Initializes the device with the specified data.
        /// </summary>
        /// <param name="spu">The serial port utility component.</param>
        /// <param name="displayName">The device name.</param>
        /// <param name="data">The device information.</param>
        /// <param name="openSystem">The open system.</param>
        public void Initialize(
            Component spu,
            string displayName, 
            MetaverseSerialPortUtilityInterop.DeviceInfo data, 
            MetaverseSerialPortUtilityInterop.OpenSystem openSystem)
        {
            ResetAll();
            _spup = spu;
            _data = data;
            _openSystem = openSystem;
            MetaverseDispatcher.AtEndOfFrame(() =>
            {
                OnDeviceName?.Invoke(displayName);
                CheckOpened(); 
            });
        }

        /// <summary>
        /// Checks if the device is opened or closed, and invokes the appropriate events.
        /// </summary>
        /// <param name="forceClosed">If true, the device is considered closed.</param>
        public void CheckOpened(bool forceClosed = false)
        {
            if (_isDisposed)
            {
                MetaverseProgram.Logger.Log("Device is disposed, please call Initialize() again.");
                return;
            }

            if (_data == null)
            {
                OnDeviceClosed?.Invoke();
                return;
            }

            if (IsThisDeviceOpened() && !forceClosed)
                OnDeviceOpen?.Invoke();
            else OnDeviceClosed?.Invoke();
        }

        /// <summary>
        /// Opens the device.
        /// </summary>
        public void Open()
        {
            if (_isDisposed)
            {
                MetaverseProgram.Logger.Log("Device is disposed, please call Initialize() again.");
                return;
            }

            if (_opening != null)
            {
                AlreadyOpening();
                return;
            }

            if (!_spup)
            {
                SpupDestroyed();
                return;
            }

            _opening = this;
            OnStartedOpening?.Invoke();

            try
            {
                CloseSerial();
                MetaverseProgram.Logger.Log("Closing device...");
                
                var timeout = DateTime.UtcNow.AddSeconds(15);
                MetaverseDispatcher.WaitUntil(() => _isDisposed || !_spup || _opening != this || DateTime.UtcNow > timeout || (!IsAnyDeviceOpened() && !IsThisDeviceOpened()), () =>
                {
                    if (_isDisposed || !_spup || _opening != this)
                    {
                        if (!_isDisposed && _opening == this)
                            OpenFailed();
                        return;
                    }
                    
                    if (DateTime.UtcNow > timeout)
                    {
                        MetaverseProgram.Logger.Log("Device close timeout");
                        OpenFailed();
                        return;
                    }
                    
                    MetaverseProgram.Logger.Log("Device closed");
                    OpenInternal();
                });
            }
            catch (Exception e)
            {
                if (!_isDisposed && _opening == this)
                    OpenFailed();
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
            if (_isDisposed || !_spup || _opening != this)
            {
                MetaverseProgram.Logger.Log("Device open cancelled");
                if (!_isDisposed && _opening == this)
                    OpenFailed();
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
                () => _isDisposed || !_spup || 
                      !IsADeviceBeingOpened()
                      || _opening != this || DateTime.UtcNow > timeout, 
                () =>
                {
                    if (_isDisposed)
                    {
                        return;
                    }

                    if (_opening != this)
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
                        _isDisposed ||
                        !_spup ||
                        IsThisDeviceOpened() ||
                        IsADeviceBeingOpened() ||
                        _opening != this ||
                        DateTime.UtcNow > timeout, () =>
                    {
                        if (_isDisposed)
                            return;
                        
                        if (_opening != this)
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
                        
                        timeout = DateTime.UtcNow.AddSeconds(5);
                        MetaverseDispatcher.WaitUntil(
                            () => _isDisposed || 
                                  !_spup ||
                                  !IsADeviceBeingOpened() || 
                                  _opening != this ||
                                  DateTime.UtcNow > timeout ||
                                  IsThisDeviceOpened(),
                            () =>
                            {
                                if (_isDisposed)
                                    return;

                                if (_opening != this)
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
                                    OnStoppedOpening?.Invoke();
                                    OnDeviceOpen?.Invoke();
                                    MetaverseProgram.Logger.Log("Device opened.");
                                    if (_opening == this)
                                        _opening = null;
                                }
                                else
                                {
                                    MetaverseProgram.Logger.Log("Serial Number: " + GetSerialNumber() + " Expected Serial Number: " + _data.SerialNumber + " Opened: " + IsAnyDeviceOpened());
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
            if (!_isDisposed && _opening == this)
                OnStoppedOpening?.Invoke();
            if (_opening == this)
                _opening = null;
#if METAVERSE_CLOUD_ENGINE_INTERNAL
            if (showDialog && !IsThisDeviceOpened())
                MetaverseProgram.RuntimeServices.InternalNotificationManager.ShowDialog(
                    "Device Open Error", 
                    "Failed to open device", 
                    "OK");
#endif
        }

        public bool IsThisDeviceOpened()
        {
            if (_data == null) return false;
            var serNum = _data.SerialNumber;
            if (string.IsNullOrEmpty(serNum))
                serNum = _data.Vendor;
            return GetSerialNumber() == serNum && IsAnyDeviceOpened();
        }

        public bool IsADeviceBeingOpened()
        {
            return _spup &&
                   MetaverseSerialPortUtilityInterop.CallInstanceMethod<bool>(_spup, ref _isOpenProcessingMethod,
                       "IsOpenProcessing");
        }

        public bool IsAnyDeviceOpened()
        {
            return _spup &&
                   MetaverseSerialPortUtilityInterop.CallInstanceMethod<bool>(_spup, ref _isOpenedMethod, "IsOpened");
        }

        private string GetSerialNumber()
        {
            return !_spup
                ? null
                : MetaverseSerialPortUtilityInterop.GetProperty<string>(_spup, ref _serialNumberProperty,
                    "SerialNumber");
        }

        private void ResetAll()
        {
            _spup = null;
            _data = null;
            _openSystem = 0;
            _isDisposed = false;
            if (_opening == this)
            {
                _opening = null;
                OpenFailed(false);
            }
            OnDeviceName.RemoveAllListeners();
            OnStartedOpening.RemoveAllListeners();
            OnStoppedOpening.RemoveAllListeners();
            OnDeviceOpen.RemoveAllListeners();
            OnDeviceClosed.RemoveAllListeners();
        }

        public void Dispose()
        {
            ResetAll();
            _isDisposed = true;
        }

        ~MetaverseSerialPortDeviceAPI()
        {
            Dispose();
        }
    }
}
