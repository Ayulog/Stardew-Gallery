using StardewModdingAPI;
using StardewValley;
using StardewValley.TokenizableStrings;
using StardewValley.GameData.Locations;

namespace StardewGallery;

internal sealed class GalleryLocationNames(ITranslationHelper i18n)
{
    private Dictionary<string, string>? mapNames;
    private readonly Dictionary<string, string> cache = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string>? aliases;
    private Dictionary<string, LocationData> locationData = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> SceneAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Custom_Ridgeside_Ridge_KennethDate_OFF"] = "Custom_Ridgeside_Ridge",
        ["Custom_Ridgeside_Ridge_KennethDate_ON"] = "Custom_Ridgeside_Ridge",
        ["Custom_Ridgeside_RSVCliff_AlissaDate"] = "Custom_Ridgeside_RSVCliff"
    };
    internal string Key(string id) => "Data/Events/" + Canonical(id);
    private string Canonical(string id)
    {
        if (id.StartsWith("Cellar", StringComparison.Ordinal) && int.TryParse(id[6..], out int cellar) && cellar is >= 2 and <= 8)
            return "Cellar";
        if (aliases is null)
        {
            aliases = new(SceneAliases, StringComparer.OrdinalIgnoreCase);
            try
            {
                locationData = new(DataLoader.Locations(Game1.content), StringComparer.OrdinalIgnoreCase);
                foreach (var (current, data) in locationData)
                    foreach (string former in data.FormerLocationNames ?? [])
                        aliases.TryAdd(former, current);
            }
            catch { }
        }
        HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);
        while (visited.Add(id) && aliases.TryGetValue(id, out string? target)) id = target;
        return id;
    }
    internal string Get(string id)
    {
        id = Canonical(id);
        if (cache.TryGetValue(id, out string? name)) return name;
        string? display = locationData.GetValueOrDefault(id)?.DisplayName;
        if (!GalleryNameText.IsMissing(display)) display = TokenParser.ParseText(display);
        if (GalleryNameText.IsMissing(display)) display = Game1.getLocationFromName(id)?.DisplayName;
        if (GalleryNameText.IsMissing(display) || display.Equals(id, StringComparison.OrdinalIgnoreCase) || display.StartsWith("Custom_", StringComparison.OrdinalIgnoreCase))
        {
            if (mapNames is null)
            {
                try { mapNames = ReadMapNames(); }
                catch { mapNames = new(StringComparer.OrdinalIgnoreCase); }
            }
            display = mapNames.GetValueOrDefault(id);
        }
        if (GalleryNameText.IsMissing(display) || display.Equals(id, StringComparison.OrdinalIgnoreCase)
            || i18n.Locale.StartsWith("zh", StringComparison.OrdinalIgnoreCase) && display.All(character => character < 128))
        {
            var translated = i18n.Get("location." + id.ToLowerInvariant());
            if (translated.HasValue()) display = translated.ToString();
            else if (LocationNameFallbacks.Entries.TryGetValue(id, out var fallback)) display = Fallback(fallback.Place, fallback.Part);
            else if (id.StartsWith("Custom_", StringComparison.OrdinalIgnoreCase) && id.EndsWith("_WarpRoom", StringComparison.OrdinalIgnoreCase))
                display = Fallback("npc:" + id[7..^9], "scene");
        }
        return cache[id] = GalleryLocationName.Resolve(id, display, key => i18n.Get(key).HasValue() ? i18n.Get(key).ToString() : null);
    }
    private string Fallback(string place, string? part)
    {
        string name = place.StartsWith("term:", StringComparison.Ordinal) ? i18n.Get("place." + place[5..]).ToString()
            : place.StartsWith("npc:", StringComparison.Ordinal) ? CharacterName(place[4..]) : Get(place);
        if (GalleryNameText.IsMissing(name)) name = GalleryLocationName.Resolve(place[(place.IndexOf(':') + 1)..], null, _ => null);
        return part is null ? name : i18n.Get("location.name-part", new { place = name, part = i18n.Get("location.part." + part).ToString() });
    }
    private string CharacterName(string id)
    {
        string? name = NPC.TryGetData(id, out var data) && data.DisplayName is not null ? TokenParser.ParseText(data.DisplayName) : null;
        if (!GalleryNameText.IsMissing(name) && name != id) return name;
        var translated = i18n.Get("character." + id.ToLowerInvariant());
        return translated.HasValue() ? translated.ToString() : NPC.GetDisplayName(id);
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
                if (GalleryNameText.IsMissing(value)) continue;
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
