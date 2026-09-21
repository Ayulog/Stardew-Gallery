using System.Text.Json;

namespace StardewGallery;

/// <summary>Only declared ModId rules are used. Names, NPCs, locations and dataset prose are inert data.</summary>
internal sealed class AiModExclusionRules
{
    internal const int MaximumBytes = 1024 * 1024;
    private const int MaximumRules = 5000;
    private readonly string[] patterns;

    internal static AiModExclusionRules Empty { get; } = new(Array.Empty<string>());
    internal int Count => patterns.Length;

    private AiModExclusionRules(string[] patterns) => this.patterns = patterns;

    internal static AiModExclusionRules Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || System.Text.Encoding.UTF8.GetByteCount(json) > MaximumBytes)
            throw new FormatException("The exclusion list is empty or exceeds its size limit.");
        using JsonDocument document = JsonDocument.Parse(json.TrimStart('\uFEFF'), new JsonDocumentOptions { MaxDepth = 16 });
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new FormatException("The exclusion list must be an object of named mod records.");
        HashSet<string> names = new(StringComparer.Ordinal);
        HashSet<string> found = new(StringComparer.OrdinalIgnoreCase);
        int records = 0;
        foreach (JsonProperty record in document.RootElement.EnumerateObject())
        {
            if (!names.Add(record.Name)) throw new FormatException("The exclusion list contains duplicate record names.");
            // This field describes the dataset; its text must never become rules or executable instructions.
            if (record.Name.Equals("INSTRUCTIONS", StringComparison.Ordinal)) continue;
            if (++records > MaximumRules || record.Value.ValueKind != JsonValueKind.Object)
                throw new FormatException("The exclusion list contains an invalid mod record.");
            string? pattern = null;
            int modIdFields = 0;
            foreach (JsonProperty field in record.Value.EnumerateObject())
            {
                if (!field.Name.Equals("ModId", StringComparison.Ordinal)) continue;
                if (++modIdFields > 1 || field.Value.ValueKind != JsonValueKind.String)
                    throw new FormatException("Each exclusion record must have one string ModId.");
                pattern = field.Value.GetString();
            }
            if (!IsSafePattern(pattern))
                throw new FormatException("An exclusion record has a missing, unsupported or excessively broad ModId.");
            found.Add(pattern!);
        }
        if (found.Count == 0) throw new FormatException("The exclusion list contains no ModId rules.");
        return new(found.OrderBy(pattern => pattern, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    /// <summary>Filtering never uses a runtime fallback, a modifying pack, or an ambiguous candidate.</summary>
    internal bool ShouldExclude(EventOriginMatch? origin)
        => origin is { Status: EventOriginMatchStatus.Identified, Provider: { IsGameBase: false } provider }
            && MatchesModId(provider.UniqueId);

    internal bool MatchesModId(string? modId)
    {
        if (string.IsNullOrWhiteSpace(modId)) return false;
        foreach (string pattern in patterns)
            if (MatchesGlob(pattern, modId)) return true;
        return false;
    }

    internal bool HasSameRules(AiModExclusionRules other)
        => patterns.SequenceEqual(other.patterns, StringComparer.OrdinalIgnoreCase);

    private static bool IsSafePattern(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern) || pattern.Length > 256 || pattern != pattern.Trim()
            || pattern.Any(character => char.IsControl(character) || character is '[' or ']' or '/' or '\\'))
            return false;
        bool wildcard = pattern.IndexOfAny(new[] { '*', '?' }) >= 0;
        // Reject catch-all forms such as *, ?*, *.*, and nearly catch-all a* patterns.
        // A real namespace such as Wem.* retains at least three literal letters/digits.
        return !wildcard || pattern.Count(char.IsLetterOrDigit) >= 3;
    }

    /// <summary>Whole-ID, ordinal-ignore-case glob matching; '*' and '?' are the only metacharacters.</summary>
    private static bool MatchesGlob(string pattern, string value)
    {
        int patternIndex = 0, valueIndex = 0, lastStar = -1, lastStarValue = 0;
        while (valueIndex < value.Length)
        {
            if (patternIndex < pattern.Length && (pattern[patternIndex] == '?'
                || char.ToUpperInvariant(pattern[patternIndex]) == char.ToUpperInvariant(value[valueIndex])))
            {
                patternIndex++;
                valueIndex++;
            }
            else if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            {
                lastStar = patternIndex++;
                lastStarValue = valueIndex;
            }
            else if (lastStar >= 0)
            {
                patternIndex = lastStar + 1;
                valueIndex = ++lastStarValue;
            }
            else return false;
        }
        while (patternIndex < pattern.Length && pattern[patternIndex] == '*') patternIndex++;
        return patternIndex == pattern.Length;
    }
}
