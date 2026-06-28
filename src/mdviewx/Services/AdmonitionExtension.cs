using System.Collections.Generic;
using Markdig;
using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Block = Markdig.Syntax.Block;

namespace mdviewx.Services;

/// <summary>
/// A parsed admonition / callout, produced from either Docusaurus (<c>:::type[title] ... :::</c>)
/// or MkDocs (<c>!!! type "title"</c> / <c>??? type "title"</c> + indented body) syntax.
/// </summary>
public sealed class AdmonitionBlock : ContainerBlock
{
    public AdmonitionBlock(BlockParser parser) : base(parser)
    {
    }

    public string Kind { get; set; } = "note";

    /// <summary>null = default title (the kind), "" = no title, otherwise a custom title.</summary>
    public string? Title { get; set; }

    public bool Collapsible { get; set; }

    public bool Open { get; set; } = true;

    /// <summary>Number of opening fence characters (Docusaurus only).</summary>
    public int FenceLength { get; set; }
}

/// <summary>
/// Parses Docusaurus-style fenced admonitions: <c>:::warning[Optional title]</c> … <c>:::</c>.
/// </summary>
public sealed class DocusaurusAdmonitionParser : BlockParser
{
    public DocusaurusAdmonitionParser()
    {
        OpeningCharacters = new[] { ':' };
    }

    public override BlockState TryOpen(BlockProcessor processor)
    {
        if (processor.IsCodeIndent)
        {
            return BlockState.None;
        }

        var line = processor.Line; // struct copy for inspection
        var count = 0;
        while (line.CurrentChar == ':')
        {
            count++;
            line.NextChar();
        }

        if (count < 3)
        {
            return BlockState.None;
        }

        line.TrimStart();
        var info = line.ToString().Trim();
        if (!AdmonitionInfo.TryParseDocusaurus(info, out var kind, out var title))
        {
            return BlockState.None;
        }

        processor.NewBlocks.Push(new AdmonitionBlock(this)
        {
            Column = processor.Column,
            Line = processor.LineIndex,
            Span = new SourceSpan(processor.Start, processor.Line.End),
            Kind = kind,
            Title = title,
            Collapsible = false,
            FenceLength = count,
        });

        return BlockState.ContinueDiscard;
    }

    public override BlockState TryContinue(BlockProcessor processor, Block block)
    {
        if (processor.IsBlankLine)
        {
            return BlockState.Continue;
        }

        var admonition = (AdmonitionBlock)block;
        var line = processor.Line; // struct copy
        var count = 0;
        while (line.CurrentChar == ':')
        {
            count++;
            line.NextChar();
        }

        if (count >= admonition.FenceLength)
        {
            line.TrimStart();
            if (line.IsEmpty)
            {
                block.UpdateSpanEnd(processor.Line.End);
                return BlockState.BreakDiscard;
            }
        }

        return BlockState.Continue;
    }
}

/// <summary>
/// Parses MkDocs-style admonitions: <c>!!! type "title"</c> (and collapsible <c>??? type</c> /
/// <c>???+ type</c>) followed by a four-space indented body.
/// </summary>
public sealed class MkDocsAdmonitionParser : BlockParser
{
    public MkDocsAdmonitionParser()
    {
        OpeningCharacters = new[] { '!', '?' };
    }

    public override BlockState TryOpen(BlockProcessor processor)
    {
        if (processor.IsCodeIndent)
        {
            return BlockState.None;
        }

        var line = processor.Line; // struct copy
        var marker = line.CurrentChar;
        var count = 0;
        while (line.CurrentChar == marker)
        {
            count++;
            line.NextChar();
        }

        if (count != 3)
        {
            return BlockState.None;
        }

        var collapsible = marker == '?';
        var open = !collapsible;
        if (collapsible && line.CurrentChar == '+')
        {
            open = true;
            line.NextChar();
        }

        // A space must separate the marker from the type.
        if (line.CurrentChar != ' ' && line.CurrentChar != '\t')
        {
            return BlockState.None;
        }

        line.TrimStart();
        var info = line.ToString().Trim();
        if (!AdmonitionInfo.TryParseMkDocs(info, out var kind, out var title))
        {
            return BlockState.None;
        }

        processor.NewBlocks.Push(new AdmonitionBlock(this)
        {
            Column = processor.Column,
            Line = processor.LineIndex,
            Span = new SourceSpan(processor.Start, processor.Line.End),
            Kind = kind,
            Title = title,
            Collapsible = collapsible,
            Open = open,
        });

        return BlockState.ContinueDiscard;
    }

    public override BlockState TryContinue(BlockProcessor processor, Block block)
    {
        if (processor.IsBlankLine)
        {
            // Keep the block open across blank lines; a following non-indented line closes it.
            return BlockState.Continue;
        }

        if (processor.IsCodeIndent)
        {
            processor.GoToCodeIndent();
            return BlockState.Continue;
        }

        return BlockState.None;
    }
}

/// <summary>Shared parsing of the admonition info string for both syntaxes.</summary>
internal static class AdmonitionInfo
{
    public static bool TryParseDocusaurus(string info, out string kind, out string? title)
    {
        kind = string.Empty;
        title = null;
        if (info.Length == 0)
        {
            return false;
        }

        var bracket = info.IndexOf('[');
        if (bracket >= 0)
        {
            kind = info[..bracket].Trim();
            var close = info.LastIndexOf(']');
            if (close > bracket)
            {
                title = info.Substring(bracket + 1, close - bracket - 1);
            }
        }
        else
        {
            var space = IndexOfWhitespace(info);
            if (space < 0)
            {
                kind = info;
            }
            else
            {
                kind = info[..space];
                var rest = info[(space + 1)..].Trim();
                if (rest.Length > 0)
                {
                    title = rest;
                }
            }
        }

        return IsWord(kind);
    }

    public static bool TryParseMkDocs(string info, out string kind, out string? title)
    {
        kind = string.Empty;
        title = null;
        if (info.Length == 0)
        {
            return false;
        }

        var space = IndexOfWhitespace(info);
        string rest;
        if (space < 0)
        {
            kind = info;
            rest = string.Empty;
        }
        else
        {
            kind = info[..space];
            rest = info[(space + 1)..].Trim();
        }

        var quote = rest.IndexOf('"');
        if (quote >= 0)
        {
            var quote2 = rest.IndexOf('"', quote + 1);
            if (quote2 > quote)
            {
                title = rest.Substring(quote + 1, quote2 - quote - 1);
            }
        }

        return IsWord(kind);
    }

    private static int IndexOfWhitespace(string s)
    {
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] == ' ' || s[i] == '\t')
            {
                return i;
            }
        }

        return -1;
    }

    private static bool IsWord(string s)
    {
        if (s.Length == 0 || !char.IsLetter(s[0]))
        {
            return false;
        }

        foreach (var c in s)
        {
            if (!char.IsLetterOrDigit(c) && c != '-' && c != '_')
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>Renders an <see cref="AdmonitionBlock"/> as a styled callout.</summary>
public sealed class AdmonitionRenderer : HtmlObjectRenderer<AdmonitionBlock>
{
    private static readonly Dictionary<string, string> Synonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["note"] = "note", ["seealso"] = "note",
        ["abstract"] = "abstract", ["summary"] = "abstract", ["tldr"] = "abstract",
        ["info"] = "info", ["todo"] = "info", ["important"] = "info",
        ["tip"] = "tip", ["hint"] = "tip",
        ["success"] = "success", ["check"] = "success", ["done"] = "success",
        ["question"] = "question", ["help"] = "question", ["faq"] = "question",
        ["warning"] = "warning", ["caution"] = "warning", ["attention"] = "warning",
        ["failure"] = "danger", ["fail"] = "danger", ["missing"] = "danger",
        ["danger"] = "danger", ["error"] = "danger",
        ["bug"] = "bug",
        ["example"] = "example",
        ["quote"] = "quote", ["cite"] = "quote",
    };

    private static readonly Dictionary<string, string> Icons = new()
    {
        ["note"] = "📝", ["abstract"] = "📑", ["info"] = "ℹ️", ["tip"] = "💡",
        ["success"] = "✅", ["question"] = "❓", ["warning"] = "⚠️", ["danger"] = "🚨",
        ["bug"] = "🐛", ["example"] = "🧪", ["quote"] = "💬",
    };

    protected override void Write(HtmlRenderer renderer, AdmonitionBlock obj)
    {
        var canonical = Synonyms.TryGetValue(obj.Kind, out var c) ? c : "note";
        var icon = Icons.TryGetValue(canonical, out var i) ? i : "📌";
        var tag = obj.Collapsible ? "details" : "div";
        var titleTag = obj.Collapsible ? "summary" : "div";

        renderer.EnsureLine();
        renderer.Write("<").Write(tag).Write(" class=\"admonition admonition-").Write(canonical).Write("\"");
        if (obj.Collapsible && obj.Open)
        {
            renderer.Write(" open");
        }

        renderer.Write(">");

        var showTitle = obj.Collapsible || obj.Title != string.Empty;
        if (showTitle)
        {
            var title = string.IsNullOrEmpty(obj.Title) ? Capitalize(obj.Kind) : obj.Title!;
            renderer.Write("<").Write(titleTag).Write(" class=\"admonition-title\">");
            renderer.Write("<span class=\"admonition-icon\">").Write(icon).Write("</span>");
            renderer.WriteEscape(title);
            renderer.Write("</").Write(titleTag).Write(">");
        }

        renderer.Write("<div class=\"admonition-body\">");
        renderer.WriteChildren(obj);
        renderer.Write("</div>");
        renderer.Write("</").Write(tag).WriteLine(">");
    }

    private static string Capitalize(string s) =>
        s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}

/// <summary>Markdig extension that enables Docusaurus and MkDocs admonitions.</summary>
public sealed class AdmonitionExtension : IMarkdownExtension
{
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        if (!pipeline.BlockParsers.Contains<DocusaurusAdmonitionParser>())
        {
            pipeline.BlockParsers.Insert(0, new DocusaurusAdmonitionParser());
        }

        if (!pipeline.BlockParsers.Contains<MkDocsAdmonitionParser>())
        {
            pipeline.BlockParsers.Insert(0, new MkDocsAdmonitionParser());
        }
    }

    public void Setup(MarkdownPipeline pipeline, Markdig.Renderers.IMarkdownRenderer renderer)
    {
        if (renderer is HtmlRenderer html && !html.ObjectRenderers.Contains<AdmonitionRenderer>())
        {
            html.ObjectRenderers.Insert(0, new AdmonitionRenderer());
        }
    }
}
