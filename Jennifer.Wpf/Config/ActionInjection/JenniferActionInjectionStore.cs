using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jennifer.Wpf.Config.ActionInjection;

/// <summary>
/// Loads and saves the <see cref="JenniferActionInjectionConfig"/> to a JSON file in the
/// Jennifer AppData folder (<c>action_injection.json</c>).
/// </summary>
/// <invariant>A missing or corrupt file is silently replaced with an empty default config.</invariant>
public static class JenniferActionInjectionStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Gets the full path to the injection config JSON file.</summary>
    public static string ConfigFilePath { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "Jennifer", "action_injection.json");

    /// <summary>
    /// Loads the injection config from disk.
    /// Returns an empty <see cref="JenniferActionInjectionConfig"/> when no file exists or parsing fails.
    /// </summary>
    /// <returns>The loaded or default injection config.</returns>
    /// <post>The returned instance is never null and its collections are non-null.
    /// Built-in actions absent from an existing config are merged in automatically.</post>
    public static JenniferActionInjectionConfig Load()
    {
        JenniferActionInjectionConfig config;
        try
        {
            if (!File.Exists(ConfigFilePath))
                return CreateDefaultConfig();

            string json = File.ReadAllText(ConfigFilePath);
            config = JsonSerializer.Deserialize<JenniferActionInjectionConfig>(json, SerializerOptions)
                     ?? new JenniferActionInjectionConfig();
        }
        catch
        {
            return new JenniferActionInjectionConfig();
        }

        // Ensure built-in actions are present even when loading an older config file.
        MergeBuiltIns(config);
        return config;
    }

    /// <summary>
    /// Adds any built-in default actions that are missing from <paramref name="config"/>.
    /// Existing entries (keyed by name) are preserved as-is so user edits are not overwritten.
    /// </summary>
    private static void MergeBuiltIns(JenniferActionInjectionConfig config)
    {
        JenniferActionInjectionConfig defaults = CreateDefaultConfig();
        var existingNames = new HashSet<string>(
            config.Actions.Select(a => a.Name ?? string.Empty),
            StringComparer.OrdinalIgnoreCase);

        foreach (InjectedAction builtin in defaults.Actions)
        {
            if (!existingNames.Contains(builtin.Name ?? string.Empty))
                config.Actions.Add(builtin);
        }
    }

    /// <summary>
    /// Saves <paramref name="config"/> to disk.
    /// </summary>
    /// <param name="config">The config to persist.</param>
    /// <pre><paramref name="config"/> is not null.</pre>
    /// <post>The config file is written to <see cref="ConfigFilePath"/>.</post>
    public static void Save(JenniferActionInjectionConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        try
        {
            string dir = Path.GetDirectoryName(ConfigFilePath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(ConfigFilePath, JsonSerializer.Serialize(config, SerializerOptions));
        }
        catch { }
    }

    /// <summary>
    /// Writes a default example config file if one does not already exist.
    /// This gives users a discoverable template to customize.
    /// </summary>
    /// <returns>The default config that was written (or would have been written).</returns>
    public static JenniferActionInjectionConfig EnsureDefaultExists()
    {
        if (File.Exists(ConfigFilePath))
            return Load();

        JenniferActionInjectionConfig def = CreateDefaultConfig();
        Save(def);
        return def;
    }

    private static JenniferActionInjectionConfig CreateDefaultConfig()
    {
        // Provides a self-documenting example. Users can add their own actions here.
        return new JenniferActionInjectionConfig
        {
            GameName = null,
            Actions =
            [
                new InjectedAction
                {
                    Name = "get_game_speed",
                    Description = "Returns the current game speed index (0=paused, 1=normal, 2=fast, 3=very fast).",
                    SchemaJson = null,
                    ShowQuickButton = true,
                },
                new InjectedAction
                {
                    Name = "set_game_speed",
                    Description = "Sets the game speed. speed: 0=pause, 1=normal, 2=fast, 3=very fast.",
                    SchemaJson = """{"type":"object","properties":{"speed":{"type":"integer","enum":[0,1,2,3]}},"required":["speed"]}""",
                    ShowQuickButton = false,
                },
                new InjectedAction
                {
                    Name = "set_custom_schedule",
                    Description = "Create a custom schedule by specifying the activity for each of the 24 hours. Each hour accepts: work, sleep, recreation, bathing.",
                    SchemaJson = BuildCustomScheduleSchema(),
                    ShowQuickButton = false,
                },
            ],
        };
    }

    private static string BuildCustomScheduleSchema()
    {
        // Builds: {"type":"object","properties":{"hour_0":{...},...,"hour_23":{...}},"required":["hour_0",...,"hour_23"]}
        var sb = new System.Text.StringBuilder();
        sb.Append("{\"type\":\"object\",\"properties\":{");
        for (int i = 0; i < 24; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append($"\"hour_{i}\":{{\"type\":\"string\",\"enum\":[\"work\",\"sleep\",\"recreation\",\"bathing\"]}}");
        }
        sb.Append("},\"required\":[");
        for (int i = 0; i < 24; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append($"\"hour_{i}\"");
        }
        sb.Append("]}");
        return sb.ToString();
    }
}
