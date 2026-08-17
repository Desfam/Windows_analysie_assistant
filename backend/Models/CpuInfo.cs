namespace WindowsDiagnosticApp.Models;

public sealed class CpuInfo
{
    public string? Manufacturer { get; init; }
    public string? Model { get; init; }
    public int? PhysicalCores { get; init; }
    public int? LogicalProcessors { get; init; }
    public double? UsagePercent { get; init; }
    public double? MaxClockSpeedGhz { get; init; }
    public HealthStatus Status { get; init; } = HealthStatus.NotChecked;
}
