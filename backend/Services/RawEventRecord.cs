using System.Diagnostics.Eventing.Reader;

namespace WindowsDiagnosticApp.Services;

/// <summary>
/// Rohdaten eines einzelnen aus dem Windows-Ereignisprotokoll gelesenen Ereignisses.
/// </summary>
public sealed class RawEventRecord
{
    public int EventId { get; init; }
    public string? ProviderName { get; init; }
    public string LogName { get; init; } = string.Empty;
    public StandardEventLevel Level { get; init; }
    public string? LevelDisplayName { get; init; }
    public DateTimeOffset TimeCreated { get; init; }
    public string? Message { get; init; }
    public string? MachineName { get; init; }
    public string? Xml { get; init; }
}
