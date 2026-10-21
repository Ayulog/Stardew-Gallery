namespace StardewGallery;

internal enum TrailerEventPlacement { Listed, ListedAsTrailer, Hidden }

/// <summary>Event copies that GameLocation.TryGetLocationEvents (1.6.15) adds to other locations' event dictionaries.</summary>
internal static class NativeGlobalEventCopies
{
    internal const string TrailerAsset = "Data\\Events\\Trailer";
    internal const string TrailerBigAsset = "Data\\Events\\Trailer_Big";

    internal static bool IsCopy(string assetName, string key, string script, IReadOnlyDictionary<string, string> farmhouseEvents)
        => !assetName.Replace('\\', '/').Equals("Data/Events/FarmHouse", StringComparison.OrdinalIgnoreCase)
            && (key.StartsWith("558291/", StringComparison.Ordinal) || key.StartsWith("558292/", StringComparison.Ordinal))
            && farmhouseEvents.TryGetValue(key, out string? original) && script == original;

    /// <summary>Trailer keys merged into Trailer_Big: every key its own events don't define (35/36 get rewritten scripts).</summary>
    internal static IReadOnlySet<string> TrailerCopyKeys(IReadOnlyDictionary<string, string>? trailerEvents, IReadOnlyDictionary<string, string>? trailerBigEvents)
        => (trailerEvents?.Keys ?? Enumerable.Empty<string>()).Where(key => trailerBigEvents?.ContainsKey(key) != true).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// List a merged trailer event once, under the Trailer asset, where the game can play it now: Pam's door leads to
    /// Trailer before her house upgrade and to Trailer_Big after it.
    /// </summary>
    internal static TrailerEventPlacement PlaceTrailerEvent(string locationName, string key, IReadOnlySet<string> copyKeys, bool pamHouseUpgraded)
        => !copyKeys.Contains(key) ? TrailerEventPlacement.Listed
            : locationName == "Trailer" ? pamHouseUpgraded ? TrailerEventPlacement.Hidden : TrailerEventPlacement.Listed
            : locationName == "Trailer_Big" ? pamHouseUpgraded ? TrailerEventPlacement.ListedAsTrailer : TrailerEventPlacement.Hidden
            : TrailerEventPlacement.Listed;

    /// <summary>Before 2.10.0 each merged trailer event was also listed as its Trailer_Big copy, which may hold photos and names.</summary>
    internal static IEnumerable<(EventIdentity Current, EventIdentity Former)> FormerTrailerCopies(GalleryCatalog catalog)
        => catalog.AllEntries.Select(entry => entry.Resolved.Identity)
            .Where(identity => identity.AssetName.Equals("Data/Events/Trailer", StringComparison.OrdinalIgnoreCase))
            .Select(identity => (Current: identity, Former: new EventIdentity(TrailerBigAsset, identity.EventId)))
            .Where(pair => catalog.Find(pair.Former) is null)
            .ToArray();
}
