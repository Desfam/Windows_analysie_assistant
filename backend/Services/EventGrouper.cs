using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using WindowsDiagnosticApp.Models;

namespace WindowsDiagnosticApp.Services;

/// <summary>
/// Fasst identische oder sehr ähnliche Rohereignisse zu gruppierten Ereigniskarten zusammen.
/// </summary>
public sealed partial class EventGrouper
{
    private const int MaxSummaryLength = 240;
    private const int MaxTitleLength = 90;
    private const int MaxOccurrences = 100;

    private readonly KnownEventCatalog _catalog;

    public EventGrouper(KnownEventCatalog catalog)
    {
        _catalog = catalog;
    }

    public IReadOnlyList<EventItem> Group(IEnumerable<RawEventRecord> records)
    {
        var groups = new Dictionary<string, List<RawEventRecord>>(StringComparer.Ordinal);

        foreach (var record in records)
        {
            var key = BuildKey(record);
            if (!groups.TryGetValue(key, out var list))
            {
                list = new List<RawEventRecord>();
                groups[key] = list;
            }

            list.Add(record);
        }

        var items = new List<EventItem>(groups.Count);
        foreach (var (key, list) in groups)
        {
            items.Add(BuildItem(key, list));
        }

        return items
            .OrderByDescending(i => i.Severity)
            .ThenByDescending(i => i.LastSeen)
            .ToList();
    }

    private EventItem BuildItem(string key, List<RawEventRecord> records)
    {
        var ordered = records.OrderByDescending(r => r.TimeCreated).ToList();
        var latest = ordered[0];
        var known = _catalog.Find(latest.ProviderName, latest.EventId);
        var severity = _catalog.MapSeverity(latest.Level, known);

        var title = known?.Title ?? DeriveTitle(latest);
        var summary = known?.Explanation ?? Truncate(SingleLine(latest.Message), MaxSummaryLength);

        var occurrences = ordered
            .Select(r => r.TimeCreated)
            .Take(MaxOccurrences)
            .ToList();

        return new EventItem
        {
            Id = Hash(key),
            EventKey = key,
            EventId = latest.EventId,
            ProviderName = latest.ProviderName,
            LogName = latest.LogName,
            Level = latest.LevelDisplayName,
            Severity = severity,
            Timestamp = latest.TimeCreated,
            Title = title,
            Summary = summary,
            OriginalMessage = latest.Message,
            MachineName = latest.MachineName,
            Count = records.Count,
            FirstSeen = records.Min(r => r.TimeCreated),
            LastSeen = records.Max(r => r.TimeCreated),
            Occurrences = occurrences,
            RawXml = latest.Xml,
            IsKnownEvent = known is not null
        };
    }

    private static string BuildKey(RawEventRecord record)
    {
        var provider = record.ProviderName ?? "unknown";
        return string.Create(CultureInfo.InvariantCulture,
            $"{record.LogName}|{provider}|{record.EventId}|{NormalizeMessage(record.Message)}");
    }

    /// <summary>Entfernt variable Bestandteile, damit ähnliche Meldungen gruppiert werden.</summary>
    internal static string NormalizeMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        var text = message.ToLowerInvariant();
        text = GuidRegex().Replace(text, "#");
        text = HexRegex().Replace(text, "#");
        text = NumberRegex().Replace(text, "#");
        text = WhitespaceRegex().Replace(text, " ").Trim();

        return text.Length > 160 ? text[..160] : text;
    }

    private static string DeriveTitle(RawEventRecord record)
    {
        var line = SingleLine(record.Message);
        if (string.IsNullOrWhiteSpace(line))
        {
            return $"Ereignis-ID {record.EventId} ({record.ProviderName})";
        }

        var firstSentence = line.Split(". ", 2)[0];
        return Truncate(firstSentence, MaxTitleLength);
    }

    private static string SingleLine(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return WhitespaceRegex().Replace(text, " ").Trim();
    }

    private static string Truncate(string text, int max)
    {
        if (text.Length <= max)
        {
            return text;
        }

        return text[..max].TrimEnd() + " …";
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes, 0, 8).ToLowerInvariant();
    }

    [GeneratedRegex(@"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}")]
    private static partial Regex GuidRegex();

    [GeneratedRegex(@"0x[0-9a-f]+")]
    private static partial Regex HexRegex();

    [GeneratedRegex(@"\d+")]
    private static partial Regex NumberRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
