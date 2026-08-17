namespace WindowsDiagnosticApp.Models;

/// <summary>
/// Allgemeine Informationen zum Rechner.
/// </summary>
public sealed class SystemSummary
{
    public string? MachineName { get; init; }
    public string? Manufacturer { get; init; }
    public string? Model { get; init; }
    public string? SystemType { get; init; }
    public DateTimeOffset? LastBootTime { get; init; }
    public string? Uptime { get; init; }
    public string? CurrentUser { get; init; }
    public HealthStatus Status { get; init; } = HealthStatus.Normal;
}
