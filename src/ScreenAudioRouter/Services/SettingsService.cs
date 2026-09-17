using System.Text.Json;
using ScreenAudioRouter.Models;

namespace ScreenAudioRouter.Services;

public static class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(AppSettings.ConfigPath))
            {
                var json = File.ReadAllText(AppSettings.ConfigPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (settings is not null)
                    return settings;
            }
        }
        catch
        {
            // fall through to defaults
        }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(AppSettings.ConfigDirectory);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(AppSettings.ConfigPath, json);
    }
}

public sealed class LogService
{
    private readonly object _gate = new();
    private readonly string _path;

    public event Action<string>? LineAdded;

    public LogService()
    {
        _path = Path.Combine(AppSettings.ConfigDirectory, "router.log");
        Directory.CreateDirectory(AppSettings.ConfigDirectory);
    }

    public string LogFilePath => _path;

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex is null ? message : $"{message} | {ex.GetType().Name}: {ex.Message}");

    private void Write(string level, string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss.fff} [{level}] {message}";
        lock (_gate)
        {
            try
            {
                File.AppendAllText(_path, line + Environment.NewLine);
            }
            catch
            {
                // logging must never crash routing
            }
        }
        LineAdded?.Invoke(line);
    }
}
