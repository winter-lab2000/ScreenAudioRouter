using System.Runtime.InteropServices;
using ScreenAudioRouter.Interop;
using ScreenAudioRouter.Models;

namespace ScreenAudioRouter.Services;

public sealed class MonitorService
{
    public List<MonitorInfo> Enumerate()
    {
        var list = new List<MonitorInfo>();
        var screens = System.Windows.Forms.Screen.AllScreens;
        var qdc = DisplayConfig.GetActiveMonitorNames();

        for (var i = 0; i < screens.Length; i++)
        {
            var s = screens[i];
            var adapterName = s.DeviceName; // \\.\DISPLAY1

            var monitorModel = "";
            if (qdc.TryGetValue(adapterName, out var name) && !string.IsNullOrWhiteSpace(name))
                monitorModel = name;
            else
                monitorModel = QueryMonitorModel(adapterName);

            var label = string.IsNullOrWhiteSpace(monitorModel)
                ? $"显示器 {i + 1}"
                : $"{i + 1}. {monitorModel}";

            list.Add(new MonitorInfo
            {
                Index = i,
                DeviceName = adapterName,
                DisplayName = label,
                HardwareName = monitorModel ?? "",
                X = s.Bounds.X,
                Y = s.Bounds.Y,
                Width = s.Bounds.Width,
                Height = s.Bounds.Height,
                IsPrimary = s.Primary
            });
        }
        return list;
    }

    /// <summary>Adapter \\.\DISPLAYn → child monitor model via EnumDisplayDevices.</summary>
    private static string QueryMonitorModel(string adapterDeviceName)
    {
        try
        {
            var adapter = new NativeMethods.DISPLAY_DEVICE
            {
                cb = Marshal.SizeOf<NativeMethods.DISPLAY_DEVICE>()
            };
            if (!NativeMethods.EnumDisplayDevicesW(adapterDeviceName, 0, ref adapter, 0))
                return "";
            var name = adapter.DeviceString?.Trim() ?? "";
            // Skip useless adapters
            if (name.Contains("Generic", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("NVIDIA", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Intel", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Display Adapter", StringComparison.OrdinalIgnoreCase))
            {
                return "";
            }
            return name;
        }
        catch
        {
            return "";
        }
    }

    /// <summary>Registry fallback under Enum\DISPLAY\&lt;hwid&gt;.</summary>
    private static string QueryViaRegistry(string adapterDeviceName)
    {
        try
        {
            // Enumerate all DISPLAY subkeys and pick by matching monitor PnP via adapter child.
            using var displayKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Enum\DISPLAY");
            if (displayKey is null) return "";

            foreach (var hw in displayKey.GetSubKeyNames())
            {
                using var hwKey = displayKey.OpenSubKey(hw);
                if (hwKey is null) continue;
                foreach (var inst in hwKey.GetSubKeyNames())
                {
                    using var instKey = hwKey.OpenSubKey(inst);
                    if (instKey is null) continue;
                    var friendly = instKey.GetValue("FriendlyName") as string;
                    if (string.IsNullOrWhiteSpace(friendly)) continue;
                    // Prefer names that look like consumer displays
                    if (friendly.Contains("Generic", StringComparison.OrdinalIgnoreCase))
                        continue;
                    // Return first non-generic; caller matches against audio names later
                    // Better: try match UID from adapter — without EDID link this is weak.
                }
            }
        }
        catch { }
        return "";
    }

    public int MonitorIndexFromWindow(nint hwnd, List<MonitorInfo> monitors)
    {
        if (monitors.Count == 0) return -1;

        var hMon = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (hMon != 0)
        {
            var mi = new NativeMethods.MONITORINFOEX
            {
                cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>(),
                szDevice = new string('\0', 32)
            };
            if (NativeMethods.GetMonitorInfoW(hMon, ref mi))
            {
                var device = mi.szDevice?.TrimEnd('\0');
                var byDevice = monitors.FindIndex(m => string.Equals(m.DeviceName, device, StringComparison.OrdinalIgnoreCase));
                if (byDevice >= 0) return byDevice;
            }
        }

        if (NativeMethods.GetWindowRect(hwnd, out var rect))
        {
            var cx = (rect.Left + rect.Right) / 2;
            var cy = (rect.Top + rect.Bottom) / 2;
            for (var i = 0; i < monitors.Count; i++)
            {
                var m = monitors[i];
                if (cx >= m.X && cx < m.X + m.Width && cy >= m.Y && cy < m.Y + m.Height)
                    return i;
            }
        }

        return 0;
    }

    /// <summary>Auto-map unbound OR duplicate-bound monitors to audio endpoints by name similarity.</summary>
    public static void AutoMapUnbound(List<MonitorInfo> monitors, List<MonitorMapping> maps,
        List<AudioDeviceInfo> outputs, List<AudioDeviceInfo> inputs)
    {
        // Drop bindings that are duplicated across monitors — force rematch by name.
        var dupOut = maps
            .Where(m => !string.IsNullOrEmpty(m.OutputDeviceId))
            .GroupBy(m => m.OutputDeviceId!)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dupIn = maps
            .Where(m => !string.IsNullOrEmpty(m.InputDeviceId))
            .GroupBy(m => m.InputDeviceId!)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var mon in monitors)
        {
            var map = maps.FirstOrDefault(m => m.MonitorIndex == mon.Index);
            if (map is null) continue;

            var key = string.IsNullOrWhiteSpace(mon.HardwareName) ? mon.DisplayName : mon.HardwareName;
            key = key.Replace("显示器", "").Trim().TrimStart('1', '2', '3', '4', '.', ' ');

            var needOut = string.IsNullOrEmpty(map.OutputDeviceId) ||
                          (map.OutputDeviceId is not null && dupOut.Contains(map.OutputDeviceId));
            var needIn = string.IsNullOrEmpty(map.InputDeviceId) ||
                         (map.InputDeviceId is not null && dupIn.Contains(map.InputDeviceId));

            if (needOut)
            {
                var taken = maps.Where(m => m.MonitorIndex != mon.Index &&
                                            !string.IsNullOrEmpty(m.OutputDeviceId) &&
                                            !(m.OutputDeviceId != null && dupOut.Contains(m.OutputDeviceId)))
                    .Select(m => m.OutputDeviceId!)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                // also exclude other monitors' preferred auto picks as we fill in order
                var outDev = FindBestMatch(key, outputs, taken);
                if (outDev is not null)
                {
                    map.OutputDeviceId = outDev.Id;
                    map.OutputDeviceName = outDev.Name;
                    taken.Add(outDev.Id);
                }
            }

            if (needIn)
            {
                var taken = maps.Where(m => m.MonitorIndex != mon.Index &&
                                            !string.IsNullOrEmpty(m.InputDeviceId) &&
                                            !(m.InputDeviceId != null && dupIn.Contains(m.InputDeviceId)))
                    .Select(m => m.InputDeviceId!)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var inDev = FindBestMatch(key, inputs, taken);
                if (inDev is not null)
                {
                    map.InputDeviceId = inDev.Id;
                    map.InputDeviceName = inDev.Name;
                    taken.Add(inDev.Id);
                }
            }
        }
    }

    private static AudioDeviceInfo? FindBestMatch(string displayKey, List<AudioDeviceInfo> devices,
        HashSet<string> excludeIds)
    {
        if (string.IsNullOrWhiteSpace(displayKey)) return null;
        AudioDeviceInfo? best = null;
        var bestScore = 0;

        foreach (var d in devices)
        {
            if (excludeIds.Contains(d.Id)) continue;
            var score = 0;
            if (d.Name.Contains(displayKey, StringComparison.OrdinalIgnoreCase))
                score = 100 + displayKey.Length;
            score += TokenScore(displayKey, d.Name);
            if (score > bestScore)
            {
                bestScore = score;
                best = d;
            }
        }
        return bestScore >= 40 ? best : null;
    }

    private static int TokenScore(string a, string b)
    {
        var score = 0;
        foreach (var token in a.Split(new[] { ' ', '(', ')', '-', '_' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Length >= 3 && b.Contains(token, StringComparison.OrdinalIgnoreCase))
                score += token.Length * 8;
        }
        return score;
    }
}
