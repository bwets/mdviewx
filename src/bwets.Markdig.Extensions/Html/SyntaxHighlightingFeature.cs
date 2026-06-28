using System.Collections.Generic;

namespace Bwets.Markdig.Extensions.Html;

/// <summary>
/// Code syntax highlighting feature (highlight.js). Markdig already emits <c>language-*</c> classes,
/// so this feature only ships the highlight runtime, themes and init.
/// </summary>
public sealed class SyntaxHighlightingFeature : HtmlMarkdownFeature
{
    private readonly bool _extendedLanguages;

    /// <param name="extendedLanguages">
    /// false bundles the common ~38 languages (small); true bundles all ~190 languages plus Razor.
    /// </param>
    public SyntaxHighlightingFeature(bool extendedLanguages = false)
    {
        _extendedLanguages = extendedLanguages;
    }

    public override IEnumerable<MarkdownAsset> Assets => new[]
    {
        new MarkdownAsset(
            "highlight.min.js",
            MarkdownAssetKind.Script,
            EmbeddedAsset.Read(_extendedLanguages ? "highlight.full.min.js" : "highlight.common.min.js")),
    };

    public override string Head =>
        "<style>pre code.hljs { padding: 1rem; border-radius: 6px; }\n" + Themes + "</style>";

    public override string BodyEnd => "<script>hljs.highlightAll();</script>";

    private static string Themes =>
        EmbeddedAsset.Read("github.min.css") +
        "\n@media (prefers-color-scheme: dark) {\n" + EmbeddedAsset.Read("github-dark.min.css") + "\n}";
}
