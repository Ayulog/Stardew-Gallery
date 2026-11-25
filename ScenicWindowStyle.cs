namespace StardewGallery;

internal enum ScenicTimePhase
{
    Day,
    Dusk,
    Night
}

internal enum ScenicWeatherKind
{
    Clear,
    Rain,
    Storm,
    Snow,
    Wind,
    GreenRain
}

internal sealed record ScenicWindowPalette(
    string NormalizedSeason,
    ScenicTimePhase TimePhase,
    ScenicWeatherKind Weather,
    (byte R, byte G, byte B) SceneTint,
    (byte R, byte G, byte B, float Alpha) AtmosphereOverlay,
    (byte R, byte G, byte B) SpriteTint);

/// <summary>
/// Pure seasonal, day/night, and weather lighting resolver for the character album's left-page scenic window.
/// </summary>
internal static class ScenicWindowStyle
{
    internal static ScenicWindowPalette Resolve(
        string? season,
        int timeOfDay,
        string? weatherId,
        bool isRaining = false,
        bool isSnowing = false,
        bool isGreenRain = false)
    {
        string normSeason = WeddingDateRecord.NormalizeSeason(season) ?? "spring";
        ScenicTimePhase phase = timeOfDay >= 2000 ? ScenicTimePhase.Night
            : timeOfDay >= 1700 ? ScenicTimePhase.Dusk
            : ScenicTimePhase.Day;

        ScenicWeatherKind weather = ResolveWeather(weatherId, isRaining, isSnowing, isGreenRain);

        (byte R, byte G, byte B) sceneTint = normSeason switch
        {
            "summer" => (255, 252, 226),
            "fall" => (255, 212, 168),
            "winter" => (202, 226, 255),
            _ => (255, 248, 248)
        };

        (byte R, byte G, byte B, float Alpha) overlay = phase switch
        {
            ScenicTimePhase.Dusk => (255, 138, 72, 0.24f),
            ScenicTimePhase.Night => (24, 36, 84, 0.48f),
            _ => (0, 0, 0, 0f)
        };

        (byte R, byte G, byte B) spriteTint = phase switch
        {
            ScenicTimePhase.Dusk => (255, 236, 214),
            ScenicTimePhase.Night => (212, 222, 248),
            _ => (255, 255, 255)
        };

        switch (weather)
        {
            case ScenicWeatherKind.GreenRain:
                overlay = BlendOverlay(overlay, (74, 178, 86, 0.30f));
                spriteTint = TintMultiply(spriteTint, (226, 252, 226));
                break;
            case ScenicWeatherKind.Storm:
                overlay = BlendOverlay(overlay, (52, 66, 94, 0.28f));
                spriteTint = TintMultiply(spriteTint, (222, 228, 242));
                break;
            case ScenicWeatherKind.Rain:
                overlay = BlendOverlay(overlay, (76, 96, 128, 0.20f));
                spriteTint = TintMultiply(spriteTint, (234, 240, 250));
                break;
            case ScenicWeatherKind.Snow:
                overlay = BlendOverlay(overlay, (226, 240, 255, phase == ScenicTimePhase.Night ? 0.14f : 0.20f));
                break;
        }

        return new ScenicWindowPalette(normSeason, phase, weather, sceneTint, overlay, spriteTint);
    }

    internal static ScenicWeatherKind ResolveWeather(
        string? weatherId,
        bool isRaining = false,
        bool isSnowing = false,
        bool isGreenRain = false)
    {
        if (isGreenRain)
            return ScenicWeatherKind.GreenRain;

        if (!string.IsNullOrWhiteSpace(weatherId))
        {
            string trimmed = weatherId.Trim();
            if (trimmed.Equals("GreenRain", StringComparison.OrdinalIgnoreCase))
                return ScenicWeatherKind.GreenRain;
            if (trimmed.Equals("Storm", StringComparison.OrdinalIgnoreCase))
                return ScenicWeatherKind.Storm;
            if (trimmed.Equals("Rain", StringComparison.OrdinalIgnoreCase))
                return ScenicWeatherKind.Rain;
            if (trimmed.Equals("Snow", StringComparison.OrdinalIgnoreCase))
                return ScenicWeatherKind.Snow;
            if (trimmed.Equals("Wind", StringComparison.OrdinalIgnoreCase))
                return ScenicWeatherKind.Wind;
            if (trimmed.Equals("Sun", StringComparison.OrdinalIgnoreCase)
                || trimmed.Equals("Festival", StringComparison.OrdinalIgnoreCase)
                || trimmed.Equals("Wedding", StringComparison.OrdinalIgnoreCase))
                return ScenicWeatherKind.Clear;
        }

        if (isSnowing)
            return ScenicWeatherKind.Snow;
        if (isRaining)
            return ScenicWeatherKind.Rain;
        return ScenicWeatherKind.Clear;
    }

    private static (byte R, byte G, byte B, float Alpha) BlendOverlay(
        (byte R, byte G, byte B, float Alpha) baseOverlay,
        (byte R, byte G, byte B, float Alpha) weatherOverlay)
    {
        if (baseOverlay.Alpha <= 0f)
            return weatherOverlay;
        float total = Math.Clamp(baseOverlay.Alpha + weatherOverlay.Alpha * (1f - baseOverlay.Alpha), 0f, 0.72f);
        float weightB = weatherOverlay.Alpha / Math.Max(0.001f, baseOverlay.Alpha + weatherOverlay.Alpha);
        float weightA = 1f - weightB;
        return (
            (byte)Math.Round(baseOverlay.R * weightA + weatherOverlay.R * weightB),
            (byte)Math.Round(baseOverlay.G * weightA + weatherOverlay.G * weightB),
            (byte)Math.Round(baseOverlay.B * weightA + weatherOverlay.B * weightB),
            total);
    }

    private static (byte R, byte G, byte B) TintMultiply((byte R, byte G, byte B) a, (byte R, byte G, byte B) b)
        => ((byte)(a.R * b.R / 255), (byte)(a.G * b.G / 255), (byte)(a.B * b.B / 255));
}
