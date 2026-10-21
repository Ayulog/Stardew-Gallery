namespace StardewGallery;

internal readonly struct EventIdentity : IEquatable<EventIdentity>
{
    private readonly string? assetName;
    private readonly string? eventId;
    private readonly string? storageEventId;

    internal EventIdentity(string assetName, string eventId)
    {
        this.assetName = (assetName ?? string.Empty).Replace('\\', '/').Trim();
        this.eventId = eventId ?? string.Empty;
        storageEventId = this.eventId.Trim();
    }

    public string AssetName => assetName ?? string.Empty;

    /// <summary>The game's exact event ID, as recorded in eventsSeen; it can keep surrounding spaces ("123 /f ...").</summary>
    public string EventId => eventId ?? string.Empty;

    /// <summary>The trimmed ID that identifies the event and keys saved gallery data, as before 2.10.0.</summary>
    public string StorageEventId => storageEventId ?? string.Empty;

    public string StorageKey => $"{AssetName}\u001f{StorageEventId}";

    public bool Equals(EventIdentity other)
        => StringComparer.OrdinalIgnoreCase.Equals(AssetName, other.AssetName)
            && StringComparer.Ordinal.Equals(StorageEventId, other.StorageEventId);

    public override bool Equals(object? obj) => obj is EventIdentity other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(
        StringComparer.OrdinalIgnoreCase.GetHashCode(AssetName),
        StringComparer.Ordinal.GetHashCode(StorageEventId));

    public override string ToString() => StorageKey;

    public static bool operator ==(EventIdentity left, EventIdentity right) => left.Equals(right);

    public static bool operator !=(EventIdentity left, EventIdentity right) => !left.Equals(right);
}
