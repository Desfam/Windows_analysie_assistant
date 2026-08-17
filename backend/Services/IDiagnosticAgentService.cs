using WindowsDiagnosticApp.Models;

namespace WindowsDiagnosticApp.Services;

public interface IDiagnosticAgentService
{
    /// <summary>
    /// Führt eine vollständige Chat-Runde inkl. Werkzeugaufrufen aus und liefert dabei
    /// eindeutig typisierte Ereignisse. Das Modell entscheidet nie selbst über den
    /// Ausführungsstatus einer Aktion – das übernimmt ausschließlich diese Orchestrierung.
    /// </summary>
    IAsyncEnumerable<AgentEvent> RunAsync(OllamaChatRequest request, CancellationToken cancellationToken);
}
