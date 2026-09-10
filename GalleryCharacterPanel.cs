using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.GameData.Characters;
using StardewGallery.Appearance;

namespace StardewGallery;

internal sealed class GalleryCharacterPanel(
    GalleryCharacter character,
    IReadOnlyList<GalleryEvent> events,
    ITranslationHelper i18n,
    Texture2D scene,
    ICharacterAppearance appearance)
{
    internal void Prepare() => appearance.Prepare(character.Name, CharacterVisual.Sprite);

    internal void DrawPhoto(SpriteBatch b)
    {
        Rectangle photo = Bounds(GallerySpreadLayout.PortraitBounds);
        b.Draw(scene, photo, Color.White);
        appearance.Draw(b, character.Name, CharacterVisual.Sprite, photo, Color.White);
    }

    internal void DrawInformation(SpriteBatch b)
    {
        Friendship? friendship = Game1.player.friendshipData.GetValueOrDefault(character.Name);
        NPC.TryGetData(character.Name, out CharacterData? data);
        string birthday = data?.BirthSeason is null || data.BirthDay <= 0 ? "-" : i18n.Get("detail.birthday-value", new
        {
            season = Translate("season", data.BirthSeason.Value.ToString()),
            day = data.BirthDay
        });
        string relationship = friendship is null ? i18n.Get("status.none") : i18n.Get($"status.{friendship.Status.ToString().ToLowerInvariant()}");
        GalleryDrawing.DrawCentered(b, character.DisplayName, GalleryDrawing.Inset(Bounds(GallerySpreadLayout.LeftRowBounds(0)), 40));
        DrawHearts(b, Bounds(GallerySpreadLayout.LeftRowBounds(1)), friendship?.Points ?? 0, data?.CanBeRomanced == true);
        string[] lines =
        [
            i18n.Get("detail.birthday", new { birthday }),
            i18n.Get("detail.gifts", new { count = friendship?.GiftsThisWeek ?? 0, today = friendship?.GiftsToday > 0 ? i18n.Get("common.yes") : i18n.Get("common.no") }),
            i18n.Get(friendship?.TalkedToToday == true ? "detail.talked" : "detail.not-talked"),
            i18n.Get("detail.seen", new { seen = events.Count(entry => Game1.player.eventsSeen.Contains(entry.EventId)), total = events.Count, relationship })
        ];
        for (int i = 0; i < lines.Length; i++)
            GalleryDrawing.DrawCentered(b, lines[i], GalleryDrawing.Inset(Bounds(GallerySpreadLayout.LeftRowBounds(i + 2)), 40));
    }

    private static void DrawHearts(SpriteBatch b, Rectangle bounds, int points, bool canBeRomanced)
    {
        int capacity = GalleryUiRules.HeartCapacity(canBeRomanced);
        int filled = GalleryUiRules.FilledHearts(points, capacity);
        const int size = 28;
        int x = bounds.Center.X - capacity * size / 2;
        int y = bounds.Center.Y - 12;
        for (int i = 0; i < capacity; i++)
            b.Draw(Game1.mouseCursors, new Vector2(x + i * size, y), new Rectangle(i < filled ? 211 : 218, 428, 7, 6), Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, .88f);
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
