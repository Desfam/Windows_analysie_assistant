using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using Microsoft.Extensions.Options;
using WindowsDiagnosticApp.Models;
using WindowsDiagnosticApp.Options;

namespace WindowsDiagnosticApp.Services;

/// <summary>
/// Liest die Windows-Ereignisprotokolle serverseitig gefiltert aus.
/// Alle Zugriffe sind ausschließlich lesend.
/// </summary>
public sealed class EventLogService : IEventLogService
{
    private readonly EventGrouper _grouper;
    private readonly EventOptions _options;
    private readonly ILogger<EventLogService> _logger;

    public EventLogService(
        EventGrouper grouper,
        IOptions<EventOptions> options,
        ILogger<EventLogService> logger)
    {
        _grouper = grouper;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<EventsResponse> GetEventsAsync(EventQuery query, CancellationToken cancellationToken)
    {
        var logs = query.Logs.Count > 0 ? query.Logs : _options.Logs;
        var warnings = new List<string>();
        var accessDenied = false;
        var raw = new List<RawEventRecord>();

        foreach (var log in logs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var records = await Task.Run(
                    () => ReadLog(log, query.Hours, cancellationToken), cancellationToken);
                raw.AddRange(records);
            }
            catch (EventLogNotFoundException)
            {
                warnings.Add($"Das Protokoll „{log}“ ist auf diesem System nicht vorhanden.");
            }
            catch (UnauthorizedAccessException)
            {
                accessDenied = true;
                warnings.Add($"Für das Protokoll „{log}“ fehlen die erforderlichen Berechtigungen. " +
                             "Starten Sie die Anwendung als Administrator, um diese Ereignisse zu sehen.");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Fehler beim Lesen des Protokolls {Log}", log);
                warnings.Add($"Das Protokoll „{log}“ konnte nicht vollständig gelesen werden.");
            }
        }

        var grouped = _grouper.Group(raw);
        var filtered = ApplyFilters(grouped, query);

        return new EventsResponse
        {
            Events = filtered,
            Counts = BuildCounts(filtered),
            Warnings = warnings,
            AccessDenied = accessDenied
        };
    }

    public async Task<EventItem?> GetEventByKeyAsync(
        string eventKey, EventQuery query, CancellationToken cancellationToken)
    {
        var response = await GetEventsAsync(query, cancellationToken);
        return response.Events.FirstOrDefault(
            e => string.Equals(e.EventKey, eventKey, StringComparison.Ordinal) ||
                 string.Equals(e.Id, eventKey, StringComparison.Ordinal));
    }

    private List<RawEventRecord> ReadLog(string log, int hours, CancellationToken cancellationToken)
    {
        var milliseconds = (long)TimeSpan.FromHours(hours).TotalMilliseconds;
        var xpath =
            "*[System[(Level=1 or Level=2 or Level=3) and " +
            $"TimeCreated[timediff(@SystemTime) <= {milliseconds.ToString(CultureInfo.InvariantCulture)}]]]";

        var eventQuery = new EventLogQuery(log, PathType.LogName, xpath)
        {
            ReverseDirection = true
        };

        var results = new List<RawEventRecord>();
        using var reader = new EventLogReader(eventQuery);

        for (var record = reader.ReadEvent();
             record is not null;
             record = reader.ReadEvent())
        {
            cancellationToken.ThrowIfCancellationRequested();

            using (record)
            {
                results.Add(Convert(record, log));
            }

            if (results.Count >= _options.MaxEvents)
            {
                break;
            }
        }

        return results;
    }

    private static RawEventRecord Convert(EventRecord record, string log)
    {
        string? message = null;
        try
        {
            message = record.FormatDescription();
        }
        catch
        {
            // Meldungstext kann nicht immer aufgelöst werden.
        }

        string? levelName = null;
        try
        {
            levelName = record.LevelDisplayName;
        }
        catch
        {
            // Anzeigename der Ebene ist nicht immer verfügbar.
        }

        string? xml = null;
        try
        {
            xml = record.ToXml();
        }
        catch
        {
            // Roh-XML ist nicht immer verfügbar.
        }

        var time = record.TimeCreated.HasValue
            ? new DateTimeOffset(record.TimeCreated.Value)
            : DateTimeOffset.Now;

        return new RawEventRecord
        {
            EventId = record.Id,
            ProviderName = record.ProviderName,
            LogName = log,
            Level = (StandardEventLevel)(record.Level ?? 0),
            LevelDisplayName = levelName,
            TimeCreated = time,
            Message = message,
            MachineName = record.MachineName,
            Xml = xml
        };
    }

    private static List<EventItem> ApplyFilters(IReadOnlyList<EventItem> items, EventQuery query)
    {
        IEnumerable<EventItem> result = items;

        if (query.Levels.Count > 0)
        {
            result = result.Where(i => query.Levels.Contains(i.Severity));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            result = result.Where(i => MatchesSearch(i, term));
        }

        return result.ToList();
    }

    private static bool MatchesSearch(EventItem item, string term)
    {
        if (int.TryParse(term, out var id) && item.EventId == id)
        {
            return true;
        }

        return Contains(item.ProviderName, term)
            || Contains(item.Title, term)
            || Contains(item.Summary, term)
            || Contains(item.OriginalMessage, term)
            || Contains(item.LogName, term);
    }

    private static bool Contains(string? value, string term) =>
        value is not null && value.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static EventCounts BuildCounts(IReadOnlyCollection<EventItem> items) => new()
    {
        Critical = items.Count(i => i.Severity == EventSeverity.Critical),
        High = items.Count(i => i.Severity == EventSeverity.High),
        Warning = items.Count(i => i.Severity == EventSeverity.Warning),
        Total = items.Count
    };
}
