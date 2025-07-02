#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace MetaverseCloudEngine.Unity.SPUP
{
    public static class WindowsComPortDeviceNameResolver
    {
        public static string GetDeviceName(string comPort)            // entry-point
        {
            if (string.IsNullOrWhiteSpace(comPort)) return null;
            comPort = comPort.Trim().ToUpperInvariant();              // “COM3” → upper-case

            string macBE = GetMacOfComPort(comPort);                  // step ❶ + ❷
            return macBE == null ? null : NameFromBthport(macBE);     // step ❸
        }

        #region ❶ + ❷  Bluetooth-MAC extraction (SetupAPI)

        private static string GetMacOfComPort(string comPort)
        {
            Guid portsClass = new("4D36E978-E325-11CE-BFC1-08002BE10318");   // “Ports (COM & LPT)”
            IntPtr hSet = SetupDiGetClassDevs(ref portsClass, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT);
            if (hSet == INVALID_HANDLE_VALUE) return null;

            try
            {
                SP_DEVINFO_DATA info = new() { cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>() };

                for (uint i = 0; SetupDiEnumDeviceInfo(hSet, i, ref info); i++)
                {
                    string portName = ReadPortName(hSet, ref info);
                    if (!string.Equals(portName, comPort, StringComparison.OrdinalIgnoreCase)) continue;

                    string[] hwIds = ReadMultiSzProperty(hSet, ref info, SPDRP_HARDWAREID);
                    foreach (var id in hwIds)
                    {
                        int p = id.IndexOf("DEV_", StringComparison.OrdinalIgnoreCase);
                        if (p >= 0 && id.Length >= p + 16)                 // “DEV_” + 12 hex chars
                            return id.Substring(p + 4, 12).ToUpperInvariant();
                    }
                }
            }
            finally { SetupDiDestroyDeviceInfoList(hSet); }
            return null;
        }

        private static string ReadPortName(IntPtr hSet, ref SP_DEVINFO_DATA info)
        {
            IntPtr hKey = SetupDiOpenDevRegKey(hSet, ref info, DICS_FLAG_GLOBAL, 0, DIREG_DEV, KEY_READ);
            if (hKey == INVALID_HANDLE_VALUE) return null;

            try
            {
                return ReadRegSz(hKey, "PortName");
            }
            finally { RegCloseKey(hKey); }
        }

        private static string[] ReadMultiSzProperty(IntPtr hSet, ref SP_DEVINFO_DATA info, uint prop)
        {
            uint req = 0;
            SetupDiGetDeviceRegistryProperty(hSet, ref info, prop, out _, null, 0, out req);
            if (req == 0) return Array.Empty<string>();

            byte[] buf = new byte[req];
            if (!SetupDiGetDeviceRegistryProperty(hSet, ref info, prop, out _, buf, req, out _))
                return Array.Empty<string>();

            string all = Encoding.Unicode.GetString(buf).TrimEnd('\0');
            return all.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        }

        private static string ReadRegSz(IntPtr hKey, string value)
        {
            uint type = 0, size = 0;
            if (RegQueryValueEx(hKey, value, IntPtr.Zero, ref type, null, ref size) != 0 || size == 0)
                return null;

            byte[] data = new byte[size];
            if (RegQueryValueEx(hKey, value, IntPtr.Zero, ref type, data, ref size) != 0)
                return null;

            return Encoding.ASCII.GetString(data).TrimEnd('\0');
        }

        #endregion

        #region ❸  Bluetooth-friendly-name lookup (unchanged method)

        private static string NameFromBthport(string macBE)
        {
            if (macBE.Length != 12) return null;
            const uint HKLM = 0x80000002u;
            string path = $@"SYSTEM\CurrentControlSet\Services\BTHPORT\Parameters\Devices\{macBE}";

            if (RegOpenKeyEx((UIntPtr)HKLM, path, 0, KEY_READ, out var h) != 0) return null;
            try
            {
                uint t = 0, sz = 0;
                if (RegQueryValueEx(h, "Name", IntPtr.Zero, ref t, null, ref sz) != 0 || sz == 0) return null;
                byte[] buf = new byte[sz];
                return RegQueryValueEx(h, "Name", IntPtr.Zero, ref t, buf, ref sz) == 0
                     ? Encoding.UTF8.GetString(buf).TrimEnd('\0')
                     : null;
            }
            finally { RegCloseKey(h); }
        }

        #endregion

        #region  Native interop + constants

        private const uint DIGCF_PRESENT     = 0x00000002;
        private const uint KEY_READ          = 0x20019;
        private const uint DICS_FLAG_GLOBAL  = 0x00000001;
        private const uint DIREG_DEV         = 0x00000001;
        private const uint SPDRP_HARDWAREID  = 0x00000001;
        private static readonly IntPtr INVALID_HANDLE_VALUE = new(-1);

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVINFO_DATA
        {
            public uint cbSize;
            public Guid ClassGuid;
            public uint DevInst;
            public IntPtr Reserved;
        }

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(ref Guid gClass, IntPtr enumStr,
            IntPtr hwndParent, uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInfo(IntPtr hSet, uint index,
            ref SP_DEVINFO_DATA devInfo);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiGetDeviceRegistryProperty(IntPtr hSet,
            ref SP_DEVINFO_DATA devInfo, uint prop, out uint proptype,
            byte[] buffer, uint buflen, out uint reqsize);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern IntPtr SetupDiOpenDevRegKey(IntPtr hSet, ref SP_DEVINFO_DATA devInfo,
            uint scope, uint hwProfile, uint keyType, uint samDesired);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr hSet);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int RegOpenKeyEx(UIntPtr hk, string sub, uint opts, uint sam, out IntPtr phk);

        [DllImport("advapi32.dll")] private static extern int RegCloseKey(IntPtr h);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int RegQueryValueEx(IntPtr h, string name, IntPtr r,
            ref uint type, byte[] data, ref uint size);

        #endregion
    }
}

#endif