using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax.Inlines;
using Microsoft.AspNetCore.Html;

namespace Sgol.Web.Presentation.Components;

public static class MarkdownContent
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .DisableHtml().UseSoftlineBreakAsHardlineBreak().UsePipeTables().UseEmphasisExtras().Build();

    public static IHtmlContent Render(string? source)
    {
        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        Pipeline.Setup(renderer);
        renderer.ObjectRenderers.Replace<Markdig.Renderers.Html.Inlines.LinkInlineRenderer>(new SafeLinkRenderer());
        renderer.LinkRewriter = url => SafeUrl(url) ? url : "#";
        renderer.Render(Markdown.Parse(source ?? "", Pipeline));
        return new HtmlString(writer.ToString());
    }

    private static bool SafeUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        (uri.Scheme == "https" || uri.Scheme == "http" || uri.Scheme == "mailto");

    private sealed class SafeLinkRenderer : HtmlObjectRenderer<LinkInline>
    {
        protected override void Write(HtmlRenderer renderer, LinkInline link)
        {
            // Images remain their alternative text: descriptions never fetch remote media.
            if (link.IsImage || !SafeUrl(link.Url))
            {
                renderer.WriteChildren(link);
                return;
            }
            renderer.Write("<a href=\"").WriteEscapeUrl(link.Url).Write("\" rel=\"nofollow noopener noreferrer\">");
            renderer.WriteChildren(link);
            renderer.Write("</a>");
        }
    }
}
