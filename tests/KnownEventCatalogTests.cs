using System.Diagnostics.Eventing.Reader;
using WindowsDiagnosticApp.Models;
using WindowsDiagnosticApp.Services;
using Xunit;

namespace WindowsDiagnosticApp.Tests;

public sealed class KnownEventCatalogTests
{
    private readonly KnownEventCatalog _catalog = new();

    [Theory]
    [InlineData(StandardEventLevel.Critical, EventSeverity.Critical)]
    [InlineData(StandardEventLevel.Error, EventSeverity.High)]
    [InlineData(StandardEventLevel.Warning, EventSeverity.Warning)]
    public void MapSeverity_MapsWindowsLevelToSeverity(StandardEventLevel level, EventSeverity expected)
    {
        var result = _catalog.MapSeverity(level, known: null);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void MapSeverity_KnownEventCanElevateSeverity()
    {
        // Kernel-Power 41 wird trotz Warnungsebene als kritisch eingestuft.
        var known = _catalog.Find("Microsoft-Windows-Kernel-Power", 41);
        Assert.NotNull(known);

        var result = _catalog.MapSeverity(StandardEventLevel.Warning, known);
        Assert.Equal(EventSeverity.Critical, result);
    }

    [Theory]
    [InlineData("Microsoft-Windows-Kernel-Power", 41)]
    [InlineData("EventLog", 6008)]
    [InlineData("Microsoft-Windows-WHEA-Logger", 18)]
    [InlineData("disk", 153)]
    [InlineData("storahci", 129)]
    [InlineData("stornvme", 129)]
    [InlineData("Service Control Manager", 7031)]
    [InlineData("Application Error", 1000)]
    [InlineData("Microsoft-Windows-WindowsUpdateClient", 20)]
    public void Find_RecognizesKnownEvents(string provider, int eventId)
    {
        var known = _catalog.Find(provider, eventId);
        Assert.NotNull(known);
        Assert.False(string.IsNullOrWhiteSpace(known!.Title));
        Assert.False(string.IsNullOrWhiteSpace(known.Explanation));
    }

    [Fact]
    public void Find_UnknownEventReturnsNull()
    {
        Assert.Null(_catalog.Find("Some-Random-Provider", 424242));
    }
}
