namespace StardewGallery;

internal sealed record WeddingDateRecord(int Year, string Season, int DayOfMonth)
{
    internal bool IsValid => Year >= 1
        && DayOfMonth >= 1
        && NormalizeSeason(Season) is not null;

    internal WeddingDateRecord Normalized()
        => new(Year, NormalizeSeason(Season) ?? Season.Trim().ToLowerInvariant(), DayOfMonth);

    internal static string? NormalizeSeason(string? season)
    {
        if (string.IsNullOrWhiteSpace(season))
            return null;
        string lower = season.Trim().ToLowerInvariant();
        return lower is "spring" or "summer" or "fall" or "winter" ? lower : null;
    }
}

/// <summary>
/// Stable identity and pure validation helpers for save-scoped spouse Wedding Memento cards (<c>Wedding/&lt;SpouseName&gt;</c>).
/// Wedding mementos live in <see cref="EventPhotoStore"/> per save, never enter the replay state machine, and never
/// inflate character heart-story denominators for unmarried or single-playthrough saves.
/// </summary>
internal static class WeddingMemento
{
    internal const string AssetName = "Wedding";

    internal static EventIdentity IdentityFor(string spouseName)
    {
        if (string.IsNullOrWhiteSpace(spouseName))
            throw new ArgumentException("A villager spouse name is required.", nameof(spouseName));
        return new EventIdentity(AssetName, spouseName.Trim());
    }

    internal static bool IsWeddingIdentity(EventIdentity identity)
        => identity.AssetName.Equals(AssetName, StringComparison.OrdinalIgnoreCase)
            && identity.StorageEventId.Length > 0;

    internal static string? TryGetSpouseName(EventIdentity identity)
        => IsWeddingIdentity(identity) ? identity.StorageEventId : null;
}
