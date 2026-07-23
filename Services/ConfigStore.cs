using System.IO;
using System.Text.Json;
using TaskFirst.Models;

namespace TaskFirst.Services;

/// <summary>Loads/saves <see cref="AppConfig"/> as JSON under %AppData%\TaskFirst.</summary>
public static class ConfigStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };

    public static string Dir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TaskFirst");

    public static string FilePath => Path.Combine(Dir, "config.json");

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json, Options);
                if (cfg is not null) return cfg;
            }
        }
        catch
        {
            // Corrupt config shouldn't brick the app — fall back to defaults.
        }
        var def = AppConfig.CreateDefault();
        Save(def);
        return def;
    }

    public static void Save(AppConfig config)
    {
        Directory.CreateDirectory(Dir);
        var json = JsonSerializer.Serialize(config, Options);
        File.WriteAllText(FilePath, json);
    }
}
