using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Network;

namespace StardewGallery;

/// <summary>
/// Directly reuses Stardew Valley's native <see cref="WeatherDebris"/> (spring cherry petals,
/// summer leaves, fall maple leaves, winter snow) and native <see cref="Game1.rainTexture"/>
/// (correct vanilla frame 0 falling streak + frames 1..3 ground-only splashes) inside the scenic window
/// at strict screen-space integer pixel zoom.
/// </summary>
internal sealed class ScenicWeatherParticles
{
    private sealed class RainDropState
    {
        public float X;
        public float Y;
        public float GroundY;
        public float SpeedY;
        public float WindX;
        public int SplashFrame; // 0 = falling, 1..3 = splashing on meadow
        public double SplashTimerMs;
    }

    private readonly List<WeatherDebris> debrisPool = [];
    private readonly List<RainDropState> rainPool = [];
    private string? activeKey;
    private int activeStageW;
    private int activeStageH;
    private int activeZoom = 4;
    private double lastUpdateTotalMs = -1d;

    internal static ScenicWeatherKind ResolveCurrentWeather()
    {
        string? weatherId = null;
        bool isRaining = false;
        bool isSnowing = false;
        bool isLightning = false;
        bool isDebris = false;
        bool isGreenRain = false;

        try
        {
            if (Game1.netWorldState?.Value is { } worldState)
            {
                string contextId = Game1.currentLocation?.GetLocationContextId() ?? "Default";
                LocationWeather? lw = worldState.GetWeatherForLocation(contextId)
                    ?? worldState.GetWeatherForLocation("Default");
                if (lw is not null)
                {
                    weatherId = lw.Weather;
                    isRaining = lw.IsRaining;
                    isSnowing = lw.IsSnowing;
                    isLightning = lw.IsLightning;
                    isDebris = lw.IsDebrisWeather;
                    isGreenRain = lw.IsGreenRain;
                }
            }
            else
            {
                isRaining = Game1.isRaining;
                isSnowing = Game1.isSnowing;
                isLightning = Game1.isLightning;
                isDebris = Game1.isDebrisWeather;
                isGreenRain = Game1.isGreenRain;
            }
        }
        catch
        {
            // Headless / uninitialized world state safely falls back to Clear.
        }

        return ScenicWindowRules.ResolveWorldWeather(
            weatherId,
            isRaining,
            isSnowing,
            isLightning,
            isDebris,
            isGreenRain);
    }

    internal void Draw(
        SpriteBatch b,
        ScenicStageLayout layout,
        string season,
        ScenicTimePhase phase,
        ScenicWeatherKind weather,
        double totalMs)
    {
        if (b is null)
            return;

        string normalizedSeason = WeddingDateRecord.NormalizeSeason(season) ?? "spring";
        EnsureInitialized(normalizedSeason, phase, weather, layout);

        double dtMs = ComputeDeltaMs(totalMs);
        if (dtMs > 0.0)
            StepSimulation(normalizedSeason, phase, weather, layout, dtMs);

        switch (weather)
        {
            case ScenicWeatherKind.GreenRain:
                DrawRainDrops(b, layout, greenRain: true);
                DrawGreenRainSpores(b, layout, totalMs);
                break;

            case ScenicWeatherKind.Storm:
                DrawRainDrops(b, layout, greenRain: false);
                DrawStormFlash(b, layout, totalMs);
                break;

            case ScenicWeatherKind.Rain:
                DrawRainDrops(b, layout, greenRain: false);
                break;

            case ScenicWeatherKind.Snow:
            case ScenicWeatherKind.Wind:
                DrawDebrisPool(b, layout);
                break;

            case ScenicWeatherKind.Clear:
                if (debrisPool.Count > 0)
                    DrawDebrisPool(b, layout);
                if (normalizedSeason == "summer" && phase == ScenicTimePhase.Night)
                    DrawSummerNightFireflies(b, layout, totalMs);
                break;
        }
    }

    private double ComputeDeltaMs(double totalMs)
    {
        if (!double.IsFinite(totalMs))
            return 0.0;
        if (lastUpdateTotalMs < 0.0 || totalMs < lastUpdateTotalMs)
        {
            lastUpdateTotalMs = totalMs;
            return 16.667;
        }
        double delta = totalMs - lastUpdateTotalMs;
        lastUpdateTotalMs = totalMs;
        return Math.Clamp(delta, 0.0, 50.0);
    }

    private void EnsureInitialized(string season, ScenicTimePhase phase, ScenicWeatherKind weather, ScenicStageLayout layout)
    {
        string key = $"{season}|{phase}|{weather}|{layout.PixelZoom}";
        if (activeKey == key && activeStageW == layout.StageWidth && activeStageH == layout.StageHeight)
            return;

        activeKey = key;
        activeStageW = layout.StageWidth;
        activeStageH = layout.StageHeight;
        activeZoom = layout.PixelZoom;
        debrisPool.Clear();
        rainPool.Clear();

        Random rng = new(HashSeed(season, weather));

        if (weather is ScenicWeatherKind.Rain or ScenicWeatherKind.Storm or ScenicWeatherKind.GreenRain)
        {
            int count = weather == ScenicWeatherKind.Storm ? 18
                : weather == ScenicWeatherKind.GreenRain ? 14
                : 12;
            for (int i = 0; i < count; i++)
                rainPool.Add(CreateRainDrop(rng, layout, randomInitialY: true));
            return;
        }

        int? debrisKind = null;
        int debrisCount = 0;
        bool gentle = false;

        if (weather == ScenicWeatherKind.Snow)
        {
            debrisKind = WeatherDebris.snow;
            debrisCount = 18;
        }
        else if (weather == ScenicWeatherKind.Wind)
        {
            debrisKind = season switch
            {
                "spring" => WeatherDebris.pinkPetals,
                "summer" => WeatherDebris.greenLeaves,
                "fall" => WeatherDebris.fallLeaves,
                "winter" => WeatherDebris.snow,
                _ => WeatherDebris.pinkPetals
            };
            debrisCount = season == "winter" ? 16 : 11;
        }
        else if (weather == ScenicWeatherKind.Clear && phase != ScenicTimePhase.Night)
        {
            // Gentle sparse seasonal ambiance on clear Spring/Fall days (1-2 drifting vanilla petals/leaves).
            if (season == "spring")
            {
                debrisKind = WeatherDebris.pinkPetals;
                debrisCount = 2;
                gentle = true;
            }
            else if (season == "fall")
            {
                debrisKind = WeatherDebris.fallLeaves;
                debrisCount = 2;
                gentle = true;
            }
        }

        if (debrisKind is { } which && debrisCount > 0)
        {
            for (int i = 0; i < debrisCount; i++)
            {
                float x = (float)(rng.NextDouble() * layout.StageWidth);
                float y = (float)(rng.NextDouble() * layout.StageHeight);
                float dx = gentle ? (float)(0.25 + rng.NextDouble() * 0.35)
                    : weather == ScenicWeatherKind.Snow ? (float)(-0.35 + rng.NextDouble() * 0.7)
                    : (float)(1.2 + rng.NextDouble() * 1.1);
                float dy = gentle ? (float)(0.35 + rng.NextDouble() * 0.25)
                    : weather == ScenicWeatherKind.Snow ? (float)(0.55 + rng.NextDouble() * 0.45)
                    : (float)(0.5 + rng.NextDouble() * 0.45);
                float rot = (float)(rng.NextDouble() * 0.08);
                WeatherDebris debris = new(new Vector2(x, y), which, rot, dx, dy);
                debrisPool.Add(debris);
            }
        }
    }

    private void StepSimulation(string season, ScenicTimePhase phase, ScenicWeatherKind weather, ScenicStageLayout layout, double dtMs)
    {
        float dtScale = (float)(dtMs / 16.6667);

        if (rainPool.Count > 0)
        {
            int groundMinY = ScenicWindowRules.NativeMeadowHorizonY * layout.PixelZoom + layout.PixelZoom * 2;
            int groundMaxY = Math.Max(groundMinY + 8, layout.StageHeight - layout.PixelZoom * 5);
            for (int i = 0; i < rainPool.Count; i++)
            {
                RainDropState drop = rainPool[i];
                if (drop.SplashFrame == 0)
                {
                    drop.X += drop.WindX * dtScale;
                    drop.Y += drop.SpeedY * dtScale;

                    if (drop.X < -16 * layout.PixelZoom)
                        drop.X += layout.StageWidth + 16 * layout.PixelZoom;

                    if (drop.Y >= drop.GroundY)
                    {
                        drop.Y = drop.GroundY;
                        drop.SplashFrame = 1;
                        drop.SplashTimerMs = 0.0;
                    }
                }
                else
                {
                    drop.SplashTimerMs += dtMs;
                    if (drop.SplashTimerMs >= 75.0)
                    {
                        drop.SplashTimerMs -= 75.0;
                        drop.SplashFrame++;
                        if (drop.SplashFrame > 3)
                        {
                            // Respawn cleanly above top sky edge with deterministic variation.
                            int seed = (i + 1) * 7919 + (int)(lastUpdateTotalMs * 13);
                            Random rng = new(seed);
                            drop.X = (float)(rng.NextDouble() * (layout.StageWidth + 24 * layout.PixelZoom));
                            drop.Y = -16f * layout.PixelZoom - (float)(rng.NextDouble() * 14f * layout.PixelZoom);
                            drop.GroundY = groundMinY + (float)(rng.NextDouble() * (groundMaxY - groundMinY));
                            drop.SplashFrame = 0;
                            drop.SplashTimerMs = 0.0;
                        }
                    }
                }
            }
            return;
        }

        if (debrisPool.Count > 0)
        {
            bool gentle = weather == ScenicWeatherKind.Clear;
            float margin = 16f * layout.PixelZoom;
            bool canRunNativeUpdate = Game1.currentGameTime is not null;

            for (int i = 0; i < debrisPool.Count; i++)
            {
                WeatherDebris debris = debrisPool[i];
                if (canRunNativeUpdate)
                {
                    try
                    {
                        // Directly reuse Stardew Valley's native WeatherDebris update & 4-frame tumbling animation.
                        debris.update(slow: gentle);
                    }
                    catch
                    {
                        debris.position.X += debris.dx * dtScale;
                        debris.position.Y += debris.dy * dtScale;
                    }
                }
                else
                {
                    debris.position.X += debris.dx * dtScale;
                    debris.position.Y += debris.dy * dtScale;
                }

                // Wrap within the scenic stage bounds (native WeatherDebris wraps to full Game1.viewport).
                if (debris.position.X > layout.StageWidth + margin)
                    debris.position.X = -margin * 0.5f;
                else if (debris.position.X < -margin)
                    debris.position.X = layout.StageWidth + margin * 0.5f;

                if (debris.position.Y > layout.StageHeight + margin * 0.5f)
                    debris.position.Y = -margin * 0.75f;
                else if (debris.position.Y < -margin)
                    debris.position.Y = layout.StageHeight + margin * 0.25f;
            }
        }
    }

    private void DrawDebrisPool(SpriteBatch b, ScenicStageLayout layout)
    {
        if (Game1.mouseCursors is null)
            return;

        for (int i = 0; i < debrisPool.Count; i++)
        {
            WeatherDebris debris = debrisPool[i];
            Vector2 pos = new(
                layout.StageX + MathF.Floor(debris.position.X),
                layout.StageY + MathF.Floor(debris.position.Y));
            b.Draw(
                Game1.mouseCursors,
                pos,
                debris.sourceRect,
                Color.White,
                0f,
                Vector2.Zero,
                layout.PixelZoom,
                SpriteEffects.None,
                1f);
        }
    }

    private void DrawRainDrops(SpriteBatch b, ScenicStageLayout layout, bool greenRain)
    {
        Texture2D? rainTex = Game1.rainTexture;
        int rowY = greenRain ? 16 : 0;
        // Native 1.6 TileSheets/rain row y=16 is already emerald-tinted; keep Color.White for crisp authentic tones.
        Color tint = greenRain ? Color.White * 0.88f : Color.White * 0.76f;

        for (int i = 0; i < rainPool.Count; i++)
        {
            RainDropState drop = rainPool[i];
            int x = layout.StageX + (int)MathF.Floor(drop.X);
            int y = layout.StageY + (int)MathF.Floor(drop.Y);

            if (rainTex is not null)
            {
                // Vanilla TileSheets/rain: frame 0 = falling raindrop streak, frames 1..3 = ground splash ripples.
                int frame = Math.Clamp(drop.SplashFrame, 0, 3);
                Rectangle src = new(frame * 16, rowY, 16, 16);
                b.Draw(
                    rainTex,
                    new Vector2(x, y),
                    src,
                    tint,
                    0f,
                    Vector2.Zero,
                    layout.PixelZoom,
                    SpriteEffects.None,
                    1f);
            }
            else if (Game1.staminaRect is not null && drop.SplashFrame == 0)
            {
                b.Draw(Game1.staminaRect, new Rectangle(x, y, layout.PixelZoom, layout.PixelZoom * 4), tint);
            }
        }
    }

    private static void DrawGreenRainSpores(SpriteBatch b, ScenicStageLayout layout, double totalMs)
    {
        if (Game1.staminaRect is null)
            return;

        double t = totalMs / 1000.0;
        int z = layout.PixelZoom;
        for (int i = 0; i < 9; i++)
        {
            double nx = 10.0 + ((i * 19.0 + Math.Sin(t * 1.1 + i * 1.3) * 6.0) % (ScenicWindowRules.NativeWidth - 20));
            double ny = 12.0 + ((i * 13.0 + t * (4.0 + (i % 3) * 1.5)) % (ScenicWindowRules.NativeHeight - 20));
            float pulse = 0.35f + 0.45f * (0.5f + 0.5f * (float)Math.Sin(t * 2.2 + i * 1.7));
            int px = layout.StageX + (int)Math.Floor(nx) * z;
            int py = layout.StageY + (int)Math.Floor(ny) * z;
            b.Draw(Game1.staminaRect, new Rectangle(px, py, z, z), new Color(170, 255, 148) * pulse);
        }
    }

    private static void DrawStormFlash(SpriteBatch b, ScenicStageLayout layout, double totalMs)
    {
        if (Game1.staminaRect is null)
            return;

        double cycleMs = totalMs % 9400.0;
        float alpha = 0f;
        if (cycleMs < 110.0)
            alpha = (float)(1.0 - cycleMs / 110.0) * 0.28f;
        else if (cycleMs is >= 170.0 and < 330.0)
            alpha = (float)(1.0 - (cycleMs - 170.0) / 160.0) * 0.20f;

        if (alpha > 0.01f)
            b.Draw(Game1.staminaRect, new Rectangle(layout.StageX, layout.StageY, layout.StageWidth, layout.StageHeight), new Color(225, 240, 255) * alpha);
    }

    private static void DrawSummerNightFireflies(SpriteBatch b, ScenicStageLayout layout, double totalMs)
    {
        if (Game1.staminaRect is null)
            return;

        // 3 gentle Stardew-style glowing fireflies drifting over the meadow grass at Summer Night.
        double t = totalMs / 1000.0;
        int z = layout.PixelZoom;
        (double BaseX, double BaseY, double FreqX, double FreqY, double Phase)[] flies =
        [
            (28.0, 48.0, 0.72, 0.95, 0.4),
            (66.0, 46.0, 0.61, 0.84, 2.3),
            (46.0, 56.0, 0.53, 1.08, 4.1)
        ];

        for (int i = 0; i < flies.Length; i++)
        {
            var f = flies[i];
            int nx = (int)Math.Round(f.BaseX + Math.Sin(t * f.FreqX + f.Phase) * 9.0);
            int ny = (int)Math.Round(f.BaseY + Math.Cos(t * f.FreqY + f.Phase) * 4.0);
            float breath = 0.22f + 0.78f * (0.5f + 0.5f * (float)Math.Sin(t * 2.3 + f.Phase));
            int px = layout.StageX + nx * z;
            int py = layout.StageY + ny * z;
            b.Draw(Game1.staminaRect, new Rectangle(px - z, py - z, z * 3, z * 3), new Color(186, 255, 110) * (0.20f * breath));
            b.Draw(Game1.staminaRect, new Rectangle(px, py, z, z), new Color(242, 255, 168) * breath);
        }
    }

    private static RainDropState CreateRainDrop(Random rng, ScenicStageLayout layout, bool randomInitialY)
    {
        int groundMinY = ScenicWindowRules.NativeMeadowHorizonY * layout.PixelZoom + layout.PixelZoom * 2;
        int groundMaxY = Math.Max(groundMinY + 8, layout.StageHeight - layout.PixelZoom * 5);
        float groundY = groundMinY + (float)(rng.NextDouble() * (groundMaxY - groundMinY));
        float y = randomInitialY
            ? -16f * layout.PixelZoom + (float)(rng.NextDouble() * (groundY + 16f * layout.PixelZoom))
            : -16f * layout.PixelZoom - (float)(rng.NextDouble() * 14f * layout.PixelZoom);
        return new RainDropState
        {
            X = (float)(rng.NextDouble() * (layout.StageWidth + 20 * layout.PixelZoom)),
            Y = y,
            GroundY = groundY,
            SpeedY = (float)((3.2 + rng.NextDouble() * 1.1) * (layout.PixelZoom / 4.0)),
            WindX = (float)(-1.05 * (layout.PixelZoom / 4.0)),
            SplashFrame = 0,
            SplashTimerMs = 0.0
        };
    }

    private static int HashSeed(string season, ScenicWeatherKind weather)
    {
        unchecked
        {
            int h = 17;
            foreach (char ch in season) h = h * 31 + ch;
            return h * 31 + (int)weather;
        }
    }
}
