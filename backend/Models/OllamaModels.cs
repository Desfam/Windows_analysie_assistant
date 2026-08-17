namespace WindowsDiagnosticApp.Models;

public sealed class OllamaStatus
{
    public bool Connected { get; init; }
    public string? Version { get; init; }
    public string BaseUrl { get; init; } = string.Empty;
    public string? Error { get; init; }
    public DateTimeOffset CheckedAt { get; init; } = DateTimeOffset.Now;
}

public sealed class OllamaModelInfo
{
    public required string Name { get; init; }
    public string? Family { get; init; }
    public string? ParameterSize { get; init; }
    public string? Quantization { get; init; }
    public long SizeBytes { get; init; }
    public DateTimeOffset? ModifiedAt { get; init; }
}

public sealed class OllamaModelsResponse
{
    public List<OllamaModelInfo> Models { get; init; } = new();
    public bool Connected { get; init; }
    public string? Error { get; init; }
}

public sealed class OllamaConfigResponse
{
    public string BaseUrl { get; init; } = string.Empty;
    public bool IsLocal { get; init; }
    public bool AllowPrivateNetwork { get; init; }
}

public sealed class OllamaConfigRequest
{
    public string? BaseUrl { get; init; }
}

public sealed class OllamaChatMessage
{
    public string Role { get; init; } = "user";
    public string Content { get; init; } = string.Empty;
}

public sealed class OllamaCaseContext
{
    public string? ComputerName { get; init; }
    public List<string> SelectedEvents { get; init; } = new();
    public List<string> CurrentEvidence { get; init; } = new();
}

public sealed class OllamaChatRequest
{
    public string? Model { get; init; }
    public List<OllamaChatMessage> Messages { get; init; } = new();
    public OllamaCaseContext? CaseContext { get; init; }
}

/// <summary>Ein Teilstück der gestreamten Chat-Antwort (NDJSON-Zeile).</summary>
public sealed class ChatStreamChunk
{
    public required string Type { get; init; }
    public string? Content { get; init; }
    public string? Message { get; init; }
    public long? DurationMs { get; init; }
    public List<ToolCallRaw>? ToolCalls { get; init; }
}

/// <summary>Ein von Ollama angeforderter, noch nicht validierter Werkzeugaufruf.</summary>
public sealed class ToolCallRaw
{
    public string? Id { get; init; }
    public required string Name { get; init; }
    public required System.Text.Json.JsonElement Arguments { get; init; }
}
