using StardewModdingAPI;
using StardewValley;
using StardewValley.TokenizableStrings;

namespace StardewGallery;

internal sealed class GalleryLocationNames(ITranslationHelper i18n)
{
    private Dictionary<string, string>? mapNames;
    private readonly Dictionary<string, string> cache = new(StringComparer.OrdinalIgnoreCase);
    internal string Get(string id)
    {
        if (cache.TryGetValue(id, out string? name)) return name;
        string? display = Game1.getLocationFromName(id)?.DisplayName;
        if (string.IsNullOrWhiteSpace(display) || display.Equals(id, StringComparison.OrdinalIgnoreCase) || display.StartsWith("Custom_", StringComparison.OrdinalIgnoreCase))
        {
            if (mapNames is null)
            {
                try { mapNames = ReadMapNames(); }
                catch { mapNames = new(StringComparer.OrdinalIgnoreCase); }
            }
            display = mapNames.GetValueOrDefault(id);
        }
        return cache[id] = GalleryLocationName.Resolve(id, display, key => i18n.Get(key).HasValue() ? i18n.Get(key).ToString() : null);
    }
    private static Dictionary<string, string> ReadMapNames()
    {
        var candidates = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var region in DataLoader.WorldMap(Game1.content).Values)
        foreach (var area in region.MapAreas ?? [])
        {
            if (!string.IsNullOrWhiteSpace(area.Condition)) continue;
            foreach (var position in area.WorldPositions ?? [])
            {
                if (!string.IsNullOrWhiteSpace(position.Condition) || position.TileArea.Width > 0 || position.TileArea.Height > 0) continue;
                string? text = position.ScrollText ?? area.ScrollText;
                if (string.IsNullOrWhiteSpace(text)) continue;
                string[] ids = (string.IsNullOrWhiteSpace(position.LocationName) ? [] : new[] { position.LocationName })
                    .Concat(position.LocationNames ?? []).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                // An area caption covering several locations is too broad to rename each room.
                if (position.ScrollText is null && (ids.Length != 1 || (area.WorldPositions?.Count ?? 0) != 1)) continue;
                string value = TokenParser.ParseText(text).Replace('\n', ' ').Trim();
                if (value.Length == 0) continue;
                foreach (string id in ids)
                {
                    if (!candidates.TryGetValue(id, out var values)) candidates[id] = values = new(StringComparer.CurrentCulture);
                    values.Add(value);
                }
            }
        }
        return candidates.Where(pair => pair.Value.Count == 1).ToDictionary(pair => pair.Key, pair => pair.Value.Single(), StringComparer.OrdinalIgnoreCase);
    }
}
