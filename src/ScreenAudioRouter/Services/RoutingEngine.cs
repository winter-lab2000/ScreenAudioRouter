using System.Windows.Threading;
using ScreenAudioRouter.Models;

namespace ScreenAudioRouter.Services;

public sealed class RoutingEngine : IDisposable
{
    private readonly MonitorService _monitors = new();
    private readonly AudioDeviceService _audio = new();
    private readonly LogService _log;
    private readonly object _gate = new();

    private DispatcherTimer? _timer;
    private AppSettings _settings = new();
    private WindowTracker _tracker;
    private List<MonitorInfo> _monitorList = [];
    private int _lastOutputMonitor = -1;
    private int _lastCaptureMonitor = -1;
    private string? _lastOutputDeviceId;
    private string? _lastCaptureDeviceId;
    private string _lastReason = "";
    private bool _running;

    public event Action<string>? StatusChanged;
    public event Action<IReadOnlyList<MonitorInfo>, IReadOnlyList<AudioDeviceInfo>, IReadOnlyList<AudioDeviceInfo>>? DevicesRefreshed;
    public event Action<TrackedWindow?>? ForegroundChanged;

    public IReadOnlyList<MonitorInfo> Monitors => _monitorList;
    public bool Running => _running;

    public RoutingEngine(LogService log)
    {
        _log = log;
        _tracker = new WindowTracker(_monitors, _settings.IgnoredProcesses);
    }

    public void ApplySettings(AppSettings settings)
    {
        lock (_gate)
        {
            _settings = settings;
            _tracker.UpdateIgnored(settings.IgnoredProcesses);
        }
        RestartTimer();
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_running) return;
            _running = true;
            _log.Info("Routing engine started");
        }
        RefreshDevices(autoMap: true);
        RestartTimer();
        StatusChanged?.Invoke("运行中");
    }

    public void Stop()
    {
        lock (_gate)
        {
            _running = false;
            _timer?.Stop();
            _timer = null;
            _log.Info("Routing engine stopped");
        }
        StatusChanged?.Invoke("已停止");
    }

    private void RestartTimer()
    {
        lock (_gate)
        {
            _timer?.Stop();
            var ms = Math.Clamp(_settings.PollIntervalMs, 100, 2000);
            _timer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(ms)
            };
            _timer.Tick += (_, _) => Tick();
            if (_running && _settings.Enabled)
                _timer.Start();
        }
    }

    public void RefreshDevices(bool autoMap = false)
    {
        try
        {
            _monitorList = _monitors.Enumerate();
            SyncMonitorMappings();
            var outputs = _audio.ListRender();
            var inputs = _audio.ListCapture();

            if (autoMap)
            {
                MonitorService.AutoMapUnbound(_monitorList, _settings.Monitors, outputs, inputs);
                WarnIfDuplicateOutputs(outputs);
            }

            DevicesRefreshed?.Invoke(_monitorList, outputs, inputs);
            var names = string.Join(", ", _monitorList.Select(m => m.DisplayName));
            _log.Info($"Devices: [{names}] | outs={outputs.Count} ins={inputs.Count}");
        }
        catch (Exception ex)
        {
            _log.Error("RefreshDevices failed", ex);
        }
    }

    private void WarnIfDuplicateOutputs(List<AudioDeviceInfo> outputs)
    {
        var bound = _settings.Monitors
            .Where(m => !string.IsNullOrEmpty(m.OutputDeviceId))
            .ToList();
        var groups = bound.GroupBy(m => m.OutputDeviceId!).Where(g => g.Count() > 1).ToList();
        foreach (var g in groups)
        {
            var name = g.First().OutputDeviceName ?? g.Key;
            var idx = string.Join("+", g.Select(x => x.MonitorIndex + 1));
            _log.Warn($"显示器 {idx} 绑定到同一输出「{name}」— 拖动窗口时听感不会变化，请改成各自独立设备（如 HDMI）。");
        }
    }

    private void SyncMonitorMappings()
    {
        var existing = _settings.Monitors.ToDictionary(m => m.MonitorIndex);
        var next = new List<MonitorMapping>();
        foreach (var mon in _monitorList)
        {
            if (existing.TryGetValue(mon.Index, out var map))
            {
                map.DeviceName = mon.DeviceName;
                map.BoundsX = mon.X;
                map.BoundsY = mon.Y;
                map.BoundsWidth = mon.Width;
                map.BoundsHeight = mon.Height;
                next.Add(map);
            }
            else
            {
                next.Add(new MonitorMapping
                {
                    MonitorIndex = mon.Index,
                    DeviceName = mon.DeviceName,
                    BoundsX = mon.X,
                    BoundsY = mon.Y,
                    BoundsWidth = mon.Width,
                    BoundsHeight = mon.Height
                });
            }
        }
        _settings.Monitors = next;
    }

    private void Tick()
    {
        if (!_running || !_settings.Enabled)
            return;

        List<TrackedWindow> windows;
        HashSet<int> playingPids;
        lock (_gate)
        {
            windows = _tracker.Snapshot(_monitorList);
            playingPids = _audio.GetActivePlaybackProcessIds();
        }

        var fg = windows.FirstOrDefault(w => w.IsForeground);
        ForegroundChanged?.Invoke(fg);

        // Prefer the monitor of a process that is ACTUALLY playing audio.
        // Chrome/Edge split UI and audio across processes — aggregate by process name.
        var target = ResolvePlaybackTarget(windows, playingPids, fg);
        if (target is not null)
            ApplyPlayback(target.Value.monitor, target.Value.processName, target.Value.reason);

        // Mic follows the focused window's monitor.
        if (fg is { MonitorIndex: >= 0 } &&
            !_settings.PinnedProcesses.Any(p => string.Equals(p, fg.ProcessName, StringComparison.OrdinalIgnoreCase)) &&
            _settings.RouteMicrophone)
        {
            var map = MapOf(fg.MonitorIndex);
            if (map is not null &&
                !string.IsNullOrEmpty(map.InputDeviceId) &&
                fg.MonitorIndex != _lastCaptureMonitor)
            {
                if (_audio.SetDefaultCapture(map.InputDeviceId))
                {
                    _lastCaptureMonitor = fg.MonitorIndex;
                    _lastCaptureDeviceId = map.InputDeviceId;
                    _log.Info($"Input → {DisplayName(map.MonitorIndex)} ({map.InputDeviceName}) via {fg.ProcessName}");
                    StatusChanged?.Invoke($"输入 → {DisplayName(map.MonitorIndex)}");
                }
                else
                {
                    _log.Warn($"Failed to set capture → {map.InputDeviceName}");
                }
            }
        }
    }

    /// <summary>
    /// Decide which monitor's output device should be default:
    /// 1) process family of a playing window (group by process name)
    /// 2) foreground if it is playing
    /// 3) foreground anyway (apps about to play)
    /// </summary>
    private (int monitor, string processName, string reason)? ResolvePlaybackTarget(
        List<TrackedWindow> windows,
        HashSet<int> playingPids,
        TrackedWindow? fg)
    {
        if (!_settings.RoutePlayback || !_settings.SwitchDefaultOutput)
            return null;

        // Map every window of playing processes (match by process name too —
        // Edge/Chrome audio PID often differs from the top-level window PID).
        var playingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var w in windows)
        {
            if (playingPids.Contains(w.ProcessId))
                playingNames.Add(w.ProcessName);
        }
        // Also: if any process name among windows shares with playing pids' names
        var allNamesFromPids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pid in playingPids)
        {
            try
            {
                using var p = System.Diagnostics.Process.GetProcessById(pid);
                allNamesFromPids.Add(p.ProcessName);
            }
            catch { }
        }
        playingNames.UnionWith(allNamesFromPids);

        if (playingNames.Count > 0)
        {
            var mediaWindows = windows
                .Where(w => playingNames.Contains(w.ProcessName) && w.MonitorIndex >= 0)
                .ToList();

            // Prefer foreground window among media processes
            var pick = mediaWindows.FirstOrDefault(w => w.IsForeground)
                       ?? mediaWindows.FirstOrDefault(w => w.Title.Length > 0)
                       ?? mediaWindows.FirstOrDefault();

            if (pick is not null)
            {
                var pinned = _settings.PinnedProcesses.Any(p =>
                    string.Equals(p, pick.ProcessName, StringComparison.OrdinalIgnoreCase));
                if (!pinned)
                    return (pick.MonitorIndex, pick.ProcessName, "playing");
            }
        }

        if (fg is { MonitorIndex: >= 0 })
        {
            var pinned = _settings.PinnedProcesses.Any(p =>
                string.Equals(p, fg.ProcessName, StringComparison.OrdinalIgnoreCase));
            if (!pinned)
                return (fg.MonitorIndex, fg.ProcessName, "foreground");
        }

        return null;
    }

    private void ApplyPlayback(int monitorIndex, string processName, string reason)
    {
        var map = MapOf(monitorIndex);
        if (map is null || string.IsNullOrEmpty(map.OutputDeviceId))
            return;

        if (monitorIndex == _lastOutputMonitor && map.OutputDeviceId == _lastOutputDeviceId)
            return;

        // Same device id on another monitor — switching is a no-op for the ear.
        if (map.OutputDeviceId == _lastOutputDeviceId &&
            monitorIndex != _lastOutputMonitor)
        {
            _log.Warn($"窗口切到 {DisplayName(monitorIndex)} 但输出设备相同「{map.OutputDeviceName}」— 需为每个显示器绑定不同音响");
            _lastOutputMonitor = monitorIndex;
            StatusChanged?.Invoke($"{DisplayName(monitorIndex)} · 同一设备");
            return;
        }

        if (_audio.SetDefaultOutput(map.OutputDeviceId))
        {
            var prev = _lastOutputMonitor;
            _lastOutputMonitor = monitorIndex;
            _lastOutputDeviceId = map.OutputDeviceId;
            _lastReason = reason;
            _log.Info($"Output → {DisplayName(monitorIndex)} ({map.OutputDeviceName}) via {processName} [{reason}] (was mon {prev + 1})");
            StatusChanged?.Invoke($"输出 → {DisplayName(monitorIndex)}");
        }
        else
        {
            _log.Warn($"SetDefaultOutput failed → {map.OutputDeviceName}");
        }
    }

    private MonitorMapping? MapOf(int monitorIndex)
    {
        lock (_gate)
            return _settings.Monitors.FirstOrDefault(m => m.MonitorIndex == monitorIndex);
    }

    private string DisplayName(int monitorIndex)
    {
        var mon = _monitorList.FirstOrDefault(m => m.Index == monitorIndex);
        return mon?.DisplayName ?? $"显示器 {monitorIndex + 1}";
    }

    public void ForceRouteTo(int monitorIndex)
    {
        var map = MapOf(monitorIndex);
        if (map is null) return;

        if (!string.IsNullOrEmpty(map.OutputDeviceId) && _audio.SetDefaultOutput(map.OutputDeviceId))
        {
            _lastOutputMonitor = monitorIndex;
            _lastOutputDeviceId = map.OutputDeviceId;
            _log.Info($"Manual output → {DisplayName(monitorIndex)} ({map.OutputDeviceName})");
        }
        if (!string.IsNullOrEmpty(map.InputDeviceId) && _audio.SetDefaultCapture(map.InputDeviceId))
        {
            _lastCaptureMonitor = monitorIndex;
            _lastCaptureDeviceId = map.InputDeviceId;
            _log.Info($"Manual input → {DisplayName(monitorIndex)} ({map.InputDeviceName})");
        }
        StatusChanged?.Invoke($"已切换到 {DisplayName(monitorIndex)}");
    }

    public void Dispose()
    {
        Stop();
        _audio.Dispose();
    }
}
