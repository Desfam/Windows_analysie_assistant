using System.Text.Json;
using Microsoft.Extensions.Options;
using WindowsDiagnosticApp.Models;
using WindowsDiagnosticApp.Options;

namespace WindowsDiagnosticApp.Services;

/// <summary>
/// Zentraler, fest im Backend definierter Katalog zulässiger Diagnoseaktionen. Das Modell
/// erhält nur diesen Katalog als Werkzeugliste und kann niemals einen freien Befehl anfordern.
/// Unbekannte Aktionen oder zusätzliche/ungültige Parameter werden abgelehnt.
/// </summary>
public sealed class DiagnosticActionCatalog
{
    private static readonly string[] AllowedLevels = { "Critical", "Error", "Warning" };
    private static readonly HashSet<string> EventsQueryAllowedKeys = new(StringComparer.Ordinal)
    {
        "logNames", "providers", "levels", "sinceHours", "maximumResults"
    };

    private readonly EventOptions _eventOptions;
    private readonly List<DiagnosticActionDefinition> _definitions;

    public DiagnosticActionCatalog(IOptions<EventOptions> eventOptions)
    {
        _eventOptions = eventOptions.Value;
        _definitions = new List<DiagnosticActionDefinition> { BuildEventsQueryDefinition() };
    }

    public IReadOnlyList<DiagnosticActionDefinition> Definitions => _definitions;

    public DiagnosticActionDefinition? Find(string actionId) =>
        _definitions.FirstOrDefault(d => string.Equals(d.ActionId, actionId, StringComparison.Ordinal));

    /// <summary>Baut die Werkzeugliste im von Ollama erwarteten Function-Calling-Format.</summary>
    public List<object> BuildToolsPayload() =>
        _definitions
            .Select(d => (object)new
            {
                type = "function",
                function = new { name = d.ActionId, description = d.Description, parameters = d.ParameterSchema }
            })
            .ToList();

    public ActionValidationResult ValidateCall(string actionId, JsonElement parameters)
    {
        var definition = Find(actionId);
        if (definition is null)
        {
            return Invalid($"Unbekannte Aktion „{actionId}“.");
        }

        if (definition.RequiresAdministrator)
        {
            return Invalid("Diese Aktion erfordert Administratorrechte, die aktuell nicht unterstützt werden.");
        }

        return actionId switch
        {
            "events.query" => ValidateEventsQuery(definition, parameters),
            _ => Invalid($"Aktion „{actionId}“ ist noch nicht implementiert.")
        };
    }

    private ActionValidationResult ValidateEventsQuery(DiagnosticActionDefinition definition, JsonElement parameters)
    {
        if (parameters.ValueKind != JsonValueKind.Object)
        {
            return Invalid("Die Parameter müssen ein JSON-Objekt sein.");
        }

        foreach (var property in parameters.EnumerateObject())
        {
            if (!EventsQueryAllowedKeys.Contains(property.Name))
            {
                return Invalid($"Unbekannter Parameter „{property.Name}“.");
            }
        }

        if (!TryReadStringArray(parameters, "logNames", out var logNames, out var logError))
        {
            return Invalid(logError!);
        }

        if (logNames.Any(name => !_eventOptions.Logs.Contains(name, StringComparer.OrdinalIgnoreCase)))
        {
            return Invalid("Mindestens ein angefordertes Protokoll ist nicht freigegeben.");
        }

        if (logNames.Count == 0)
        {
            logNames = _eventOptions.Logs.ToList();
        }

        if (!TryReadStringArray(parameters, "levels", out var levels, out var levelError))
        {
            return Invalid(levelError!);
        }

        if (levels.Any(level => !AllowedLevels.Contains(level, StringComparer.OrdinalIgnoreCase)))
        {
            return Invalid("Mindestens eine angeforderte Ereignisebene ist ungültig.");
        }

        if (levels.Count == 0)
        {
            levels = new List<string> { "Error", "Warning" };
        }

        if (!TryReadStringArray(parameters, "providers", out var providers, out var providerError))
        {
            return Invalid(providerError!);
        }

        if (providers.Count > 10)
        {
            return Invalid("Zu viele Provider angefordert (maximal 10).");
        }

        if (providers.Any(p => p.Length > 100))
        {
            return Invalid("Ein Providername ist zu lang.");
        }

        if (!TryReadInt(parameters, "sinceHours", 24, out var sinceHours, out var hoursError))
        {
            return Invalid(hoursError!);
        }

        if (sinceHours < 1 || sinceHours > _eventOptions.MaxHours)
        {
            return Invalid($"sinceHours muss zwischen 1 und {_eventOptions.MaxHours} liegen.");
        }

        var defaultMax = Math.Min(50, _eventOptions.MaxEvents);
        if (!TryReadInt(parameters, "maximumResults", defaultMax, out var maximumResults, out var maxError))
        {
            return Invalid(maxError!);
        }

        if (maximumResults < 1 || maximumResults > _eventOptions.MaxEvents)
        {
            return Invalid($"maximumResults muss zwischen 1 und {_eventOptions.MaxEvents} liegen.");
        }

        var query = new EventsQueryParameters
        {
            LogNames = logNames,
            Providers = providers,
            Levels = levels,
            SinceHours = sinceHours,
            MaximumResults = maximumResults
        };

        return new ActionValidationResult { IsValid = true, Definition = definition, Parameters = query };
    }

    private static bool TryReadStringArray(
        JsonElement parameters, string key, out List<string> values, out string? error)
    {
        values = new List<string>();
        error = null;

        if (!parameters.TryGetProperty(key, out var element))
        {
            return true;
        }

        if (element.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            error = $"„{key}“ muss ein Array aus Zeichenketten sein.";
            return false;
        }

        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                error = $"„{key}“ darf nur Zeichenketten enthalten.";
                return false;
            }

            var text = item.GetString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                values.Add(text);
            }
        }

        return true;
    }

    private static bool TryReadInt(
        JsonElement parameters, string key, int fallback, out int value, out string? error)
    {
        value = fallback;
        error = null;

        if (!parameters.TryGetProperty(key, out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out value))
        {
            error = $"„{key}“ muss eine ganze Zahl sein.";
            value = fallback;
            return false;
        }

        return true;
    }

    private static ActionValidationResult Invalid(string error) =>
        new() { IsValid = false, Error = error };

    private DiagnosticActionDefinition BuildEventsQueryDefinition()
    {
        var schema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                logNames = new { type = "array", items = new { type = "string", @enum = _eventOptions.Logs } },
                providers = new { type = "array", items = new { type = "string" } },
                levels = new { type = "array", items = new { type = "string", @enum = AllowedLevels } },
                sinceHours = new { type = "integer", minimum = 1, maximum = _eventOptions.MaxHours },
                maximumResults = new { type = "integer", minimum = 1, maximum = _eventOptions.MaxEvents }
            },
            required = Array.Empty<string>()
        });

        return new DiagnosticActionDefinition
        {
            ActionId = "events.query",
            Title = "Windows-Ereignisse abfragen",
            Description =
                "Durchsucht die konfigurierten Windows-Ereignisprotokolle lesend nach Fehlern und Warnungen. " +
                "Verändert das System nicht.",
            Category = "events",
            RiskLevel = ActionRiskLevel.R0,
            ChangesSystem = false,
            RequiresAdministrator = false,
            RequiresConfirmation = false,
            TimeoutSeconds = 20,
            ParameterSchema = schema
        };
    }
}
