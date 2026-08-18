using System.Text.Json;

namespace WindowsDiagnosticApp.Models;

/// <summary>
/// Verbindliches Risikomodell.
/// R0 = Information/nur lesend, R1 = Diagnose/leicht belastend,
/// R2 = begrenzte Änderung, R3 = erhebliche Änderung, R4 = kritisch, R5 = destruktiv.
/// Aktuell produktiv ausführbar: R0 und R1.
/// </summary>
public enum ActionRiskLevel
{
    R0,
    R1,
    R2,
    R3,
    R4,
    R5
}

/// <summary>Betriebsmodus einer Aktion.</summary>
public enum DiagnosticActionMode
{
    Diagnostic,
    Repair,
    Verification
}

/// <summary>Sensitivität der Ausgabe einer Aktion.</summary>
public enum OutputSensitivity
{
    /// <summary>Keine besonderen Anforderungen.</summary>
    Public,
    /// <summary>Interne Netzwerkinformationen (IPs, DNS, Freigaben).</summary>
    InternalNetworkData,
    /// <summary>Prozess- und Dienstliste (kann auf installierte Software schließen lassen).</summary>
    ProcessList,
    /// <summary>Sicherheitsrelevante Daten (Credentials, Tokens, Schlüssel).</summary>
    SecuritySensitive
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
    public DiagnosticActionMode Mode { get; init; } = DiagnosticActionMode.Diagnostic;
    public ActionRiskLevel RiskLevel { get; init; } = ActionRiskLevel.R0;
    public bool ChangesSystem { get; init; }
    public bool RequiresAdministrator { get; init; }
    public bool RequiresConfirmation { get; init; }
    public bool RequiresRestart { get; init; }
    public bool MayInterruptNetwork { get; init; }
    /// <summary>Darf die Aktion ohne Benutzerbestätigung automatisch ausgeführt werden?</summary>
    public bool AutomaticExecutionAllowed { get; init; } = true;
    public int TimeoutSeconds { get; init; } = 20;
    /// <summary>Windows-Versionen, auf denen die Aktion verfügbar ist. Leer = alle.</summary>
    public IReadOnlyList<string> SupportedSystems { get; init; } = Array.Empty<string>();
    /// <summary>PowerShell-Modul, das für diese Aktion benötigt wird (null = kein spezifisches Modul).</summary>
    public string? RequiredModule { get; init; }
    /// <summary>Externes Binary, das für diese Aktion benötigt wird (null = kein externes Binary).</summary>
    public string? RequiredBinary { get; init; }
    public OutputSensitivity OutputSensitivity { get; init; } = OutputSensitivity.Public;
    /// <summary>JSON-Schema der Parameter, wird unverändert an Ollama als Tool-Definition gesendet.</summary>
    public required JsonElement ParameterSchema { get; init; }
}

/// <summary>Ergebnis der serverseitigen Validierung eines angeforderten Tool-Aufrufs.</summary>
public sealed class ActionValidationResult
{
    public bool IsValid { get; init; }
    public string? Error { get; init; }
    public string? ErrorCode { get; init; }
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

/// <summary>Validierte Parameter für Aktionen mit sinceHours-Parameter.</summary>
public sealed class SinceHoursParameters
{
    public int SinceHours { get; init; } = 24;
    public int MaximumResults { get; init; } = 50;
}

/// <summary>Validierte Parameter für die Aktion <c>network.dns.resolve</c>.</summary>
public sealed class DnsResolveParameters
{
    public required string Name { get; init; }
}

/// <summary>Validierte Parameter für die Aktion <c>network.port.test</c>.</summary>
public sealed class PortTestParameters
{
    public required string Host { get; init; }
    public int Port { get; init; }
}

/// <summary>Validierte Parameter für die Aktion <c>service.status</c>.</summary>
public sealed class ServiceStatusParameters
{
    public required string ServiceName { get; init; }
}

/// <summary>Validierte Parameter für die Aktion <c>process.list</c>.</summary>
public sealed class ProcessListParameters
{
    public int Top { get; init; } = 30;
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
    public string? ErrorCode { get; init; }
    public ProcessExecutionDetails? Execution { get; init; }
}
