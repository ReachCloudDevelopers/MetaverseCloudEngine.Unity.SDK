using System;
using System.Reflection;
using MetaverseCloudEngine.Unity.Async;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.SPUP
{
    public class MetaverseSerialPortDeviceListUiItem : MonoBehaviour
    {
        public UnityEvent<string> onDeviceName;
        public UnityEvent onDeviceOpen;
        public UnityEvent onDeviceClosed;
        
        private Component _spup;
        private MetaverseSerialPortUtilityInterop.DeviceInfo _data;
        private MetaverseSerialPortUtilityInterop.OpenSystem _openSystem;

        private static bool _opening;
        private static FieldInfo _deviceNameField;
        private static FieldInfo _openMethodField;
        private static PropertyInfo _vendorIdProperty;
        private static PropertyInfo _productIdProperty;
        private static PropertyInfo _serialNumberProperty;
        private static PropertyInfo _portProperty;
        private static MethodInfo _openMethod;
        private static MethodInfo _closeMethod;
        private static MethodInfo _isOpenProcessingMethod;
        
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
            if (MetaverseSerialPortUtilityInterop.GetField<string>(_spup, ref _deviceNameField, "DeviceName") == device)
                onDeviceOpen?.Invoke();
            else onDeviceClosed?.Invoke();
        }

        public void Open()
        {
            if (!_spup)
                return;

            if (!this)
                return;
            
            if (_opening)
                return;
            
            _opening = true;

            try
            {
                MetaverseSerialPortUtilityInterop.CallInstanceMethod(_spup, ref _closeMethod, "Close");
                MetaverseProgram.Logger.Log("Closing device...");
                MetaverseDispatcher.WaitForSeconds(1f, () =>
                {
                    MetaverseProgram.Logger.Log("Device closed");
                    OpenInternal();
                });
            }
            catch
            {
                _opening = false;
            }
        }

        private void OpenInternal()
        {
            if (!this || !_spup || !_opening)
            {
                MetaverseProgram.Logger.Log("Device open cancelled");
                _opening = false;
                return;
            }

            try
            {
                static bool IsHexString(string s)
                {
                    if (string.IsNullOrEmpty(s))
                        return false;

                    foreach (char c in s)
                        if (!System.Uri.IsHexDigit(c))
                            return false;
                    return true;
                }

                
                MetaverseSerialPortUtilityInterop.SetField(_spup, ref _openMethodField, "OpenMethod", (int)_openSystem);
                MetaverseSerialPortUtilityInterop.SetProperty(_spup, ref _vendorIdProperty, "VendorID", _data.Vendor);
                MetaverseSerialPortUtilityInterop.SetProperty(_spup, ref _productIdProperty, "ProductID", _data.Product);
                if (_openSystem is MetaverseSerialPortUtilityInterop.OpenSystem.Usb or MetaverseSerialPortUtilityInterop.OpenSystem.Pci)
                {
                    if (!IsHexString(_data.Product))
                        MetaverseSerialPortUtilityInterop.SetProperty(_spup, ref _productIdProperty, "ProductID", "");
                    if (!IsHexString(_data.Vendor))
                        MetaverseSerialPortUtilityInterop.SetProperty(_spup, ref _vendorIdProperty, "VendorID", "");
                }
                
                MetaverseSerialPortUtilityInterop.SetProperty(_spup, ref _serialNumberProperty, "SerialNumber", _data.SerialNumber);
                MetaverseSerialPortUtilityInterop.SetField(_spup, ref _deviceNameField, "DeviceName", string.IsNullOrEmpty(_data.SerialNumber) 
                    ? _data.Vendor
                    : _data.SerialNumber);
                MetaverseSerialPortUtilityInterop.SetProperty(_spup, ref _portProperty, "Port", _data.PortName ?? "");
            }
            catch (Exception e)
            {
                _opening = false;
                MetaverseProgram.Logger.Log("Device open error: " + e.Message);
            }

            MetaverseProgram.Logger.Log("Specified device to open: VID:" + _data.Vendor + " PID:" + _data.Product + " SER:" + _data.SerialNumber + " PORT:" + _data.PortName);

            var timeout = DateTime.UtcNow.AddSeconds(15);
            MetaverseDispatcher.WaitUntil(
                () => !this || !_spup || 
                      //!_spu.IsOpenProcessing()
                      MetaverseSerialPortUtilityInterop.CallInstanceMethod<bool>(_spup, ref _isOpenProcessingMethod, "IsOpenProcessing")
                      || !_opening || DateTime.UtcNow > timeout, 
                () =>
                {
                    if (!_opening) return;
                    _opening = false;
                
                    if (DateTime.UtcNow > timeout)
                    {
                        MetaverseProgram.Logger.Log("Device open timeout");
                        return;
                    }
                
                    if (!this) return;
                    MetaverseProgram.Logger.Log("Opening device");
                
                    MetaverseSerialPortUtilityInterop.CallInstanceMethod(_spup, ref _openMethod, "Open");
                    onDeviceOpen?.Invoke();
                });
        }
    }
}