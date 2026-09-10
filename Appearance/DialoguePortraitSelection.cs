namespace StardewGallery.Appearance;

internal sealed record DialoguePortraitEntry(string? CopyFrom, bool Disabled, PortraitFrame? Portrait, string? Condition = null);

internal static class DialoguePortraitSelection
{
    internal static PortraitFrame? Resolve(IReadOnlyDictionary<string, DialoguePortraitEntry> data, IEnumerable<string> keys)
    {
        DialoguePortraitEntry? entry = null;
        foreach (string key in keys.Append("default"))
        {
            if (!data.TryGetValue(key, out var candidate) || candidate.Disabled) continue;
            // Conditional entries were added after the supported DDFC 0.7 API.
            // Do not run third-party GSQs while browsing the gallery.
            if (!string.IsNullOrWhiteSpace(candidate.Condition)) return null;
            entry = candidate;
            break;
        }
        HashSet<string> visited = new(StringComparer.Ordinal);
        while (entry is not null && !entry.Disabled && string.IsNullOrWhiteSpace(entry.Condition))
        {
            if (entry.Portrait is not null) return entry.Portrait;
            if (entry.CopyFrom is not { Length: > 0 } parent || !visited.Add(parent) || !data.TryGetValue(parent, out entry)) break;
        }
        return null;
    }
}
