using Microsoft.Extensions.Logging.Abstractions;
using WindowsDiagnosticApp.Models;
using WindowsDiagnosticApp.Services;
using Xunit;

namespace WindowsDiagnosticApp.Tests;

public sealed class DiagnosticActionExecutorTests
{
    private sealed class FakeEventLogService : IEventLogService
    {
        private readonly EventsResponse _response;
        public FakeEventLogService(EventsResponse response) => _response = response;

        public Task<EventsResponse> GetEventsAsync(EventQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(_response);

        public Task<EventItem?> GetEventByKeyAsync(string eventKey, EventQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<EventItem?>(null);
    }

    private static EventItem MakeEvent(int id, string provider, EventSeverity severity, string message) => new()
    {
        Id = $"{provider}-{id}",
        EventKey = $"{provider}-{id}",
        EventId = id,
        ProviderName = provider,
        Severity = severity,
        LastSeen = DateTimeOffset.Now,
        FirstSeen = DateTimeOffset.Now,
        OriginalMessage = message
    };

    private static DiagnosticActionExecutor CreateExecutor(EventsResponse response) =>
        new(new FakeEventLogService(response), NullLogger<DiagnosticActionExecutor>.Instance);

    [Fact]
    public async Task EventsQuery_ReturnsRealEvents_NoDemoData()
    {
        var response = new EventsResponse
        {
            Events = new List<EventItem>
            {
                MakeEvent(129, "stornvme", EventSeverity.Warning, "Reset to device"),
                MakeEvent(51, "disk", EventSeverity.High, "Paging error"),
                MakeEvent(1000, "Application Error", EventSeverity.High, "App crash")
            }
        };
        var parameters = new EventsQueryParameters { MaximumResults = 10 };

        var result = await CreateExecutor(response).ExecuteAsync("events.query", parameters, CancellationToken.None);

        Assert.True(result.Success);
        var data = Assert.IsType<EventsQueryActionResult>(result.Data);
        Assert.Equal(3, data.Events.Count);
        Assert.Equal(3, data.Summary.Total);
    }

    [Fact]
    public async Task EventsQuery_FiltersByProvider()
    {
        var response = new EventsResponse
        {
            Events = new List<EventItem>
            {
                MakeEvent(129, "stornvme", EventSeverity.Warning, "Reset"),
                MakeEvent(51, "disk", EventSeverity.High, "Paging error")
            }
        };
        var parameters = new EventsQueryParameters { Providers = new List<string> { "stornvme" }, MaximumResults = 10 };

        var result = await CreateExecutor(response).ExecuteAsync("events.query", parameters, CancellationToken.None);
        var data = Assert.IsType<EventsQueryActionResult>(result.Data);

        Assert.Single(data.Events);
        Assert.Equal("stornvme", data.Events[0].Provider);
    }

    [Fact]
    public async Task EventsQuery_NoEvents_ReturnsEmpty()
    {
        var result = await CreateExecutor(new EventsResponse()).ExecuteAsync(
            "events.query", new EventsQueryParameters(), CancellationToken.None);

        var data = Assert.IsType<EventsQueryActionResult>(result.Data);
        Assert.Empty(data.Events);
        Assert.Equal(0, data.Summary.Total);
    }

    [Fact]
    public async Task EventsQuery_RespectsMaximumResults()
    {
        var events = Enumerable.Range(1, 20)
            .Select(i => MakeEvent(i, "disk", EventSeverity.Warning, $"msg {i}"))
            .ToList();
        var parameters = new EventsQueryParameters { MaximumResults = 5 };

        var result = await CreateExecutor(new EventsResponse { Events = events }).ExecuteAsync(
            "events.query", parameters, CancellationToken.None);
        var data = Assert.IsType<EventsQueryActionResult>(result.Data);

        Assert.Equal(5, data.Events.Count);
    }
}
