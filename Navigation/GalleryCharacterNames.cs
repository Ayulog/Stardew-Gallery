using StardewModdingAPI;
using StardewValley;
using StardewValley.TokenizableStrings;
using StardewValley.GameData.Characters;

namespace StardewGallery;

internal sealed class GalleryCharacterNames
{
    private readonly Dictionary<string, string> names = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> aliases = new(StringComparer.Ordinal);
    private readonly Dictionary<EventIdentity, Dictionary<string, string>> sceneAliases = [];
    private readonly ITranslationHelper i18n;
    internal GalleryCharacterNames(GalleryCatalog catalog, ITranslationHelper i18n)
    {
        this.i18n = i18n;
        var characters = DataLoader.Characters(Game1.content);
        foreach (var (id, data) in characters)
        {
            string? display = data.DisplayName is null ? null : TokenParser.ParseText(data.DisplayName);
            names[id] = GalleryNameText.IsMissing(display) ? NPC.GetDisplayName(id) : display;
        }
        foreach (var character in catalog.Characters)
            if (!GalleryNameText.IsMissing(character.DisplayName) && character.DisplayName != character.Name || !names.ContainsKey(character.Name))
                names[character.Name] = character.DisplayName;
        foreach (string id in new[] { "AlanaAvis", "JunimoJade", "Apples", "MrQi" }) names.TryAdd(id, id);
        foreach (var (id, data) in characters)
        {
            string plain = id.TrimEnd('·');
            if (plain != id && names.ContainsKey(plain) && data.SocialTab == SocialTabBehavior.HiddenAlways
                && string.Equals(data.CanSocialize, "FALSE", StringComparison.OrdinalIgnoreCase)) aliases[id] = plain;
        }
        foreach (GalleryEvent entry in catalog.AllEntries)
        {
            var local = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string script in entry.Fragments.Scripts.Prepend(entry.Script).Distinct())
            foreach (string command in Event.ParseCommands(script))
            {
                if (!command.StartsWith("changeName ", StringComparison.OrdinalIgnoreCase)
                    && !command.StartsWith("addTemporaryActor ", StringComparison.OrdinalIgnoreCase)) continue;
                string[] a = ArgUtility.SplitBySpaceQuoteAware(command);
                if (a.Length >= 3 && a[0].Equals("changeName", StringComparison.OrdinalIgnoreCase)
                    && !names.ContainsKey(a[1]) && names.ContainsKey(a[2])) local[a[1]] = a[2];
                else if (a.Length >= 10 && a[0].Equals("addTemporaryActor", StringComparison.OrdinalIgnoreCase)
                    && names.ContainsKey(a[1]) && !names.ContainsKey(a[9])) local[a[9]] = a[1];
            }
            if (local.Count > 0) sceneAliases[entry.Resolved.Identity] = local;
        }
        // SVE explicitly uses this temporary costume actor in Claire's Joja scenes.
        if (names.ContainsKey("Claire")) aliases["ClaireJoja"] = "Claire";
        if (names.ContainsKey("Claire")) aliases["ClaireTheater"] = "Claire";
    }
    internal string? Key(string raw, GalleryEvent? entry = null)
    {
        string id = raw.TrimEnd('?');
        if (aliases.TryGetValue(id, out string? canonical)) return canonical;
        if (names.ContainsKey(id)) return id;
        if (entry is not null && sceneAliases.TryGetValue(entry.Resolved.Identity, out var local) && local.TryGetValue(id, out string? actor)) return actor;
        // These repeated suffixes distinguish cosmetic actor copies, not extra NPCs.
        string plain = id.TrimEnd('·');
        return plain != id && names.ContainsKey(plain) ? plain : null;
    }
    internal string Get(string raw)
    {
        string id = Key(raw) ?? raw;
        string? display = names.GetValueOrDefault(id);
        if (!GalleryNameText.IsMissing(display) && display != id) return display!;
        var translated = i18n.Get("character." + id.ToLowerInvariant());
        if (translated.HasValue() && !GalleryNameText.IsMissing(translated.ToString())) return translated.ToString();
        return GalleryLocationName.Resolve(id, null, _ => null);
    }
}
