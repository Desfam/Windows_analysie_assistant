namespace WindowsDiagnosticApp.Options;

/// <summary>
/// Zentrale, konfigurierbare Grenzwerte für die Statusbewertung.
/// </summary>
public sealed class ThresholdOptions
{
    public const string SectionName = "Thresholds";

    public double RamWarningPercent { get; set; } = 85;
    public double RamCriticalPercent { get; set; } = 95;
    public double DiskFreeWarningPercent { get; set; } = 15;
    public double DiskFreeCriticalPercent { get; set; } = 5;
}
