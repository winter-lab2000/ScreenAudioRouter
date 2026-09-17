using System.Diagnostics;
using ScreenAudioRouter.Interop;
using ScreenAudioRouter.Models;

namespace ScreenAudioRouter.Services;

public sealed class WindowTracker
{
    private readonly MonitorService _monitors;
    private readonly HashSet<string> _ignored;

    public WindowTracker(MonitorService monitors, IEnumerable<string> ignoredProcesses)
    {
        _monitors = monitors;
        _ignored = new HashSet<string>(ignoredProcesses.Select(p => p.ToLowerInvariant()), StringComparer.OrdinalIgnoreCase);
    }

    public void UpdateIgnored(IEnumerable<string> processes)
    {
        _ignored.Clear();
        foreach (var p in processes)
            _ignored.Add(p.ToLowerInvariant());
    }

    public List<TrackedWindow> Snapshot(List<MonitorInfo> monitors)
    {
        var result = new List<TrackedWindow>();
        var foreground = NativeMethods.GetForegroundWindow();

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (!NativeMethods.IsWindowVisible(hWnd) || NativeMethods.IsIconic(hWnd))
                return true;
            if (NativeMethods.GetWindowTextLengthW(hWnd) <= 0)
                return true;

            NativeMethods.GetWindowThreadProcessId(hWnd, out var pid);
            if (pid == 0 || pid == (uint)Environment.ProcessId)
                return true;

            string processName = "";
            try
            {
                using var proc = Process.GetProcessById((int)pid);
                processName = proc.ProcessName;
            }
            catch
            {
                return true;
            }

            if (_ignored.Contains(processName))
                return true;

            // Skip tool windows / very small chrome-less popups without title
            if (!NativeMethods.GetWindowRect(hWnd, out var rect))
                return true;
            var area = Math.Abs(rect.Width * rect.Height);
            if (area < 200 * 120)
                return true;

            result.Add(new TrackedWindow
            {
                Hwnd = hWnd,
                ProcessId = (int)pid,
                ProcessName = processName,
                Title = NativeMethods.GetWindowText(hWnd),
                MonitorIndex = _monitors.MonitorIndexFromWindow(hWnd, monitors),
                IsForeground = hWnd == foreground
            });
            return true;
        }, 0);

        return result;
    }

    public TrackedWindow? GetForeground(List<MonitorInfo> monitors)
    {
        var all = Snapshot(monitors);
        return all.FirstOrDefault(w => w.IsForeground);
    }
}
