namespace WindowsDiagnosticApp.Models;

/// <summary>Auditierbares Ergebnis eines ausschließlich serverseitig festgelegten Prozesses.</summary>
public sealed class ProcessExecutionDetails
{
    public required string Program { get; init; }
    public List<string> Arguments { get; init; } = new();
    public required DateTimeOffset StartedAt { get; init; }
    public required DateTimeOffset CompletedAt { get; init; }
    public long DurationMs { get; init; }
    public int ExitCode { get; init; }
    public string StandardOutput { get; init; } = string.Empty;
    public string StandardError { get; init; } = string.Empty;
    public bool OutputTruncated { get; init; }
    public bool TimedOut { get; init; }
    public string? StartError { get; init; }
}

public sealed class WingetStatusActionResult
{
    public bool Available { get; init; }
    public string? Path { get; init; }
    public string? Version { get; init; }
    public bool Callable { get; init; }
    public string UserContext { get; init; } = string.Empty;
}

public sealed class WingetSourceItem
{
    public required string Name { get; init; }
    public string? Argument { get; init; }
    public string? Type { get; init; }
}

public sealed class WingetSourcesActionResult
{
    public bool Available { get; init; }
    public string? Path { get; init; }
    public bool ProcessSucceeded { get; init; }
    public bool Parsed { get; init; }
    public List<WingetSourceItem> Sources { get; init; } = new();
    public string RawOutput { get; init; } = string.Empty;
}