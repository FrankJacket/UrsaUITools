using Markdig;
using ReverseMarkdown;

namespace Ursa.UITools;

/// <summary>Converts between the HTML produced by the editor and Markdown.</summary>
public static class UrsaEditorConverter
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseEmphasisExtras().DisableHtml().Build();

    private static readonly Converter HtmlConverter = new();

    /// <summary>Converts Markdown to HTML. Raw HTML in the Markdown is escaped, not kept.</summary>
    public static string MarkdownToHtml(string? markdown) =>
        string.IsNullOrWhiteSpace(markdown)
            ? string.Empty
            : Markdown.ToHtml(markdown, Pipeline);

    /// <summary>Converts HTML to Markdown.</summary>
    public static string HtmlToMarkdown(string? html) =>
        string.IsNullOrWhiteSpace(html) ? string.Empty : HtmlConverter.Convert(html).Trim();
}
