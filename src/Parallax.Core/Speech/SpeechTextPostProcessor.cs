using System.Text.RegularExpressions;

namespace Parallax.Core.Speech;

public static class SpeechTextPostProcessor
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

    public static string Apply(string text, string customWords, bool appendTrailingSpace)
    {
        string processed = ApplyCustomWords(text.Trim(), ParseCustomWords(customWords));
        return appendTrailingSpace && processed.Length > 0 && !processed.EndsWith(' ')
            ? processed + " "
            : processed;
    }

    private static string ApplyCustomWords(string text, IReadOnlyList<CustomWordRule> rules)
    {
        if (text.Length == 0 || rules.Count == 0)
        {
            return text;
        }

        string processed = text;
        foreach (var rule in rules)
        {
            foreach (string alias in rule.Aliases)
            {
                string pattern = CreateBoundedPattern(alias);
                processed = Regex.Replace(
                    processed,
                    pattern,
                    rule.Canonical,
                    RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
                    RegexTimeout);
            }
        }

        return processed;
    }

    private static IReadOnlyList<CustomWordRule> ParseCustomWords(string customWords)
    {
        return customWords
            .Split([';', '\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseRule)
            .Where(rule => rule != null)
            .Select(rule => rule!)
            .DistinctBy(rule => rule.Canonical, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static CustomWordRule? ParseRule(string value)
    {
        string[] parts = value.Split('=', 2, StringSplitOptions.TrimEntries);
        string canonical = parts[0];
        if (string.IsNullOrWhiteSpace(canonical))
        {
            return null;
        }

        string[] aliases = parts.Length == 2
            ? parts[1].Split(['|', ','], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            : [canonical];
        aliases = aliases.Append(canonical)
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new CustomWordRule(canonical, aliases);
    }

    private static string CreateBoundedPattern(string alias)
    {
        string escaped = Regex.Escape(alias).Replace("\\ ", "\\s+");
        return $@"(?<![\p{{L}}\p{{N}}]){escaped}(?![\p{{L}}\p{{N}}])";
    }

    private sealed record CustomWordRule(string Canonical, string[] Aliases);
}
