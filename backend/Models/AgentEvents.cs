namespace WindowsDiagnosticApp.Models;

/// <summary>Vom Backend erzeugter Diagnoseknoten. Der Status wird ausschließlich hier gesetzt.</summary>
public sealed class AgentGraphNode
{
    public required string Id { get; init; }
    public required string Kind { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string State { get; init; }
    public required string RiskLevel { get; init; }
    public required bool ChangesSystem { get; init; }
}

/// <summary>Statusänderung eines bereits vorhandenen Graph-Knotens.</summary>
public sealed class AgentGraphNodePatch
{
    public required string Id { get; init; }
    public required string State { get; init; }
    public string? Result { get; init; }
    public string? Error { get; init; }
}

/// <summary>Ein aus einem echten Aktionsergebnis abgeleiteter Beleg.</summary>
public sealed class AgentEvidence
{
    public required string Id { get; init; }
    public int? EventId { get; init; }
    public string? Provider { get; init; }
    public required string Summary { get; init; }
    public DateTimeOffset? Timestamp { get; init; }
}

/// <summary>
/// Eindeutig typisiertes Stream-Ereignis der Agenten-Orchestrierung. Das Frontend darf
/// diese Ereignisse nicht als normalen Antworttext darstellen, sondern muss sie an die
/// jeweils zuständige Zustandslogik weiterreichen.
/// </summary>
public sealed class AgentEvent
{
    public required string Type { get; init; }
    public string? Content { get; init; }
    public string? ActionId { get; init; }
    public string? NodeId { get; init; }
    public object? Parameters { get; init; }
    public string? Reason { get; init; }
    public AgentGraphNode? Node { get; init; }
    public AgentGraphNodePatch? NodePatch { get; init; }
    public string? ExecutionId { get; init; }
    public string? ActionState { get; init; }
    public object? Result { get; init; }
    public AgentEvidence? Evidence { get; init; }
    public string? MessageId { get; init; }
    public long? DurationMs { get; init; }
    public string? Code { get; init; }
    public string? Message { get; init; }
    public string? Phase { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
}
