namespace WindowsDiagnosticApp.Models;

/// <summary>
/// Antwort des Ereignis-Endpunkts inklusive Zählern und Fehlerhinweisen.
/// </summary>
public sealed class EventsResponse
{
    public List<EventItem> Events { get; init; } = new();
    public EventCounts Counts { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    public bool AccessDenied { get; init; }
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.Now;
}

public sealed class EventCounts
{
    public int Critical { get; init; }
    public int High { get; init; }
    public int Warning { get; init; }
    public int Total { get; init; }
}
