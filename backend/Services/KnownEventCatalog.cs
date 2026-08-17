using System.Diagnostics.Eventing.Reader;
using WindowsDiagnosticApp.Models;

namespace WindowsDiagnosticApp.Services;

/// <summary>
/// Beschreibung eines bekannten, verständlich erklärten Windows-Ereignisses.
/// </summary>
public sealed record KnownEvent(
    string ProviderMatch,
    int EventId,
    string Title,
    string Explanation,
    EventSeverity? SeverityOverride);

/// <summary>
/// Zentrale, verständliche Regeldefinition für bekannte Windows-Ereignisse
/// sowie die Zuordnung der Windows-Ebenen zu den Anwendungs-Schweregraden.
/// </summary>
public sealed class KnownEventCatalog
{
    private readonly IReadOnlyList<KnownEvent> _events;

    public KnownEventCatalog()
    {
        _events = BuildCatalog();
    }

    /// <summary>
    /// Ordnet die Windows-Ebene dem Anwendungs-Schweregrad zu.
    /// Bekannte Ereignisse dürfen den Schweregrad gezielt erhöhen.
    /// </summary>
    public EventSeverity MapSeverity(StandardEventLevel level, KnownEvent? known)
    {
        if (known?.SeverityOverride is { } forced)
        {
            return forced;
        }

        return level switch
        {
            StandardEventLevel.Critical => EventSeverity.Critical,
            StandardEventLevel.Error => EventSeverity.High,
            StandardEventLevel.Warning => EventSeverity.Warning,
            _ => EventSeverity.Warning
        };
    }

    /// <summary>Sucht ein bekanntes Ereignis zu Provider und Ereignis-ID.</summary>
    public KnownEvent? Find(string? providerName, int eventId)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return null;
        }

        foreach (var candidate in _events)
        {
            if (candidate.EventId == eventId &&
                providerName.Contains(candidate.ProviderMatch, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IReadOnlyList<KnownEvent> BuildCatalog()
    {
        return new List<KnownEvent>
        {
            new("Kernel-Power", 41,
                "Windows wurde unerwartet neu gestartet",
                "Windows wurde neu gestartet, ohne zuvor ordnungsgemäß heruntergefahren worden zu sein. " +
                "Dieses Ereignis beschreibt meistens die Folge eines Absturzes oder Stromausfalls und nicht " +
                "automatisch die eigentliche Ursache.",
                EventSeverity.Critical),

            new("EventLog", 6008,
                "Unerwartetes Herunterfahren des Systems",
                "Das vorherige Herunterfahren des Systems war unerwartet. Häufig ist dies die Folge eines " +
                "Stromausfalls, eines Absturzes oder eines erzwungenen Ausschaltens.",
                EventSeverity.Critical),

            new("Bugcheck", 1001,
                "Bluescreen / Systemabsturz erkannt",
                "Windows hat einen schwerwiegenden Fehler festgestellt und musste das System anhalten " +
                "(Bluescreen). Die Ereignisdaten enthalten den Fehlercode des Absturzes.",
                EventSeverity.Critical),

            new("WHEA-Logger", 17,
                "Hardwarefehler wurde korrigiert",
                "Die Hardware-Fehlerüberwachung (WHEA) hat einen Hardwarefehler gemeldet, der automatisch " +
                "korrigiert werden konnte. Wiederholtes Auftreten kann auf ein Hardwareproblem hindeuten.",
                EventSeverity.High),

            new("WHEA-Logger", 18,
                "Schwerwiegender Hardwarefehler",
                "Die Hardware-Fehlerüberwachung (WHEA) hat einen nicht korrigierbaren Hardwarefehler gemeldet. " +
                "Dies deutet auf ein ernstes Problem mit CPU, Arbeitsspeicher oder Mainboard hin.",
                EventSeverity.Critical),

            new("WHEA-Logger", 19,
                "Korrigierter Hardwarefehler (WHEA)",
                "Die Hardware-Fehlerüberwachung (WHEA) hat einen korrigierten Fehler protokolliert. " +
                "Häufiges Auftreten sollte beobachtet werden.",
                EventSeverity.High),

            new("disk", 7,
                "Fehlerhafter Block auf einem Datenträger",
                "Auf einem Datenträger wurde ein fehlerhafter Block gefunden. Dies kann ein Hinweis auf ein " +
                "beginnendes Speicherproblem sein.",
                EventSeverity.High),

            new("disk", 51,
                "Fehler beim Zugriff auf einen Datenträger",
                "Beim Zugriff auf einen Datenträger ist ein Fehler aufgetreten. Wiederholtes Auftreten kann auf " +
                "einen defekten Datenträger oder ein Kabelproblem hindeuten.",
                EventSeverity.High),

            new("disk", 55,
                "Dateisystemstruktur beschädigt",
                "Das Dateisystem auf einem Datenträger ist beschädigt. Eine Datenträgerprüfung kann erforderlich sein.",
                EventSeverity.High),

            new("disk", 153,
                "Ein-/Ausgabe-Vorgang auf dem Datenträger wurde wiederholt",
                "Ein Zugriff auf den Datenträger musste wiederholt werden. Dies kann auf ein nachlassendes " +
                "Laufwerk oder eine schlechte Verbindung hindeuten.",
                EventSeverity.High),

            new("storahci", 129,
                "Zurücksetzen des Speichercontrollers (AHCI)",
                "Der SATA-/AHCI-Speichercontroller musste zurückgesetzt werden, weil ein Laufwerk nicht " +
                "rechtzeitig geantwortet hat. Häufiges Auftreten deutet auf ein Laufwerks- oder Kabelproblem hin.",
                EventSeverity.High),

            new("stornvme", 129,
                "Zurücksetzen des NVMe-Speichercontrollers",
                "Der NVMe-Speichercontroller musste zurückgesetzt werden, weil das Laufwerk nicht rechtzeitig " +
                "geantwortet hat. Häufiges Auftreten deutet auf ein Problem mit der SSD hin.",
                EventSeverity.High),

            new("Ntfs", 55,
                "NTFS-Dateisystem beschädigt",
                "Die Struktur des NTFS-Dateisystems auf einem Datenträger ist beschädigt. Eine Prüfung des " +
                "Datenträgers (chkdsk) kann erforderlich sein.",
                EventSeverity.High),

            new("Service Control Manager", 7000,
                "Ein Dienst konnte nicht gestartet werden",
                "Ein Windows-Dienst konnte nicht gestartet werden. Dies kann Auswirkungen auf abhängige " +
                "Funktionen haben.",
                EventSeverity.High),

            new("Service Control Manager", 7001,
                "Ein Dienst ist von einem nicht gestarteten Dienst abhängig",
                "Ein Windows-Dienst konnte nicht starten, weil ein benötigter anderer Dienst nicht gestartet wurde.",
                EventSeverity.High),

            new("Service Control Manager", 7023,
                "Ein Dienst wurde mit einem Fehler beendet",
                "Ein Windows-Dienst wurde unerwartet mit einem Fehler beendet.",
                EventSeverity.High),

            new("Service Control Manager", 7031,
                "Ein Dienst wurde unerwartet beendet",
                "Ein Windows-Dienst wurde unerwartet beendet. Windows hat gegebenenfalls eine " +
                "Wiederherstellungsaktion ausgeführt.",
                EventSeverity.High),

            new("Service Control Manager", 7034,
                "Ein Dienst wurde unerwartet beendet",
                "Ein Windows-Dienst wurde unerwartet beendet, ohne eine Wiederherstellungsaktion auszulösen.",
                EventSeverity.High),

            new("DNS Client", 1014,
                "Zeitüberschreitung bei der Namensauflösung (DNS)",
                "Eine DNS-Namensauflösung ist wegen einer Zeitüberschreitung fehlgeschlagen. Dies kann auf ein " +
                "Netzwerk- oder DNS-Serverproblem hindeuten.",
                EventSeverity.Warning),

            new("Display", 4101,
                "Der Anzeigetreiber wurde zurückgesetzt",
                "Der Grafiktreiber hat nicht mehr reagiert und wurde automatisch zurückgesetzt (TDR). " +
                "Häufiges Auftreten kann auf ein Treiber- oder Grafikkartenproblem hindeuten.",
                EventSeverity.Warning),

            new("Application Error", 1000,
                "Ein Programm ist abgestürzt",
                "Eine Anwendung wurde unerwartet beendet (Absturz). Die Ereignisdaten enthalten den Namen des " +
                "betroffenen Programms und Moduls.",
                EventSeverity.High),

            new("Application Hang", 1002,
                "Ein Programm reagiert nicht mehr",
                "Eine Anwendung hat aufgehört zu reagieren und wurde als „hängend“ erkannt.",
                EventSeverity.High),

            new("WindowsUpdateClient", 20,
                "Ein Windows-Update konnte nicht installiert werden",
                "Die Installation eines Windows-Updates ist fehlgeschlagen. Windows versucht die Installation " +
                "in der Regel später erneut.",
                EventSeverity.High),

            new("WindowsUpdateClient", 31,
                "Ein Windows-Update konnte nicht heruntergeladen werden",
                "Der Download eines Windows-Updates ist fehlgeschlagen. Dies kann an der Netzwerkverbindung liegen.",
                EventSeverity.High)
        };
    }
}
