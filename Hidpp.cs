using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace MouseBatteryTray
{
    /// <summary>HID++ 2.0 客户端：通过 hidapi 与罗技接收器/设备通信，读取电量电压。</summary>
    internal static class NativeHid
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct HIDDeviceInfo
        {
            public IntPtr path;
            public ushort vendor_id;
            public ushort product_id;
            public IntPtr serial_number;
            public ushort release_number;
            public IntPtr manufacturer_string;
            public IntPtr product_string;
            public ushort usage_page;
            public ushort usage;
            public int interface_number;
            public IntPtr next;
            public int bus_type;
        }

        [DllImport("hidapi.dll")]
        public static extern int hid_init();

        [DllImport("hidapi.dll")]
        public static extern IntPtr hid_enumerate(ushort vendor, ushort product);

        [DllImport("hidapi.dll")]
        public static extern void hid_free_enumeration(IntPtr devs);

        [DllImport("hidapi.dll")]
        public static extern IntPtr hid_open_path(IntPtr path);

        [DllImport("hidapi.dll")]
        public static extern void hid_close(IntPtr dev);

        [DllImport("hidapi.dll")]
        public static extern int hid_write(IntPtr dev, byte[] data, UIntPtr length);

        [DllImport("hidapi.dll")]
        public static extern int hid_read_timeout(IntPtr dev, byte[] data, UIntPtr length, int milliseconds);

        [DllImport("hidapi.dll")]
        public static extern int hid_set_nonblocking(IntPtr dev, int nonblock);
    }

    internal sealed class LogitechBattery
    {
        private const ushort LogitechVid = 0x046D;
        private const byte ReportLong = 0x11;
        private const int ReportLen = 20;
        private const uint FeatureSet = 0x0001;
        private const uint BatteryFeature = 0x1001;

        public sealed class Reading
        {
            public int Millivolts;
            public int Percent;
            public string DeviceName;
            public int DeviceIndex;
            public bool Success;
            public string Message;
        }

        private static readonly int[] VoltageAnchors = { 4200, 4150, 4050, 3950, 3852, 3800, 3700, 3600, 3500, 3400 };
        private static readonly int[] PercentAnchors = { 100, 100, 88, 74, 59, 50, 32, 16, 5, 0 };

        /// <summary>把毫伏折算成百分比：按校准锚点做分段线性插值。</summary>
        public static int VoltageToPercent(int mv)
        {
            if (mv >= VoltageAnchors[0]) return 100;
            if (mv <= VoltageAnchors[VoltageAnchors.Length - 1]) return 0;
            for (int i = 0; i < VoltageAnchors.Length - 1; i++)
            {
                int hiV = VoltageAnchors[i], loV = VoltageAnchors[i + 1];
                if (mv <= hiV && mv >= loV)
                {
                    int hiP = PercentAnchors[i], loP = PercentAnchors[i + 1];
                    double ratio = (double)(mv - loV) / (double)(hiV - loV);
                    return (int)Math.Round(loP + ratio * (hiP - loP));
                }
            }
            return 0;
        }

        public static Reading Read()
        {
            var result = new Reading { Success = false, Message = "未找到设备" };
            IntPtr list = IntPtr.Zero;
            try
            {
                NativeHid.hid_init();
                list = NativeHid.hid_enumerate(LogitechVid, 0);
                IntPtr cur = list;
                while (cur != IntPtr.Zero)
                {
                    var info = (NativeHid.HIDDeviceInfo)Marshal.PtrToStructure(cur, typeof(NativeHid.HIDDeviceInfo));
                    cur = info.next;

                    // 只看长报告集合（usage 0x0002），并限定罗技厂商页
                    if (info.usage_page != 0xFF00 || info.usage != 0x0002)
                        continue;

                    string name = info.product_string != IntPtr.Zero
                        ? Marshal.PtrToStringUni(info.product_string)
                        : "Logitech";

                    var r = TryDevice(info.path, name);
                    if (r != null && r.Success)
                        return r;
                }
            }
            finally
            {
                if (list != IntPtr.Zero) NativeHid.hid_free_enumeration(list);
            }
            return result;
        }

        private static Reading TryDevice(IntPtr path, string name)
        {
            IntPtr dev = NativeHid.hid_open_path(path);
            if (dev == IntPtr.Zero) return null;
            try
            {
                NativeHid.hid_set_nonblocking(dev, 1);

                byte[] candidates = { 0xFF, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 };
                foreach (byte idx in candidates)
                {
                    byte fsIdx;
                    if (!GetFeatureIndex(dev, idx, FeatureSet, out fsIdx)) continue;

                    byte battIdx;
                    if (!FindBatteryFeature(dev, idx, fsIdx, out battIdx)) continue;

                    byte[] resp = Xfer(dev, idx, battIdx, 0x00, new byte[0], 0x0F);
                    if (resp == null || resp.Length < 6) continue;

                    int mv = (resp[4] << 8) | resp[5];
                    if (mv < 1500 || mv > 6000) continue;   // 明显不合理，跳过

                    return new Reading
                    {
                        Success = true,
                        Millivolts = mv,
                        Percent = VoltageToPercent(mv),
                        DeviceName = name,
                        DeviceIndex = idx,
                        Message = "ok"
                    };
                }
            }
            finally
            {
                NativeHid.hid_close(dev);
            }
            return null;
        }

        private static bool GetFeatureIndex(IntPtr dev, byte idx, uint featureId, out byte featureIndex)
        {
            featureIndex = 0;
            byte[] resp = Xfer(dev, idx, 0x00, 0x00, new byte[] { (byte)(featureId >> 8), (byte)(featureId & 0xFF) }, 0x0F);
            if (resp == null || resp.Length < 6) return false;
            if (resp[2] != 0x00) return false;
            if (resp[4] == 0x00 || resp[4] == 0xFF) return false;
            featureIndex = resp[4];
            return true;
        }

        private static bool FindBatteryFeature(IntPtr dev, byte idx, byte featSetIdx, out byte batteryIdx)
        {
            batteryIdx = 0;
            byte[] countResp = Xfer(dev, idx, featSetIdx, 0x00, new byte[0], 0x0F);
            if (countResp == null || countResp.Length < 5) return false;
            int count = countResp[4];
            if (count <= 0 || count > 64) return false;

            for (byte i = 1; i <= count; i++)
            {
                byte[] r = Xfer(dev, idx, featSetIdx, 0x01, new byte[] { i }, 0x0F);
                if (r == null || r.Length < 6) continue;
                uint fid = (uint)((r[4] << 8) | r[5]);
                if (fid == BatteryFeature)
                {
                    batteryIdx = i;
                    return true;
                }
            }
            return false;
        }

        /// <summary>发送一条 HID++ 长报告并等待严格匹配的响应。</summary>
        private static byte[] Xfer(IntPtr dev, byte deviceIndex, byte featureIndex, byte function, byte[] parameters, byte swId)
        {
            Drain(dev);
            byte[] packet = new byte[ReportLen];
            packet[0] = ReportLong;
            packet[1] = deviceIndex;
            packet[2] = featureIndex;
            packet[3] = (byte)((function << 4) | (swId & 0x0F));
            for (int i = 0; i < parameters.Length && 4 + i < ReportLen; i++)
                packet[4 + i] = parameters[i];

            int written = NativeHid.hid_write(dev, packet, (UIntPtr)packet.Length);
            if (written < 0) return null;

            byte expected = (byte)((function << 4) | (swId & 0x0F));
            var buffer = new byte[64];
            var deadline = DateTime.UtcNow.AddMilliseconds(700);
            while (DateTime.UtcNow < deadline)
            {
                int n = NativeHid.hid_read_timeout(dev, buffer, (UIntPtr)buffer.Length, 60);
                if (n > 0)
                {
                    if (buffer[0] != 0x10 && buffer[0] != 0x11) continue;
                    if (buffer[1] == deviceIndex && buffer[2] == featureIndex && buffer[3] == expected)
                    {
                        var resp = new byte[n];
                        Array.Copy(buffer, resp, n);
                        return resp;
                    }
                }
            }
            return null;
        }

        private static void Drain(IntPtr dev)
        {
            var junk = new byte[64];
            for (int i = 0; i < 8; i++)
            {
                if (NativeHid.hid_read_timeout(dev, junk, (UIntPtr)junk.Length, 10) <= 0) break;
            }
        }
    }
}
