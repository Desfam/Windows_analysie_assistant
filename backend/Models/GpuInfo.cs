namespace WindowsDiagnosticApp.Models;

public sealed class GpuInfo
{
    public string? Name { get; init; }
    public string? Manufacturer { get; init; }
    public string? DriverVersion { get; init; }
    public double? VideoMemoryBytes { get; init; }
    public HealthStatus Status { get; init; } = HealthStatus.NotChecked;
}
