using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ScreenCurtain
{
    internal enum DisplayConnectionKind
    {
        Unknown,
        Internal,
        External
    }

    internal sealed class DisplayTargetInfo
    {
        public int X;
        public int Y;
        public uint Width;
        public uint Height;
        public uint OutputTechnology;
        public string FriendlyName;
        public DisplayConnectionKind Kind;

        public string TechnologyName
        {
            get
            {
                switch (OutputTechnology)
                {
                    case 0x80000000: return "Internal";
                    case 6: return "LVDS";
                    case 11: return "DisplayPort Embedded";
                    case 13: return "UDI Embedded";
                    case 10: return "DisplayPort";
                    case 5: return "HDMI";
                    case 4: return "DVI";
                    case 0: return "VGA";
                    case 15: return "Miracast";
                    case 16: return "Indirect Wired";
                    case 17: return "Indirect Virtual";
                    default: return "Technology " + OutputTechnology;
                }
            }
        }
    }

    internal static class DisplayConfigReader
    {
        private const uint QDC_ONLY_ACTIVE_PATHS = 0x00000002;
        private const uint DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME = 2;
        private const uint DISPLAYCONFIG_OUTPUT_TECHNOLOGY_INTERNAL = 0x80000000;
        private const uint DISPLAYCONFIG_OUTPUT_TECHNOLOGY_LVDS = 6;
        private const uint DISPLAYCONFIG_OUTPUT_TECHNOLOGY_DISPLAYPORT_EMBEDDED = 11;
        private const uint DISPLAYCONFIG_OUTPUT_TECHNOLOGY_UDI_EMBEDDED = 13;

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINTL
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RATIONAL
        {
            public uint Numerator;
            public uint Denominator;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PATH_SOURCE_INFO
        {
            public LUID adapterId;
            public uint id;
            public uint modeInfoIdx;
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PATH_TARGET_INFO
        {
            public LUID adapterId;
            public uint id;
            public uint modeInfoIdx;
            public uint outputTechnology;
            public uint rotation;
            public uint scaling;
            public RATIONAL refreshRate;
            public uint scanLineOrdering;
            [MarshalAs(UnmanagedType.Bool)] public bool targetAvailable;
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PATH_INFO
        {
            public PATH_SOURCE_INFO sourceInfo;
            public PATH_TARGET_INFO targetInfo;
            public uint flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SOURCE_MODE
        {
            public uint width;
            public uint height;
            public uint pixelFormat;
            public POINTL position;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct REGION2D
        {
            public uint cx;
            public uint cy;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VIDEO_SIGNAL_INFO
        {
            public ulong pixelRate;
            public RATIONAL hSyncFreq;
            public RATIONAL vSyncFreq;
            public REGION2D activeSize;
            public REGION2D totalSize;
            public uint videoStandard;
            public uint scanLineOrdering;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TARGET_MODE
        {
            public VIDEO_SIGNAL_INFO targetVideoSignalInfo;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct MODE_UNION
        {
            [FieldOffset(0)] public SOURCE_MODE sourceMode;
            [FieldOffset(0)] public TARGET_MODE targetMode;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MODE_INFO
        {
            public uint infoType;
            public uint id;
            public LUID adapterId;
            public MODE_UNION modeInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DEVICE_INFO_HEADER
        {
            public uint type;
            public uint size;
            public LUID adapterId;
            public uint id;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct TARGET_DEVICE_NAME
        {
            public DEVICE_INFO_HEADER header;
            public uint flags;
            public uint outputTechnology;
            public ushort edidManufactureId;
            public ushort edidProductCodeId;
            public uint connectorInstance;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string monitorFriendlyDeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string monitorDevicePath;
        }

        [DllImport("user32.dll")]
        private static extern int GetDisplayConfigBufferSizes(
            uint flags,
            out uint numPathArrayElements,
            out uint numModeInfoArrayElements);

        [DllImport("user32.dll")]
        private static extern int QueryDisplayConfig(
            uint flags,
            ref uint numPathArrayElements,
            [Out] PATH_INFO[] pathInfoArray,
            ref uint numModeInfoArrayElements,
            [Out] MODE_INFO[] modeInfoArray,
            IntPtr currentTopologyId);

        [DllImport("user32.dll")]
        private static extern int DisplayConfigGetDeviceInfo(ref TARGET_DEVICE_NAME deviceInfo);

        public static List<DisplayTargetInfo> GetActiveTargets()
        {
            List<DisplayTargetInfo> result = new List<DisplayTargetInfo>();
            try
            {
                uint pathCount;
                uint modeCount;
                int error = GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out pathCount, out modeCount);
                if (error != 0 || pathCount == 0)
                {
                    Log.Warn("GetDisplayConfigBufferSizes 失败：{0}", error);
                    return result;
                }

                PATH_INFO[] paths = new PATH_INFO[pathCount];
                MODE_INFO[] modes = new MODE_INFO[modeCount];
                error = QueryDisplayConfig(
                    QDC_ONLY_ACTIVE_PATHS,
                    ref pathCount,
                    paths,
                    ref modeCount,
                    modes,
                    IntPtr.Zero);
                if (error != 0) return result;

                for (int i = 0; i < pathCount; i++)
                {
                    PATH_INFO path = paths[i];
                    TARGET_DEVICE_NAME name = new TARGET_DEVICE_NAME();
                    name.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME;
                    name.header.size = (uint)Marshal.SizeOf(typeof(TARGET_DEVICE_NAME));
                    name.header.adapterId = path.targetInfo.adapterId;
                    name.header.id = path.targetInfo.id;
                    if (DisplayConfigGetDeviceInfo(ref name) != 0) continue;

                    DisplayTargetInfo target = new DisplayTargetInfo();
                    target.OutputTechnology = name.outputTechnology;
                    target.FriendlyName = name.monitorFriendlyDeviceName;
                    target.Width = 0;
                    target.Height = 0;
                    uint idx = path.sourceInfo.modeInfoIdx;
                    if (idx < modeCount)
                    {
                        MODE_INFO mode = modes[idx];
                        target.X = mode.modeInfo.sourceMode.position.X;
                        target.Y = mode.modeInfo.sourceMode.position.Y;
                        target.Width = mode.modeInfo.sourceMode.width;
                        target.Height = mode.modeInfo.sourceMode.height;
                    }

                    target.Kind = Classify(target.OutputTechnology);
                    result.Add(target);
                }
            }
            catch (Exception ex)
            {
                Log.Exception("读取 DisplayConfig 目标信息失败", ex);
            }
            return result;
        }

        private static DisplayConnectionKind Classify(uint technology)
        {
            if (technology == DISPLAYCONFIG_OUTPUT_TECHNOLOGY_INTERNAL)
                return DisplayConnectionKind.Internal;
            if (technology == DISPLAYCONFIG_OUTPUT_TECHNOLOGY_LVDS ||
                technology == DISPLAYCONFIG_OUTPUT_TECHNOLOGY_DISPLAYPORT_EMBEDDED ||
                technology == DISPLAYCONFIG_OUTPUT_TECHNOLOGY_UDI_EMBEDDED)
                return DisplayConnectionKind.Internal;

            // Known external transport types. Unknown technologies remain
            // unknown so the DDC path does not guess.
            switch (technology)
            {
                case 0:  // VGA
                case 1:  // S-Video
                case 2:  // Composite
                case 3:  // Component
                case 4:  // DVI
                case 5:  // HDMI
                case 8:  // D_JPN
                case 9:  // SDI
                case 10: // DisplayPort external
                case 12: // UDI external
                case 14: // SDTV dongle
                case 15: // Miracast
                case 16: // Indirect wired
                case 17: // Indirect virtual
                    return DisplayConnectionKind.External;
                default:
                    return DisplayConnectionKind.Unknown;
            }
        }
    }
}
