namespace WindowsDiagnosticApp.Options;

/// <summary>
/// Konfiguration für das Auslesen der Windows-Ereignisprotokolle.
/// </summary>
public sealed class EventOptions
{
    public const string SectionName = "Events";

    public int DefaultHours { get; set; } = 24;
    public int MaxEvents { get; set; } = 500;
    public int MaxHours { get; set; } = 168;
    public List<string> Logs { get; set; } = new()
    {
        "System",
        "Application",
        "Microsoft-Windows-WindowsUpdateClient/Operational"
    };
}
