namespace Ursa.UITools.Tests;

public class UrsaEditorConverterTests
{
    [Fact]
    public void Markdown_becomes_html()
    {
        var html = UrsaEditorConverter.MarkdownToHtml("# Title\n\nSome **bold** text\n\n- one\n- two");

        Assert.Contains("<h1>Title</h1>", html);
        Assert.Contains("<strong>bold</strong>", html);
        Assert.Contains("<li>two</li>", html);
    }

    [Fact]
    public void Html_becomes_markdown()
    {
        var markdown = UrsaEditorConverter.HtmlToMarkdown("<h2>Title</h2><p>Some <strong>bold</strong> and <em>italic</em></p><ul><li>one</li></ul>");

        Assert.Contains("## Title", markdown);
        Assert.Contains("**bold**", markdown);
        Assert.Contains("*italic*", markdown);
        Assert.Contains("- one", markdown);
    }

    [Fact]
    public void Raw_html_in_markdown_is_not_kept()
    {
        var html = UrsaEditorConverter.MarkdownToHtml("hello <script>alert(1)</script>");

        Assert.DoesNotContain("<script>", html);
    }

    [Fact]
    public void Empty_values_stay_empty()
    {
        Assert.Equal("", UrsaEditorConverter.MarkdownToHtml(null));
        Assert.Equal("", UrsaEditorConverter.HtmlToMarkdown("  "));
    }
}
