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
        public string DevicePath;
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

        [DllImport("user32.dll")]
        private static extern int SetDisplayConfig(
            uint numPathArrayElements,
            [In] PATH_INFO[] pathArray,
            uint numModeInfoArrayElements,
            [In] MODE_INFO[] modeInfoArray,
            uint flags);

        private const uint SDC_USE_SUPPLIED_DISPLAY_CONFIG = 0x00000010;
        private const uint SDC_APPLY = 0x00000080;
        private const uint SDC_ALLOW_CHANGES = 0x00000400;
        private const uint MODE_INFO_TYPE_SOURCE = 1;
        private static PATH_INFO[] _savedPaths;
        private static MODE_INFO[] _savedModes;

        public static bool TryDisableExternalOutputs()
        {
            try
            {
                uint pathCount;
                uint modeCount;
                if (GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out pathCount, out modeCount) != 0 ||
                    pathCount == 0)
                {
                    return false;
                }

                PATH_INFO[] paths = new PATH_INFO[pathCount];
                MODE_INFO[] modes = new MODE_INFO[modeCount];
                if (QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero) != 0)
                {
                    return false;
                }

                List<PATH_INFO> kept = new List<PATH_INFO>();
                List<MODE_INFO> newModes = new List<MODE_INFO>();
                Log.Info("SDC sizes path={0} mode={1} name={2}; counts={3}/{4}",
                    Marshal.SizeOf(typeof(PATH_INFO)),
                    Marshal.SizeOf(typeof(MODE_INFO)),
                    Marshal.SizeOf(typeof(TARGET_DEVICE_NAME)),
                    pathCount,
                    modeCount);
                bool hasInternal = false;
                bool hasExternal = false;
                for (uint i = 0; i < pathCount; i++)
                {
                    uint technology = GetPathOutputTechnology(paths[i]);
                    DisplayConnectionKind kind = Classify(technology);
                    if (kind == DisplayConnectionKind.Internal) hasInternal = true;
                    if (kind == DisplayConnectionKind.External) hasExternal = true;
                    if (kind != DisplayConnectionKind.Internal) continue;

                    PATH_INFO path = paths[i];
                    uint sourceIdx = path.sourceInfo.modeInfoIdx;
                    if (sourceIdx < modeCount)
                    {
                        MODE_INFO mode = modes[sourceIdx];
                        if (mode.infoType == MODE_INFO_TYPE_SOURCE)
                        {
                            mode.modeInfo.sourceMode.position.X = 0;
                            mode.modeInfo.sourceMode.position.Y = 0;
                        }
                        path.sourceInfo.modeInfoIdx = (uint)newModes.Count;
                        newModes.Add(mode);
                    }
                    else
                    {
                        path.sourceInfo.modeInfoIdx = 0xFFFFFFFF;
                    }

                    uint targetIdx = path.targetInfo.modeInfoIdx;
                    if (targetIdx < modeCount)
                    {
                        path.targetInfo.modeInfoIdx = (uint)newModes.Count;
                        newModes.Add(modes[targetIdx]);
                    }
                    else
                    {
                        path.targetInfo.modeInfoIdx = 0xFFFFFFFF;
                    }

                    kept.Add(path);
                }

                if (!hasInternal || !hasExternal || kept.Count == 0)
                {
                    Log.Warn("SetDisplayConfig: hasInternal={0} hasExternal={1} kept={2}", hasInternal, hasExternal, kept.Count);
                    return false;
                }

                _savedPaths = paths;
                _savedModes = modes;

                // Sanity check the P/Invoke and flags: re-applying the current
                // active configuration should be accepted.
                int noOp = SetDisplayConfig(
                    pathCount,
                    paths,
                    modeCount,
                    modes,
                    SDC_APPLY | SDC_USE_SUPPLIED_DISPLAY_CONFIG);
                Log.Info("SetDisplayConfig no-op A={0}", noOp);
                if (noOp != 0)
                {
                    noOp = SetDisplayConfig(
                        pathCount,
                        paths,
                        modeCount,
                        modes,
                        SDC_APPLY | SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_ALLOW_CHANGES);
                    Log.Info("SetDisplayConfig no-op B={0}", noOp);
                }

                int error = SetDisplayConfig(
                    (uint)kept.Count,
                    kept.ToArray(),
                    (uint)newModes.Count,
                    newModes.ToArray(),
                    SDC_APPLY | SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_ALLOW_CHANGES);
                if (error != 0)
                {
                    // Some drivers reject compacted mode arrays. Retry with
                    // the original mode array and only the internal paths.
                    Log.Warn("SetDisplayConfig compact 失败：{0}，尝试原始 mode 数组", error);
                    PATH_INFO[] keptOriginal = new PATH_INFO[kept.Count];
                    for (int i = 0; i < kept.Count; i++)
                    {
                        keptOriginal[i] = kept[i];
                        keptOriginal[i].sourceInfo.modeInfoIdx = paths[i].sourceInfo.modeInfoIdx;
                        keptOriginal[i].targetInfo.modeInfoIdx = paths[i].targetInfo.modeInfoIdx;
                    }
                    error = SetDisplayConfig(
                        (uint)keptOriginal.Length,
                        keptOriginal,
                        modeCount,
                        modes,
                        SDC_APPLY | SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_ALLOW_CHANGES);
                }
                if (error != 0)
                {
                    Log.Warn("SetDisplayConfig 关闭外接输出失败：{0}", error);
                    _savedPaths = null;
                    _savedModes = null;
                    return false;
                }
                Log.Info("SetDisplayConfig 已切换到仅内置屏（路径 {0}/{1}）", kept.Count, pathCount);
                return true;
            }
            catch (Exception ex)
            {
                Log.Exception("SetDisplayConfig 关闭外接输出异常", ex);
                return false;
            }
        }

        public static bool TryRestoreExternalOutputs()
        {
            try
            {
                if (_savedPaths == null || _savedModes == null) return false;
                int error = SetDisplayConfig(
                    (uint)_savedPaths.Length,
                    _savedPaths,
                    (uint)_savedModes.Length,
                    _savedModes,
                    SDC_APPLY | SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_ALLOW_CHANGES);
                if (error != 0)
                {
                    Log.Warn("SetDisplayConfig 恢复外接输出失败：{0}", error);
                    return false;
                }
                _savedPaths = null;
                _savedModes = null;
                Log.Info("SetDisplayConfig 已恢复外接输出");
                return true;
            }
            catch (Exception ex)
            {
                Log.Exception("SetDisplayConfig 恢复外接输出异常", ex);
                return false;
            }
        }

        private static uint GetPathOutputTechnology(PATH_INFO path)
        {
            TARGET_DEVICE_NAME name = new TARGET_DEVICE_NAME();
            name.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME;
            name.header.size = (uint)Marshal.SizeOf(typeof(TARGET_DEVICE_NAME));
            name.header.adapterId = path.targetInfo.adapterId;
            name.header.id = path.targetInfo.id;
            if (DisplayConfigGetDeviceInfo(ref name) != 0) return uint.MaxValue;
            return name.outputTechnology;
        }

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
                    target.DevicePath = name.monitorDevicePath;
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
