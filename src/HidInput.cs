using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace BodycamFpvFix
{
    /// <summary>A HID game controller found on the system (an RC radio in USB joystick mode, for example).</summary>
    public sealed class HidDeviceInfo
    {
        public string Path;
        public string Name;
        public ushort VendorId;
        public ushort ProductId;

        public string Key => $"{VendorId:X4}:{ProductId:X4}";
        public override string ToString() => $"{Name}  ({Key})";
    }

    /// <summary>One axis of the device with its logical range.</summary>
    public sealed class AxisInfo
    {
        public string Name;
        public int Min;
        public int Max;
        public int Mid => (Min + Max + 1) / 2;
    }

    /// <summary>Snapshot of all inputs of one HID report.</summary>
    public sealed class InputState
    {
        public readonly Dictionary<string, int> Axes = new Dictionary<string, int>();
        public readonly HashSet<int> Buttons = new HashSet<int>();
    }

    /// <summary>Opens a HID joystick and reads its reports (shared, read-only access).</summary>
    public sealed class HidJoystick : IDisposable
    {
        readonly SafeFileHandle handle;
        readonly FileStream stream;
        readonly IntPtr preparsed;
        readonly int reportLength;
        readonly uint maxData;
        readonly Dictionary<ushort, string> axisByIndex = new Dictionary<ushort, string>();
        readonly Dictionary<ushort, int> buttonByIndex = new Dictionary<ushort, int>();
        Task<int> pending;
        byte[] buffer;

        public readonly HidDeviceInfo Info;
        public readonly List<AxisInfo> Axes = new List<AxisInfo>();

        HidJoystick(HidDeviceInfo info, SafeFileHandle h)
        {
            Info = info;
            handle = h;
            Native.HidD_GetPreparsedData(h, out preparsed);
            Native.HidP_GetCaps(preparsed, out var caps);
            reportLength = caps.InputReportByteLength;
            maxData = Native.HidP_MaxDataListLength(Native.HidP_Input, preparsed);

            ushort nv = caps.NumberInputValueCaps;
            var vc = new Native.HIDP_CAPS72[nv];
            if (nv > 0) Native.HidP_GetValueCaps(Native.HidP_Input, vc, ref nv, preparsed);
            foreach (var v in vc)
            {
                int min = v.LogicalMin, max = v.LogicalMax;
                if (max <= min && v.BitSize > 0 && v.BitSize < 32) { min = 0; max = (1 << v.BitSize) - 1; }
                ushort last = v.IsRange != 0 ? v.UsageMax : v.UsageMin;
                for (int u = v.UsageMin, di = v.DataIndexMin; u <= last; u++, di++)
                {
                    string baseName = UsageName(v.UsagePage, (ushort)u);
                    string name = baseName;
                    for (int k = 2; Axes.Exists(a => a.Name == name); k++) name = baseName + k;
                    axisByIndex[(ushort)di] = name;
                    Axes.Add(new AxisInfo { Name = name, Min = min, Max = max });
                }
            }

            ushort nb = caps.NumberInputButtonCaps;
            var bc = new Native.HIDP_CAPS72[nb];
            if (nb > 0) Native.HidP_GetButtonCaps(Native.HidP_Input, bc, ref nb, preparsed);
            foreach (var b in bc)
            {
                ushort last = b.IsRange != 0 ? b.UsageMax : b.UsageMin;
                for (int u = b.UsageMin, di = b.DataIndexMin; u <= last; u++, di++)
                    buttonByIndex[(ushort)di] = u;
            }

            stream = new FileStream(h, FileAccess.Read, Math.Max(1, reportLength), true);
        }

        public static HidJoystick Open(HidDeviceInfo info)
        {
            var h = Native.CreateFile(info.Path, Native.GENERIC_READ, Native.FILE_SHARE_READ | Native.FILE_SHARE_WRITE,
                IntPtr.Zero, Native.OPEN_EXISTING, Native.FILE_FLAG_OVERLAPPED, IntPtr.Zero);
            if (h.IsInvalid) return null;
            return new HidJoystick(info, h);
        }

        /// <summary>Reads the next report. Returns null on timeout, throws IOException when the device is gone.</summary>
        public InputState Read(int timeoutMs)
        {
            if (pending == null)
            {
                buffer = new byte[reportLength];
                pending = stream.ReadAsync(buffer, 0, buffer.Length);
            }
            try
            {
                if (!pending.Wait(timeoutMs)) return null;
            }
            catch (AggregateException ex)
            {
                pending = null;
                throw new IOException("Device removed", ex.InnerException);
            }
            int n = pending.Result;
            var report = buffer;
            pending = null;
            if (n <= 0) throw new IOException("Device removed");

            var state = new InputState();
            var list = new Native.HIDP_DATA[maxData];
            uint len = maxData;
            if (Native.HidP_GetData(Native.HidP_Input, list, ref len, preparsed, report, (uint)n) != Native.HIDP_STATUS_SUCCESS)
                return state;
            for (int i = 0; i < len; i++)
            {
                if (axisByIndex.TryGetValue(list[i].DataIndex, out var axis)) state.Axes[axis] = (int)list[i].RawValue;
                else if (buttonByIndex.TryGetValue(list[i].DataIndex, out var button)) state.Buttons.Add(button); // HidP_GetData lists pressed buttons only
            }
            return state;
        }

        public void Dispose()
        {
            try { stream.Dispose(); } catch { }
            try { Native.HidD_FreePreparsedData(preparsed); } catch { }
        }

        static string UsageName(ushort page, ushort usage)
        {
            if (page == 0x01)
            {
                switch (usage)
                {
                    case 0x30: return "X";
                    case 0x31: return "Y";
                    case 0x32: return "Z";
                    case 0x33: return "Rx";
                    case 0x34: return "Ry";
                    case 0x35: return "Rz";
                    case 0x36: return "Slider";
                    case 0x37: return "Dial";
                    case 0x38: return "Wheel";
                    case 0x39: return "Hat";
                }
            }
            if (page == 0x02)
            {
                switch (usage)
                {
                    case 0xBA: return "Rudder";
                    case 0xBB: return "Throttle";
                    case 0xC4: return "Accelerator";
                    case 0xC5: return "Brake";
                }
            }
            return $"{page:X2}:{usage:X2}";
        }

        /// <summary>Lists HID devices that report themselves as joystick or gamepad. XInput devices (Xbox pads, including our own virtual one) are skipped.</summary>
        public static List<HidDeviceInfo> FindJoysticks()
        {
            var result = new List<HidDeviceInfo>();
            Native.HidD_GetHidGuid(out var guid);
            IntPtr set = Native.SetupDiGetClassDevs(ref guid, IntPtr.Zero, IntPtr.Zero, Native.DIGCF_PRESENT | Native.DIGCF_DEVICEINTERFACE);
            try
            {
                for (int i = 0; ; i++)
                {
                    var data = new Native.SP_DEVICE_INTERFACE_DATA();
                    data.cbSize = Marshal.SizeOf(data);
                    if (!Native.SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, i, ref data)) break;
                    string path = InterfacePath(set, ref data);
                    if (path == null || path.IndexOf("&ig_", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                    using (var h = Native.CreateFile(path, 0, Native.FILE_SHARE_READ | Native.FILE_SHARE_WRITE, IntPtr.Zero, Native.OPEN_EXISTING, 0, IntPtr.Zero))
                    {
                        if (h.IsInvalid) continue;
                        if (!Native.HidD_GetPreparsedData(h, out var pp)) continue;
                        try
                        {
                            Native.HidP_GetCaps(pp, out var caps);
                            if (caps.UsagePage != 0x01 || (caps.Usage != 0x04 && caps.Usage != 0x05)) continue;
                        }
                        finally { Native.HidD_FreePreparsedData(pp); }

                        var attr = new Native.HIDD_ATTRIBUTES();
                        attr.Size = Marshal.SizeOf(attr);
                        Native.HidD_GetAttributes(h, ref attr);
                        var sb = new StringBuilder(256);
                        string name = Native.HidD_GetProductString(h, sb, sb.Capacity * 2) ? sb.ToString().Trim() : "";
                        if (name.Length == 0) name = "HID joystick";
                        result.Add(new HidDeviceInfo { Path = path, Name = name, VendorId = attr.VendorID, ProductId = attr.ProductID });
                    }
                }
            }
            finally { Native.SetupDiDestroyDeviceInfoList(set); }
            return result;
        }

        static string InterfacePath(IntPtr set, ref Native.SP_DEVICE_INTERFACE_DATA data)
        {
            Native.SetupDiGetDeviceInterfaceDetail(set, ref data, IntPtr.Zero, 0, out int required, IntPtr.Zero);
            if (required <= 0) return null;
            IntPtr buf = Marshal.AllocHGlobal(required);
            try
            {
                Marshal.WriteInt32(buf, IntPtr.Size == 8 ? 8 : 6);
                if (!Native.SetupDiGetDeviceInterfaceDetail(set, ref data, buf, required, out required, IntPtr.Zero)) return null;
                return Marshal.PtrToStringUni(buf + 4);
            }
            finally { Marshal.FreeHGlobal(buf); }
        }
    }

    static class Native
    {
        public const int HidP_Input = 0;
        public const int HIDP_STATUS_SUCCESS = 0x110000;
        public const uint GENERIC_READ = 0x80000000;
        public const uint FILE_SHARE_READ = 1, FILE_SHARE_WRITE = 2, OPEN_EXISTING = 3, FILE_FLAG_OVERLAPPED = 0x40000000;
        public const int DIGCF_PRESENT = 0x02, DIGCF_DEVICEINTERFACE = 0x10;

        [StructLayout(LayoutKind.Sequential)]
        public struct SP_DEVICE_INTERFACE_DATA { public int cbSize; public Guid InterfaceClassGuid; public int Flags; public IntPtr Reserved; }

        [StructLayout(LayoutKind.Sequential)]
        public struct HIDD_ATTRIBUTES { public int Size; public ushort VendorID; public ushort ProductID; public ushort VersionNumber; }

        [StructLayout(LayoutKind.Sequential)]
        public struct HIDP_CAPS
        {
            public ushort Usage, UsagePage, InputReportByteLength, OutputReportByteLength, FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
            public ushort NumberLinkCollectionNodes, NumberInputButtonCaps, NumberInputValueCaps, NumberInputDataIndices,
                NumberOutputButtonCaps, NumberOutputValueCaps, NumberOutputDataIndices,
                NumberFeatureButtonCaps, NumberFeatureValueCaps, NumberFeatureDataIndices;
        }

        // Shared 72-byte layout of HIDP_VALUE_CAPS and HIDP_BUTTON_CAPS; only the fields used here.
        [StructLayout(LayoutKind.Explicit, Size = 72)]
        public struct HIDP_CAPS72
        {
            [FieldOffset(0)] public ushort UsagePage;
            [FieldOffset(12)] public byte IsRange;
            [FieldOffset(18)] public ushort BitSize;        // value caps only
            [FieldOffset(40)] public int LogicalMin;        // value caps only
            [FieldOffset(44)] public int LogicalMax;        // value caps only
            [FieldOffset(56)] public ushort UsageMin;       // Usage when not a range
            [FieldOffset(58)] public ushort UsageMax;
            [FieldOffset(68)] public ushort DataIndexMin;   // DataIndex when not a range
            [FieldOffset(70)] public ushort DataIndexMax;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HIDP_DATA { public ushort DataIndex; public ushort Reserved; public uint RawValue; }

        [DllImport("hid.dll")] public static extern void HidD_GetHidGuid(out Guid guid);
        [DllImport("hid.dll")] public static extern bool HidD_GetAttributes(SafeFileHandle h, ref HIDD_ATTRIBUTES attr);
        [DllImport("hid.dll", CharSet = CharSet.Unicode)] public static extern bool HidD_GetProductString(SafeFileHandle h, StringBuilder buffer, int bufferBytes);
        [DllImport("hid.dll")] public static extern bool HidD_GetPreparsedData(SafeFileHandle h, out IntPtr pp);
        [DllImport("hid.dll")] public static extern bool HidD_FreePreparsedData(IntPtr pp);
        [DllImport("hid.dll")] public static extern int HidP_GetCaps(IntPtr pp, out HIDP_CAPS caps);
        [DllImport("hid.dll")] public static extern int HidP_GetValueCaps(int type, [Out] HIDP_CAPS72[] caps, ref ushort length, IntPtr pp);
        [DllImport("hid.dll")] public static extern int HidP_GetButtonCaps(int type, [Out] HIDP_CAPS72[] caps, ref ushort length, IntPtr pp);
        [DllImport("hid.dll")] public static extern uint HidP_MaxDataListLength(int type, IntPtr pp);
        [DllImport("hid.dll")] public static extern int HidP_GetData(int type, [Out] HIDP_DATA[] list, ref uint length, IntPtr pp, byte[] report, uint reportLength);

        [DllImport("setupapi.dll", SetLastError = true)] public static extern IntPtr SetupDiGetClassDevs(ref Guid guid, IntPtr enumerator, IntPtr parent, int flags);
        [DllImport("setupapi.dll", SetLastError = true)] public static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr devInfo, ref Guid guid, int index, ref SP_DEVICE_INTERFACE_DATA data);
        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)] public static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref SP_DEVICE_INTERFACE_DATA data, IntPtr detail, int size, out int required, IntPtr devInfo);
        [DllImport("setupapi.dll")] public static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    }
}
