using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Path = System.IO.Path;

namespace mdviewx.Services;

public sealed class Preferences
{
    public bool SidebarPinned { get; set; } = true;
    public double SidebarWidth { get; set; } = 280;
    public int SidebarTab { get; set; }
}

public interface IPreferencesService
{
    Preferences Current { get; }
    void Save();
}

/// <summary>Persists user preferences to the local app data folder.</summary>
public sealed class PreferencesService : IPreferencesService
{
    private readonly string _filePath;

    public PreferencesService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "mdviewx");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "preferences.json");
        Current = Load();
    }

    public Preferences Current { get; }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(Current, PreferencesJsonContext.Default.Preferences);
            File.WriteAllText(_filePath, json);
        }
        catch
        {
            // Best-effort persistence.
        }
    }

    private Preferences Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var prefs = JsonSerializer.Deserialize(json, PreferencesJsonContext.Default.Preferences);
                if (prefs is not null)
                {
                    return prefs;
                }
            }
        }
        catch
        {
            // Corrupt preferences are non-fatal; fall back to defaults.
        }

        return new Preferences();
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Preferences))]
internal partial class PreferencesJsonContext : JsonSerializerContext
{
}
