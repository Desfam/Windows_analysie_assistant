using System.Text.Json;

namespace WindowsDiagnosticApp.Models;

/// <summary>Risikostufe einer Diagnoseaktion. R0 = keine Auswirkung (nur lesend).</summary>
public enum ActionRiskLevel
{
    R0,
    R1,
    R2,
    R3,
    R4
}

/// <summary>
/// Fest im Backend definierte Beschreibung einer zulässigen Diagnoseaktion. Das Modell
/// kennt nur diese Definitionen (als Tool-Katalog) und kann niemals einen freien Befehl
/// anfordern.
/// </summary>
public sealed class DiagnosticActionDefinition
{
    public required string ActionId { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public ActionRiskLevel RiskLevel { get; init; } = ActionRiskLevel.R0;
    public bool ChangesSystem { get; init; }
    public bool RequiresAdministrator { get; init; }
    public bool RequiresConfirmation { get; init; }
    public int TimeoutSeconds { get; init; } = 20;

    /// <summary>JSON-Schema der Parameter, wird unverändert an Ollama als Tool-Definition gesendet.</summary>
    public required JsonElement ParameterSchema { get; init; }
}

/// <summary>Ergebnis der serverseitigen Validierung eines angeforderten Tool-Aufrufs.</summary>
public sealed class ActionValidationResult
{
    public bool IsValid { get; init; }
    public string? Error { get; init; }
    public DiagnosticActionDefinition? Definition { get; init; }

    /// <summary>Die geparsten, geprüften Parameter (aktionsspezifischer Typ), nur wenn gültig.</summary>
    public object? Parameters { get; init; }
}

/// <summary>Validierte Parameter für die Aktion <c>events.query</c>.</summary>
public sealed class EventsQueryParameters
{
    public List<string> LogNames { get; init; } = new();
    public List<string> Providers { get; init; } = new();
    public List<string> Levels { get; init; } = new();
    public int SinceHours { get; init; } = 24;
    public int MaximumResults { get; init; } = 50;
}

public sealed class EventsQueryResultEvent
{
    public int EventId { get; init; }
    public string? Provider { get; init; }
    public string Level { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed class EventsQuerySummary
{
    public int Total { get; init; }
}

/// <summary>Strukturiertes, reales Ergebnis der Aktion <c>events.query</c>.</summary>
public sealed class EventsQueryActionResult
{
    public required EventsQueryParameters Query { get; init; }
    public List<EventsQueryResultEvent> Events { get; init; } = new();
    public EventsQuerySummary Summary { get; init; } = new();
}

/// <summary>Parameterloser Marker für fest verdrahtete, lesende Diagnoseaktionen.</summary>
public sealed class EmptyDiagnosticParameters;

/// <summary>Strukturiertes Ergebnis einer sicheren Statusprüfung.</summary>
public sealed class DiagnosticStatusActionResult
{
    public required string Summary { get; init; }
    public Dictionary<string, object?> Values { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Ergebnis der tatsächlichen Backend-Ausführung einer Aktion. Nur die Anwendung erzeugt
/// dieses Objekt – niemals das Modell.
/// </summary>
public sealed class ActionExecutionResult
{
    public required string ActionId { get; init; }
    public required bool Success { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset CompletedAt { get; init; }
    public object? Data { get; init; }
    public string? Error { get; init; }
    public ProcessExecutionDetails? Execution { get; init; }
}
