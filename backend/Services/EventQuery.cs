using WindowsDiagnosticApp.Models;

namespace WindowsDiagnosticApp.Services;

/// <summary>
/// Validierte Abfrageparameter für den Ereignis-Endpunkt.
/// </summary>
public sealed class EventQuery
{
    public IReadOnlyList<EventSeverity> Levels { get; init; } = Array.Empty<EventSeverity>();
    public int Hours { get; init; } = 24;
    public IReadOnlyList<string> Logs { get; init; } = Array.Empty<string>();
    public string? Search { get; init; }
}
