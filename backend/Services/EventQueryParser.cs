using Microsoft.Extensions.Options;
using WindowsDiagnosticApp.Models;
using WindowsDiagnosticApp.Options;

namespace WindowsDiagnosticApp.Services;

/// <summary>
/// Validiert und normalisiert die Abfrageparameter des Ereignis-Endpunkts.
/// </summary>
public sealed class EventQueryParser
{
    private const int MaxSearchLength = 200;
    private readonly EventOptions _options;

    public EventQueryParser(IOptions<EventOptions> options)
    {
        _options = options.Value;
    }

    public EventQuery Parse(string? level, int? hours, string? log, string? search)
    {
        return new EventQuery
        {
            Levels = ParseLevels(level),
            Hours = ClampHours(hours),
            Logs = ParseLogs(log),
            Search = ParseSearch(search)
        };
    }

    private IReadOnlyList<EventSeverity> ParseLevels(string? level)
    {
        if (string.IsNullOrWhiteSpace(level))
        {
            return Array.Empty<EventSeverity>();
        }

        var result = new List<EventSeverity>();
        foreach (var part in level.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (part.ToLowerInvariant())
            {
                case "critical":
                    Add(result, EventSeverity.Critical);
                    break;
                case "error":
                case "high":
                    Add(result, EventSeverity.High);
                    break;
                case "warning":
                    Add(result, EventSeverity.Warning);
                    break;
            }
        }

        return result;
    }

    private int ClampHours(int? hours)
    {
        var value = hours ?? _options.DefaultHours;
        return Math.Clamp(value, 1, _options.MaxHours);
    }

    private IReadOnlyList<string> ParseLogs(string? log)
    {
        if (string.IsNullOrWhiteSpace(log))
        {
            return Array.Empty<string>();
        }

        var requested = log.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Nur konfigurierte Protokolle zulassen, um beliebige Protokollzugriffe zu verhindern.
        return _options.Logs
            .Where(allowed => requested.Any(r => string.Equals(r, allowed, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private static string? ParseSearch(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        var trimmed = search.Trim();
        return trimmed.Length > MaxSearchLength ? trimmed[..MaxSearchLength] : trimmed;
    }

    private static void Add(List<EventSeverity> list, EventSeverity value)
    {
        if (!list.Contains(value))
        {
            list.Add(value);
        }
    }
}
