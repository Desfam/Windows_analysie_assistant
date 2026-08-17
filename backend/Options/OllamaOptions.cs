namespace WindowsDiagnosticApp.Options;

/// <summary>
/// Konfiguration der lokalen Ollama-Anbindung. Der Zugriff erfolgt ausschließlich
/// über das Backend; das Frontend spricht Ollama niemals direkt an.
/// </summary>
public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";

    public string BaseUrl { get; set; } = "http://127.0.0.1:11434";

    /// <summary>Kurzes Timeout für Status-/Modellabfragen, damit die UI nicht hängt.</summary>
    public int StatusTimeoutSeconds { get; set; } = 3;

    /// <summary>Timeout für Chat-Anfragen (Streaming benötigt mehr Zeit).</summary>
    public int ChatTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Erlaubt neben Loopback auch private Netzwerkadressen (z. B. 192.168.x.x).
    /// Öffentliche Ziele bleiben immer gesperrt (SSRF-Schutz).
    /// </summary>
    public bool AllowPrivateNetwork { get; set; } = true;
}
