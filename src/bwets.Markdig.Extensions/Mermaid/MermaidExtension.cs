using System;
using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;

namespace Bwets.Markdig.Extensions.Mermaid;

/// <summary>
/// Renders fenced <c>```mermaid</c> code blocks as <c>&lt;pre class="mermaid"&gt;</c> so a
/// client-side mermaid library turns them into diagrams. All other code blocks fall back to the
/// default rendering.
/// </summary>
public sealed class MermaidCodeBlockRenderer : CodeBlockRenderer
{
    protected override void Write(HtmlRenderer renderer, CodeBlock obj)
    {
        if (obj is FencedCodeBlock fenced
            && fenced.Info is { } info
            && info.Trim().Equals("mermaid", StringComparison.OrdinalIgnoreCase))
        {
            renderer.EnsureLine();
            renderer.Write("<pre class=\"mermaid\">");
            renderer.WriteLeafRawLines(obj, true, true);
            renderer.Write("</pre>");
            renderer.EnsureLine();
            return;
        }

        base.Write(renderer, obj);
    }
}

/// <summary>Markdig extension enabling mermaid diagram rendering.</summary>
public sealed class MermaidExtension : IMarkdownExtension
{
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
        if (renderer is not HtmlRenderer html)
        {
            return;
        }

        for (var i = 0; i < html.ObjectRenderers.Count; i++)
        {
            if (html.ObjectRenderers[i] is CodeBlockRenderer and not MermaidCodeBlockRenderer)
            {
                html.ObjectRenderers[i] = new MermaidCodeBlockRenderer();
                return;
            }
        }

        html.ObjectRenderers.Add(new MermaidCodeBlockRenderer());
    }
}
