using System.Text;
using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using bwets.Markdig.Extensions;

namespace MarkdownBlaze.Services;

public sealed record Heading(int Level, string Text, string Slug);

public sealed record RenderResult(
    string Path, string File, string Title, string BodyHtml, IReadOnlyList<Heading> Headings);

/// <summary>
/// Renders Markdown to body HTML using the bwets.Markdig.Extensions pipeline (YAML front-matter,
/// advanced extensions, admonitions, mermaid; highlighting via highlight.js classes). Relative links
/// are rewritten for in-app navigation and local images are inlined as data URIs (so they load inside
/// the app:// / http://localhost WebView origin).
/// </summary>
public sealed class MarkdownService
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseYamlFrontMatter()
        .UseAdvancedExtensions()
        .UseAdmonitions()
        .UseMermaid()
        .Build();

    public RenderResult? Render(string markdownFilePath)
    {
        if (string.IsNullOrWhiteSpace(markdownFilePath) || !File.Exists(markdownFilePath))
            return null;

        var fullPath = Path.GetFullPath(markdownFilePath);
        string markdown;
        try { markdown = File.ReadAllText(fullPath); }
        catch { return null; } // locked / permission denied / not really a file → caller shows a fallback
        var baseDir = Path.GetDirectoryName(fullPath) ?? string.Empty;

        return RenderCore(markdown, baseDir, fullPath, Path.GetFileName(fullPath));
    }

    /// <summary>
    /// Renders raw Markdown that has no backing file path (e.g. a file dropped into the WebView, where
    /// the real path is not available). Relative links/images cannot be resolved without a base folder.
    /// </summary>
    public RenderResult RenderText(string fileName, string markdown)
    {
        var name = string.IsNullOrWhiteSpace(fileName) ? "Untitled.md" : fileName.Trim();
        return RenderCore(markdown ?? string.Empty, string.Empty, name, name);
    }

    private static RenderResult RenderCore(string markdown, string baseDir, string path, string file)
    {
        var document = Markdown.Parse(markdown, Pipeline);
        RewriteLinksAndImages(document, baseDir);

        using var writer = new StringWriter();
        var htmlRenderer = new HtmlRenderer(writer);
        Pipeline.Setup(htmlRenderer);
        htmlRenderer.Render(document);
        writer.Flush();
        var body = writer.ToString();

        var headings = ExtractHeadings(document);
        var title = DeriveTitle(headings, path);

        return new RenderResult(path, file, title, body, headings);
    }

    // ---- startup file ---------------------------------------------------------------------------

    /// <summary>File extensions MarkdownBlaze opens (startup argument + drag &amp; drop).</summary>
    public static readonly string[] SupportedExtensions = [".md", ".markdown", ".mdx", ".txt"];

    public static bool IsSupported(string path) =>
        SupportedExtensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));

    public string? GetStartupFilePath()
    {
        foreach (var arg in Environment.GetCommandLineArgs().Skip(1))
        {
            if (string.IsNullOrWhiteSpace(arg)) continue;
            var path = arg.Trim().Trim('"');
            if (!IsSupported(path)) continue;
            var resolved = ResolveExisting(path);
            if (resolved is not null) return resolved;
        }
        // No readable supported argument → fall back to the bundled manual, shown like any other file.
        return GetUserManualPath();
    }

    /// <summary>The bundled user manual shown as a welcome screen when no file is opened.</summary>
    public string? GetUserManualPath()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "UserManual.md");
        return File.Exists(path) ? path : null;
    }

    private static string? ResolveExisting(string path)
    {
        if (File.Exists(path)) return Path.GetFullPath(path);
        if (Path.IsPathRooted(path)) return null;
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, path);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    // ---- headings / title -----------------------------------------------------------------------

    private static List<Heading> ExtractHeadings(MarkdownDocument document)
    {
        var headings = new List<Heading>();
        foreach (var h in document.Descendants<HeadingBlock>())
        {
            var text = InlineText(h.Inline);
            if (string.IsNullOrWhiteSpace(text)) continue;
            headings.Add(new Heading(h.Level, text, h.GetAttributes().Id ?? string.Empty));
        }
        return headings;
    }

    private static string DeriveTitle(IReadOnlyList<Heading> headings, string path)
    {
        var heading = headings.FirstOrDefault(h => h.Level == 1) ?? headings.FirstOrDefault();
        return heading is not null && !string.IsNullOrWhiteSpace(heading.Text)
            ? heading.Text
            : Path.GetFileNameWithoutExtension(path);
    }

    private static string InlineText(ContainerInline? inline)
    {
        if (inline is null) return string.Empty;
        var sb = new StringBuilder();
        foreach (var node in inline.Descendants())
        {
            switch (node)
            {
                case LiteralInline literal: sb.Append(literal.Content.ToString()); break;
                case CodeInline code: sb.Append(code.Content); break;
            }
        }
        return sb.ToString().Trim();
    }

    // ---- link / image rewriting -----------------------------------------------------------------

    private static void RewriteLinksAndImages(MarkdownDocument document, string baseDir)
    {
        foreach (var link in document.Descendants<LinkInline>())
        {
            if (link.IsImage)
            {
                var embedded = EmbedImage(link.Url, baseDir);
                if (embedded is not null) link.Url = embedded;
            }
            else
            {
                RewriteLink(link, baseDir);
            }
        }
    }

    /// <summary>
    /// Internal markdown links (relative paths, folders → index.md, extension-less → .md) become
    /// in-app navigation targets carried in a data-nav attribute; external/anchor links are left as-is.
    /// </summary>
    private static void RewriteLink(LinkInline link, string baseDir)
    {
        var url = link.Url;
        if (string.IsNullOrWhiteSpace(url)) return;
        if (url.StartsWith('#') || url.StartsWith("//", StringComparison.Ordinal)) return;
        if (Uri.TryCreate(url, UriKind.Absolute, out _)) return; // http:, mailto:, file:, ...

        var splitIndex = url.IndexOfAny(['#', '?']);
        var path = splitIndex >= 0 ? url[..splitIndex] : url;
        if (path.Length == 0 || path.StartsWith('/') || path.StartsWith('\\')) return;

        try
        {
            var decoded = Uri.UnescapeDataString(path);
            var fullPath = Path.GetFullPath(Path.Combine(baseDir, decoded));

            var endsWithSeparator = decoded.EndsWith('/') || decoded.EndsWith('\\');
            if (endsWithSeparator || Directory.Exists(fullPath))
            {
                fullPath = Path.Combine(fullPath, "index.md");
            }
            else
            {
                var fileName = Path.GetFileName(decoded);
                if (fileName.Length > 0 && fileName is not ("." or "..")
                    && string.IsNullOrEmpty(Path.GetExtension(fileName)))
                {
                    fullPath += ".md";
                }
            }

            if (fullPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                link.GetAttributes().AddProperty("data-nav", fullPath);
                link.Url = "#";
            }
        }
        catch { /* leave the link untouched */ }
    }

    private static string? EmbedImage(string? url, string baseDir)
    {
        if (string.IsNullOrWhiteSpace(url) || url.StartsWith("data:", StringComparison.Ordinal))
            return null;
        if (Uri.TryCreate(url, UriKind.Absolute, out var abs) && !abs.IsFile)
            return null; // remote image, keep as-is

        try
        {
            var splitIndex = url.IndexOfAny(['#', '?']);
            var path = splitIndex >= 0 ? url[..splitIndex] : url;
            var decoded = Uri.UnescapeDataString(path);
            var full = Path.IsPathRooted(decoded) ? decoded : Path.GetFullPath(Path.Combine(baseDir, decoded));
            if (!File.Exists(full)) return null;

            var bytes = File.ReadAllBytes(full);
            return $"data:{MimeFor(Path.GetExtension(full))};base64,{Convert.ToBase64String(bytes)}";
        }
        catch { return null; }
    }

    private static string MimeFor(string ext) => ext.ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".svg" => "image/svg+xml",
        ".webp" => "image/webp",
        ".bmp" => "image/bmp",
        ".ico" => "image/x-icon",
        _ => "application/octet-stream",
    };
}
