namespace WindowsDiagnosticApp.Models;

/// <summary>
/// Ein aufbereitetes, ggf. gruppiertes Windows-Ereignis.
/// </summary>
public sealed class EventItem
{
    public required string Id { get; init; }
    public required string EventKey { get; init; }
    public int EventId { get; init; }
    public string? ProviderName { get; init; }
    public string? LogName { get; init; }
    public string? Level { get; init; }
    public EventSeverity Severity { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public string? Title { get; init; }
    public string? Summary { get; init; }
    public string? OriginalMessage { get; init; }
    public string? MachineName { get; init; }
    public int Count { get; init; }
    public DateTimeOffset FirstSeen { get; init; }
    public DateTimeOffset LastSeen { get; init; }
    public List<DateTimeOffset> Occurrences { get; init; } = new();
    public string? RawXml { get; init; }
    public bool IsKnownEvent { get; init; }
}
