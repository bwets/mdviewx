using System.Collections.Generic;

namespace mdviewx.Services;

/// <summary>A heading in the rendered document, for building an outline / table of contents.</summary>
public sealed partial record HeadingInfo(int Level, string Text, string Id);

/// <summary>The result of rendering a markdown file.</summary>
public sealed record RenderResult(Uri Uri, IReadOnlyList<HeadingInfo> Headings);

public interface IMarkdownRenderer
{
    /// <summary>
    /// Returns the path to the markdown file passed on the command line, or null if none.
    /// </summary>
    string? GetStartupFilePath();

    /// <summary>
    /// Reads the markdown file, converts it to a standalone HTML document written to a temporary
    /// file, and returns its <see cref="Uri"/> together with the document's headings. Returns null
    /// if the file cannot be read.
    /// </summary>
    RenderResult? Render(string markdownFilePath);
}
