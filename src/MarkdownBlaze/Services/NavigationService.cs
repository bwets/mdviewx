using System.Diagnostics;

namespace MarkdownBlaze.Services;

public sealed record SessionItem(int Index, string Path, string Title, bool Current);

/// <summary>
/// Holds the viewer state: the back/forward session history, the current document (rendered HTML +
/// headings + title), the global history, file-watch reloads and link handling. Raises
/// <see cref="Changed"/> whenever the UI should refresh.
/// </summary>
public sealed class NavigationService : IDisposable
{
    private readonly MarkdownService _md;
    private readonly HistoryStore _history;
    private readonly FileWatcher _watcher = new();

    private readonly List<string> _session = [];
    private int _index = -1;
    private readonly Dictionary<string, string> _titleCache = new(StringComparer.OrdinalIgnoreCase);

    public NavigationService(MarkdownService md, HistoryStore history)
    {
        _md = md;
        _history = history;
        _watcher.Changed += Reload;
        JsBridge.Link += OnLink;
        JsBridge.FileDropped += OnFileDropped;
    }

    public string? CurrentPath { get; private set; }
    public string CurrentTitle { get; private set; } = "MarkdownBlaze";
    public string CurrentHtml { get; private set; } = "";
    public IReadOnlyList<Heading> CurrentHeadings { get; private set; } = [];
    public int RenderToken { get; private set; }

    public bool CanBack => _index > 0;
    public bool CanForward => _index >= 0 && _index < _session.Count - 1;

    public IReadOnlyList<HistoryEntry> GlobalHistory => _history.Entries;

    public event Action? Changed;

    public IReadOnlyList<SessionItem> SessionItems()
    {
        var list = new List<SessionItem>(_session.Count);
        for (var i = 0; i < _session.Count; i++)
            list.Add(new SessionItem(i, _session[i], TitleFor(_session[i]), i == _index));
        return list;
    }

    public void Initialize()
    {
        var path = _md.GetStartupFilePath();
        if (path is not null) Navigate(path);
        else Changed?.Invoke();
    }

    public void Navigate(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        var full = Path.GetFullPath(path);

        if (_index < _session.Count - 1)
            _session.RemoveRange(_index + 1, _session.Count - _index - 1);
        if (_session.Count == 0 || !PathEquals(_session[^1], full))
            _session.Add(full);
        _index = _session.Count - 1;
        SetCurrent(_session[_index]);
    }

    /// <summary>
    /// Renders Markdown dropped into the window that has no resolvable file path (WebView2 does not
    /// expose one). <see cref="CurrentPath"/> stays null, so it is not file-watched and "open
    /// containing folder" is inert; relative images/links cannot be resolved without a base folder.
    /// </summary>
    public void OpenContent(string fileName, string markdown)
    {
        var result = _md.RenderText(fileName, markdown);
        CurrentPath = null;
        CurrentTitle = result.Title;
        CurrentHtml = result.BodyHtml;
        CurrentHeadings = result.Headings;
        RenderToken++;
        Changed?.Invoke();
    }

    private void OnFileDropped(string kind, string a, string b)
    {
        switch (kind)
        {
            case "path": Navigate(a); break;      // engine gave a real path → open like any other file
            case "text": OpenContent(a, b); break; // only the file's contents are available
        }
    }

    public void Back() { if (CanBack) { _index--; SetCurrent(_session[_index]); } }
    public void Forward() { if (CanForward) { _index++; SetCurrent(_session[_index]); } }
    public void GoToIndex(int i) { if (i >= 0 && i < _session.Count) { _index = i; SetCurrent(_session[i]); } }

    public void Reload()
    {
        if (CurrentPath is not null) { Render(CurrentPath); Changed?.Invoke(); }
    }

    private void SetCurrent(string path)
    {
        _watcher.Watch(path);
        Render(path);
        _history.Record(path, CurrentTitle);
        Changed?.Invoke();
    }

    private void Render(string path)
    {
        var result = _md.Render(path);
        CurrentPath = path;
        if (result is null)
        {
            CurrentTitle = Path.GetFileNameWithoutExtension(path);
            CurrentHtml = "<p><em>Unable to open this file.</em></p>";
            CurrentHeadings = [];
        }
        else
        {
            CurrentTitle = result.Title;
            CurrentHtml = result.BodyHtml;
            CurrentHeadings = result.Headings;
            _titleCache[Path.GetFullPath(path)] = result.Title;
        }
        RenderToken++;
    }

    private string TitleFor(string path) =>
        _titleCache.TryGetValue(Path.GetFullPath(path), out var t) ? t : Path.GetFileNameWithoutExtension(path);

    private void OnLink(string kind, string value)
    {
        switch (kind)
        {
            case "nav": Navigate(value); break;
            case "ext": OpenExternal(value); break;
        }
    }

    // ---- external actions (also used by the history context menu) --------------------------------

    public static void OpenExternal(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* best-effort */ }
    }

    public static void OpenInNewWindow(string path)
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (exe is not null)
                Process.Start(new ProcessStartInfo(exe, $"\"{path}\"") { UseShellExecute = false });
        }
        catch { /* best-effort */ }
    }

    public static void OpenContainingFolder(string path)
    {
        if (!File.Exists(path)) return;
        try
        {
            ProcessStartInfo info;
            if (OperatingSystem.IsWindows())
                info = new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"");
            else if (OperatingSystem.IsMacOS())
                info = new ProcessStartInfo("open", $"-R \"{path}\"");
            else
                info = new ProcessStartInfo("xdg-open", $"\"{Path.GetDirectoryName(path)}\"");
            info.UseShellExecute = true;
            Process.Start(info);
        }
        catch { /* best-effort */ }
    }

    private static bool PathEquals(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    public void Dispose()
    {
        JsBridge.Link -= OnLink;
        JsBridge.FileDropped -= OnFileDropped;
        _watcher.Dispose();
    }
}
