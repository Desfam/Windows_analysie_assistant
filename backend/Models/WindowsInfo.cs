namespace WindowsDiagnosticApp.Models;

public sealed class WindowsUpdateEntry
{
    public string? Id { get; init; }
    public DateTimeOffset? InstalledOn { get; init; }
}

public sealed class WindowsInfo
{
    public string? Edition { get; init; }
    public string? Version { get; init; }
    public string? Build { get; init; }
    public DateTimeOffset? InstallDate { get; init; }
    public List<WindowsUpdateEntry> RecentUpdates { get; init; } = new();
    public int? PendingUpdateCount { get; init; }
    public HealthStatus Status { get; init; } = HealthStatus.NotChecked;
}
