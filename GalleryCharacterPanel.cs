using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Characters;
using StardewGallery.Appearance;

namespace StardewGallery;

internal sealed class GalleryCharacterPanel(
    GalleryCharacter character,
    IReadOnlyList<GalleryEvent> events,
    ITranslationHelper i18n,
    ICharacterAppearance appearance,
    GalleryCatalog? catalog = null,
    ScenicBackdropRenderer? scenicRenderer = null)
{
    private static RasterizerState? clipRasterizer;
    private static RasterizerState ClipRasterizer => clipRasterizer ??= new RasterizerState { ScissorTestEnable = true };

    private readonly ScenicWeatherParticles weatherParticles = new();

    internal void Prepare() => appearance.Prepare(character.Name, CharacterVisual.Sprite);

    internal void DrawPhoto(SpriteBatch b, float menuScale, int drawOffsetX, int drawOffsetY)
    {
        if (b?.GraphicsDevice is not { } device)
            return;

        Rectangle photo = Bounds(GallerySpreadLayout.PortraitBounds);
        Rectangle backdrop = GalleryDrawing.Inset(photo, -4, -4);
        Rectangle previousScissor = device.ScissorRectangle;
        RasterizerState? previousRasterizer = device.RasterizerState;
        Rectangle screenBackdrop = GalleryDrawing.ScaleRectangle(backdrop, menuScale, drawOffsetX, drawOffsetY);
        Rectangle clip = Rectangle.Intersect(screenBackdrop, device.Viewport.Bounds);
        if (previousRasterizer?.ScissorTestEnable == true)
            clip = Rectangle.Intersect(clip, previousScissor);
        if (clip.Width <= 0 || clip.Height <= 0)
            return;

        string season = WeddingDateRecord.NormalizeSeason(Game1.currentSeason) ?? "spring";
        int timeOfDay = Game1.timeOfDay;
        ScenicTimePhase phase = ScenicWindowRules.ResolveTimePhase(timeOfDay);
        ScenicWeatherKind weather = ScenicWeatherParticles.ResolveCurrentWeather();
        ScenicStageLayout layout = ScenicWindowRules.ComputeStageLayout(screenBackdrop.X, screenBackdrop.Y, screenBackdrop.Width, screenBackdrop.Height);
        Matrix restoreTransform = Matrix.CreateScale(menuScale) * Matrix.CreateTranslation(drawOffsetX, drawOffsetY, 0f);

        // Detach from the book's fractional menuScale matrix so the 96x68 pixel backdrop, 16x32 character sprite,
        // and native WeatherDebris / rainTexture render in unscaled physical screen pixels at strict integer zoom
        // (vanilla 4x = Game1.pixelZoom on normal viewports) with zero nearest-neighbor column squashing.
        b.End();
        bool batchOpen = false;
        try
        {
            device.ScissorRectangle = clip;
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, ClipRasterizer, null, Matrix.Identity);
            batchOpen = true;

            if (scenicRenderer is not null)
                scenicRenderer.Draw(b, layout, season, timeOfDay, weather);
            else if (Game1.staminaRect is { } rect)
                b.Draw(rect, new Rectangle(layout.StageX, layout.StageY, layout.StageWidth, layout.StageHeight),
                    ScenicBackdropRenderer.FallbackColor(season, phase));

            Color spriteTint = ResolveSpriteTint(phase, weather);
            if (appearance is CharacterAppearance concreteAppearance)
                concreteAppearance.DrawScenicSprite(b, character.Name, screenBackdrop, layout, spriteTint);
            else
                appearance.Draw(b, character.Name, CharacterVisual.Sprite, screenBackdrop, spriteTint);

            double totalMs = Game1.currentGameTime?.TotalGameTime.TotalMilliseconds ?? 0d;
            weatherParticles.Draw(b, layout, season, phase, weather, totalMs);

            b.End();
            batchOpen = false;
        }
        finally
        {
            if (batchOpen)
            {
                try { b.End(); } catch { }
            }
            device.ScissorRectangle = previousScissor;
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, previousRasterizer, null, restoreTransform);
        }
    }

    private static Color ResolveSpriteTint(ScenicTimePhase phase, ScenicWeatherKind weather)
    {
        Color tint = phase switch
        {
            ScenicTimePhase.Dusk => new Color(255, 240, 224),
            ScenicTimePhase.Night => new Color(216, 226, 250),
            _ => Color.White
        };

        if (weather == ScenicWeatherKind.GreenRain)
            return new Color((byte)(tint.R * 230 / 255), (byte)(tint.G * 252 / 255), (byte)(tint.B * 230 / 255));
        if (weather == ScenicWeatherKind.Storm)
            return new Color((byte)(tint.R * 228 / 255), (byte)(tint.G * 234 / 255), (byte)(tint.B * 246 / 255));
        return tint;
    }

    // Nothing shown here can change while the page is open, so format it once instead of every frame.
    private (string[] Lines, int Points, bool CanBeRomanced, string Relationship)? information;

    internal void DrawInformation(SpriteBatch b)
    {
        information ??= ReadInformation();
        (string[] lines, int points, bool canBeRomanced, string relationship) = information.Value;
        GalleryDrawing.DrawCentered(b, character.DisplayName + GalleryDrawing.TextSeparator + relationship,
            GalleryDrawing.Inset(Bounds(GallerySpreadLayout.LeftRowBounds(0)), 40, 4));
        DrawHearts(b, Bounds(GallerySpreadLayout.LeftRowBounds(1)), points, canBeRomanced);
        for (int i = 0; i < lines.Length; i++)
            GalleryDrawing.DrawCentered(b, lines[i], GalleryDrawing.Inset(Bounds(GallerySpreadLayout.LeftRowBounds(i + 2)), 40, 4));
    }

    private (string[] Lines, int Points, bool CanBeRomanced, string Relationship) ReadInformation()
    {
        Friendship? friendship = Game1.player.friendshipData.GetValueOrDefault(character.Name);
        NPC.TryGetData(character.Name, out CharacterData? data);
        string birthday = data?.BirthSeason is null || data.BirthDay <= 0 ? "-" : i18n.Get("detail.birthday-value", new
        {
            season = Translate("season", data.BirthSeason.Value.ToString()),
            day = data.BirthDay
        });
        string relationship = friendship is null ? i18n.Get("status.none") : i18n.Get($"status.{friendship.Status.ToString().ToLowerInvariant()}");
        StoryNodeProgress progress = catalog is not null
            ? CharacterStoryProgress.Measure(events, catalog, Game1.player.eventsSeen)
            : new StoryNodeProgress(events.Count(entry => Game1.player.eventsSeen.Contains(entry.EventId)), events.Count);
        string[] lines =
        [
            i18n.Get("detail.birthday", new { birthday }),
            i18n.Get("detail.gifts", new { count = friendship?.GiftsThisWeek ?? 0, today = friendship?.GiftsToday > 0 ? i18n.Get("common.yes") : i18n.Get("common.no") }),
            i18n.Get(friendship?.TalkedToToday == true ? "detail.talked" : "detail.not-talked"),
            i18n.Get("detail.seen", new { seen = progress.CollectedNodes, total = progress.TotalNodes, relationship })
        ];
        return (lines, friendship?.Points ?? 0, data?.CanBeRomanced == true, relationship);
    }

    private static void DrawHearts(SpriteBatch b, Rectangle bounds, int points, bool canBeRomanced)
    {
        int capacity = GalleryUiRules.HeartCapacity(canBeRomanced);
        int filled = GalleryUiRules.FilledHearts(points, capacity);
        int size = Math.Min(26, (bounds.Width - 80) / Math.Max(1, capacity));
        int x = bounds.Center.X - capacity * size / 2;
        int y = bounds.Center.Y - size / 2;
        for (int i = 0; i < capacity; i++)
            GalleryUiSkin.DrawBadge(b, i < filled ? UiBadge.HeartFull : UiBadge.HeartEmpty, new Rectangle(x + i * size, y, size, size));
    }

    private string Translate(string group, string value)
    {
        string key = $"{group}.{value.ToLowerInvariant()}";
        string translated = i18n.Get(key);
        return translated == key ? value : translated;
    }

    private static Rectangle Bounds((int X, int Y, int Width, int Height) bounds)
        => new(bounds.X, bounds.Y, bounds.Width, bounds.Height);
}
