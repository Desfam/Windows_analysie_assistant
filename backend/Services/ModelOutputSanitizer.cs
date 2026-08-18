using System.Text.RegularExpressions;

namespace WindowsDiagnosticApp.Services;

internal sealed record SanitizedModelOutput(string Text, bool HadThinkingContent, bool HasIncompleteThinkingBlock);

/// <summary>Entfernt Modellüberlegungen, bevor Modelltext weiterverarbeitet oder angezeigt wird.</summary>
internal static class ModelOutputSanitizer
{
    private static readonly string[] ThinkingTags = { "think", "analysis", "reasoning" };

    public static SanitizedModelOutput Sanitize(string? content)
    {
        var text = content ?? string.Empty;
        var hadThinking = false;
        var incomplete = false;

        foreach (var tag in ThinkingTags)
        {
            var complete = Regex.Replace(
                text,
                $@"(?is)<{tag}\b[^>]*>.*?</{tag}\s*>",
                string.Empty,
                RegexOptions.CultureInvariant);
            if (!string.Equals(complete, text, StringComparison.Ordinal))
            {
                hadThinking = true;
                text = complete;
            }

            var closing = Regex.Match(text, $@"(?is)^.*?</{tag}\s*>", RegexOptions.CultureInvariant);
            if (closing.Success)
            {
                hadThinking = true;
                text = text[closing.Length..];
            }

            var opening = Regex.Match(text, $@"(?is)<{tag}\b[^>]*>.*$", RegexOptions.CultureInvariant);
            if (opening.Success)
            {
                hadThinking = true;
                incomplete = true;
                text = text[..opening.Index];
            }
        }

        text = Regex.Replace(text, @"\n\s*\n\s*\n+", "\n\n", RegexOptions.CultureInvariant);
        return new SanitizedModelOutput(text.Trim(), hadThinking, incomplete);
    }
}