using System.Diagnostics.Eventing.Reader;
using WindowsDiagnosticApp.Models;
using WindowsDiagnosticApp.Services;
using Xunit;

namespace WindowsDiagnosticApp.Tests;

public sealed class EventGrouperTests
{
    private readonly EventGrouper _grouper = new(new KnownEventCatalog());

    private static RawEventRecord Record(string provider, int id, string message, DateTimeOffset time) => new()
    {
        EventId = id,
        ProviderName = provider,
        LogName = "System",
        Level = StandardEventLevel.Error,
        LevelDisplayName = "Fehler",
        TimeCreated = time,
        Message = message,
        MachineName = "TEST-PC",
        Xml = "<Event />"
    };

    [Fact]
    public void Group_CombinesIdenticalEvents()
    {
        var now = DateTimeOffset.Now;
        var records = new[]
        {
            Record("disk", 153, "Der Ein-/Ausgabevorgang wurde wiederholt.", now.AddMinutes(-10)),
            Record("disk", 153, "Der Ein-/Ausgabevorgang wurde wiederholt.", now.AddMinutes(-5)),
            Record("disk", 153, "Der Ein-/Ausgabevorgang wurde wiederholt.", now)
        };

        var result = _grouper.Group(records);

        var item = Assert.Single(result);
        Assert.Equal(3, item.Count);
        Assert.Equal(now, item.LastSeen);
        Assert.Equal(now.AddMinutes(-10), item.FirstSeen);
        Assert.Equal(3, item.Occurrences.Count);
    }

    [Fact]
    public void Group_SimilarMessagesWithDifferentNumbersAreGrouped()
    {
        var now = DateTimeOffset.Now;
        var records = new[]
        {
            Record("DNS Client Events", 1014, "Timeout bei der Auflösung von host-12.example.com", now.AddMinutes(-1)),
            Record("DNS Client Events", 1014, "Timeout bei der Auflösung von host-98.example.com", now)
        };

        var result = _grouper.Group(records);

        var item = Assert.Single(result);
        Assert.Equal(2, item.Count);
    }

    [Fact]
    public void Group_DifferentEventsStaySeparate()
    {
        var now = DateTimeOffset.Now;
        var records = new[]
        {
            Record("disk", 153, "Ein-/Ausgabe wiederholt", now),
            Record("Ntfs", 55, "Dateisystem beschädigt", now)
        };

        var result = _grouper.Group(records);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Group_UsesKnownTitleAndSeverity()
    {
        var records = new[]
        {
            Record("Microsoft-Windows-Kernel-Power", 41, "irgendein technischer Text", DateTimeOffset.Now)
        };

        var item = Assert.Single(_grouper.Group(records));
        Assert.True(item.IsKnownEvent);
        Assert.Equal(EventSeverity.Critical, item.Severity);
        Assert.Equal("Windows wurde unerwartet neu gestartet", item.Title);
    }
}
