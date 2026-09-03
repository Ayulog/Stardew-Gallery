namespace StardewGallery;

internal static class ReplayBackupRetention
{
    internal const int MaxStale = 2;

    internal static IReadOnlyList<string> Retain(IEnumerable<string> names)
    {
        List<string> sorted = names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .OrderByDescending(name => name, StringComparer.Ordinal)
            .ToList();
        return sorted.Take(MaxStale).ToList();
    }

    internal static IReadOnlyList<string> Discard(IEnumerable<string> names)
    {
        List<string> sorted = names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .OrderByDescending(name => name, StringComparer.Ordinal)
            .ToList();
        return sorted.Skip(MaxStale).ToList();
    }
}
