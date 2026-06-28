using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Path = System.IO.Path;

namespace mdviewx.Services;

public sealed record HistoryEntry(string Path, string Title, DateTimeOffset LastOpenedUtc);

public interface IHistoryService
{
    /// <summary>Opened files, most-recent first.</summary>
    IReadOnlyList<HistoryEntry> Entries { get; }

    /// <summary>Records that a file was opened (de-duplicated, moved to the front, persisted).</summary>
    void Record(string path, string title);
}

/// <summary>Persists the global list of opened files to the user's local app data folder.</summary>
public sealed class HistoryService : IHistoryService
{
    private const int MaxEntries = 200;
    private readonly string _filePath;
    private readonly List<HistoryEntry> _entries;

    public HistoryService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "mdviewx");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "history.json");
        _entries = Load();
    }

    public IReadOnlyList<HistoryEntry> Entries => _entries;

    public void Record(string path, string title)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var full = Path.GetFullPath(path);
        if (string.IsNullOrWhiteSpace(title))
        {
            title = Path.GetFileNameWithoutExtension(full);
        }

        _entries.RemoveAll(e => string.Equals(e.Path, full, StringComparison.OrdinalIgnoreCase));
        _entries.Insert(0, new HistoryEntry(full, title, DateTimeOffset.UtcNow));
        if (_entries.Count > MaxEntries)
        {
            _entries.RemoveRange(MaxEntries, _entries.Count - MaxEntries);
        }

        Save();
    }

    private List<HistoryEntry> Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var list = JsonSerializer.Deserialize(json, HistoryJsonContext.Default.ListHistoryEntry);
                if (list is not null)
                {
                    return list;
                }
            }
        }
        catch
        {
            // Corrupt or unreadable history is non-fatal; start fresh.
        }

        return new List<HistoryEntry>();
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_entries, HistoryJsonContext.Default.ListHistoryEntry);
            File.WriteAllText(_filePath, json);
        }
        catch
        {
            // Best-effort persistence.
        }
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(List<HistoryEntry>))]
internal partial class HistoryJsonContext : JsonSerializerContext
{
}
