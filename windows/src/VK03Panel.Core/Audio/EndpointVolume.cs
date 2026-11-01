using System.Runtime.InteropServices;
using System.Text;

namespace VK03Panel.Core.Audio;

/// <summary>
/// Default render endpoint volume through the Core Audio COM API, the Windows counterpart of
/// the macOS CoreAudio code path. All calls are best-effort: a missing endpoint yields null.
/// </summary>
public sealed class EndpointVolume
{
    private static readonly Guid MmDeviceEnumeratorClsid = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    private static readonly Guid AudioEndpointVolumeIid = new("5CDF2C82-841E-4546-9722-0CF74078229A");

    private readonly object _sync = new();
    private IMMDeviceEnumerator? _enumerator;
    private IAudioEndpointVolume? _volume;
    private string _deviceId = string.Empty;

    public (double? Volume, bool Muted)? Read()
    {
        lock (_sync)
        {
            try
            {
                if (!EnsureEndpoint())
                {
                    return null;
                }

                _volume!.GetMasterVolumeLevelScalar(out var scalar);
                _volume.GetMute(out var muted);
                return (Math.Clamp(scalar, 0, 1), muted);
            }
            catch (Exception)
            {
                ReleaseEndpoint();
                return null;
            }
        }
    }

    public bool SetVolume(double requested)
    {
        lock (_sync)
        {
            try
            {
                if (!EnsureEndpoint())
                {
                    return false;
                }

                _volume!.SetMasterVolumeLevelScalar((float)Math.Clamp(requested, 0, 1), Guid.Empty);
                if (requested > 0.0001)
                {
                    _volume.SetMute(false, Guid.Empty);
                }

                return true;
            }
            catch (Exception)
            {
                ReleaseEndpoint();
                return false;
            }
        }
    }

    public bool SetMuted(bool muted)
    {
        lock (_sync)
        {
            try
            {
                if (!EnsureEndpoint())
                {
                    return false;
                }

                _volume!.SetMute(muted, Guid.Empty);
                return true;
            }
            catch (Exception)
            {
                ReleaseEndpoint();
                return false;
            }
        }
    }

    public bool IsAvailable
    {
        get
        {
            lock (_sync)
            {
                return EnsureEndpoint();
            }
        }
    }

    private bool EnsureEndpoint()
    {
        if (_volume is not null)
        {
            return true;
        }

        try
        {
            _enumerator ??= (IMMDeviceEnumerator)Activator.CreateInstance(Type.GetTypeFromCLSID(MmDeviceEnumeratorClsid)!)!;
            if (_enumerator.GetDefaultAudioEndpoint(0, 0, out var device) != 0 || device is null)
            {
                return false;
            }

            try
            {
                device.GetId(out _deviceId);
                var iid = AudioEndpointVolumeIid;
                if (device.Activate(ref iid, 1, IntPtr.Zero, out var pointer) != 0 || pointer == IntPtr.Zero)
                {
                    return false;
                }

                _volume = (IAudioEndpointVolume)Marshal.GetObjectForIUnknown(pointer);
                Marshal.Release(pointer);
                return true;
            }
            finally
            {
                Marshal.ReleaseComObject(device);
            }
        }
        catch (Exception)
        {
            ReleaseEndpoint();
            return false;
        }
    }

    private void ReleaseEndpoint()
    {
        if (_volume is not null)
        {
            try
            {
                Marshal.ReleaseComObject(_volume);
            }
            catch (Exception)
            {
                // Ignore release failures.
            }

            _volume = null;
        }
    }

    public string DeviceId => _deviceId;

    public string Describe()
    {
        var value = Read();
        if (value is null)
        {
            return "默认输出端点不可用";
        }

        var (volume, muted) = value.Value;
        return $"默认输出 · 音量 {((volume ?? 0) * 100).ToString("0", System.Globalization.CultureInfo.InvariantCulture)}%{(muted ? " · 已静音" : string.Empty)}";
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);

        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);

        int GetDevice(string id, out IMMDevice device);

        int RegisterEndpointNotificationCallback(IntPtr client);

        int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        int Activate(ref Guid interfaceId, int classContext, IntPtr activationParams, out IntPtr interfacePointer);

        int OpenPropertyStore(int access, out IntPtr properties);

        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);

        int GetState(out int state);
    }

    [ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(IntPtr notify);

        int UnregisterControlChangeNotify(IntPtr notify);

        int GetChannelCount(out uint channelCount);

        int SetMasterVolumeLevel(float levelDb, Guid eventContext);

        int SetMasterVolumeLevelScalar(float level, Guid eventContext);

        int GetMasterVolumeLevel(out float levelDb);

        int GetMasterVolumeLevelScalar(out float level);

        int SetChannelVolumeLevel(uint channel, float levelDb, Guid eventContext);

        int SetChannelVolumeLevelScalar(uint channel, float level, Guid eventContext);

        int GetChannelVolumeLevel(uint channel, out float levelDb);

        int GetChannelVolumeLevelScalar(uint channel, out float level);

        int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, Guid eventContext);

        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);

        int GetVolumeStepInfo(out uint step, out uint stepCount);

        int VolumeStepUp(Guid eventContext);

        int VolumeStepDown(Guid eventContext);

        int QueryHardwareSupport(out uint hardwareSupportMask);

        int GetVolumeRange(out float minDb, out float maxDb, out float incrementDb);
    }
}
