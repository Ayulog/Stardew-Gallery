namespace StardewGallery;

/// <summary>A content provider identity, not a claim about copyright or historical authorship.</summary>
internal sealed record EventSourceActor(string UniqueId, string Name, string? Version = null, bool IsGameBase = false)
{
    internal static EventSourceActor GameBase { get; } = new("StardewValley", "Stardew Valley", IsGameBase: true);
}

internal static class EventAssetName
{
    internal static string Normalize(string name) => (name ?? string.Empty).Replace('\\', '/').Trim();
}
