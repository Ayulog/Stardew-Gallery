using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace StardewGallery;

/// <summary>
/// Loads and caches the 12 native 96x68 seasonal/day-night pixel-art horizon backdrops from <c>assets/</c>
/// and draws them at strict screen-space integer pixel zoom.
/// </summary>
internal sealed class ScenicBackdropRenderer(IModContentHelper modContent, IMonitor monitor)
{
    private readonly Dictionary<string, Texture2D?> cache = new(StringComparer.OrdinalIgnoreCase);

    internal void Draw(SpriteBatch b, ScenicStageLayout layout, string? rawSeason, int timeOfDay, ScenicWeatherKind weather)
    {
        if (b is null)
            return;

        string assetPath = ScenicWindowRules.ResolveAssetPath(rawSeason, timeOfDay);
        Texture2D? texture = LoadBackdrop(assetPath);
        Rectangle stageRect = new(layout.StageX, layout.StageY, layout.StageWidth, layout.StageHeight);

        if (texture is not null && !texture.IsDisposed)
        {
            b.Draw(texture, stageRect, Color.White);
        }
        else if (Game1.staminaRect is { } fallbackRect)
        {
            b.Draw(fallbackRect, stageRect, FallbackColor(rawSeason, ScenicWindowRules.ResolveTimePhase(timeOfDay)));
        }

        (byte r, byte g, byte bl, float alpha) = ScenicWindowRules.WeatherWashOverlay(weather);
        if (alpha > 0.005f && Game1.staminaRect is { } washRect)
            b.Draw(washRect, stageRect, new Color(r, g, bl) * alpha);
    }

    internal void Invalidate() => cache.Clear();

    private Texture2D? LoadBackdrop(string assetPath)
    {
        if (cache.TryGetValue(assetPath, out Texture2D? existing))
        {
            if (existing is null || !existing.IsDisposed)
                return existing;
            cache.Remove(assetPath);
        }

        try
        {
            Texture2D loaded = modContent.Load<Texture2D>(assetPath);
            cache[assetPath] = loaded;
            return loaded;
        }
        catch (Exception error)
        {
            cache[assetPath] = null;
            monitor.LogOnce($"Could not load scenic backdrop '{assetPath}': {error.Message}", LogLevel.Warn);
            return null;
        }
    }

    internal static Color FallbackColor(string? rawSeason, ScenicTimePhase phase)
    {
        string season = WeddingDateRecord.NormalizeSeason(rawSeason) ?? "spring";
        if (phase == ScenicTimePhase.Night)
            return season == "winter" ? new Color(24, 39, 89) : new Color(26, 38, 80);
        if (phase == ScenicTimePhase.Dusk)
            return new Color(150, 89, 133);
        return season switch
        {
            "summer" => new Color(72, 167, 244),
            "fall" => new Color(110, 176, 232),
            "winter" => new Color(139, 194, 240),
            _ => new Color(106, 187, 244)
        };
    }
}
