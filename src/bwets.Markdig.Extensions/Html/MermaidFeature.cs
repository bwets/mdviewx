using System.Collections.Generic;
using Markdig;

namespace Bwets.Markdig.Extensions.Html;

/// <summary>Mermaid feature: renders <c>```mermaid</c> blocks and ships the mermaid runtime + init.</summary>
public sealed class MermaidFeature : HtmlMarkdownFeature
{
    public override void Configure(MarkdownPipelineBuilder pipeline) => pipeline.UseMermaid();

    public override IEnumerable<MarkdownAsset> Assets => new[]
    {
        new MarkdownAsset("mermaid.min.js", MarkdownAssetKind.Script, EmbeddedAsset.Read("mermaid.min.js")),
    };

    public override string Head => "<style>pre.mermaid { background: transparent; text-align: center; line-height: normal; }</style>";

    public override string BodyEnd =>
        "<script>(function(){var dark=window.matchMedia&&window.matchMedia('(prefers-color-scheme: dark)').matches;" +
        "mermaid.initialize({startOnLoad:false,theme:dark?'dark':'default'});mermaid.run();})();</script>";
}
