using System;
using System.Linq;
using System.Reflection;
using MetaverseCloudEngine.Unity.Async;
using UnityEngine;
using UnityEngine.Events;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System.Runtime.InteropServices;
using System.Text;
#endif

namespace MetaverseCloudEngine.Unity.SPUP
{
    /// <summary>
    /// Represents a device that can be opened and closed using the Metaverse Serial Port Utility.
    /// </summary>
    public class MetaverseSerialPortDeviceAPI : IDisposable
    {
        /*──────────────────────────────  Unity Events  ──────────────────────────*/

        [Tooltip("The serial-port utility component.")]
        public readonly UnityEvent<string> OnDeviceName     = new();
        [Tooltip("Invoked when the device starts opening.")]
        public readonly UnityEvent         OnStartedOpening = new();
        [Tooltip("Invoked when the device stops opening.")]
        public readonly UnityEvent         OnStoppedOpening = new();
        [Tooltip("Invoked when the device is opened.")]
        public readonly UnityEvent         OnDeviceOpen     = new();
        [Tooltip("Invoked when the device is closed.")]
        public readonly UnityEvent         OnDeviceClosed   = new();

        /*─────────────────────────────  Private fields  ─────────────────────────*/

        private Component _spup;
        private MetaverseSerialPortUtilityInterop.DeviceInfo  _data;
        private MetaverseSerialPortUtilityInterop.OpenSystem _openSystem;
        private bool _isDisposed;

        private static MetaverseSerialPortDeviceAPI _opening;

        private static FieldInfo    _openMethodField;
        private static PropertyInfo _deviceNameProperty;
        private static PropertyInfo _vendorIdProperty;
        private static PropertyInfo _productIdProperty;
        private static PropertyInfo _serialNumberProperty;
        private static PropertyInfo _portProperty;
        private static MethodInfo   _openMethod;
        private static MethodInfo   _closeMethod;
        private static MethodInfo   _isOpenProcessingMethod;
        private static MethodInfo   _isOpenedMethod;

        /*──────────────────────────────  Properties  ────────────────────────────*/

        /// <summary>True once <see cref="Initialize"/> has been called.</summary>
        public bool IsInitialized => _spup && _data != null;

        /// <summary>A serialised form for debug/logging.</summary>
        public string DeviceString => $"{(_data?.ToString() ?? "")},{_openSystem}";

        /*──────────────────────────────  Public API  ────────────────────────────*/

        /// <summary>
        /// Initialise this API with SPUP component, friendly name and discovery info.
        /// </summary>
        public void Initialize(
            Component spu,
            string displayName,
            MetaverseSerialPortUtilityInterop.DeviceInfo data,
            MetaverseSerialPortUtilityInterop.OpenSystem openSystem)
        {
            ResetAll();

            _spup       = spu;
            _data       = data;
            _openSystem = openSystem;

            /* ───────────────── Resolve a nicer name on Windows ─────────────────*/
            var resolvedName = displayName;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (!string.IsNullOrEmpty(displayName) &&
                displayName.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
            {
                resolvedName = TryGetComPortFriendlyName(displayName);
            }
#endif

            /* Delay the event dispatch until Unity’s end-of-frame                */
            MetaverseDispatcher.AtEndOfFrame(() =>
            {
                OnDeviceName?.Invoke(resolvedName);
                CheckOpened();
            });
        }

        /// <summary>
        /// Checks opened/closed state and fires the proper UnityEvents.
        /// </summary>
        public void CheckOpened(bool forceClosed = false)
        {
            if (_isDisposed)
            {
                MetaverseProgram.Logger.Log("[MetaverseSerialPortDeviceAPI] Device is disposed, call Initialize() again.");
                return;
            }

            if (_data == null) { OnDeviceClosed?.Invoke(); return; }

            if (IsThisDeviceOpened() && !forceClosed) OnDeviceOpen ?.Invoke();
            else                                      OnDeviceClosed?.Invoke();
        }

        /// <summary>Starts the async open sequence.</summary>
        public void Open()
        {
            if (_isDisposed)
            {
                MetaverseProgram.Logger.Log("[MetaverseSerialPortDeviceAPI] Device is disposed, call Initialize() again.");
                return;
            }
            if (_opening != null || !_spup) return;

            _opening = this;
            OnStartedOpening?.Invoke();

            try
            {
                CloseSerial();
                MetaverseProgram.Logger.Log("[MetaverseSerialPortDeviceAPI] Closing current device …");

                var timeout = DateTime.UtcNow.AddSeconds(15);
                MetaverseDispatcher.WaitUntil(
                    () => _isDisposed || !_spup || _opening != this ||
                          DateTime.UtcNow > timeout ||
                          (!IsAnyDeviceOpened() && !IsThisDeviceOpened()),
                    () =>
                    {
                        if (_isDisposed || !_spup || _opening != this)
                        {
                            if (!_isDisposed && _opening == this) OpenFailed();
                            return;
                        }

                        if (DateTime.UtcNow > timeout)
                        {
                            MetaverseProgram.Logger.Log("[MetaverseSerialPortDeviceAPI] Close timeout.");
                            OpenFailed();
                            return;
                        }

                        MetaverseProgram.Logger.Log("[MetaverseSerialPortDeviceAPI] Closed. Opening new device …");
                        OpenInternal();
                    });
            }
            catch (Exception e)
            {
                if (!_isDisposed && _opening == this) OpenFailed();
                MetaverseProgram.Logger.Log($"[MetaverseSerialPortDeviceAPI] Device close error: {e.Message}");
            }
        }

        /// <summary>True if this exact device is opened.</summary>
        public bool IsThisDeviceOpened()
        {
            if (_data == null) return false;

            var ser = _data.SerialNumber;
            if (string.IsNullOrEmpty(ser)) ser = _data.Vendor;

            return GetSerialNumber() == ser && IsAnyDeviceOpened();
        }

        /// <summary>True if SPUP is currently processing any open request.</summary>
        public bool IsADeviceBeingOpened()
        {
            return _spup &&
                   MetaverseSerialPortUtilityInterop.CallInstanceMethod<bool>(
                       _spup, ref _isOpenProcessingMethod,
                       MetaverseSerialPortUtilityInterop.InstanceMethodID.IsOpenProcessing);
        }

        /// <summary>True if any device is already open.</summary>
        public bool IsAnyDeviceOpened()
        {
            return _spup &&
                   MetaverseSerialPortUtilityInterop.CallInstanceMethod<bool>(
                       _spup, ref _isOpenedMethod,
                       MetaverseSerialPortUtilityInterop.InstanceMethodID.IsOpened);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            ResetAll();
            _isDisposed = true;
        }

        /*──────────────────────────────  Private API  ───────────────────────────*/

        private void OpenInternal()
        {
            if (_isDisposed || !_spup || _opening != this)
            {
                MetaverseProgram.Logger.Log("[MetaverseSerialPortDeviceAPI] Open cancelled.");
                if (!_isDisposed && _opening == this) OpenFailed();
                return;
            }

            try
            {
                static bool Hex(string s) => !string.IsNullOrEmpty(s) && s.All(Uri.IsHexDigit);

                MetaverseSerialPortUtilityInterop.SetField(_spup, ref _openMethodField,
                    MetaverseSerialPortUtilityInterop.SettableFieldID.OpenMethod, (int)_openSystem);

                MetaverseSerialPortUtilityInterop.SetProperty(_spup, ref _vendorIdProperty,
                    MetaverseSerialPortUtilityInterop.SettablePropertyID.VendorID,   _data.Vendor);
                MetaverseSerialPortUtilityInterop.SetProperty(_spup, ref _portProperty,
                    MetaverseSerialPortUtilityInterop.SettablePropertyID.Port,       _data.PortName ?? string.Empty);
                MetaverseSerialPortUtilityInterop.SetProperty(_spup, ref _productIdProperty,
                    MetaverseSerialPortUtilityInterop.SettablePropertyID.ProductID, _data.Product);

                if (_openSystem is MetaverseSerialPortUtilityInterop.OpenSystem.Usb or
                                   MetaverseSerialPortUtilityInterop.OpenSystem.Pci)
                {
                    if (!Hex(_data.Product)) MetaverseSerialPortUtilityInterop.SetProperty(_spup, ref _productIdProperty,
                        MetaverseSerialPortUtilityInterop.SettablePropertyID.ProductID, string.Empty);
                    if (!Hex(_data.Vendor))  MetaverseSerialPortUtilityInterop.SetProperty(_spup, ref _vendorIdProperty,
                        MetaverseSerialPortUtilityInterop.SettablePropertyID.VendorID,  string.Empty);
                }

                MetaverseSerialPortUtilityInterop.SetProperty(_spup, ref _serialNumberProperty,
                    MetaverseSerialPortUtilityInterop.SettablePropertyID.SerialNumber, _data.SerialNumber);

                MetaverseSerialPortUtilityInterop.SetProperty(_spup, ref _deviceNameProperty,
                    MetaverseSerialPortUtilityInterop.SettablePropertyID.DeviceName,
                    string.IsNullOrEmpty(_data.SerialNumber) ? _data.Vendor : _data.SerialNumber);
            }
            catch (Exception e)
            {
                MetaverseProgram.Logger.Log($"[MetaverseSerialPortDeviceAPI] Open error: {e.Message}");
                OpenFailed();
                return;
            }

            MetaverseProgram.Logger.Log(
                $"[MetaverseSerialPortDeviceAPI] Requesting open – VID:{_data.Vendor}, PID:{_data.Product}, " +
                $"SER:{_data.SerialNumber}, PORT:{_data.PortName}");

            /* Continue with the long state-machine (unchanged from original)… */
            var timeout = DateTime.UtcNow.AddSeconds(15);

            MetaverseDispatcher.WaitUntil(
                () => _isDisposed || !_spup ||
                      !IsADeviceBeingOpened() ||
                      _opening != this || DateTime.UtcNow > timeout,
                () =>
                {
                    if (_isDisposed) return;

                    if (_opening != this) { OpenFailed(); return; }
                    if (DateTime.UtcNow > timeout)
                    {
                        MetaverseProgram.Logger.Log("[MetaverseSerialPortDeviceAPI] Open timeout.");
                        OpenFailed();
                        return;
                    }

                    MetaverseProgram.Logger.Log("[MetaverseSerialPortDeviceAPI] Opening device …");
                    OpenSerial();

                    timeout = DateTime.UtcNow.AddSeconds(15);
                    MetaverseDispatcher.WaitUntil(
                        () => _isDisposed || !_spup ||
                              IsThisDeviceOpened() ||
                              IsADeviceBeingOpened() ||
                              _opening != this ||
                              DateTime.UtcNow > timeout,
                        () =>
                        {
                            if (_isDisposed) return;

                            if (_opening != this) { OpenFailed(); return; }
                            if (DateTime.UtcNow > timeout)
                            {
                                MetaverseProgram.Logger.Log("[MetaverseSerialPortDeviceAPI] Open timeout.");
                                OpenFailed();
                                return;
                            }

                            MetaverseProgram.Logger.Log("[MetaverseSerialPortDeviceAPI] Finalising …");

                            timeout = DateTime.UtcNow.AddSeconds(5);
                            MetaverseDispatcher.WaitUntil(
                                () => _isDisposed || !_spup ||
                                      !IsADeviceBeingOpened() ||
                                      _opening != this ||
                                      IsThisDeviceOpened() ||
                                      DateTime.UtcNow > timeout,
                                () =>
                                {
                                    if (_isDisposed) return;

                                    if (_opening != this)
                                    {
                                        OpenFailed();
                                        return;
                                    }

                                    if (DateTime.UtcNow > timeout)
                                    {
                                        MetaverseProgram.Logger.Log("[MetaverseSerialPortDeviceAPI] Final timeout.");
                                        OpenFailed();
                                        return;
                                    }

                                    if (IsThisDeviceOpened())
                                    {
                                        OnStoppedOpening?.Invoke();
                                        OnDeviceOpen?.Invoke();
                                        MetaverseProgram.Logger.Log("[MetaverseSerialPortDeviceAPI] Device opened.");
                                        if (_opening == this) _opening = null;
                                    }
                                    else
                                    {
                                        MetaverseProgram.Logger.Log(
                                            $"[MetaverseSerialPortDeviceAPI] Serial mismatch – got {GetSerialNumber()} expected {_data.SerialNumber}");
                                        OpenFailed();
                                    }
                                });
                        });
                });
        }

        private void OpenSerial()
        {
            MetaverseSerialPortUtilityInterop.CallInstanceMethod(
                _spup, ref _openMethod, MetaverseSerialPortUtilityInterop.InstanceMethodID.Open);
        }

        private void CloseSerial()
        {
            MetaverseSerialPortUtilityInterop.CallInstanceMethod(
                _spup, ref _closeMethod, MetaverseSerialPortUtilityInterop.InstanceMethodID.Close);
        }

        private void OpenFailed()
        {
            if (!_isDisposed && _opening == this)
                OnStoppedOpening?.Invoke();

            if (_opening == this) _opening = null;
        }

        private string GetSerialNumber()
        {
            return !_spup
                ? null
                : MetaverseSerialPortUtilityInterop.GetProperty<string>(
                      _spup, ref _serialNumberProperty,
                      MetaverseSerialPortUtilityInterop.GettablePropertyID.SerialNumber);
        }

        private void ResetAll()
        {
            _spup       = null;
            _data       = null;
            _openSystem = 0;
            _isDisposed = false;

            if (_opening == this)
            {
                _opening = null;
                OpenFailed();
            }

            OnDeviceName    .RemoveAllListeners();
            OnStartedOpening.RemoveAllListeners();
            OnStoppedOpening.RemoveAllListeners();
            OnDeviceOpen    .RemoveAllListeners();
            OnDeviceClosed  .RemoveAllListeners();
        }

        ~MetaverseSerialPortDeviceAPI() => Dispose();

        /*─────────────────────────  Windows-only helpers  ───────────────────────*/

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private static string TryGetComPortFriendlyName(string portName)
        {
            const uint DIGCF_PRESENT         = 0x00000002;
            const uint DIGCF_DEVICEINTERFACE = 0x00000010;
            Guid GUID_DEVINTERFACE_COMPORT   = new Guid("86E0D1E0-8089-11D0-9CE4-08003E301F73");

            IntPtr hDevInfo = SetupDiGetClassDevs(
                ref GUID_DEVINTERFACE_COMPORT, null, IntPtr.Zero,
                DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);

            if (hDevInfo == IntPtr.Zero || hDevInfo.ToInt64() == -1)
                return portName;

            try
            {
                SP_DEVINFO_DATA info = new() { cbSize = (uint)Marshal.SizeOf(typeof(SP_DEVINFO_DATA)) };

                for (uint i = 0;
                     SetupDiEnumDeviceInfo(hDevInfo, i, ref info);
                     i++)
                {
                    byte[]     buf   = new byte[512];
                    uint       dtype = 0;
                    const uint SPDRP_FRIENDLYNAME = 0x0000000C;

                    if (SetupDiGetDeviceRegistryProperty(
                            hDevInfo, ref info, SPDRP_FRIENDLYNAME,
                            out dtype, buf, (uint)buf.Length, out _))
                    {
                        string name = Encoding.Unicode.GetString(buf).TrimEnd('\0');
                        if (name.Contains($"({portName})", StringComparison.OrdinalIgnoreCase) ||
                            name.Contains(portName,           StringComparison.OrdinalIgnoreCase))
                        {
                            return name;
                        }
                    }
                }
            }
            catch { /* ignore and fall back */ }
            finally
            {
                SetupDiDestroyDeviceInfoList(hDevInfo);
            }

            return portName; // fallback
        }

        /*────────────  SetupAPI P/Invoke  ────────────*/

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVINFO_DATA
        {
            public uint cbSize;
            public Guid ClassGuid;
            public uint DevInst;
            public IntPtr Reserved;
        }

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(
            ref Guid ClassGuid, string Enumerator, IntPtr hwndParent, uint Flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInfo(
            IntPtr DeviceInfoSet, uint MemberIndex, ref SP_DEVINFO_DATA DeviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiGetDeviceRegistryProperty(
            IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData,
            uint Property, out uint PropertyRegDataType,
            [Out] byte[] PropertyBuffer, uint PropertyBufferSize, out uint RequiredSize);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);
#endif // UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    }
}
