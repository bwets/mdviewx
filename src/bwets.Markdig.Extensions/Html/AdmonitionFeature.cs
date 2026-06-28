using Markdig;

namespace Bwets.Markdig.Extensions.Html;

/// <summary>Admonitions feature: enables the Markdig extension and ships its styling.</summary>
public sealed class AdmonitionFeature : HtmlMarkdownFeature
{
    public override void Configure(MarkdownPipelineBuilder pipeline) => pipeline.UseAdmonitions();

    public override string Head => "<style>" + Css + "</style>";

    private const string Css = @"
.admonition { margin: 1em 0; border: 1px solid #d0d7de; border-left: 4px solid var(--adm, #448aff); border-radius: 6px; overflow: hidden; background: #ffffff; }
.admonition-title { display: flex; align-items: center; gap: .5em; font-weight: 600; padding: .5em .9em; background: var(--adm-bg, rgba(68,138,255,.1)); }
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
@media (prefers-color-scheme: dark) { .admonition { background: #0d1117; border-color: #30363d; } }
";
}
