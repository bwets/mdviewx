using Markdig;
using Bwets.Markdig.Extensions.Admonitions;
using Bwets.Markdig.Extensions.Mermaid;

namespace Bwets.Markdig.Extensions;

/// <summary>
/// Extension methods to activate individual Markdig extensions on a <see cref="MarkdownPipelineBuilder"/>.
/// Each is independent, so only the ones you need have to be enabled.
/// </summary>
public static class MarkdownPipelineExtensions
{
    /// <summary>Enables Docusaurus (<c>:::</c>) and MkDocs (<c>!!!</c>/<c>???</c>) admonitions.</summary>
    public static MarkdownPipelineBuilder UseAdmonitions(this MarkdownPipelineBuilder pipeline)
    {
        if (!pipeline.Extensions.Contains<AdmonitionExtension>())
        {
            pipeline.Extensions.Add(new AdmonitionExtension());
        }

        return pipeline;
    }

    /// <summary>Renders <c>```mermaid</c> code blocks as mermaid diagram containers.</summary>
    public static MarkdownPipelineBuilder UseMermaid(this MarkdownPipelineBuilder pipeline)
    {
        if (!pipeline.Extensions.Contains<MermaidExtension>())
        {
            pipeline.Extensions.Add(new MermaidExtension());
        }

        return pipeline;
    }
}
