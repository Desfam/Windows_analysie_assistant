using WindowsDiagnosticApp.Services;
using Xunit;

namespace WindowsDiagnosticApp.Tests;

public sealed class TextToolCallParserTests
{
    [Fact]
    public void Parse_PlainJsonObject_IsRecognized()
    {
        const string content = """{"name":"events.query","arguments":{"levels":["Error"],"maximumResults":10}}""";

        var result = TextToolCallParser.Parse(content);

        Assert.Equal(TextToolCallOutcome.Parsed, result.Outcome);
        Assert.Equal("events.query", result.Call!.Name);
        Assert.Equal(10, result.Call.Arguments.GetProperty("maximumResults").GetInt32());
    }

    [Fact]
    public void Parse_JsonCodeBlock_IsRecognized()
    {
        const string content = """
        ```json
        {"name":"events.query","arguments":{"sinceHours":24}}
        ```
        """;

        var result = TextToolCallParser.Parse(content);

        Assert.Equal(TextToolCallOutcome.Parsed, result.Outcome);
        Assert.Equal("events.query", result.Call!.Name);
    }

    [Fact]
    public void Parse_ToolCallTag_IsRecognized()
    {
        const string content = """<tool_call>{"name":"events.query","arguments":{}}</tool_call>""";

        var result = TextToolCallParser.Parse(content);

        Assert.Equal(TextToolCallOutcome.Parsed, result.Outcome);
        Assert.Equal("events.query", result.Call!.Name);
    }

    [Fact]
    public void Parse_MissingArguments_DefaultsToEmptyObject()
    {
        var result = TextToolCallParser.Parse("""{"name":"events.query"}""");

        Assert.Equal(TextToolCallOutcome.Parsed, result.Outcome);
        Assert.Equal(System.Text.Json.JsonValueKind.Object, result.Call!.Arguments.ValueKind);
    }

    [Fact]
    public void Parse_TrailingExplanation_StillRecognizesCall()
    {
        const string content = """{"name":"events.query","arguments":{}}  Ich pruefe das jetzt.""";

        var result = TextToolCallParser.Parse(content);

        Assert.Equal(TextToolCallOutcome.Parsed, result.Outcome);
    }

    [Theory]
    [InlineData("Ich kann das lokal noch nicht pruefen.")]
    [InlineData("Bitte nenne mir die genaue Fehlermeldung.")]
    [InlineData("")]
    public void Parse_NormalAnswer_IsNotAToolCall(string content)
    {
        Assert.Equal(TextToolCallOutcome.NotAToolCall, TextToolCallParser.Parse(content).Outcome);
    }

    [Fact]
    public void Parse_ForeignCodeBlock_IsNotAToolCall()
    {
        const string content = """
        ```powershell
        Get-WinEvent -LogName System
        ```
        """;

        Assert.Equal(TextToolCallOutcome.NotAToolCall, TextToolCallParser.Parse(content).Outcome);
    }

    [Fact]
    public void Parse_JsonWithoutName_IsNotAToolCall()
    {
        Assert.Equal(
            TextToolCallOutcome.NotAToolCall,
            TextToolCallParser.Parse("""{"ergebnis":"kein Werkzeugaufruf"}""").Outcome);
    }

    [Fact]
    public void Parse_ArgumentsWrongType_IsInvalid()
    {
        var result = TextToolCallParser.Parse("""{"name":"events.query","arguments":"alles"}""");

        Assert.Equal(TextToolCallOutcome.Invalid, result.Outcome);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Parse_MarkedButBrokenJson_IsInvalid()
    {
        var result = TextToolCallParser.Parse("<tool_call>{\"name\":\"events.query\",</tool_call>");

        Assert.Equal(TextToolCallOutcome.Invalid, result.Outcome);
    }

    [Fact]
    public void Parse_EmptyName_IsInvalid()
    {
        var result = TextToolCallParser.Parse("""{"name":"   ","arguments":{}}""");

        Assert.Equal(TextToolCallOutcome.Invalid, result.Outcome);
    }
}
