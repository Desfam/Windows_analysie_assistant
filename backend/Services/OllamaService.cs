using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WindowsDiagnosticApp.Models;
using WindowsDiagnosticApp.Options;

namespace WindowsDiagnosticApp.Services;

/// <summary>
/// Kapselt die gesamte Kommunikation mit dem lokalen Ollama-Dienst. Das Frontend
/// erreicht Ollama ausschließlich über diesen Service.
/// </summary>
public sealed class OllamaService : IOllamaService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OllamaConfigStore _config;
    private readonly OllamaOptions _options;
    private readonly ILogger<OllamaService> _logger;

    public OllamaService(
        IHttpClientFactory httpClientFactory,
        OllamaConfigStore config,
        IOptions<OllamaOptions> options,
        ILogger<OllamaService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<OllamaStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        var baseUrl = _config.BaseUrl;
        var client = _httpClientFactory.CreateClient("ollama");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(_options.StatusTimeoutSeconds));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        try
        {
            using var response = await client.GetAsync($"{baseUrl}/api/version", linked.Token);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(linked.Token);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: linked.Token);
            var version = doc.RootElement.TryGetProperty("version", out var v) ? v.GetString() : null;

            return new OllamaStatus { Connected = true, Version = version, BaseUrl = baseUrl };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Ollama-Status konnte nicht abgefragt werden.");
            return new OllamaStatus
            {
                Connected = false,
                BaseUrl = baseUrl,
                Error = "Ollama ist nicht erreichbar."
            };
        }
    }

    public async Task<OllamaModelsResponse> GetModelsAsync(CancellationToken cancellationToken)
    {
        var baseUrl = _config.BaseUrl;
        var client = _httpClientFactory.CreateClient("ollama");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(_options.StatusTimeoutSeconds));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        try
        {
            using var response = await client.GetAsync($"{baseUrl}/api/tags", linked.Token);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(linked.Token);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: linked.Token);

            var models = new List<OllamaModelInfo>();
            if (doc.RootElement.TryGetProperty("models", out var modelsElement) &&
                modelsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in modelsElement.EnumerateArray())
                {
                    models.Add(ParseModel(item));
                }
            }

            return new OllamaModelsResponse { Models = models, Connected = true };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Ollama-Modelle konnten nicht geladen werden.");
            return new OllamaModelsResponse { Connected = false, Error = "Ollama ist nicht erreichbar." };
        }
    }

    public async IAsyncEnumerable<ChatStreamChunk> StreamRawAsync(
        string model,
        IReadOnlyList<object> messages,
        IReadOnlyList<object>? tools,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            yield return new ChatStreamChunk { Type = "error", Message = "Es wurde kein Modell ausgewählt." };
            yield break;
        }

        var baseUrl = _config.BaseUrl;
        var client = _httpClientFactory.CreateClient("ollama");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(_options.ChatTimeoutSeconds));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        var token = linked.Token;

        var (response, startError) = await SendChatRequestAsync(client, baseUrl, model, messages, tools, token);

        if (startError is not null)
        {
            yield return new ChatStreamChunk { Type = "error", Message = startError };
            yield break;
        }

        if (response is null)
        {
            yield break;
        }

        var stopwatch = Stopwatch.StartNew();
        var timedOut = false;
        await foreach (var chunk in ReadChatStreamAsync(response, stopwatch, token))
        {
            if (chunk.Type == "error" && chunk.Message is not null &&
                chunk.Message.Contains("hat nicht innerhalb", StringComparison.OrdinalIgnoreCase))
            {
                timedOut = true;
            }

            yield return chunk;
        }

        if (!timedOut && !cancellationToken.IsCancellationRequested && linked.IsCancellationRequested)
        {
            yield return new ChatStreamChunk
            {
                Type = "error",
                Message = $"Das ausgewählte Modell hat nicht innerhalb von {_options.ChatTimeoutSeconds} Sekunden geantwortet."
            };
        }

        response.Dispose();
    }

    /// <summary>
    /// Sendet eine Chat-Anfrage an Ollama. Manche Modelle unterstützen keine Werkzeuge und
    /// lehnen die Anfrage mit HTTP 400 ab, wenn <c>tools</c> gesetzt ist – in diesem Fall wird
    /// automatisch ohne Werkzeuge wiederholt (der Chat bleibt dann rein textbasiert).
    /// </summary>
    private async Task<(HttpResponseMessage? Response, string? Error)> SendChatRequestAsync(
        HttpClient client,
        string baseUrl,
        string model,
        IReadOnlyList<object> messages,
        IReadOnlyList<object>? tools,
        CancellationToken token)
    {
        var payload = BuildChatPayload(model, messages, tools);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/chat")
        {
            Content = JsonContent.Create(payload)
        };

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return (null, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ollama-Chat konnte nicht gestartet werden.");
            return (null, "Die Anfrage an das Modell ist fehlgeschlagen.");
        }

        if (response.IsSuccessStatusCode)
        {
            return (response, null);
        }

        var statusCode = response.StatusCode;
        var errorBody = await response.Content.ReadAsStringAsync(token);
        _logger.LogWarning(
            "Ollama Chat-Fehler ({StatusCode}) für Modell {Model}: {Body}",
            (int)statusCode,
            model,
            errorBody);

        if (statusCode == HttpStatusCode.BadRequest && tools is { Count: > 0 })
        {
            response.Dispose();

            if (errorBody.Contains("does not support tools", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation(
                    "Modell {Model} unterstützt keine Werkzeuge, wiederhole Anfrage ohne Werkzeuge.", model);
                return await SendChatRequestAsync(client, baseUrl, model, messages, null, token);
            }

            return (null, BuildOllamaFailureMessage(statusCode, errorBody));
        }

        response.Dispose();
        return (null, BuildOllamaFailureMessage(statusCode, errorBody));
    }

    private async IAsyncEnumerable<ChatStreamChunk> ReadChatStreamAsync(
        HttpResponseMessage response,
        Stopwatch stopwatch,
        [EnumeratorCancellation] CancellationToken token)
    {
        using var stream = await response.Content.ReadAsStreamAsync(token);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var timeoutError = false;

        while (!reader.EndOfStream)
        {
            string? line;

            try
            {
                line = await reader.ReadLineAsync(token);
            }
            catch (OperationCanceledException)
            {
                timeoutError = token.IsCancellationRequested;
                break;
            }

            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            // Fehlerhafte oder inhaltslose Zeilen werden übersprungen, nie geworfen.
            if (!TryParseChatLine(line, stopwatch.ElapsedMilliseconds, out var parsed) || parsed is null)
            {
                continue;
            }

            yield return parsed;

            if (parsed.Type == "done")
            {
                yield break;
            }
        }

        if (timeoutError)
        {
            yield return new ChatStreamChunk
            {
                Type = "error",
                Message = $"Das ausgewählte Modell hat nicht innerhalb von {_options.ChatTimeoutSeconds} Sekunden geantwortet."
            };
        }
    }

    /// <summary>
    /// Parst eine einzelne NDJSON-Zeile des Ollama-Streams. Gibt <c>false</c> zurück
    /// (ohne Ausnahme), wenn die Zeile leer, unvollständig oder inhaltslos ist.
    /// </summary>
    internal static bool TryParseChatLine(string line, long elapsedMs, out ChatStreamChunk? chunk)
    {
        chunk = null;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(line);
        }
        catch (JsonException)
        {
            return false;
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (root.TryGetProperty("error", out var errorElement) &&
                errorElement.ValueKind == JsonValueKind.String)
            {
                chunk = new ChatStreamChunk { Type = "error", Message = errorElement.GetString() };
                return true;
            }

            if (root.TryGetProperty("message", out var messageForTools) &&
                messageForTools.TryGetProperty("tool_calls", out var toolCallsElement) &&
                toolCallsElement.ValueKind == JsonValueKind.Array &&
                toolCallsElement.GetArrayLength() > 0)
            {
                var calls = new List<ToolCallRaw>();
                foreach (var call in toolCallsElement.EnumerateArray())
                {
                    if (!call.TryGetProperty("function", out var function) ||
                        !function.TryGetProperty("name", out var nameElement))
                    {
                        continue;
                    }

                    var name = nameElement.GetString();
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    var arguments = function.TryGetProperty("arguments", out var argsElement)
                        ? argsElement.Clone()
                        : JsonDocument.Parse("{}").RootElement.Clone();
                    var id = call.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;

                    calls.Add(new ToolCallRaw { Id = id, Name = name, Arguments = arguments });
                }

                if (calls.Count > 0)
                {
                    chunk = new ChatStreamChunk { Type = "toolcalls", ToolCalls = calls };
                    return true;
                }
            }

            var hasNativeThinking = TryGetThinking(root, out _) ||
                (root.TryGetProperty("message", out var messageForThinking) &&
                 TryGetThinking(messageForThinking, out _));

            if (root.TryGetProperty("done", out var doneElement) &&
                doneElement.ValueKind == JsonValueKind.True)
            {
                chunk = new ChatStreamChunk { Type = "done", DurationMs = elapsedMs };
                return true;
            }

            if (root.TryGetProperty("message", out var message) &&
                message.TryGetProperty("content", out var content))
            {
                var text = content.GetString();
                // "thinking"-Feld wird bewusst nicht als normale Antwort weitergegeben.
                if (!string.IsNullOrEmpty(text))
                {
                    chunk = new ChatStreamChunk { Type = "delta", Content = text };
                    return true;
                }
            }

            if (hasNativeThinking)
            {
                // Native thinking is deliberately discarded and never becomes content.
                return false;
            }

            return false;
        }
    }

    private static object BuildChatPayload(string model, IReadOnlyList<object> messages, IReadOnlyList<object>? tools)
    {
        return tools is { Count: > 0 }
            ? new { model, messages, stream = true, think = false, tools }
            : new { model, messages, stream = true, think = false };
    }

    private static bool TryGetThinking(JsonElement element, out string? thinking)
    {
        foreach (var name in new[] { "thinking", "analysis" })
        {
            if (element.TryGetProperty(name, out var value) &&
                value.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(value.GetString()))
            {
                thinking = value.GetString();
                return true;
            }
        }

        thinking = null;
        return false;
    }

    private static string BuildOllamaFailureMessage(HttpStatusCode statusCode, string? body)
    {
        if (!string.IsNullOrWhiteSpace(body))
        {
            var normalized = body.Trim();
            if (normalized.Length > 400)
            {
                normalized = normalized[..397] + "...";
            }

            if (normalized.Contains("does not support tools", StringComparison.OrdinalIgnoreCase))
            {
                return "Modell unterstützt keine Tools.";
            }

            if (normalized.Contains("context", StringComparison.OrdinalIgnoreCase) &&
                normalized.Contains("window", StringComparison.OrdinalIgnoreCase))
            {
                return "Kontextfenster überschritten.";
            }

            if (normalized.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                statusCode == HttpStatusCode.RequestTimeout)
            {
                return "Zeitüberschreitung bei der Anfrage.";
            }

            if (normalized.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains("refused", StringComparison.OrdinalIgnoreCase) ||
                statusCode == HttpStatusCode.ServiceUnavailable)
            {
                return "Verbindung zu Ollama unterbrochen.";
            }

            if ((int)statusCode >= 500)
            {
                return "Ollama-Runner wurde beendet.";
            }
        }

        return statusCode switch
        {
            HttpStatusCode.BadRequest => "Modell lieferte keine verwertbare Antwort.",
            HttpStatusCode.RequestTimeout => "Zeitüberschreitung bei der Anfrage.",
            HttpStatusCode.ServiceUnavailable => "Verbindung zu Ollama unterbrochen.",
            _ when (int)statusCode >= 500 => "Ollama-Runner wurde beendet.",
            _ => "Die Anfrage an das Modell ist fehlgeschlagen."
        };
    }

    /// <summary>Baut aus dem Fallkontext einen zusätzlichen System-Textblock (nur echte Angaben).</summary>
    internal static string? BuildContextBlock(OllamaCaseContext? context)
    {
        if (context is null)
        {
            return null;
        }

        var builder = new StringBuilder();
        builder.AppendLine("Kontext dieses Diagnosefalls (nur diese Angaben sind belegt):");

        if (!string.IsNullOrWhiteSpace(context.ComputerName))
        {
            builder.AppendLine($"- Rechnername: {context.ComputerName}");
        }

        if (context.SelectedEvents.Count > 0)
        {
            builder.AppendLine("- Vom Benutzer ausgewählte Ereignisse:");
            foreach (var evt in context.SelectedEvents.Take(20))
            {
                builder.AppendLine($"  • {evt}");
            }
        }

        if (context.CurrentEvidence.Count > 0)
        {
            builder.AppendLine("- Bereits bestätigte Belege:");
            foreach (var evidence in context.CurrentEvidence.Take(20))
            {
                builder.AppendLine($"  • {evidence}");
            }
        }

        var text = builder.ToString().Trim();
        return text.Length > 0 ? text : null;
    }

    internal static OllamaModelInfo ParseModel(JsonElement item)
    {
        var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "unbekannt" : "unbekannt";
        long size = item.TryGetProperty("size", out var s) && s.TryGetInt64(out var sv) ? sv : 0;
        DateTimeOffset? modified = null;
        if (item.TryGetProperty("modified_at", out var m) &&
            m.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(m.GetString(), out var parsed))
        {
            modified = parsed;
        }

        string? family = null;
        string? parameterSize = null;
        string? quantization = null;
        if (item.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Object)
        {
            family = details.TryGetProperty("family", out var f) ? f.GetString() : null;
            parameterSize = details.TryGetProperty("parameter_size", out var p) ? p.GetString() : null;
            quantization = details.TryGetProperty("quantization_level", out var q) ? q.GetString() : null;
        }

        return new OllamaModelInfo
        {
            Name = name,
            Family = family,
            ParameterSize = parameterSize,
            Quantization = quantization,
            SizeBytes = size,
            ModifiedAt = modified
        };
    }
}
