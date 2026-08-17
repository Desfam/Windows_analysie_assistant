using WindowsDiagnosticApp.Models;
using WindowsDiagnosticApp.Services;

namespace WindowsDiagnosticApp.Endpoints;

/// <summary>Registriert alle REST-Endpunkte der Anwendung.</summary>
public static class ApiEndpoints
{
    public static void MapApiEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/health", () => Results.Ok(new HealthResponse()));

        var system = api.MapGroup("/system");

        system.MapGet("/summary", async (ISystemInfoService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetSummaryAsync(ct)));

        system.MapGet("/cpu", async (ISystemInfoService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetCpuAsync(ct)));

        system.MapGet("/memory", async (ISystemInfoService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetMemoryAsync(ct)));

        system.MapGet("/gpus", async (ISystemInfoService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetGpusAsync(ct)));

        system.MapGet("/disks", async (ISystemInfoService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetDisksAsync(ct)));

        system.MapGet("/windows", async (ISystemInfoService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetWindowsAsync(ct)));

        api.MapGet("/events", async (
            string? level,
            int? hours,
            string? log,
            string? search,
            EventQueryParser parser,
            IEventLogService events,
            CancellationToken ct) =>
        {
            var query = parser.Parse(level, hours, log, search);
            var result = await events.GetEventsAsync(query, ct);
            return Results.Ok(result);
        });

        api.MapGet("/events/{eventKey}", async (
            string eventKey,
            string? level,
            int? hours,
            string? log,
            string? search,
            EventQueryParser parser,
            IEventLogService events,
            CancellationToken ct) =>
        {
            var query = parser.Parse(level, hours, log, search);
            var item = await events.GetEventByKeyAsync(eventKey, query, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });
    }
}
