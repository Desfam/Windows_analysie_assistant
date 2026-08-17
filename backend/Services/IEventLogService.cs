using WindowsDiagnosticApp.Models;

namespace WindowsDiagnosticApp.Services;

public interface IEventLogService
{
    Task<EventsResponse> GetEventsAsync(EventQuery query, CancellationToken cancellationToken);

    Task<EventItem?> GetEventByKeyAsync(string eventKey, EventQuery query, CancellationToken cancellationToken);
}
