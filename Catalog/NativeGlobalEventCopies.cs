namespace StardewGallery;

internal static class NativeGlobalEventCopies
{
    internal static bool IsCopy(string assetName, string key, string script, IReadOnlyDictionary<string, string> farmhouseEvents)
        => !assetName.Replace('\\', '/').Equals("Data/Events/FarmHouse", StringComparison.OrdinalIgnoreCase)
            && (key.StartsWith("558291/", StringComparison.Ordinal) || key.StartsWith("558292/", StringComparison.Ordinal))
            && farmhouseEvents.TryGetValue(key, out string? original) && script == original;
}
