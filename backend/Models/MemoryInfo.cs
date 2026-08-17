namespace WindowsDiagnosticApp.Models;

public sealed class MemoryInfo
{
    public double? TotalBytes { get; init; }
    public double? UsedBytes { get; init; }
    public double? AvailableBytes { get; init; }
    public double? UsagePercent { get; init; }
    public HealthStatus Status { get; init; } = HealthStatus.NotChecked;
}
