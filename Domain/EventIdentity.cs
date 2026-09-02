namespace StardewGallery;

internal readonly struct EventIdentity : IEquatable<EventIdentity>
{
    private readonly string? assetName;
    private readonly string? eventId;

    internal EventIdentity(string assetName, string eventId)
    {
        this.assetName = (assetName ?? string.Empty).Replace('\\', '/').Trim();
        this.eventId = (eventId ?? string.Empty).Trim();
    }

    public string AssetName => assetName ?? string.Empty;

    public string EventId => eventId ?? string.Empty;

    public string StorageKey => $"{AssetName}\u001f{EventId}";

    public bool Equals(EventIdentity other)
        => StringComparer.OrdinalIgnoreCase.Equals(AssetName, other.AssetName)
            && StringComparer.Ordinal.Equals(EventId, other.EventId);

    public override bool Equals(object? obj) => obj is EventIdentity other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(
        StringComparer.OrdinalIgnoreCase.GetHashCode(AssetName),
        StringComparer.Ordinal.GetHashCode(EventId));

    public override string ToString() => StorageKey;

    public static bool operator ==(EventIdentity left, EventIdentity right) => left.Equals(right);

    public static bool operator !=(EventIdentity left, EventIdentity right) => !left.Equals(right);
}
