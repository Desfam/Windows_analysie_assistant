using System.Text.Json;
using WindowsDiagnosticApp.Models;

namespace WindowsDiagnosticApp.Services;

internal enum TextToolCallOutcome
{
    /// <summary>Der Inhalt ist eine normale Antwort und wird unverändert angezeigt.</summary>
    NotAToolCall,

    /// <summary>Ein gültig aufgebauter Werkzeugaufruf wurde erkannt.</summary>
    Parsed,

    /// <summary>Ein Werkzeugaufruf war erkennbar gemeint, ist aber fehlerhaft.</summary>
    Invalid
}

internal sealed record TextToolCallResult(TextToolCallOutcome Outcome, ToolCallRaw? Call, string? Error)
{
    public static readonly TextToolCallResult NotAToolCall = new(TextToolCallOutcome.NotAToolCall, null, null);

    public static TextToolCallResult Invalid(string error) => new(TextToolCallOutcome.Invalid, null, error);

    public static TextToolCallResult Parsed(ToolCallRaw call) => new(TextToolCallOutcome.Parsed, call, null);
}

/// <summary>
/// Erkennt textbasierte Werkzeugaufrufe. Manche Modelle liefern den Aufruf nicht in
/// <c>message.tool_calls</c>, sondern als JSON-Text in <c>message.content</c>. Ein so erkannter
/// Aufruf wird nie als Antworttext angezeigt, sondern wie ein nativer Aufruf validiert.
/// Akzeptiert wird ausschließlich das Schema <c>{ "name": "...", "arguments": { ... } }</c>.
/// </summary>
internal static class TextToolCallParser
{
    private const string ToolCallOpen = "<tool_call>";
    private const string ToolCallClose = "</tool_call>";

    public static TextToolCallResult Parse(string? content)
    {
        var trimmed = (content ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return TextToolCallResult.NotAToolCall;
        }

        if (!TryExtractCandidate(trimmed, out var candidate, out var explicitMarker))
        {
            return TextToolCallResult.NotAToolCall;
        }

        if (!TryExtractJsonObject(candidate, out var json))
        {
            return explicitMarker
                ? TextToolCallResult.Invalid("Der Werkzeugaufruf enthielt kein gültiges JSON.")
                : TextToolCallResult.NotAToolCall;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return explicitMarker
                ? TextToolCallResult.Invalid("Der Werkzeugaufruf enthielt kein gültiges JSON.")
                : TextToolCallResult.NotAToolCall;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return explicitMarker
                    ? TextToolCallResult.Invalid("Der Werkzeugaufruf muss ein JSON-Objekt sein.")
                    : TextToolCallResult.NotAToolCall;
            }

            if (!root.TryGetProperty("name", out var nameElement) ||
                nameElement.ValueKind != JsonValueKind.String)
            {
                return explicitMarker
                    ? TextToolCallResult.Invalid("Im Werkzeugaufruf fehlt der Name der Aktion.")
                    : TextToolCallResult.NotAToolCall;
            }

            var name = nameElement.GetString();
            if (string.IsNullOrWhiteSpace(name))
            {
                return TextToolCallResult.Invalid("Im Werkzeugaufruf fehlt der Name der Aktion.");
            }

            if (!TryReadArguments(root, out var arguments))
            {
                return TextToolCallResult.Invalid("Die Parameter des Werkzeugaufrufs sind ungültig.");
            }

            return TextToolCallResult.Parsed(new ToolCallRaw { Name = name!.Trim(), Arguments = arguments });
        }
    }

    private static bool TryReadArguments(JsonElement root, out JsonElement arguments)
    {
        if (!root.TryGetProperty("arguments", out var element) || element.ValueKind == JsonValueKind.Null)
        {
            arguments = EmptyObject();
            return true;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            arguments = default;
            return false;
        }

        arguments = element.Clone();
        return true;
    }

    private static JsonElement EmptyObject()
    {
        using var document = JsonDocument.Parse("{}");
        return document.RootElement.Clone();
    }

    /// <summary>
    /// Löst die drei unterstützten Verpackungen auf. <paramref name="explicitMarker"/> ist true,
    /// wenn der Text eindeutig als Werkzeugaufruf gekennzeichnet war und ein Fehler daher
    /// gemeldet statt ignoriert werden muss.
    /// </summary>
    private static bool TryExtractCandidate(string trimmed, out string candidate, out bool explicitMarker)
    {
        explicitMarker = false;

        var tagStart = trimmed.IndexOf(ToolCallOpen, StringComparison.OrdinalIgnoreCase);
        if (tagStart >= 0)
        {
            explicitMarker = true;
            var start = tagStart + ToolCallOpen.Length;
            var tagEnd = trimmed.IndexOf(ToolCallClose, start, StringComparison.OrdinalIgnoreCase);
            candidate = (tagEnd >= 0 ? trimmed[start..tagEnd] : trimmed[start..]).Trim();
            return !string.IsNullOrWhiteSpace(candidate);
        }

        var fenceStart = trimmed.IndexOf("```", StringComparison.Ordinal);
        if (fenceStart >= 0)
        {
            var afterFence = trimmed[(fenceStart + 3)..];
            var newline = afterFence.IndexOf('\n');
            if (newline >= 0)
            {
                var language = afterFence[..newline].Trim();
                if (language.Length == 0 || language.Equals("json", StringComparison.OrdinalIgnoreCase))
                {
                    explicitMarker = true;
                    var code = newline + 1 < afterFence.Length ? afterFence[(newline + 1)..] : string.Empty;
                    var closing = code.LastIndexOf("```", StringComparison.Ordinal);
                    candidate = (closing >= 0 ? code[..closing] : code).Trim();
                    return !string.IsNullOrWhiteSpace(candidate);
                }
            }
        }

        var jsonStart = trimmed.IndexOf('{');
        while (jsonStart >= 0)
        {
            var candidateText = trimmed[jsonStart..].Trim();
            if (TryExtractJsonObject(candidateText, out var json) &&
                JsonDocument.Parse(json).RootElement.ValueKind == JsonValueKind.Object &&
                JsonDocument.Parse(json).RootElement.TryGetProperty("name", out _))
            {
                candidate = json;
                return true;
            }

            jsonStart = trimmed.IndexOf('{', jsonStart + 1);
        }

        candidate = string.Empty;
        return false;
    }

    /// <summary>
    /// Schneidet das erste vollständige JSON-Objekt heraus, damit nachfolgender Erklärtext
    /// die Auswertung nicht verhindert.
    /// </summary>
    private static bool TryExtractJsonObject(string text, out string json)
    {
        json = string.Empty;
        if (text.Length == 0 || text[0] != '{')
        {
            return false;
        }

        var depth = 0;
        var inString = false;
        var escaped = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (c == '\\')
                {
                    escaped = true;
                }
                else if (c == '"')
                {
                    inString = false;
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    inString = true;
                    break;
                case '{':
                    depth++;
                    break;
                case '}':
                    depth--;
                    if (depth == 0)
                    {
                        json = text[..(i + 1)];
                        return true;
                    }

                    break;
            }
        }

        return false;
    }
}
