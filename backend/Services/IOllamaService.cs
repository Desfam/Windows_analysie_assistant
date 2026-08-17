using WindowsDiagnosticApp.Models;

namespace WindowsDiagnosticApp.Services;

public interface IOllamaService
{
    Task<OllamaStatus> GetStatusAsync(CancellationToken cancellationToken);

    Task<OllamaModelsResponse> GetModelsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Streamt eine einzelne Ollama-Chat-Runde (roh, inkl. möglicher Tool-Aufrufe).
    /// Wird von der Agenten-Orchestrierung für jede Gesprächsrunde erneut aufgerufen.
    /// </summary>
    IAsyncEnumerable<ChatStreamChunk> StreamRawAsync(
        string model,
        IReadOnlyList<object> messages,
        IReadOnlyList<object>? tools,
        CancellationToken cancellationToken);
}
