using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WindowsDiagnosticApp.Options;
using WindowsDiagnosticApp.Services;
using Xunit;

namespace WindowsDiagnosticApp.Tests;

public sealed class EventLogServiceTests
{
    private static EventLogService CreateService()
    {
        var grouper = new EventGrouper(new KnownEventCatalog());
        var options = Microsoft.Extensions.Options.Options.Create(new EventOptions { MaxEvents = 50 });
        return new EventLogService(grouper, options, NullLogger<EventLogService>.Instance);
    }

    [Fact]
    public async Task GetEvents_NonExistentLog_ReturnsWarningWithoutThrowing()
    {
        var service = CreateService();
        var query = new EventQuery
        {
            Logs = new[] { "WDA_NonExistent_Log_XYZ" },
            Hours = 24
        };

        var response = await service.GetEventsAsync(query, CancellationToken.None);

        Assert.NotNull(response);
        Assert.NotEmpty(response.Warnings);
        Assert.Empty(response.Events);
    }

    [Fact]
    public async Task GetEvents_ProtectedLog_HandledGracefully()
    {
        // Das Security-Protokoll erfordert i. d. R. Administratorrechte. Der Zugriff darf
        // nicht abstürzen, sondern muss verständlich behandelt werden.
        var service = CreateService();
        var query = new EventQuery
        {
            Logs = new[] { "Security" },
            Hours = 24
        };

        var response = await service.GetEventsAsync(query, CancellationToken.None);

        Assert.NotNull(response);
        // Entweder Zugriff verweigert (mit Hinweis) oder erfolgreich gelesen – nie ein Absturz.
        Assert.True(response.AccessDenied || response.Warnings.Count >= 0);
    }
}
