namespace StardewGallery;

internal readonly record struct ScenicStageLayout(
    int PixelZoom,
    int StageX,
    int StageY,
    int StageWidth,
    int StageHeight,
    int ActorFeetY);

internal readonly record struct ScenicParsedAnimation(
    int[] StartFrames,
    int[] LoopFrames,
    int[] EndFrames);

/// <summary>
/// Pure-C# rules for the character album's 96x68 native-pixel seasonal/day-night scenic window:
/// 12-state asset mapping, weather priority, screen-space integer pixel zoom (eliminating fractional-scale
/// sprite squashing), and safe <c>Data/AnimationDescriptions</c> upright hobby animation selection.
/// </summary>
internal static class ScenicWindowRules
{
    internal const int NativeWidth = 96;
    internal const int NativeHeight = 68;
    internal const int NativeMeadowHorizonY = 40;
    internal const int NativeActorFeetY = 55;

    private static readonly string[] Seasons = ["Spring", "Summer", "Fall", "Winter"];
    private static readonly string[] Phases = ["Day", "Dusk", "Night"];

    // Curated upright outdoor hobby animations from vanilla Data/AnimationDescriptions (all checked against active sheet bounds at runtime).
    private static readonly Dictionary<string, string[]> CuratedHobbyKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Abigail"] = ["abigail_flute"],
        ["Haley"] = ["haley_photo"],
        ["Elliott"] = ["elliott_read"],
        ["Leah"] = ["leah_draw"],
        ["Sam"] = ["sam_guitar", "sam_gameboy"],
        ["Sebastian"] = ["sebastian_smoke"],
        ["Penny"] = ["penny_read"],
        ["Alex"] = ["alex_football", "alex_lift_weights"],
        ["Emily"] = ["emily_exercise"],
        ["Shane"] = ["shane_charlie"],
        ["Harvey"] = ["harvey_read"],
        ["Maru"] = ["maru_tinker"],
        ["Jas"] = ["jas_jumprope", "jas_read"],
        ["Vincent"] = ["vincent_play"],
        ["Caroline"] = ["caroline_read"],
        ["Robin"] = ["robin_exercise"],
        ["Jodi"] = ["jodi_exercise"],
        ["Marnie"] = ["marnie_exercise"],
        ["Evelyn"] = ["evelyn_garden"],
        ["Wizard"] = ["wizard_read"],
        ["Leo"] = ["leo_parrot"],
        ["Krobus"] = ["krobus_happy"]
    };

    private static readonly string[] SafeModdedHobbySuffixes =
    [
        "_read", "_flute", "_guitar", "_photo", "_draw", "_paint", "_sketch",
        "_book", "_music", "_dance", "_exercise", "_wave", "_laugh", "_cheer",
        "_happy", "_jumprope", "_camera", "_sing", "_flower", "_garden",
        "_violin", "_harp", "_lute", "_stretch", "_charlie", "_parrot"
    ];

    private static readonly string[] DisallowedAnimationTokens =
    [
        "sleep", "bed", "sit", "chair", "bench", "couch", "stool", "desk", "table",
        "computer", "towel", "swim", "bath", "shower", "dead", "cry", "hurt", "sad",
        "kiss", "shop", "counter", "bar", "stove", "sink", "fish", "piano", "tv",
        "arcade", "pool", "car", "horse", "boat", "train", "clinic", "hospital",
        "passout", "passed", "lie", "laying", "drink", "drunk", "wedding"
    ];

    internal static IReadOnlyList<string> AllAssetPaths { get; } = BuildAllAssetPaths();

    private static string[] BuildAllAssetPaths()
    {
        List<string> paths = new(12);
        foreach (string season in Seasons)
            foreach (string phase in Phases)
                paths.Add($"assets/Scenic-{season}-{phase}.png");
        return paths.ToArray();
    }

    internal static ScenicTimePhase ResolveTimePhase(int timeOfDay)
        => timeOfDay >= 2000 ? ScenicTimePhase.Night
            : timeOfDay >= 1700 ? ScenicTimePhase.Dusk
            : ScenicTimePhase.Day;

    internal static string ResolveAssetPath(string? rawSeason, int timeOfDay)
    {
        string season = NormalizeSeasonCapitalized(rawSeason);
        string phase = ResolveTimePhase(timeOfDay) switch
        {
            ScenicTimePhase.Night => "Night",
            ScenicTimePhase.Dusk => "Dusk",
            _ => "Day"
        };
        return $"assets/Scenic-{season}-{phase}.png";
    }

    internal static string NormalizeSeasonCapitalized(string? rawSeason) => (WeddingDateRecord.NormalizeSeason(rawSeason) ?? "spring") switch
    {
        "summer" => "Summer",
        "fall" => "Fall",
        "winter" => "Winter",
        _ => "Spring"
    };

    /// <summary>
    /// Resolves world weather with Stardew 1.6 priority: GreenRain -> Storm -> Rain -> Snow -> Wind -> Clear.
    /// </summary>
    internal static ScenicWeatherKind ResolveWorldWeather(
        string? weatherId,
        bool isRaining = false,
        bool isSnowing = false,
        bool isLightning = false,
        bool isDebrisWeather = false,
        bool isGreenRain = false)
    {
        string trimmed = weatherId?.Trim() ?? string.Empty;

        if (isGreenRain || trimmed.Equals("GreenRain", StringComparison.OrdinalIgnoreCase))
            return ScenicWeatherKind.GreenRain;
        if (isLightning || trimmed.Equals("Storm", StringComparison.OrdinalIgnoreCase))
            return ScenicWeatherKind.Storm;
        if (isRaining || trimmed.Equals("Rain", StringComparison.OrdinalIgnoreCase))
            return ScenicWeatherKind.Rain;
        if (isSnowing || trimmed.Equals("Snow", StringComparison.OrdinalIgnoreCase))
            return ScenicWeatherKind.Snow;
        if (isDebrisWeather || trimmed.Equals("Wind", StringComparison.OrdinalIgnoreCase))
            return ScenicWeatherKind.Wind;

        return ScenicWeatherKind.Clear;
    }

    /// <summary>
    /// Subtle weather overcast wash applied over the dedicated Day/Dusk/Night artwork.
    /// Clear, Wind, and Snow keep the crisp 12-state pixel art untouched.
    /// </summary>
    internal static (byte R, byte G, byte B, float Alpha) WeatherWashOverlay(ScenicWeatherKind weather) => weather switch
    {
        ScenicWeatherKind.GreenRain => (48, 138, 62, 0.24f),
        ScenicWeatherKind.Storm => (32, 44, 68, 0.22f),
        ScenicWeatherKind.Rain => (46, 62, 92, 0.13f),
        _ => (0, 0, 0, 0f)
    };

    /// <summary>
    /// Picks a strict screen-space integer pixel zoom for the scenic window.
    /// Defaults to vanilla <c>Game1.pixelZoom</c> (4) on normal viewports (including 1080p @ 100%-150% and 720p)
    /// so characters render at true in-game pixel size with zero fractional-scale squashing, stepping down
    /// cleanly to 3/2/1 only on very small windows.
    /// </summary>
    internal static int ComputePixelZoom(int clipWidth, int clipHeight)
    {
        if (clipWidth <= 0 || clipHeight <= 0)
            return 4;

        // Ensure standard 16x32 sprite (32 * zoom) fits comfortably inside the clipped cutout while keeping >= 4x at normal sizes.
        int zoom = clipHeight >= 168 && clipWidth >= 180 ? 4
            : clipHeight >= 120 && clipWidth >= 128 ? 3
            : clipHeight >= 76 && clipWidth >= 84 ? 2
            : 1;

        // If the visible cutout somehow exceeds the 96x68 native backdrop at this zoom, bump to the next integer zoom.
        while (zoom < 6 && (NativeWidth * zoom < clipWidth - 8 || NativeHeight * zoom < clipHeight - 8))
            zoom++;

        return zoom;
    }

    /// <summary>
    /// Computes exact integer screen-pixel framing for the 96x68 backdrop and actor ground line inside <paramref name="clipX"/>..<paramref name="clipWidth"/>.
    /// </summary>
    internal static ScenicStageLayout ComputeStageLayout(int clipX, int clipY, int clipWidth, int clipHeight)
    {
        int zoom = ComputePixelZoom(clipWidth, clipHeight);
        int stageW = NativeWidth * zoom;
        int stageH = NativeHeight * zoom;
        int stageX = clipX + (clipWidth - stageW) / 2;
        int stageY = clipY + (clipHeight - stageH) / 2;

        // Place the villager's feet on the flat meadow (native y = 55) while keeping headroom & bottom margin on tighter crops.
        int desiredFeetY = stageY + NativeActorFeetY * zoom;
        int minFeetY = clipY + Math.Min(clipHeight - 4, 32 * zoom + 4);
        int maxFeetY = clipY + clipHeight - Math.Max(6, zoom * 2);
        int feetY = minFeetY <= maxFeetY
            ? Math.Clamp(desiredFeetY, minFeetY, maxFeetY)
            : clipY + clipHeight - 4;

        return new ScenicStageLayout(zoom, stageX, stageY, stageW, stageH, feetY);
    }

    /// <summary>
    /// Selects a safe upright outdoor hobby animation key from <c>Data/AnimationDescriptions</c> for an NPC,
    /// validating that every frame index fits within the character's currently active sprite sheet.
    /// </summary>
    internal static string? SelectHobbyAnimationKey(
        string? characterName,
        IReadOnlyDictionary<string, string>? descriptions,
        int textureWidth,
        int textureHeight,
        int frameWidth,
        int frameHeight,
        out ScenicParsedAnimation parsed)
    {
        parsed = default;
        if (string.IsNullOrWhiteSpace(characterName) || descriptions is null || descriptions.Count == 0)
            return null;
        if (frameWidth <= 0 || frameHeight <= 0 || textureWidth != frameWidth * 4 || textureHeight < frameHeight * 4)
            return null;

        int totalFrames = (textureWidth / frameWidth) * (textureHeight / frameHeight);
        string trimmed = characterName.Trim();

        // 1. Prefer verified curated vanilla hobby keys.
        if (CuratedHobbyKeys.TryGetValue(trimmed, out string[]? preferred))
        {
            foreach (string key in preferred)
            {
                if (TryLookupCaseInsensitive(descriptions, key, out string? raw)
                    && TryParseAnimationFrames(raw, totalFrames, out parsed))
                    return key;
            }
        }

        // 2. For modded NPCs (or fallback), accept upright hobby-named keys for this NPC with strict posture filtering.
        string prefix = trimmed.ToLowerInvariant() + "_";
        foreach ((string key, string raw) in descriptions.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            string lower = key.ToLowerInvariant();
            if (!lower.StartsWith(prefix, StringComparison.Ordinal))
                continue;
            if (DisallowedAnimationTokens.Any(bad => lower.Contains(bad, StringComparison.Ordinal)))
                continue;
            if (!SafeModdedHobbySuffixes.Any(good => lower.Contains(good, StringComparison.Ordinal)))
                continue;
            if (TryParseAnimationFrames(raw, totalFrames, out parsed))
                return key;
        }

        return null;
    }

    /// <summary>
    /// Parses a <c>Data/AnimationDescriptions</c> entry (<c>start/loop/end</c>) and verifies every frame is in <c>[0, totalFrames)</c>.
    /// </summary>
    internal static bool TryParseAnimationFrames(string? rawEntry, int totalFrames, out ScenicParsedAnimation parsed)
    {
        parsed = default;
        if (string.IsNullOrWhiteSpace(rawEntry) || totalFrames <= 0)
            return false;

        string[] parts = rawEntry.Split('/');
        if (parts.Length < 3)
            return false;

        if (!TryParseFrameSequence(parts[0], totalFrames, allowEmpty: true, out int[] start)
            || !TryParseFrameSequence(parts[1], totalFrames, allowEmpty: false, out int[] loop)
            || !TryParseFrameSequence(parts[2], totalFrames, allowEmpty: true, out int[] end))
            return false;

        // Require at least one non-base or multi-frame hobby pose (or valid custom pose frame >= 16) so we don't replay frame 0 idle as a hobby.
        bool hasDistinctPose = loop.Length > 1 || loop.Any(f => f != 0) || start.Any(f => f != 0);
        if (!hasDistinctPose)
            return false;

        parsed = new ScenicParsedAnimation(start, loop, end);
        return true;
    }

    private static bool TryParseFrameSequence(string segment, int totalFrames, bool allowEmpty, out int[] frames)
    {
        frames = [];
        string[] tokens = segment.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
            return allowEmpty;

        List<int> list = new(tokens.Length);
        foreach (string token in tokens)
        {
            if (!int.TryParse(token, out int index) || index < 0 || index >= totalFrames)
                return false;
            list.Add(index);
        }
        frames = list.ToArray();
        return true;
    }

    private static bool TryLookupCaseInsensitive(IReadOnlyDictionary<string, string> map, string key, out string? value)
    {
        if (map.TryGetValue(key, out value))
            return true;
        foreach (var pair in map)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = pair.Value;
                return true;
            }
        }
        value = null;
        return false;
    }
}
