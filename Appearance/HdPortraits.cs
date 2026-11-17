using System.Collections;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace StardewGallery.Appearance;

/// <summary>
/// Read-only adapter for <c>tlitookilakin.HDPortraits</c> metadata (<c>Mods/HDPortraits/&lt;Key&gt;</c> and
/// <c>Mods/HDPortraits</c>) using public asset projection without invoking third-party methods or patching Harmony.
/// </summary>
internal sealed class HdPortraits(IGameContentHelper content, IModRegistry registry)
{
    internal const string ModId = "tlitookilakin.HDPortraits";
    internal const string DictionaryAssetName = "Mods/HDPortraits";
    internal const string AssetPrefix = "Mods/HDPortraits/";

    private bool dictionaryLoaded;
    private Dictionary<string, HdPortraitEntry?>? dictionaryEntries;
    private readonly Dictionary<string, HdPortraitEntry?> perKeyEntries = new(StringComparer.OrdinalIgnoreCase);

    internal void Invalidate()
    {
        dictionaryLoaded = false;
        dictionaryEntries = null;
        perKeyEntries.Clear();
    }

    internal HdPortraitEntry? Get(NPC? npc, string name, Texture2D original)
    {
        if (!registry.IsLoaded(ModId))
            return null;

        IReadOnlyList<string> keys = BuildCandidateKeys(npc, name, original);
        foreach (string key in keys)
        {
            HdPortraitEntry? direct = LoadPerCharacterEntry(key);
            if (direct is { IsConfigured: true })
                return direct;
        }

        Dictionary<string, HdPortraitEntry?>? shared = LoadDictionaryEntries();
        return shared is null ? null : HdPortraitSelection.Resolve(shared, keys);
    }

    private HdPortraitEntry? LoadPerCharacterEntry(string key)
    {
        if (perKeyEntries.TryGetValue(key, out HdPortraitEntry? cached))
            return cached;

        object? raw;
        try
        {
            raw = content.Load<object>(AssetPrefix + key);
        }
        catch (Exception ex) when (ex is ContentLoadException or FileNotFoundException or DirectoryNotFoundException)
        {
            perKeyEntries[key] = null;
            return null;
        }

        if (raw is null or IDictionary)
        {
            perKeyEntries[key] = null;
            return null;
        }

        HdPortraitEntry? parsed = ParseMetadataModel(raw);
        perKeyEntries[key] = parsed;
        return parsed;
    }

    private Dictionary<string, HdPortraitEntry?>? LoadDictionaryEntries()
    {
        if (dictionaryLoaded)
            return dictionaryEntries;

        object? raw;
        try
        {
            raw = content.Load<object>(DictionaryAssetName);
        }
        catch (Exception ex) when (ex is ContentLoadException or FileNotFoundException or DirectoryNotFoundException)
        {
            dictionaryLoaded = true;
            return dictionaryEntries = null;
        }

        if (raw is not IDictionary dict)
            throw new InvalidDataException("HD Portraits shared metadata asset is not a dictionary.");

        Dictionary<string, HdPortraitEntry?> loaded = new(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry pair in dict)
        {
            if (pair.Key is string key && !string.IsNullOrWhiteSpace(key) && pair.Value is not null)
                loaded[key.Trim()] = ParseMetadataModel(pair.Value);
        }
        dictionaryLoaded = true;
        return dictionaryEntries = loaded;
    }

    internal static HdPortraitEntry ParseMetadataModel(object model)
    {
        (int width, int height) = ReadDimensions(model);
        if (width is <= 0 or > 8192 || height is <= 0 or > 8192)
            throw new InvalidDataException($"Invalid HD Portraits dimensions {width}x{height}.");

        string? texturePath = PublicAppearanceData.String(model, "Portrait")
            ?? PublicAppearanceData.String(model, "TexturePath")
            ?? PublicAppearanceData.String(model, "Texture");

        HdPortraitAnimation? animation = ReadAnimation(PublicAppearanceData.Get(model, "Animation") ?? model);
        return new HdPortraitEntry(width, height, texturePath, animation);
    }

    private static (int Width, int Height) ReadDimensions(object model)
    {
        object? sizeObj = PublicAppearanceData.Get(model, "Size");
        if (sizeObj is int square)
            return (square, square);
        if (sizeObj is Point pt)
            return (pt.X, pt.Y);
        if (sizeObj is IList { Count: >= 2 } list
            && TryToInt(list[0], out int listW)
            && TryToInt(list[1], out int listH))
        {
            return (listW, listH);
        }
        if (sizeObj is not null)
        {
            int sx = PublicAppearanceData.Int(sizeObj, "X", PublicAppearanceData.Int(sizeObj, "Width", -1));
            int sy = PublicAppearanceData.Int(sizeObj, "Y", PublicAppearanceData.Int(sizeObj, "Height", -1));
            if (sx > 0 && sy > 0)
                return (sx, sy);
        }

        int width = PublicAppearanceData.Int(model, "Width", 64);
        int height = PublicAppearanceData.Int(model, "Height", 64);
        return (width, height);
    }

    private static HdPortraitAnimation? ReadAnimation(object source)
    {
        int hFrames = PublicAppearanceData.Int(source, "HFrames", 1);
        int vFrames = PublicAppearanceData.Int(source, "VFrames", 1);
        if (hFrames <= 0 || vFrames <= 0 || (long)hFrames * vFrames <= 1)
            return null;

        List<int> delays = [];
        if (PublicAppearanceData.Get(source, "Delays") is IEnumerable enumerable and not string)
        {
            foreach (object? item in enumerable)
            {
                if (TryToInt(item, out int ms))
                    delays.Add(ms);
            }
        }

        int fallbackDelay = PublicAppearanceData.Int(source, "Speed", PublicAppearanceData.Int(source, "Delay", 200));
        return new HdPortraitAnimation(hFrames, vFrames, delays, fallbackDelay > 0 ? fallbackDelay : 200);
    }

    private static bool TryToInt(object? value, out int number)
    {
        switch (value)
        {
            case int i: number = i; return true;
            case long l when l is >= int.MinValue and <= int.MaxValue: number = (int)l; return true;
            case short s: number = s; return true;
            default: number = 0; return false;
        }
    }

    private static IReadOnlyList<string> BuildCandidateKeys(NPC? npc, string name, Texture2D original)
    {
        List<string> keys = [];
        void AddKey(string? candidate)
        {
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                string trimmed = candidate.Trim();
                if (!keys.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
                    keys.Add(trimmed);
            }
        }

        if (original.Name is { Length: > 0 } assetName)
        {
            string normalized = assetName.Replace('\\', '/');
            const string prefix = "Portraits/";
            if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                AddKey(normalized[prefix.Length..]);
        }

        if (npc?.currentLocation is { } location
            && location.TryGetMapProperty("UniquePortrait", out string unique)
            && ArgUtility.SplitBySpace(unique).Contains(name))
        {
            AddKey(name + "_" + location.Name);
        }

        if (npc?.LastAppearanceId is { Length: > 0 } appearance)
            AddKey(name + "_" + appearance);

        if (original.Name?.EndsWith("_Beach", StringComparison.OrdinalIgnoreCase) == true
            || npc?.GetData()?.Appearance?.Any(a => a.Id == npc.LastAppearanceId && a.IsIslandAttire) == true)
        {
            AddKey(name + "_Beach");
        }

        try
        {
            AddKey(NPC.getTextureNameForCharacter(name));
        }
        catch
        {
            // Headless tests without initialized character data still resolve by character name.
        }

        AddKey(name);
        return keys;
    }
}
