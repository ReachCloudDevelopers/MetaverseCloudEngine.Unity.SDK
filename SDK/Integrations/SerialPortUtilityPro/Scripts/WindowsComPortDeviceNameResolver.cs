#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace MetaverseCloudEngine.Unity.SPUP
{
    public static class WindowsComPortDeviceNameResolver
    {
        public static string GetDeviceName(string comPort)
        {
            if (string.IsNullOrWhiteSpace(comPort)) return null;
            comPort = comPort.Trim().ToUpperInvariant();
            var macBe = GetMacForComPort(comPort);
            return string.IsNullOrWhiteSpace(macBe) ? null : NameFromBthport(macBe);
        }

        private static string GetMacForComPort(string comPort)
        {
            Guid portsGuid = new("4D36E978-E325-11CE-BFC1-08002BE10318");
            IntPtr portsSet = SetupDiGetClassDevs(ref portsGuid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT);
            if (portsSet == INVALID_HANDLE_VALUE) return null;

            try
            {
                SP_DEVINFO_DATA info = new() { cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>() };

                for (uint i = 0; SetupDiEnumDeviceInfo(portsSet, i, ref info); i++)
                {
                    var portName = ReadPortName(portsSet, ref info);
                    if (!string.Equals(portName, comPort, StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (string.IsNullOrWhiteSpace(portName))
                        {
                            var deviceUniqueId = ReadDeviceUniqueId(portsSet, ref info);
                            if (!string.IsNullOrWhiteSpace(deviceUniqueId))
                            {
                                var split = deviceUniqueId.Split("#");
                                if (split.Length == 2)
                                    return split[1].Split("_")[0];
                            }
                        }
                        continue;
                    }

                    string instanceId = GetInstanceId(portsSet, ref info);
                    string containerId = GetContainerIdFromInstanceId(instanceId);
                    if (containerId == null) continue;

                    string mac = FindMacByContainerId(containerId);
                    if (mac != null) return mac;
                }
            }
            finally { SetupDiDestroyDeviceInfoList(portsSet); }

            return null;
        }

        private static string FindMacByContainerId(string containerId)
        {
            Guid[] searchGuids =
            {
                new Guid("E0CBF06C-CD8B-4647-BB8A-263B43F0F974"),
                new Guid("4D36E96C-E325-11CE-BFC1-08002BE10318")
            };

            foreach (var id in searchGuids)
            {
                var g = id;
                IntPtr set = SetupDiGetClassDevs(ref g, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT);
                if (set == INVALID_HANDLE_VALUE) continue;

                try
                {
                    SP_DEVINFO_DATA info = new() { cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>() };

                    for (uint i = 0; SetupDiEnumDeviceInfo(set, i, ref info); i++)
                    {
                        string instId = GetInstanceId(set, ref info);
                        string contId = GetContainerIdFromInstanceId(instId);

                        if (!string.Equals(contId, containerId, StringComparison.InvariantCultureIgnoreCase))
                            continue;

                        if (TryMacViaParents(info.DevInst, out var mac))
                            return mac;
                    }
                }
                finally { SetupDiDestroyDeviceInfoList(set); }
            }
            return null;
        }

        private static string ReadDeviceUniqueId(IntPtr set, ref SP_DEVINFO_DATA info)
        {
            IntPtr hKey = SetupDiOpenDevRegKey(set, ref info, DICS_FLAG_GLOBAL, 0, DIREG_DEV, KEY_READ);
            if (hKey == INVALID_HANDLE_VALUE) return null;
            try { return ReadRegSz(hKey, "Bluetooth_UniqueID"); }
            finally { RegCloseKey(hKey); }
        }

        private static string ReadPortName(IntPtr set, ref SP_DEVINFO_DATA info)
        {
            IntPtr hKey = SetupDiOpenDevRegKey(set, ref info, DICS_FLAG_GLOBAL, 0, DIREG_DEV, KEY_READ);
            if (hKey == INVALID_HANDLE_VALUE) return null;
            try { return ReadRegSz(hKey, "PortName"); }
            finally { RegCloseKey(hKey); }
        }

        private static string GetInstanceId(IntPtr set, ref SP_DEVINFO_DATA info)
        {
            uint req = 0;
            SetupDiGetDeviceInstanceId(set, ref info, null, 0, out req);
            if (req == 0) return null;

            var sb = new StringBuilder((int)req);
            return SetupDiGetDeviceInstanceId(set, ref info, sb, req, out _) ? sb.ToString() : null;
        }

        private static string GetContainerIdFromInstanceId(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId)) return null;

            const uint HKLM = 0x80000002u;
            string path = $@"SYSTEM\CurrentControlSet\Enum\{instanceId}";

            if (RegOpenKeyEx((UIntPtr)HKLM, path, 0, KEY_READ, out var hKey) != 0) return null;
            try
            {
                uint type = 0, sz = 0;
                if (RegQueryValueEx(hKey, "ContainerID", IntPtr.Zero, ref type, null, ref sz) != 0 || sz == 0)
                    return null;

                byte[] buf = new byte[sz];
                if (RegQueryValueEx(hKey, "ContainerID", IntPtr.Zero, ref type, buf, ref sz) != 0)
                    return null;

                if (type == 1)
                    return Encoding.Unicode.GetString(buf).TrimEnd('\0').Trim('{', '}').ToUpperInvariant();

                if (type == 3 && sz >= 16)
                {
                    byte[] gbytes = new byte[16];
                    Array.Copy(buf, gbytes, 16);
                    return new Guid(gbytes).ToString("D").ToUpperInvariant();
                }
            }
            finally { RegCloseKey(hKey); }

            return null;
        }

        private static bool TryMacViaParents(uint childDevInst, out string mac)
        {
            mac = null;
            uint dev = childDevInst;
            const int MAX_DEPTH = 8;

            for (int d = 0; d < MAX_DEPTH; d++)
            {
                StringBuilder idBuf = new(512);
                if (CM_Get_Device_ID(dev, idBuf, idBuf.Capacity, 0) != CR_SUCCESS) break;

                if (TryExtractMac(idBuf.ToString(), out mac)) return true;
                if (CM_Get_Parent(out uint parent, dev, 0) != CR_SUCCESS) break;
                dev = parent;
            }
            return false;
        }

        private static bool TryExtractMac(string source, out string mac)
        {
            mac = null;
            if (string.IsNullOrEmpty(source)) return false;

            int p = source.IndexOf("DEV_", StringComparison.InvariantCultureIgnoreCase);
            if (p >= 0 && source.Length >= p + 16)
            {
                mac = source.Substring(p + 4, 12).ToUpperInvariant();
                return true;
            }

            for (int i = 0; i <= source.Length - 12; i++)
            {
                bool ok = true;
                for (int j = 0; j < 12 && ok; j++)
                    ok &= Uri.IsHexDigit(source[i + j]);
                if (ok)
                {
                    mac = source.Substring(i, 12).ToUpperInvariant();
                    return true;
                }
            }
            return false;
        }

        private static string ReadRegSz(IntPtr hKey, string name)
        {
            uint type = 0, sz = 0;
            if (RegQueryValueEx(hKey, name, IntPtr.Zero, ref type, null, ref sz) != 0 || sz == 0) return null;

            byte[] buf = new byte[sz];
            return RegQueryValueEx(hKey, name, IntPtr.Zero, ref type, buf, ref sz) == 0
                 ? Encoding.ASCII.GetString(buf).TrimEnd('\0')
                 : null;
        }

        private static string NameFromBthport(string macBE)
        {
            if (macBE?.Length != 12) return null;
            const uint HKLM = 0x80000002u;
            string path = $@"SYSTEM\CurrentControlSet\Services\BTHPORT\Parameters\Devices\{macBE}";

            if (RegOpenKeyEx((UIntPtr)HKLM, path, 0, KEY_READ, out var hKey) != 0) return null;
            try
            {
                uint t = 0, sz = 0;
                if (RegQueryValueEx(hKey, "Name", IntPtr.Zero, ref t, null, ref sz) != 0 || sz == 0) return null;

                byte[] buf = new byte[sz];
                return RegQueryValueEx(hKey, "Name", IntPtr.Zero, ref t, buf, ref sz) == 0
                     ? Encoding.UTF8.GetString(buf).TrimEnd('\0')
                     : null;
            }
            finally { RegCloseKey(hKey); }
        }

        private const uint DIGCF_PRESENT        = 0x00000002;
        private const uint KEY_READ             = 0x20019;
        private const uint DICS_FLAG_GLOBAL     = 0x00000001;
        private const uint DIREG_DEV            = 0x00000001;
        private const uint CR_SUCCESS           = 0x00000000;

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
        private static extern bool SetupDiEnumDeviceInfo(IntPtr set, uint idx, ref SP_DEVINFO_DATA data);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiGetDeviceInstanceId(IntPtr set, ref SP_DEVINFO_DATA data,
            StringBuilder id, uint len, out uint req);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern IntPtr SetupDiOpenDevRegKey(IntPtr set, ref SP_DEVINFO_DATA data,
            uint scope, uint hwProfile, uint keyType, uint sam);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);

        [DllImport("cfgmgr32.dll")]
        private static extern uint CM_Get_Parent(out uint parent, uint devInst, uint flags);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        private static extern uint CM_Get_Device_ID(uint devInst, StringBuilder id, int len, uint flags);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int RegOpenKeyEx(UIntPtr root, string path, uint opts, uint sam, out IntPtr hKey);

        [DllImport("advapi32.dll")]
        private static extern int RegCloseKey(IntPtr hKey);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int RegQueryValueEx(IntPtr hKey, string value, IntPtr r,
            ref uint type, byte[] data, ref uint size);
    }
}

#endif
