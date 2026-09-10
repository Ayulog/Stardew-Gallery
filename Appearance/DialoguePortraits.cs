using System.Collections;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace StardewGallery.Appearance;

internal sealed class DialoguePortraits(IGameContentHelper content, IModRegistry registry)
{
    internal const string ModId = "Mangupix.DialogueDisplayFrameworkContinued";
    internal const string AssetName = "aedenthorn.DialogueDisplayFramework/dictionary";
    private Dictionary<string, DialoguePortraitEntry>? entries;

    internal void Invalidate() => entries = null;

    internal PortraitFrame? Get(NPC? npc, string name, Texture2D texture)
    {
        if (!registry.IsLoaded(ModId)) return null;
        if (entries is null)
        {
            entries = new(StringComparer.Ordinal);
            // Read the public asset without populating DDFC's shared dialogue cache.
            if (content.Load<object>(AssetName) is IDictionary data)
            foreach (DictionaryEntry pair in data)
            {
                if (pair.Key is not string key || pair.Value is null) continue;
                object? portrait = PublicAppearanceData.Get(pair.Value, "Portrait");
                entries[key] = new(PublicAppearanceData.String(pair.Value, "CopyFrom"),
                    PublicAppearanceData.Bool(pair.Value, "Disabled"), portrait is null ? null : new(
                        PublicAppearanceData.String(portrait, "TexturePath"),
                        PublicAppearanceData.Int(portrait, "X", -1), PublicAppearanceData.Int(portrait, "Y", -1),
                        PublicAppearanceData.Int(portrait, "W", 64), PublicAppearanceData.Int(portrait, "H", 64),
                        PublicAppearanceData.Float(portrait, "Alpha", 1), PublicAppearanceData.Bool(portrait, "Disabled")),
                    PublicAppearanceData.String(pair.Value, "Condition"));
            }
        }
        List<string> keys = [];
        if (npc?.currentLocation is { } location && location.TryGetMapProperty("UniquePortrait", out string unique)
            && ArgUtility.SplitBySpace(unique).Contains(name))
            keys.Add(name + "_" + location.Name);
        if (npc?.LastAppearanceId is { Length: > 0 } appearance)
            keys.Add(name + "_" + appearance);
        if (texture.Name?.EndsWith("_Beach", StringComparison.OrdinalIgnoreCase) == true
            || npc?.GetData()?.Appearance?.Any(a => a.Id == npc.LastAppearanceId && a.IsIslandAttire) == true)
            keys.Add(name + "_Beach");
        keys.Add(name);
        return DialoguePortraitSelection.Resolve(entries, keys);
    }
}
