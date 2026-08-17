using System.Text.Json;
using WindowsDiagnosticApp.Services;
using Xunit;

namespace WindowsDiagnosticApp.Tests;

public sealed class OllamaServiceParsingTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{ not valid json")]
    [InlineData("[1, 2, 3]")]
    [InlineData("\"just a string\"")]
    public void TryParseChatLine_InvalidOrIncomplete_ReturnsFalseWithoutThrowing(string line)
    {
        var ok = OllamaService.TryParseChatLine(line, 10, out var chunk);
        Assert.False(ok);
        Assert.Null(chunk);
    }

    [Fact]
    public void TryParseChatLine_DeltaLine_ReturnsContent()
    {
        var line = "{\"message\":{\"role\":\"assistant\",\"content\":\"Hallo\"},\"done\":false}";
        var ok = OllamaService.TryParseChatLine(line, 5, out var chunk);

        Assert.True(ok);
        Assert.NotNull(chunk);
        Assert.Equal("delta", chunk!.Type);
        Assert.Equal("Hallo", chunk.Content);
    }

    [Fact]
    public void TryParseChatLine_EmptyContent_IsSkipped()
    {
        var line = "{\"message\":{\"role\":\"assistant\",\"content\":\"\"},\"done\":false}";
        Assert.False(OllamaService.TryParseChatLine(line, 5, out var chunk));
        Assert.Null(chunk);
    }

    [Fact]
    public void TryParseChatLine_ThinkingOnly_IsNotSurfacedAsAnswer()
    {
        var line = "{\"message\":{\"role\":\"assistant\",\"content\":\"\",\"thinking\":\"interne Gedanken\"}}";
        Assert.False(OllamaService.TryParseChatLine(line, 5, out var chunk));
        Assert.Null(chunk);
    }

    [Fact]
    public void TryParseChatLine_DoneLine_ReturnsDoneWithDuration()
    {
        var line = "{\"done\":true,\"total_duration\":123456}";
        var ok = OllamaService.TryParseChatLine(line, 4200, out var chunk);

        Assert.True(ok);
        Assert.Equal("done", chunk!.Type);
        Assert.Equal(4200, chunk.DurationMs);
    }

    [Fact]
    public void TryParseChatLine_ErrorLine_ReturnsErrorChunk()
    {
        var line = "{\"error\":\"model 'foo' not found\"}";
        var ok = OllamaService.TryParseChatLine(line, 0, out var chunk);

        Assert.True(ok);
        Assert.Equal("error", chunk!.Type);
        Assert.Equal("model 'foo' not found", chunk.Message);
    }

    [Fact]
    public void ParseModel_NormalizesTagsEntry()
    {
        const string json = """
        {
          "name": "llama3.2:latest",
          "modified_at": "2026-01-15T10:20:30.000Z",
          "size": 123456789,
          "details": {
            "family": "llama",
            "parameter_size": "8B",
            "quantization_level": "Q4_K_M"
          }
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var model = OllamaService.ParseModel(doc.RootElement);

        Assert.Equal("llama3.2:latest", model.Name);
        Assert.Equal("llama", model.Family);
        Assert.Equal("8B", model.ParameterSize);
        Assert.Equal("Q4_K_M", model.Quantization);
        Assert.Equal(123456789, model.SizeBytes);
        Assert.NotNull(model.ModifiedAt);
    }

    [Fact]
    public void ParseModel_MissingDetails_DoesNotThrow()
    {
        const string json = """{ "name": "mini:latest", "size": 42 }""";
        using var doc = JsonDocument.Parse(json);
        var model = OllamaService.ParseModel(doc.RootElement);

        Assert.Equal("mini:latest", model.Name);
        Assert.Null(model.Family);
        Assert.Null(model.ParameterSize);
        Assert.Equal(42, model.SizeBytes);
    }
}
