using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StardewGallery;

internal enum GalleryEventCategory { All, Heart, Ordinary, Flow }

internal sealed record GalleryEventGroup(string Key, IReadOnlyList<GalleryEvent> Sources)
{
    public GalleryEvent Representative => Sources[0];
    public IReadOnlyList<string> NpcNames { get; } = Sources.SelectMany(entry => entry.RelatedNpcNames).Distinct(StringComparer.Ordinal).ToArray();
    public bool IsFlow => Sources.All(entry => entry.IsFlow);
    public bool IsHeart(string? npc = null) => Sources.Any(entry => entry.HeartNpcNames.Any(name => npc is null || name == npc));

    internal bool Matches(GalleryEventCategory category, string? npc, string? asset, string query,
        Func<string, string> npcName, Func<GalleryEvent, string> location)
    {
        if (npc is not null && !NpcNames.Contains(npc, StringComparer.Ordinal)) return false;
        if (asset is not null && !Sources.Any(entry => entry.AssetName.Equals(asset, StringComparison.OrdinalIgnoreCase))) return false;
        bool categoryMatch = category switch
        {
            GalleryEventCategory.Heart => !IsFlow && IsHeart(npc),
            GalleryEventCategory.Ordinary => !IsFlow && !IsHeart(npc),
            GalleryEventCategory.Flow => IsFlow,
            _ => !IsFlow || query.Length > 0
        };
        return categoryMatch && (query.Length == 0
            || Representative.EventId.Contains(query, StringComparison.OrdinalIgnoreCase)
            || NpcNames.Any(name => name.Contains(query, StringComparison.OrdinalIgnoreCase) || npcName(name).Contains(query, StringComparison.CurrentCultureIgnoreCase))
            || Sources.Any(entry => entry.AssetName.Contains(query, StringComparison.OrdinalIgnoreCase) || location(entry).Contains(query, StringComparison.CurrentCultureIgnoreCase)));
    }

    internal static IReadOnlyList<GalleryEventGroup> Build(IEnumerable<GalleryEvent> entries)
        => entries.DistinctBy(entry => entry.Resolved.Identity).GroupBy(Fingerprint, StringComparer.Ordinal)
            .Select(group => new GalleryEventGroup(group.Key, group.OrderBy(entry => entry.AssetName, StringComparer.Ordinal).ToArray()))
            .OrderBy(group => group.Representative.EventId, StringComparer.Ordinal).ThenBy(group => group.Key, StringComparer.Ordinal).ToArray();

    private static string Fingerprint(GalleryEvent entry)
    {
        // Incomplete fragments cannot establish equivalence across locations.
        if (entry.Fragments.MissingKeys.Count > 0) return entry.Identity;
        string value = JsonSerializer.Serialize(new { entry.EventId, entry.EventKey, entry.Script, entry.Fragments.Scripts });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}
