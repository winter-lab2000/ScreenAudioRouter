using System.Text.Json.Serialization;

namespace ScreenAudioRouter.Models;

public sealed class AppSettings
{
    public bool Enabled { get; set; } = true;
    public bool RoutePlayback { get; set; } = true;
    public bool RouteMicrophone { get; set; } = true;
    public bool SwitchDefaultOutput { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public int PollIntervalMs { get; set; } = 250;
    public List<MonitorMapping> Monitors { get; set; } = [];
    public List<string> IgnoredProcesses { get; set; } =
    [
        "explorer", "ApplicationFrameHost", "ShellExperienceHost", "TextInputHost",
        "SystemSettings", "ScreenAudioRouter"
    ];
    public List<string> PinnedProcesses { get; set; } = [];

    public static string ConfigDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenAudioRouter");

    public static string ConfigPath => Path.Combine(ConfigDirectory, "settings.json");
}

public sealed class MonitorMapping
{
    public int MonitorIndex { get; set; }
    public string DeviceName { get; set; } = "";
    public int BoundsX { get; set; }
    public int BoundsY { get; set; }
    public int BoundsWidth { get; set; }
    public int BoundsHeight { get; set; }
    public string? OutputDeviceId { get; set; }
    public string? OutputDeviceName { get; set; }
    public string? InputDeviceId { get; set; }
    public string? InputDeviceName { get; set; }
    public int VolumePercent { get; set; } = -1;
}

public sealed class AudioDeviceInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsCapture { get; set; }
    public bool IsDefault { get; set; }
    public bool IsDefaultCommunications { get; set; }
}

public sealed class MonitorInfo
{
    public int Index { get; set; }
    public string DeviceName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool IsPrimary { get; set; }
    public string HardwareName { get; set; } = "";
}

public sealed class TrackedWindow
{
    public nint Hwnd { get; set; }
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = "";
    public string Title { get; set; } = "";
    public int MonitorIndex { get; set; } = -1;
    public bool IsForeground { get; set; }
}
