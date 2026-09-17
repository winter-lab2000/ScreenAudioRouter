using Microsoft.Win32;

namespace ScreenAudioRouter.Services;

/// <summary>
/// Autostart via HKCU Run key — no admin required, no installer task needed.
/// </summary>
public static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ScreenAudioRouter";

    public static string ExecutablePath =>
        Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "ScreenAudioRouter.exe");

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            var value = key?.GetValue(ValueName) as string;
            return !string.IsNullOrEmpty(value);
        }
        catch
        {
            return false;
        }
    }

    public static bool Enable()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (key is null) return false;
            key.SetValue(ValueName, $"\"{ExecutablePath}\"");
            return IsEnabled();
        }
        catch
        {
            return false;
        }
    }

    public static bool Disable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
            return !IsEnabled();
        }
        catch
        {
            return false;
        }
    }

    public static bool SetEnabled(bool enabled) => enabled ? Enable() : Disable();
}
