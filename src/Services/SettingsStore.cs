using System;
using System.IO;
using System.Text.Json;
using UniversalSensRandomizer.Json;
using UniversalSensRandomizer.Models;

namespace UniversalSensRandomizer.Services;

public sealed class SettingsStore
{
    private readonly string settingsPath;

    public SettingsStore()
    {
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "UniversalSensRandomizer");
        Directory.CreateDirectory(folder);
        settingsPath = Path.Combine(folder, "settings.json");
    }

    public string SettingsPath => settingsPath;

    public PersistedSettings Load()
    {
        if (!File.Exists(settingsPath))
        {
            return new PersistedSettings();
        }

        try
        {
            using FileStream stream = File.OpenRead(settingsPath);
            PersistedSettings? loaded = JsonSerializer.Deserialize(stream, AppJsonContext.Default.PersistedSettings);
            return loaded is { } value ? value : new PersistedSettings();
        }
        catch (Exception)
        {
            return new PersistedSettings();
        }
    }

    public void Save(PersistedSettings settings)
    {
        string tempPath = settingsPath + ".tmp";
        using (FileStream stream = File.Create(tempPath))
        {
            JsonSerializer.Serialize(stream, settings, AppJsonContext.Default.PersistedSettings);
        }
        File.Move(tempPath, settingsPath, overwrite: true);
    }
}
