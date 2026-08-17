using Microsoft.Extensions.Options;
using WindowsDiagnosticApp.Options;

namespace WindowsDiagnosticApp.Services;

/// <summary>
/// Hält die aktuell gültige, serverseitig validierte Ollama-Basisadresse.
/// Verhindert, dass pro Chat-Anfrage eine freie URL übergeben wird.
/// </summary>
public sealed class OllamaConfigStore
{
    private readonly OllamaOptions _options;
    private readonly object _gate = new();
    private string _baseUrl;

    public OllamaConfigStore(IOptions<OllamaOptions> options)
    {
        _options = options.Value;
        var validation = OllamaUrlValidator.Validate(_options.BaseUrl, _options.AllowPrivateNetwork);
        _baseUrl = validation.NormalizedUrl ?? "http://127.0.0.1:11434";
    }

    public bool AllowPrivateNetwork => _options.AllowPrivateNetwork;

    public string BaseUrl
    {
        get
        {
            lock (_gate)
            {
                return _baseUrl;
            }
        }
    }

    /// <summary>Setzt eine neue, bereits validierte Basisadresse.</summary>
    public void SetBaseUrl(string normalizedUrl)
    {
        lock (_gate)
        {
            _baseUrl = normalizedUrl;
        }
    }
}
