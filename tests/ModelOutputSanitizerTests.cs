using WindowsDiagnosticApp.Services;
using Xunit;

namespace WindowsDiagnosticApp.Tests;

public sealed class ModelOutputSanitizerTests
{
    [Theory]
    [InlineData("<think>intern</think>Finale Antwort", "Finale Antwort")]
    [InlineData("<analysis>intern</analysis>Finale Antwort", "Finale Antwort")]
    [InlineData("<reasoning>intern</reasoning>Finale Antwort", "Finale Antwort")]
    [InlineData("<THINK>intern\nmehr</THINK>Finale Antwort", "Finale Antwort")]
    [InlineData("Vorher\n\n<think>intern</think>\n\nNachher", "Vorher\n\nNachher")]
    public void Sanitize_RemovesThinkingTags(string content, string expected)
    {
        var result = ModelOutputSanitizer.Sanitize(content);

        Assert.Equal(expected, result.Text);
        Assert.True(result.HadThinkingContent);
    }

    [Fact]
    public void Sanitize_RemovesMultipleBlocks()
    {
        var result = ModelOutputSanitizer.Sanitize("<think>a</think>Antwort<analysis>b</analysis><reasoning>c</reasoning>");

        Assert.Equal("Antwort", result.Text);
    }

    [Fact]
    public void Sanitize_ClosingTagOnly_DropsEverythingBeforeIt()
    {
        var result = ModelOutputSanitizer.Sanitize("interne Gedanken\n</think>Finale Antwort");

        Assert.Equal("Finale Antwort", result.Text);
    }

    [Fact]
    public void Sanitize_OpenTagOnly_HidesIncompleteThinking()
    {
        var result = ModelOutputSanitizer.Sanitize("<think>interne Gedanken");

        Assert.Equal(string.Empty, result.Text);
        Assert.True(result.HasIncompleteThinkingBlock);
    }

    [Fact]
    public void Sanitize_ThinkingTagsSplitAcrossChunks_WhenAssembled()
    {
        var result = ModelOutputSanitizer.Sanitize(string.Concat("<thi", "nk>intern</thi", "nk>Finale"));

        Assert.Equal("Finale", result.Text);
    }
}
