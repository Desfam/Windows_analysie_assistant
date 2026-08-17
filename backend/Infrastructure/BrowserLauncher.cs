using System.Diagnostics;

namespace WindowsDiagnosticApp.Infrastructure;

/// <summary>Öffnet die lokale Weboberfläche im Standardbrowser.</summary>
public static class BrowserLauncher
{
    public static void Open(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            // Wenn kein Browser geöffnet werden kann, bleibt die Adresse manuell erreichbar.
        }
    }
}
