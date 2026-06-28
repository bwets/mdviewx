using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Path = System.IO.Path;
#if WINDOWS
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
#endif

namespace mdviewx.Services;

public sealed class MarkdownRenderer : IMarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = BuildPipeline();

    private static MarkdownPipeline BuildPipeline()
    {
        var builder = new MarkdownPipelineBuilder()
            .UseYamlFrontMatter()
            .UseAdvancedExtensions();
        builder.Extensions.Add(new AdmonitionExtension());
        builder.Extensions.Add(new MermaidExtension());
        return builder.Build();
    }

    // GitHub light/dark highlight themes are small enough to inline; the large JS libraries
    // (highlight.js, mermaid) are written next to the preview as sidecar files (see EnsureAssets).
    private static readonly string HighlightCss = BuildHighlightCss();
    private static bool _assetsWritten;

    private static string BuildHighlightCss()
    {
        var light = ReadEmbedded("github.min.css");
        var dark = ReadEmbedded("github-dark.min.css");
        return $"{light}\n@media (prefers-color-scheme: dark) {{\n{dark}\n}}";
    }

    /// <summary>
    /// Writes the bundled JS libraries next to the preview files once, so each preview can reference
    /// them via a relative &lt;script src&gt; instead of inlining megabytes into every document.
    /// </summary>
    private static void EnsureAssets(string outputDir)
    {
        if (_assetsWritten)
        {
            return;
        }

        File.WriteAllText(Path.Combine(outputDir, "highlight.min.js"), ReadEmbedded("highlight.min.js"));
        File.WriteAllText(Path.Combine(outputDir, "mermaid.min.js"), ReadEmbedded("mermaid.min.js"));
        _assetsWritten = true;
    }

    private static string ReadEmbedded(string endsWith)
    {
        var assembly = typeof(MarkdownRenderer).Assembly;
        var name = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(endsWith, StringComparison.OrdinalIgnoreCase));
        if (name is null)
        {
            return string.Empty;
        }

        using var stream = assembly.GetManifestResourceStream(name);
        if (stream is null)
        {
            return string.Empty;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public string? GetStartupFilePath()
    {
        foreach (var candidate in EnumerateCandidateArgs())
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var path = candidate.Trim().Trim('"');
            if (path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    /// <summary>
    /// Yields every possible argument string from all launch mechanisms. On Desktop the file is
    /// passed via the process command line; on WinAppSDK (WinUI 3) command-line and file-association
    /// launches surface through the WindowsAppSDK activation API instead.
    /// </summary>
    private static IEnumerable<string> EnumerateCandidateArgs()
    {
        // Desktop / generic .NET command line (args[0] is the executable, skipped by the .md filter).
        foreach (var arg in Environment.GetCommandLineArgs())
        {
            yield return arg;
        }

#if WINDOWS
        AppActivationArguments? activated = null;
        try
        {
            activated = Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs();
        }
        catch
        {
            // Activation info may be unavailable in some hosting modes; ignore and fall back.
        }

        if (activated?.Data is ILaunchActivatedEventArgs launch && !string.IsNullOrWhiteSpace(launch.Arguments))
        {
            // The whole string handles a single (possibly quoted) path that contains spaces...
            yield return launch.Arguments;
            // ...and the split handles multiple space-separated arguments.
            foreach (var token in launch.Arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                yield return token;
            }
        }

        if (activated?.Data is IFileActivatedEventArgs fileArgs)
        {
            foreach (var file in fileArgs.Files.OfType<StorageFile>())
            {
                yield return file.Path;
            }
        }
#endif
    }

    public RenderResult? Render(string markdownFilePath)
    {
        if (string.IsNullOrWhiteSpace(markdownFilePath) || !File.Exists(markdownFilePath))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(markdownFilePath);
        var markdown = File.ReadAllText(fullPath);
        var baseDir = Path.GetDirectoryName(fullPath) ?? string.Empty;

        // Parse, then rewrite relative link/image URLs to absolute file URIs rooted at the
        // opened file's folder, before rendering to HTML.
        var document = Markdown.Parse(markdown, Pipeline);
        RewriteRelativeUrls(document, baseDir);

        using var writer = new StringWriter();
        var htmlRenderer = new HtmlRenderer(writer);
        Pipeline.Setup(htmlRenderer);
        htmlRenderer.Render(document);
        writer.Flush();
        var body = writer.ToString();

        // Headings (with their auto-generated ids) are read after rendering, so the ids match the
        // anchors in the produced HTML.
        var headings = ExtractHeadings(document);

        var html = BuildDocument(Path.GetFileName(fullPath), body);

        var outputDir = Path.Combine(Path.GetTempPath(), "mdviewx");
        Directory.CreateDirectory(outputDir);
        EnsureAssets(outputDir);
        // One output file per source path so navigating between documents always changes the
        // WebView URL (otherwise it may not reload).
        var key = ((uint)fullPath.ToLowerInvariant().GetHashCode()).ToString("x8");
        var outputFile = Path.Combine(outputDir, $"preview_{key}.html");
        File.WriteAllText(outputFile, html, Encoding.UTF8);

        return new RenderResult(new Uri(outputFile), headings);
    }

    private static List<HeadingInfo> ExtractHeadings(MarkdownDocument document)
    {
        var headings = new List<HeadingInfo>();
        foreach (var heading in document.Descendants<HeadingBlock>())
        {
            var text = GetInlineText(heading.Inline);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var id = heading.GetAttributes().Id ?? string.Empty;
            headings.Add(new HeadingInfo(heading.Level, text, id));
        }

        return headings;
    }

    private static string GetInlineText(ContainerInline? inline)
    {
        if (inline is null)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var node in inline.Descendants())
        {
            switch (node)
            {
                case LiteralInline literal:
                    sb.Append(literal.Content.ToString());
                    break;
                case CodeInline code:
                    sb.Append(code.Content);
                    break;
            }
        }

        return sb.ToString().Trim();
    }

    /// <summary>
    /// Rewrites relative link and image URLs so they resolve against the opened file's folder,
    /// turning them into absolute file URIs. Absolute URLs (http, mailto, ...), protocol-relative
    /// URLs, root-relative paths and in-page anchors are left untouched.
    /// </summary>
    private static void RewriteRelativeUrls(MarkdownDocument document, string baseDir)
    {
        foreach (var link in document.Descendants<LinkInline>())
        {
            link.Url = RewriteUrl(link.Url, baseDir, link.IsImage);
        }
    }

    private static string? RewriteUrl(string? url, string baseDir, bool isImage)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return url;
        }

        // In-page anchor, protocol-relative, or anything with a scheme (http:, mailto:, file:, ...).
        if (url.StartsWith('#') || url.StartsWith("//", StringComparison.Ordinal))
        {
            return url;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            return url;
        }

        // Preserve any query/fragment suffix while resolving the path part.
        var splitIndex = url.IndexOfAny(new[] { '#', '?' });
        var path = splitIndex >= 0 ? url[..splitIndex] : url;
        var suffix = splitIndex >= 0 ? url[splitIndex..] : string.Empty;

        // Empty path (pure query/fragment) or root-relative path we cannot reliably resolve.
        if (path.Length == 0 || path.StartsWith('/') || path.StartsWith('\\'))
        {
            return url;
        }

        try
        {
            var decoded = Uri.UnescapeDataString(path);
            var fullPath = Path.GetFullPath(Path.Combine(baseDir, decoded));

            if (!isImage)
            {
                var endsWithSeparator = decoded.EndsWith('/') || decoded.EndsWith('\\');
                if (endsWithSeparator || Directory.Exists(fullPath))
                {
                    // A link to a folder resolves to its index document.
                    fullPath = Path.Combine(fullPath, "index.md");
                }
                else
                {
                    // Wiki-style links (e.g. Docusaurus) omit the .md extension; add it back so the
                    // file resolves. Skip "."/".." segments.
                    var fileName = Path.GetFileName(decoded);
                    if (fileName.Length > 0 && fileName is not ("." or "..")
                        && string.IsNullOrEmpty(Path.GetExtension(fileName)))
                    {
                        fullPath += ".md";
                    }
                }
            }

            return new Uri(fullPath).AbsoluteUri + suffix;
        }
        catch
        {
            return url;
        }
    }

    private static string BuildDocument(string title, string body) => $$"""
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset="utf-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1" />
            <title>{{title}}</title>
            <style>
                :root { color-scheme: light dark; }
                body {
                    font-family: -apple-system, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
                    line-height: 1.6;
                    max-width: 880px;
                    margin: 0 auto;
                    padding: 2rem 1.5rem 4rem;
                    color: #1f2328;
                    background: #ffffff;
                }
                h1, h2 { border-bottom: 1px solid #d0d7de; padding-bottom: .3em; }
                h1, h2, h3, h4 { font-weight: 600; line-height: 1.25; margin-top: 1.5em; }
                a { color: #0969da; text-decoration: none; }
                a:hover { text-decoration: underline; }
                code {
                    font-family: "Cascadia Code", Consolas, monospace;
                    background: rgba(175,184,193,.2);
                    padding: .2em .4em;
                    border-radius: 6px;
                    font-size: 85%;
                }
                pre { margin: 1em 0; overflow: auto; }
                pre code.hljs { padding: 1rem; border-radius: 6px; }
                pre.mermaid { background: transparent; text-align: center; line-height: normal; }
                blockquote {
                    margin: 0;
                    padding: 0 1em;
                    color: #59636e;
                    border-left: .25em solid #d0d7de;
                }
                table { border-collapse: collapse; }
                th, td { border: 1px solid #d0d7de; padding: 6px 13px; }
                tr:nth-child(2n) { background: #f6f8fa; }
                img { max-width: 100%; }
                .admonition {
                    margin: 1em 0;
                    border: 1px solid #d0d7de;
                    border-left: 4px solid var(--adm, #448aff);
                    border-radius: 6px;
                    overflow: hidden;
                    background: #ffffff;
                }
                .admonition-title {
                    display: flex;
                    align-items: center;
                    gap: .5em;
                    font-weight: 600;
                    padding: .5em .9em;
                    background: var(--adm-bg, rgba(68,138,255,.1));
                }
                .admonition-icon { font-style: normal; line-height: 1; }
                .admonition-body { padding: .2em .9em; }
                .admonition-body > :first-child { margin-top: .6em; }
                .admonition-body > :last-child { margin-bottom: .6em; }
                details.admonition > summary.admonition-title { cursor: pointer; list-style: none; }
                details.admonition > summary.admonition-title::-webkit-details-marker { display: none; }
                .admonition-note     { --adm: #448aff; --adm-bg: rgba(68,138,255,.1); }
                .admonition-abstract { --adm: #00b0ff; --adm-bg: rgba(0,176,255,.1); }
                .admonition-info     { --adm: #00b8d4; --adm-bg: rgba(0,184,212,.1); }
                .admonition-tip      { --adm: #00bfa5; --adm-bg: rgba(0,191,165,.1); }
                .admonition-success  { --adm: #00c853; --adm-bg: rgba(0,200,83,.1); }
                .admonition-question { --adm: #9c27b0; --adm-bg: rgba(156,39,176,.1); }
                .admonition-warning  { --adm: #ff9100; --adm-bg: rgba(255,145,0,.12); }
                .admonition-danger   { --adm: #ff1744; --adm-bg: rgba(255,23,68,.1); }
                .admonition-bug      { --adm: #f50057; --adm-bg: rgba(245,0,87,.1); }
                .admonition-example  { --adm: #7c4dff; --adm-bg: rgba(124,77,255,.1); }
                .admonition-quote    { --adm: #9e9e9e; --adm-bg: rgba(158,158,158,.12); }
                @media (prefers-color-scheme: dark) {
                    body { color: #e6edf3; background: #0d1117; }
                    h1, h2 { border-color: #30363d; }
                    a { color: #4493f8; }
                    code { background: #161b22; }
                    blockquote { color: #9198a1; border-color: #30363d; }
                    th, td { border-color: #30363d; }
                    tr:nth-child(2n) { background: #161b22; }
                    .admonition { background: #0d1117; border-color: #30363d; }
                }
            </style>
            <style>
            {{HighlightCss}}
            </style>
        </head>
        <body>
        {{body}}
        <script src="highlight.min.js"></script>
        <script>hljs.highlightAll();</script>
        <script src="mermaid.min.js"></script>
        <script>
            (function () {
                var dark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
                mermaid.initialize({ startOnLoad: false, theme: dark ? 'dark' : 'default' });
                mermaid.run();
            })();
        </script>
        </body>
        </html>
        """;
}
