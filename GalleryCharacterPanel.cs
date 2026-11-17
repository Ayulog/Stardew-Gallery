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
    Texture2D scene,
    ICharacterAppearance appearance,
    GalleryCatalog? catalog = null)
{
    internal void Prepare() => appearance.Prepare(character.Name, CharacterVisual.Sprite);

    internal void DrawPhoto(SpriteBatch b)
    {
        Rectangle photo = Bounds(GallerySpreadLayout.PortraitBounds);
        Rectangle backdrop = GalleryDrawing.Inset(photo, -4, -4);
        // User-confirmed rule: the seasonal day/dusk or night art is drawn in its original colours (no tint overlays).
        GalleryUiSkin.DrawScene(b, backdrop, scene);
        appearance.Draw(b, character.Name, CharacterVisual.Sprite, photo, Color.White);
        DrawWeatherParticles(b, backdrop, ResolveCurrentPalette());
    }

    private static ScenicWindowPalette ResolveCurrentPalette()
    {
        string? weatherId = Game1.currentLocation?.GetWeather()?.Weather;
        bool greenRain = false;
        try { greenRain = Utility.isGreenRainDay(); } catch { }
        return ScenicWindowStyle.Resolve(
            Game1.currentSeason,
            Game1.timeOfDay,
            weatherId,
            Game1.isRaining,
            Game1.isSnowing,
            greenRain);
    }

    /// <summary>Restrained pixel rain / green-rain motes / snowflakes over the scene; clear weather draws nothing.</summary>
    private static void DrawWeatherParticles(SpriteBatch b, Rectangle photo, ScenicWindowPalette palette)
    {
        double ms = Game1.currentGameTime?.TotalGameTime.TotalMilliseconds ?? 0d;
        if (palette.Weather is ScenicWeatherKind.Rain or ScenicWeatherKind.Storm or ScenicWeatherKind.GreenRain)
        {
            Color dropColor = palette.Weather == ScenicWeatherKind.GreenRain
                ? new Color(154, 240, 148) * 0.55f
                : new Color(200, 222, 255) * 0.5f;
            int count = palette.Weather == ScenicWeatherKind.Storm ? 16 : 10;
            int phase = (int)(ms * 0.22);
            for (int i = 0; i < count; i++)
            {
                int rx = photo.X + 16 + ((i * 53 + phase / 2) % Math.Max(24, photo.Width - 32));
                int ry = photo.Y + 12 + ((i * 41 + phase) % Math.Max(24, photo.Height - 30));
                b.Draw(Game1.staminaRect, new Rectangle(rx, ry, 2, 8), dropColor);
            }
            return;
        }
        if (palette.Weather == ScenicWeatherKind.Snow)
        {
            int phase = (int)(ms * 0.05);
            for (int i = 0; i < 12; i++)
            {
                int sx = photo.X + 16 + ((i * 47 + phase / 2 + (i % 3) * 7) % Math.Max(24, photo.Width - 32));
                int sy = photo.Y + 12 + ((i * 37 + phase) % Math.Max(24, photo.Height - 24));
                b.Draw(Game1.staminaRect, new Rectangle(sx, sy, 3, 3), Color.White * 0.7f);
            }
        }
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
