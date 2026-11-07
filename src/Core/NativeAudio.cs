using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ScreenCurtain
{
    internal sealed class AudioEndpointState
    {
        public string Id;
        public bool Muted;
    }

    internal static class NativeAudio
    {
        private const int DEVICE_STATE_ACTIVE = 0x00000001;
        private const int CLSCTX_ALL = 23;

        [ThreadStatic]
        private static bool _comInitialized;

        [DllImport("ole32.dll")]
        private static extern int CoInitializeEx(IntPtr pvReserved, uint dwCoInit);

        [DllImport("ole32.dll")]
        private static extern void CoUninitialize();

        [ComImport]
        [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumeratorComObject
        {
        }

        private enum EDataFlow
        {
            eRender = 0,
            eCapture = 1,
            eAll = 2
        }

        private enum ERole
        {
            eConsole = 0,
            eMultimedia = 1,
            eCommunications = 2
        }

        [ComImport]
        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            [PreserveSig]
            int EnumAudioEndpoints(EDataFlow dataFlow, int dwStateMask, out IMMDeviceCollection devices);

            [PreserveSig]
            int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);

            [PreserveSig]
            int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice device);

            [PreserveSig]
            int RegisterEndpointNotificationCallback(IntPtr pClient);

            [PreserveSig]
            int UnregisterEndpointNotificationCallback(IntPtr pClient);
        }

        [ComImport]
        [Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceCollection
        {
            [PreserveSig]
            int GetCount(out int pcDevices);

            [PreserveSig]
            int Item(int nDevice, out IMMDevice device);
        }

        [ComImport]
        [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig]
            int Activate(
                ref Guid iid,
                int dwClsCtx,
                IntPtr pActivationParams,
                [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);

            [PreserveSig]
            int OpenPropertyStore(int stgmAccess, out IntPtr properties);

            [PreserveSig]
            int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);

            [PreserveSig]
            int GetState(out int pdwState);
        }

        [ComImport]
        [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioEndpointVolume
        {
            [PreserveSig]
            int RegisterControlChangeNotify(IntPtr pNotify);

            [PreserveSig]
            int UnregisterControlChangeNotify(IntPtr pNotify);

            [PreserveSig]
            int GetChannelCount(out int pnChannelCount);

            [PreserveSig]
            int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);

            [PreserveSig]
            int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);

            [PreserveSig]
            int GetMasterVolumeLevel(out float pfLevelDB);

            [PreserveSig]
            int GetMasterVolumeLevelScalar(out float pfLevel);

            [PreserveSig]
            int SetChannelVolumeLevel(int nChannel, float fLevelDB, ref Guid pguidEventContext);

            [PreserveSig]
            int SetChannelVolumeLevelScalar(int nChannel, float fLevel, ref Guid pguidEventContext);

            [PreserveSig]
            int GetChannelVolumeLevel(int nChannel, out float pfLevelDB);

            [PreserveSig]
            int GetChannelVolumeLevelScalar(int nChannel, out float pfLevel);

            [PreserveSig]
            int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid pguidEventContext);

            [PreserveSig]
            int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);

            [PreserveSig]
            int GetVolumeStepInfo(out int pnStep, out int pnStepCount);

            [PreserveSig]
            int VolumeStepUp(ref Guid pguidEventContext);

            [PreserveSig]
            int VolumeStepDown(ref Guid pguidEventContext);

            [PreserveSig]
            int QueryHardwareSupport(out int pdwHardwareSupportMask);

            [PreserveSig]
            int GetVolumeRange(out float pflVolumeMindB, out float pflVolumeMaxdB, out float pflVolumeIncrementdB);
        }

        public static bool EnsureComInitialized()
        {
            if (_comInitialized) return true;
            try
            {
                int hr = CoInitializeEx(IntPtr.Zero, 0x2); // COINIT_APARTMENTTHREADED
                if (hr < 0)
                {
                    // A thread may already be initialized in MTA; Core Audio can
                    // still be used, so don't treat ChangedMode as fatal.
                    if (hr == unchecked((int)0x80010106)) return true;
                    Log.Warn("CoInitializeEx 失败：0x{0:X8}", hr);
                    return false;
                }
                _comInitialized = true;
                return true;
            }
            catch (Exception ex)
            {
                Log.Exception("初始化 COM 失败", ex);
                return false;
            }
        }

        public static List<AudioEndpointState> GetActiveRenderEndpoints()
        {
            List<AudioEndpointState> result = new List<AudioEndpointState>();
            IMMDeviceEnumerator enumerator = null;
            IMMDeviceCollection collection = null;
            try
            {
                if (!EnsureComInitialized()) return result;
                enumerator = (IMMDeviceEnumerator)(new MMDeviceEnumeratorComObject());
                int hr = enumerator.EnumAudioEndpoints(EDataFlow.eRender, DEVICE_STATE_ACTIVE, out collection);
                if (hr < 0 || collection == null) return result;

                int count;
                hr = collection.GetCount(out count);
                if (hr < 0) return result;

                for (int i = 0; i < count; i++)
                {
                    IMMDevice device = null;
                    object volumeObject = null;
                    try
                    {
                        hr = collection.Item(i, out device);
                        if (hr < 0 || device == null) continue;

                        string id;
                        hr = device.GetId(out id);
                        if (hr < 0 || string.IsNullOrEmpty(id)) continue;

                        Guid iid = typeof(IAudioEndpointVolume).GUID;
                        hr = device.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out volumeObject);
                        if (hr < 0 || volumeObject == null) continue;

                        IAudioEndpointVolume volume = (IAudioEndpointVolume)volumeObject;
                        bool muted;
                        hr = volume.GetMute(out muted);
                        if (hr < 0) continue;

                        AudioEndpointState state = new AudioEndpointState();
                        state.Id = id;
                        state.Muted = muted;
                        result.Add(state);
                    }
                    catch (Exception ex)
                    {
                        Log.Exception("读取音频端点状态失败", ex);
                    }
                    finally
                    {
                        ReleaseCom(volumeObject);
                        ReleaseCom(device);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Exception("枚举播放设备失败", ex);
            }
            finally
            {
                ReleaseCom(collection);
                ReleaseCom(enumerator);
            }
            return result;
        }

        public static bool SetMute(string deviceId, bool muted)
        {
            if (string.IsNullOrEmpty(deviceId)) return false;
            IMMDeviceEnumerator enumerator = null;
            IMMDevice device = null;
            object volumeObject = null;
            try
            {
                if (!EnsureComInitialized()) return false;
                enumerator = (IMMDeviceEnumerator)(new MMDeviceEnumeratorComObject());
                int hr = enumerator.GetDevice(deviceId, out device);
                if (hr < 0 || device == null) return false;

                Guid iid = typeof(IAudioEndpointVolume).GUID;
                hr = device.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out volumeObject);
                if (hr < 0 || volumeObject == null) return false;

                IAudioEndpointVolume volume = (IAudioEndpointVolume)volumeObject;
                Guid context = Guid.Empty;
                hr = volume.SetMute(muted, ref context);
                return hr >= 0;
            }
            catch (Exception ex)
            {
                Log.Exception("设置播放设备静音失败", ex);
                return false;
            }
            finally
            {
                ReleaseCom(volumeObject);
                ReleaseCom(device);
                ReleaseCom(enumerator);
            }
        }

        public static bool GetMute(string deviceId, out bool muted)
        {
            muted = false;
            if (string.IsNullOrEmpty(deviceId)) return false;
            IMMDeviceEnumerator enumerator = null;
            IMMDevice device = null;
            object volumeObject = null;
            try
            {
                if (!EnsureComInitialized()) return false;
                enumerator = (IMMDeviceEnumerator)(new MMDeviceEnumeratorComObject());
                int hr = enumerator.GetDevice(deviceId, out device);
                if (hr < 0 || device == null) return false;

                Guid iid = typeof(IAudioEndpointVolume).GUID;
                hr = device.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out volumeObject);
                if (hr < 0 || volumeObject == null) return false;

                IAudioEndpointVolume volume = (IAudioEndpointVolume)volumeObject;
                hr = volume.GetMute(out muted);
                return hr >= 0;
            }
            catch
            {
                return false;
            }
            finally
            {
                ReleaseCom(volumeObject);
                ReleaseCom(device);
                ReleaseCom(enumerator);
            }
        }

        private static void ReleaseCom(object value)
        {
            try
            {
                if (value != null && Marshal.IsComObject(value))
                {
                    Marshal.ReleaseComObject(value);
                }
            }
            catch
            {
            }
        }
    }
}
