using System.Reflection;
using System.Text;

namespace YoutubePlaylistSynchroniszer;

/// <summary>
/// Registers, loads and saves all <see cref="Settings"/> fields to the single config file. Settings
/// are discovered by reflection (the field name is the key), so adding one to <see cref="Settings"/>
/// is the only step needed.
/// </summary>
internal static class SettingsManager
{
    static readonly Dictionary<string, ISettingSetup> iSettingSetups = [];
    static readonly Dictionary<string, ISetting> iSettings = [];

    public static ISetting[] GetAllSettings() => [.. iSettings.Values];

    static SettingsManager()
    {
        foreach (var field in typeof(Settings).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            object? value = field.GetValue(null);
            if (value is ISettingSetup setupSetting)
            {
                setupSetting.InitializeKey(field.Name);
                iSettingSetups.Add(field.Name, setupSetting);
                if (value is ISetting setting)
                    iSettings[field.Name] = setting;
            }
        }
    }

    /// <summary>Loads settings from the config file, creating it with defaults if missing.</summary>
    public static void LoadSettings()
    {
        string path = ConfigPathResolver.ConfigPath;
        if (!File.Exists(path))
        {
            SaveSettings();
            return;
        }

        foreach (var line in File.ReadAllLines(path))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith(AppConstants.CommentPrefix)) continue;

            // Split on the FIRST separator only so values that contain it survive intact.
            var parts = line.Split(AppConstants.KeyValueSeparator, 2);
            if (parts.Length != 2) continue;

            string key = parts[0].Trim();
            string value = parts[1].Trim();

            if (iSettingSetups.TryGetValue(key, out var setting))
                setting.LoadFromStr(value);
        }
    }

    /// <summary>Serializes all settings to the single config file (atomic write).</summary>
    public static void SaveSettings()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{AppConstants.CommentPrefix} {AppConstants.AppTitle} Configuration");
        sb.AppendLine($"{AppConstants.CommentPrefix} Generated {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        foreach (var setting in iSettingSetups.Values)
            sb.AppendLine($"{setting.Key} {AppConstants.KeyValueSeparator} {setting.Serialize()}");

        try
        {
            Directory.CreateDirectory(ConfigPathResolver.ConfigFolder);
            AtomicFile.WriteAllText(ConfigPathResolver.ConfigPath, sb.ToString());
        }
        catch (Exception ex)
        {
            Log("Failed to save settings: " + ex, LogLevel.Error);
        }
    }

    /// <summary>Reset every registered setting to its shipped default and persist.</summary>
    public static void ResetAllToDefaults()
    {
        foreach (var s in iSettings.Values) s.ResetToDefault();
        SaveSettings();
    }
}
