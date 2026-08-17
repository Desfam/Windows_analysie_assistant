using WindowsDiagnosticApp.Models;

namespace WindowsDiagnosticApp.Services;

public interface IDiagnosticActionExecutor
{
    /// <summary>Führt eine bereits validierte Aktion tatsächlich aus. Nur echte Ergebnisse.</summary>
    Task<ActionExecutionResult> ExecuteAsync(
        string actionId, object validatedParameters, CancellationToken cancellationToken);
}

/// <summary>
/// Führt zulässige, rein lesende Diagnoseaktionen aus. Verwendet die bereits vorhandenen
/// Collector-Services der Systemübersicht wieder, statt eine zweite Erfassungslogik zu erzeugen.
/// </summary>
public sealed class DiagnosticActionExecutor : IDiagnosticActionExecutor
{
    private const int MessageMaxLength = 500;

    private readonly IEventLogService _eventLogService;
    private readonly ILogger<DiagnosticActionExecutor> _logger;

    public DiagnosticActionExecutor(IEventLogService eventLogService, ILogger<DiagnosticActionExecutor> logger)
    {
        _eventLogService = eventLogService;
        _logger = logger;
    }

    public async Task<ActionExecutionResult> ExecuteAsync(
        string actionId, object validatedParameters, CancellationToken cancellationToken)
    {
        return actionId switch
        {
            "events.query" => await ExecuteEventsQueryAsync((EventsQueryParameters)validatedParameters, cancellationToken),
            _ => new ActionExecutionResult
            {
                ActionId = actionId,
                Success = false,
                StartedAt = DateTimeOffset.Now,
                CompletedAt = DateTimeOffset.Now,
                Error = "Aktion ist nicht implementiert."
            }
        };
    }

    private async Task<ActionExecutionResult> ExecuteEventsQueryAsync(
        EventsQueryParameters parameters, CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.Now;

        var query = new EventQuery
        {
            Levels = parameters.Levels.Select(MapLevel).Distinct().ToList(),
            Hours = parameters.SinceHours,
            Logs = parameters.LogNames
        };

        EventsResponse response;
        try
        {
            response = await _eventLogService.GetEventsAsync(query, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "events.query fehlgeschlagen.");
            return new ActionExecutionResult
            {
                ActionId = "events.query",
                Success = false,
                StartedAt = startedAt,
                CompletedAt = DateTimeOffset.Now,
                Error = "Die Ereignisabfrage ist fehlgeschlagen."
            };
        }

        // Ausschließlich real gelesene Ereignisse – keine Demo-Ereignisse werden ergänzt.
        var events = response.Events
            .Where(e => parameters.Providers.Count == 0 ||
                        parameters.Providers.Any(p =>
                            e.ProviderName is not null &&
                            e.ProviderName.Contains(p, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(e => e.LastSeen)
            .Take(parameters.MaximumResults)
            .Select(e => new EventsQueryResultEvent
            {
                EventId = e.EventId,
                Provider = e.ProviderName,
                Level = e.Severity.ToString(),
                Timestamp = e.LastSeen,
                Message = Truncate(e.OriginalMessage ?? e.Summary ?? string.Empty)
            })
            .ToList();

        var data = new EventsQueryActionResult
        {
            Query = parameters,
            Events = events,
            Summary = new EventsQuerySummary { Total = events.Count }
        };

        return new ActionExecutionResult
        {
            ActionId = "events.query",
            Success = true,
            StartedAt = startedAt,
            CompletedAt = DateTimeOffset.Now,
            Data = data
        };
    }

    private static EventSeverity MapLevel(string level) => level switch
    {
        "Critical" => EventSeverity.Critical,
        "Error" => EventSeverity.High,
        _ => EventSeverity.Warning
    };

    private static string Truncate(string text) =>
        text.Length <= MessageMaxLength ? text : text[..MessageMaxLength].TrimEnd() + " …";
}
