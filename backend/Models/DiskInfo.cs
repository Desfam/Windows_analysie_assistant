namespace WindowsDiagnosticApp.Models;

public sealed class DiskInfo
{
    public string? DriveLetter { get; init; }
    public string? FileSystem { get; init; }
    public double? TotalBytes { get; init; }
    public double? UsedBytes { get; init; }
    public double? FreeBytes { get; init; }
    public double? UsagePercent { get; init; }
    public HealthStatus Status { get; init; } = HealthStatus.NotChecked;
}
