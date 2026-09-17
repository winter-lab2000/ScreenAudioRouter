using System.Runtime.InteropServices;
using ScreenAudioRouter.Interop;
using ScreenAudioRouter.Models;
using static ScreenAudioRouter.Interop.CoreAudio;

namespace ScreenAudioRouter.Services;

public sealed class AudioDeviceService : IDisposable
{
    private IMMDeviceEnumerator? _enumerator;
    private bool _disposed;

    private IMMDeviceEnumerator Enumerator
    {
        get
        {
            if (_enumerator is null)
            {
                var com = new MMDeviceEnumerator();
                _enumerator = (IMMDeviceEnumerator)com;
            }
            return _enumerator;
        }
    }

    public List<AudioDeviceInfo> ListRender() => List(EDataFlow.eRender);
    public List<AudioDeviceInfo> ListCapture() => List(EDataFlow.eCapture);

    private List<AudioDeviceInfo> List(EDataFlow flow)
    {
        var result = new List<AudioDeviceInfo>();
        string? defaultId = null;
        string? defaultCommId = null;

        try
        {
            if (Enumerator.GetDefaultAudioEndpoint(flow, ERole.eConsole, out var def) == 0 && def is not null)
            {
                def.GetId(out defaultId);
                Marshal.ReleaseComObject(def);
            }
        }
        catch { /* ignore */ }

        try
        {
            if (Enumerator.GetDefaultAudioEndpoint(flow, ERole.eCommunications, out var defc) == 0 && defc is not null)
            {
                defc.GetId(out defaultCommId);
                Marshal.ReleaseComObject(defc);
            }
        }
        catch { /* ignore */ }

        try
        {
            if (Enumerator.EnumAudioEndpoints(flow, DEVICE_STATE_ACTIVE, out var devices) != 0 || devices is null)
                return result;

            devices.GetCount(out var count);
            for (uint i = 0; i < count; i++)
            {
                if (devices.Item(i, out var dev) != 0 || dev is null)
                    continue;
                try
                {
                    dev.GetId(out var id);
                    var name = GetDeviceFriendlyName(dev) ?? id;
                    result.Add(new AudioDeviceInfo
                    {
                        Id = id,
                        Name = name,
                        IsCapture = flow == EDataFlow.eCapture,
                        IsDefault = id == defaultId,
                        IsDefaultCommunications = id == defaultCommId
                    });
                }
                finally
                {
                    Marshal.ReleaseComObject(dev);
                }
            }
            Marshal.ReleaseComObject(devices);
        }
        catch
        {
            // enumerator may throw if audio service is restarting
        }

        return result;
    }

    public string? GetDefaultOutputId()
    {
        try
        {
            if (Enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out var dev) == 0 && dev is not null)
            {
                dev.GetId(out var id);
                Marshal.ReleaseComObject(dev);
                return id;
            }
        }
        catch { }
        return null;
    }

    public string? GetDefaultCaptureId()
    {
        try
        {
            if (Enumerator.GetDefaultAudioEndpoint(EDataFlow.eCapture, ERole.eCommunications, out var dev) == 0 && dev is not null)
            {
                dev.GetId(out var id);
                Marshal.ReleaseComObject(dev);
                return id;
            }
        }
        catch { }
        return null;
    }

    public bool SetDefaultOutput(string deviceId)
    {
        var ok1 = SetDefaultEndpoint(deviceId, ERole.eConsole);
        var ok2 = SetDefaultEndpoint(deviceId, ERole.eMultimedia);
        var ok3 = SetDefaultEndpoint(deviceId, ERole.eCommunications);
        return ok1 || ok2 || ok3;
    }

    public bool SetDefaultCapture(string deviceId)
    {
        var ok1 = SetDefaultEndpoint(deviceId, ERole.eCommunications);
        var ok2 = SetDefaultEndpoint(deviceId, ERole.eConsole);
        var ok3 = SetDefaultEndpoint(deviceId, ERole.eMultimedia);
        return ok1 || ok2 || ok3;
    }

    /// <summary>
    /// List processes that currently have active audio sessions on the default render device.
    /// Used as a best-effort signal that an app is playing sound.
    /// </summary>
    public HashSet<int> GetActivePlaybackProcessIds()
    {
        var pids = new HashSet<int>();
        try
        {
            if (Enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out var device) != 0 || device is null)
                return pids;

            var iid = typeof(IAudioSessionManager2).GUID;
            if (device.Activate(ref iid, 0x1 /* CLSCTX_INPROC_SERVER */, 0, out var unk) != 0 || unk is null)
            {
                Marshal.ReleaseComObject(device);
                return pids;
            }

            var mgr = (IAudioSessionManager2)unk;
            if (mgr.GetSessionEnumerator(out var sessions) == 0 && sessions is not null)
            {
                sessions.GetCount(out var count);
                for (var i = 0; i < count; i++)
                {
                    if (sessions.GetSession(i, out var ctl) != 0 || ctl is null)
                        continue;
                    try
                    {
                        var ctl2 = (IAudioSessionControl2)ctl;
                        ctl2.GetProcessId(out var pid);
                        if (pid != 0)
                            pids.Add((int)pid);
                    }
                    catch
                    {
                        // session may not expose process id
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(ctl);
                    }
                }
                Marshal.ReleaseComObject(sessions);
            }
            Marshal.ReleaseComObject(mgr);
            Marshal.ReleaseComObject(device);
        }
        catch
        {
            // audio stack not ready
        }
        return pids;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_enumerator is not null)
        {
            Marshal.ReleaseComObject(_enumerator);
            _enumerator = null;
        }
    }
}
