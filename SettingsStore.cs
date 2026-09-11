using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TILageMonitor;

public sealed class AppSettings
{
    [JsonPropertyName("darkMode")]
    public bool DarkMode { get; set; }

    [JsonPropertyName("autoStart")]
    public bool AutoStart { get; set; }

    /// <summary>
    /// Globale Benachrichtigungen (Toasts/Balloons). Default: true.
    /// Tray-Menü kann dies umschalten; pro-Dienst Filter gilt nur wenn aktiv.
    /// </summary>
    [JsonPropertyName("notificationsEnabled")]
    public bool NotificationsEnabled { get; set; } = true;

    /// <summary>
    /// Pro-Dienst Benachrichtigungen. Fehlende Keys = aktiviert.
    /// Keys: erezept, epa, kim, wanda, ogd, vsdm, tianschluss
    /// </summary>
    [JsonPropertyName("notifyServices")]
    public Dictionary<string, bool> NotifyServices { get; set; } = CreateDefaultNotifyServices();

    public static readonly string[] ServiceKeys =
    [
        "erezept", "epa", "kim", "wanda", "ogd", "vsdm", "tianschluss"
    ];

    public static readonly Dictionary<string, string> ServiceDisplayNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["erezept"] = "eRezept",
            ["epa"] = "ePA",
            ["kim"] = "KIM",
            ["wanda"] = "WANDA",
            ["ogd"] = "OGD",
            ["vsdm"] = "VSDM",
            ["tianschluss"] = "TI-Anschluss"
        };

    public static Dictionary<string, bool> CreateDefaultNotifyServices()
    {
        var dict = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in ServiceKeys)
            dict[key] = true;
        return dict;
    }

    /// <summary>True, wenn Benachrichtigungen für diesen Dienst aktiv sind (Default: true).</summary>
    public bool IsNotifyEnabled(string serviceKey)
    {
        if (NotifyServices is null || NotifyServices.Count == 0)
            return true;

        if (NotifyServices.TryGetValue(serviceKey, out var enabled))
            return enabled;

        // Case-insensitive fallback
        foreach (var kv in NotifyServices)
        {
            if (string.Equals(kv.Key, serviceKey, StringComparison.OrdinalIgnoreCase))
                return kv.Value;
        }

        return true;
    }

    public void EnsureNotifyDefaults()
    {
        NotifyServices ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in ServiceKeys)
        {
            if (!NotifyServices.ContainsKey(key) &&
                !NotifyServices.Keys.Any(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase)))
            {
                NotifyServices[key] = true;
            }
        }
    }
}

public static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static string SettingsDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TILageMonitor");

    public static string SettingsPath =>
        Path.Combine(SettingsDirectory, "settings.json");

    public static string SettingsDirectoryPath => SettingsDirectory;

    public static bool Exists => File.Exists(SettingsPath);

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                var defaults = new AppSettings();
                defaults.EnsureNotifyDefaults();
                return defaults;
            }

            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions)
                           ?? new AppSettings();
            settings.EnsureNotifyDefaults();
            return settings;
        }
        catch
        {
            var defaults = new AppSettings();
            defaults.EnsureNotifyDefaults();
            return defaults;
        }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            settings.EnsureNotifyDefaults();
            Directory.CreateDirectory(SettingsDirectory);
            AtomicJsonStore.Write(SettingsPath, settings, JsonOptions);
        }
        catch
        {
            // Einstellungen sind optional – Fehler still ignorieren
        }
    }
}
