using System;
using System.Linq;
using System.Reflection;
using MetaverseCloudEngine.Unity.Async;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.SPUP
{
    /// <summary>
    /// Represents a device that can be opened and closed using the Metaverse Serial Port Utility.
    /// </summary>
    public class MetaverseSerialPortDeviceAPI : IDisposable
    {
        public bool loggingEnabled;
        [Tooltip("The serial port utility component.")]
        public readonly UnityEvent<string> OnDeviceName = new();
        [Tooltip("Invoked when the device starts opening.")]
        public readonly UnityEvent OnStartedOpening = new();
        [Tooltip("Invoked when the device stops opening.")]
        public readonly UnityEvent OnStoppedOpening = new();
        [Tooltip("Invoked when the device is opened.")]
        public readonly UnityEvent OnDeviceOpen = new();
        [Tooltip("Invoked when the device is closed.")]
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
        
        /// <summary>
        /// Gets a value indicating whether the device is initialized.
        /// </summary>
        public bool IsInitialized => _spup && _data != null;
        
        /// <summary>
        /// Gets the device string.
        /// </summary>
        public string DeviceString => $"{(_data?.ToString() ?? "")},{_openSystem}";

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
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
                OnDeviceName?.Invoke(_openSystem == MetaverseSerialPortUtilityInterop.OpenSystem.BluetoothSsp
                    ? WindowsComPortDeviceNameResolver.GetDeviceName(_data.PortName) ?? displayName
                    : displayName);
#else
                OnDeviceName?.Invoke(displayName);
#endif
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
                if (loggingEnabled)
                    MetaverseProgram.Logger.Log("[MetaverseSerialPortDeviceAPI] Device is disposed, please call Initialize() again.");
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
                if (loggingEnabled)
                    MetaverseProgram.Logger.Log("[MetaverseSerialPortDeviceAPI] Device is disposed, please call Initialize() again.");
                return;
            }

            if (_opening != null)
            {
                if (loggingEnabled)
                    MetaverseProgram.Logger.Log("[MetaverseSerialPortDeviceAPI] Another device is being opened, please wait. (" + (_opening?.DeviceString ?? "unknown") + ")");
                return;
            }

            if (!_spup)
            {
                if (loggingEnabled)
                    MetaverseProgram.Logger.Log("[MetaverseSerialPortDeviceAPI] Serial Port Utility component is not assigned.");
                return;
            }

            _opening = this;
            OnStartedOpening?.Invoke();

            try
            {
                CloseSerial();
                if (loggingEnabled)
                    MetaverseProgram.Logger.Log("[MetaverseSerialPortDeviceAPI] Closing device...");
                
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
                        if (loggingEnabled)
                            MetaverseProgram.Logger.Log("[MetaverseSerialPortDeviceAPI] Device close timeout");
                        OpenFailed();
                        return;
                    }
                    
                    if (loggingEnabled)
                        MetaverseProgram.Logger.Log("[MetaverseSerialPortDeviceAPI] Device closed");
                    OpenInternal();
                });
            }
            catch (Exception e)
            {
                if (!_isDisposed && _opening == this)
                    OpenFailed();
                if (loggingEnabled)
                    MetaverseProgram.Logger.Log($"[MetaverseSerialPortDeviceAPI] Device close error: {e.Message}");
            }
        }

        /// <summary>
        /// Checks if this device is opened.
        /// </summary>
        /// <returns>A value indicating whether this device is opened.</returns>
        public bool IsThisDeviceOpened()
        {
            if (_data == null) return false;
            var serNum = _data.SerialNumber;
            if (string.IsNullOrEmpty(serNum))
                serNum = _data.Vendor;
            return GetSerialNumber() == serNum && IsAnyDeviceOpened();
        }

        /// <summary>
        /// Checks if any device is *being* opened.
        /// </summary>
        /// <returns>True if any device is being opened, otherwise false.</returns>
        public bool IsADeviceBeingOpened()
        {
            return _spup &&
                   MetaverseSerialPortUtilityInterop.CallInstanceMethod<bool>(_spup, ref _isOpenProcessingMethod, 
                       MetaverseSerialPortUtilityInterop.InstanceMethodID.IsOpenProcessing);
        }

        /// <summary>
        /// Checks if any device is opened.
        /// </summary>
        /// <returns>True if any device is opened, otherwise false.</returns>
        public bool IsAnyDeviceOpened()
        {
            return _spup &&
                   MetaverseSerialPortUtilityInterop.CallInstanceMethod<bool>(_spup, ref _isOpenedMethod, 
                       MetaverseSerialPortUtilityInterop.InstanceMethodID.IsOpened);
        }

        /// <summary>
        /// Disposes the device.
        /// </summary>
        public void Dispose()
        {
            ResetAll();
            _isDisposed = true;
        }

        private void OpenInternal()
        {
            if (_isDisposed || !_spup || _opening != this)
            {
                if (loggingEnabled)
                    MetaverseProgram.Logger.Log("[MetaverseSerialPortDeviceAPI] Device open cancelled");
                if (!_isDisposed && _opening == this)
                    OpenFailed();
                return;
            }

            try
            {
                static bool IsHexString(string s) => !string.IsNullOrEmpty(s) && s.All(Uri.IsHexDigit);

                MetaverseSerialPortUtilityInterop.SetField(_spup, ref _openMethodField, MetaverseSerialPortUtilityInterop.SettableFieldID.OpenMethod, (int)_openSystem);
                MetaverseSerialPortUtilityInterop.SetProperty(_spup, ref _vendorIdProperty, MetaverseSerialPortUtilityInterop.SettablePropertyID.VendorID, _data.Vendor);
                MetaverseSerialPortUtilityInterop.SetProperty(_spup, ref _portProperty, MetaverseSerialPortUtilityInterop.SettablePropertyID.Port, _data.PortName ?? string.Empty);
                MetaverseSerialPortUtilityInterop.SetProperty(_spup, ref _productIdProperty, MetaverseSerialPortUtilityInterop.SettablePropertyID.ProductID, _data.Product);
                if (_openSystem is MetaverseSerialPortUtilityInterop.OpenSystem.Usb or MetaverseSerialPortUtilityInterop.OpenSystem.Pci)
                {
                    if (!IsHexString(_data.Product)) MetaverseSerialPortUtilityInterop.SetProperty(_spup, ref _productIdProperty, MetaverseSerialPortUtilityInterop.SettablePropertyID.ProductID, string.Empty);
                    if (!IsHexString(_data.Vendor)) MetaverseSerialPortUtilityInterop.SetProperty(_spup, ref _vendorIdProperty, MetaverseSerialPortUtilityInterop.SettablePropertyID.VendorID, string.Empty);
                }
                MetaverseSerialPortUtilityInterop.SetProperty(_spup, ref _serialNumberProperty, MetaverseSerialPortUtilityInterop.SettablePropertyID.SerialNumber, _data.SerialNumber);
                MetaverseSerialPortUtilityInterop.SetProperty(_spup, ref _deviceNameProperty, MetaverseSerialPortUtilityInterop.SettablePropertyID.DeviceName, string.IsNullOrEmpty(_data.SerialNumber) 
                    ? _data.Vendor
                    : _data.SerialNumber);
            }
            catch (Exception e)
            {
                if (loggingEnabled)
                    MetaverseProgram.Logger.Log($"[MetaverseSerialPortDeviceAPI] Device open error: {e.Message}");
                OpenFailed();
                return;
            }

            if (loggingEnabled)
                MetaverseProgram.Logger.Log(
                    $"[MetaverseSerialPortDeviceAPI] Specified device to open:-VID:{_data.Vendor},PID:{_data.Product},SER:{_data.SerialNumber},PORT:{_data.PortName}");

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
                        OpenFailed();
                        return;
                    }
                
                    if (DateTime.UtcNow > timeout)
                    {
                        OpenFailed();
                        return;
                    }

                    if (loggingEnabled)
                        MetaverseProgram.Logger.Log("[MetaverseSerialPortDeviceAPI] Opening device");

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
                            OpenFailed();
                            return;
                        }
                        
                        if (DateTime.UtcNow > timeout)
                        {
                            if (loggingEnabled)
                                MetaverseProgram.Logger.Log("[MetaverseSerialPortDeviceAPI] Device open timeout");
                            OpenFailed();
                            return;
                        }
                        
                        if (loggingEnabled)
                            MetaverseProgram.Logger.Log("[MetaverseSerialPortDeviceAPI] Device processing finished");
                        
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
                                    OpenFailed();
                                    return;
                                }
                    
                                if (DateTime.UtcNow > timeout)
                                {
                                    if (loggingEnabled)
                                        MetaverseProgram.Logger.Log("[MetaverseSerialPortDeviceAPI] Device open timeout");
                                    OpenFailed();
                                    return;
                                }

                                if (IsThisDeviceOpened())
                                {
                                    OnStoppedOpening?.Invoke();
                                    OnDeviceOpen?.Invoke();
                                    if (loggingEnabled)
                                        MetaverseProgram.Logger.Log("[MetaverseSerialPortDeviceAPI] Device opened.");
                                    if (_opening == this)
                                        _opening = null;
                                }
                                else
                                {
                                    if (loggingEnabled)
                                        MetaverseProgram.Logger.Log(
                                            $"[MetaverseSerialPortDeviceAPI] Serial Number: {GetSerialNumber()} Expected Serial Number: {_data.SerialNumber} Opened: {IsAnyDeviceOpened()}");
                                    OpenFailed();
                                    CheckOpened();
                                }
                            });
                    });
                });
        }

        private void OpenSerial()
        {
            MetaverseSerialPortUtilityInterop.CallInstanceMethod(_spup, ref _openMethod, MetaverseSerialPortUtilityInterop.InstanceMethodID.Open);
        }

        private void CloseSerial()
        {
            MetaverseSerialPortUtilityInterop.CallInstanceMethod(_spup, ref _closeMethod, MetaverseSerialPortUtilityInterop.InstanceMethodID.Close);
        }

        private void OpenFailed()
        {
            if (!_isDisposed && _opening == this)
                OnStoppedOpening?.Invoke();
            if (_opening == this)
                _opening = null;
        }

        private string GetSerialNumber()
        {
            return !_spup
                ? null
                : MetaverseSerialPortUtilityInterop.GetProperty<string>(_spup, ref _serialNumberProperty,
                    MetaverseSerialPortUtilityInterop.GettablePropertyID.SerialNumber);
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
                OpenFailed();
            }
            OnDeviceName.RemoveAllListeners();
            OnStartedOpening.RemoveAllListeners();
            OnStoppedOpening.RemoveAllListeners();
            OnDeviceOpen.RemoveAllListeners();
            OnDeviceClosed.RemoveAllListeners();
        }

        ~MetaverseSerialPortDeviceAPI()
        {
            Dispose();
        }
    }
}
