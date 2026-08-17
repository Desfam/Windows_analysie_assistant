namespace WindowsDiagnosticApp.Models;

public sealed class HealthResponse
{
    public string Status { get; init; } = "ok";
    public string Application { get; init; } = "Windows Diagnose Assistent";
    public string Version { get; init; } = "1.0.0";
    public string MachineName { get; init; } = Environment.MachineName;
    public DateTimeOffset ServerTime { get; init; } = DateTimeOffset.Now;
}
