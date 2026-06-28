using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Markdig;

namespace Bwets.Markdig.Extensions.Html;

public enum MarkdownAssetKind
{
    Script,
    Style,
}

/// <summary>A CSS or JS asset that a feature needs, which the host may inline or write as a file.</summary>
public sealed class MarkdownAsset
{
    public MarkdownAsset(string fileName, MarkdownAssetKind kind, string content)
    {
        FileName = fileName;
        Kind = kind;
        Content = content;
    }

    public string FileName { get; }

    public MarkdownAssetKind Kind { get; }

    public string Content { get; }
}

/// <summary>How asset content is delivered to the rendered page.</summary>
public enum MarkdownAssetDelivery
{
    /// <summary>Embed the content directly in the page (works anywhere, larger HTML).</summary>
    Inline,

    /// <summary>Reference assets by file name; the host must write <see cref="MarkdownAsset"/> files next to the page.</summary>
    External,
}

/// <summary>
/// A self-contained Markdown HTML feature: optional Markdig pipeline configuration plus the
/// client-side assets (CSS/JS) and HTML fragments needed to present it in an HTML host.
/// </summary>
public abstract class HtmlMarkdownFeature
{
    /// <summary>Configures the Markdig pipeline for this feature (parsers/renderers).</summary>
    public virtual void Configure(MarkdownPipelineBuilder pipeline)
    {
    }

    /// <summary>Stylesheet/script assets that may be inlined or written next to the document.</summary>
    public virtual IEnumerable<MarkdownAsset> Assets => Enumerable.Empty<MarkdownAsset>();

    /// <summary>Extra markup for the document head (placed after this feature's style assets).</summary>
    public virtual string Head => string.Empty;

    /// <summary>Extra markup for the end of the body (placed after this feature's script assets), e.g. init code.</summary>
    public virtual string BodyEnd => string.Empty;
}

/// <summary>An ordered set of enabled <see cref="HtmlMarkdownFeature"/>s, composed into a page.</summary>
public sealed class MarkdownHtmlFeatures
{
    private readonly List<HtmlMarkdownFeature> _features = new List<HtmlMarkdownFeature>();

    public MarkdownHtmlFeatures Add(HtmlMarkdownFeature feature)
    {
        _features.Add(feature);
        return this;
    }

    public IReadOnlyList<HtmlMarkdownFeature> Features => _features;

    /// <summary>Applies every feature's Markdig pipeline configuration.</summary>
    public void Configure(MarkdownPipelineBuilder pipeline)
    {
        foreach (var feature in _features)
        {
            feature.Configure(pipeline);
        }
    }

    /// <summary>All distinct assets across the enabled features (host writes these when delivering externally).</summary>
    public IReadOnlyList<MarkdownAsset> Assets =>
        _features.SelectMany(f => f.Assets)
            .GroupBy(a => a.FileName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

    /// <summary>Builds the <c>&lt;head&gt;</c> content (style assets + extra head markup) for the features.</summary>
    public string RenderHead(MarkdownAssetDelivery delivery)
    {
        var sb = new StringBuilder();
        foreach (var feature in _features)
        {
            foreach (var asset in feature.Assets.Where(a => a.Kind == MarkdownAssetKind.Style))
            {
                sb.AppendLine(RenderAsset(asset, delivery));
            }

            if (!string.IsNullOrEmpty(feature.Head))
            {
                sb.AppendLine(feature.Head);
            }
        }

        return sb.ToString();
    }

    /// <summary>Builds the end-of-body content (script assets + init markup) for the features.</summary>
    public string RenderBodyEnd(MarkdownAssetDelivery delivery)
    {
        var sb = new StringBuilder();
        foreach (var feature in _features)
        {
            foreach (var asset in feature.Assets.Where(a => a.Kind == MarkdownAssetKind.Script))
            {
                sb.AppendLine(RenderAsset(asset, delivery));
            }

            if (!string.IsNullOrEmpty(feature.BodyEnd))
            {
                sb.AppendLine(feature.BodyEnd);
            }
        }

        return sb.ToString();
    }

    private static string RenderAsset(MarkdownAsset asset, MarkdownAssetDelivery delivery)
    {
        if (delivery == MarkdownAssetDelivery.External)
        {
            return asset.Kind == MarkdownAssetKind.Script
                ? $"<script src=\"{asset.FileName}\"></script>"
                : $"<link rel=\"stylesheet\" href=\"{asset.FileName}\" />";
        }

        return asset.Kind == MarkdownAssetKind.Script
            ? "<script>" + EscapeScript(asset.Content) + "</script>"
            : "<style>" + asset.Content + "</style>";
    }

    // Prevent an embedded "</script>" literal (e.g. inside a grammar) from closing the inline script tag.
    private static string EscapeScript(string content) =>
        content.Replace("</script", "<\\/script");
}

/// <summary>Activation methods for the bundled HTML features.</summary>
public static class MarkdownHtmlFeatureExtensions
{
    public static MarkdownHtmlFeatures UseAdmonitions(this MarkdownHtmlFeatures features) =>
        features.Add(new AdmonitionFeature());

    public static MarkdownHtmlFeatures UseMermaid(this MarkdownHtmlFeatures features) =>
        features.Add(new MermaidFeature());

    /// <param name="extendedLanguages">
    /// false bundles the common ~38 languages (small); true bundles all ~190 languages plus Razor.
    /// </param>
    public static MarkdownHtmlFeatures UseSyntaxHighlighting(this MarkdownHtmlFeatures features, bool extendedLanguages = false) =>
        features.Add(new SyntaxHighlightingFeature(extendedLanguages));
}

/// <summary>Reads files embedded in this assembly's <c>Assets</c> folder.</summary>
internal static class EmbeddedAsset
{
    public static string Read(string fileName)
    {
        var assembly = typeof(EmbeddedAsset).GetTypeInfo().Assembly;
        var name = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        if (name == null)
        {
            return string.Empty;
        }

        using (var stream = assembly.GetManifestResourceStream(name))
        {
            if (stream == null)
            {
                return string.Empty;
            }

            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
