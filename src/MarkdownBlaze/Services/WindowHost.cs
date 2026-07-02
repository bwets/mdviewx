using Photino.NET;

namespace MarkdownBlaze.Services;

/// <summary>
/// Holds the app's <see cref="PhotinoWindow"/> (set once at startup) so Blazor components can use
/// native features such as the OS "open file" dialog.
/// </summary>
public sealed class WindowHost
{
    public PhotinoWindow? Window { get; set; }

    /// <summary>Shows the native open-file dialog filtered to supported types; returns the chosen path or null.</summary>
    public async Task<string?> PickFileAsync(string? startDir)
    {
        var window = Window;
        if (window is null) return null;

        var filters = new (string, string[])[]
        {
            ("Markdown", MarkdownService.SupportedExtensions.Select(ext => "*" + ext).ToArray()),
            ("All files", ["*.*"]),
        };

        var files = await window.ShowOpenFileAsync("Open file", startDir, multiSelect: false, filters);
        return files is { Length: > 0 } ? files[0] : null;
    }
}
