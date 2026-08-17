namespace WindowsDiagnosticApp.Models;

/// <summary>
/// Statuswert für einen Bereich der Rechnerübersicht.
/// </summary>
public enum HealthStatus
{
    Normal,
    Warning,
    Critical,
    NotChecked
}
