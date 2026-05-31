using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Jennifer.Wpf.Config;

/// <summary>
/// Loads and saves <see cref="JenniferSettings"/> to a JSON file in the user's AppData folder.
/// </summary>
/// <invariant>The settings directory is created on first save; a missing file returns default settings without throwing.</invariant>
public static class JenniferSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Gets the full path to the settings JSON file.</summary>
    public static string SettingsFilePath { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Jennifer", "settings.json");

    /// <summary>
    /// Loads settings from disk. Returns default <see cref="JenniferSettings"/> when no file exists or parsing fails.
    /// </summary>
    /// <returns>The loaded or default settings.</returns>
    /// <post>The returned instance is never null and always has valid defaults.</post>
    public static JenniferSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                return new JenniferSettings();
            }

            string json = File.ReadAllText(SettingsFilePath);
            return JsonSerializer.Deserialize<JenniferSettings>(json, SerializerOptions) ?? new JenniferSettings();
        }
        catch
        {
            // Corrupt / unreadable settings — fall back to defaults silently.
            return new JenniferSettings();
        }
    }

    /// <summary>
    /// Saves <paramref name="settings"/> to disk asynchronously.
    /// </summary>
    /// <param name="settings">The settings to persist.</param>
    /// <pre><paramref name="settings"/> is not null.</pre>
    /// <post>The settings file is written to <see cref="SettingsFilePath"/>. Errors are swallowed to avoid crashing the UI.</post>
    public static async Task SaveAsync(JenniferSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            string directory = Path.GetDirectoryName(SettingsFilePath)!;
            Directory.CreateDirectory(directory);

            string json = JsonSerializer.Serialize(settings, SerializerOptions);
            await File.WriteAllTextAsync(SettingsFilePath, json).ConfigureAwait(false);
        }
        catch
        {
            // Settings save failure should never crash the application.
        }
    }

    /// <summary>
    /// Saves <paramref name="settings"/> to disk synchronously. Prefer <see cref="SaveAsync"/> where possible.
    /// </summary>
    /// <param name="settings">The settings to persist.</param>
    public static void Save(JenniferSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            string directory = Path.GetDirectoryName(SettingsFilePath)!;
            Directory.CreateDirectory(directory);

            string json = JsonSerializer.Serialize(settings, SerializerOptions);
            File.WriteAllText(SettingsFilePath, json);
        }
        catch { }
    }
}
