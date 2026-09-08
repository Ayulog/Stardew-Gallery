using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.GameData.Characters;

namespace StardewGallery;

internal sealed class GalleryCharacterPanel(
    GalleryCharacter character,
    IReadOnlyList<GalleryEvent> events,
    ITranslationHelper i18n,
    Texture2D scene)
{
    private AnimatedSprite? previewSprite;

    internal void DrawPhoto(SpriteBatch b)
    {
        Rectangle photo = Bounds(GallerySpreadLayout.PortraitBounds);
        b.Draw(scene, photo, Color.White);
        previewSprite ??= Game1.getCharacterFromName(character.Name)?.Sprite?.Clone();
        if (previewSprite?.Texture is null)
        {
            string asset = "Portraits/" + NPC.getTextureNameForCharacter(character.Name);
            if (Game1.content.DoesAssetExist<Texture2D>(asset))
                b.Draw(Game1.content.Load<Texture2D>(asset), new Rectangle(photo.Center.X - 64, photo.Center.Y - 64, 128, 128), new Rectangle(0, 0, 64, 64), Color.White);
            return;
        }

        switch ((int)(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 1800) % 4)
        {
            case 0: previewSprite.AnimateDown(Game1.currentGameTime); break;
            case 1: previewSprite.AnimateLeft(Game1.currentGameTime); break;
            case 2: previewSprite.AnimateUp(Game1.currentGameTime); break;
            default: previewSprite.AnimateRight(Game1.currentGameTime); break;
        }
        float scale = Math.Min(4f, Math.Min(photo.Width * .65f / previewSprite.SpriteWidth, photo.Height * .72f / previewSprite.SpriteHeight));
        Vector2 size = new(previewSprite.SpriteWidth * scale, previewSprite.SpriteHeight * scale);
        Vector2 position = new(photo.Center.X - size.X / 2, photo.Y + (int)Math.Round(photo.Height * .76f) - size.Y);
        previewSprite.drawShadow(b, position, scale, .45f);
        previewSprite.draw(b, position, .9f, 0, 0, Color.White, false, scale);
    }

    internal void DrawInformation(SpriteBatch b)
    {
        Friendship? friendship = Game1.player.friendshipData.GetValueOrDefault(character.Name);
        NPC.TryGetData(character.Name, out CharacterData? data);
        string birthday = data?.BirthSeason is null ? "-" : i18n.Get("detail.birthday-value", new
        {
            season = Translate("season", data.BirthSeason.Value.ToString()),
            day = data.BirthDay
        });
        string relationship = friendship is null ? i18n.Get("status.none") : i18n.Get($"status.{friendship.Status.ToString().ToLowerInvariant()}");
        DrawRow(b, character.DisplayName, 0);
        if (!character.IsSocial)
        {
            string[] profile = [i18n.Get("directory.non-social"), i18n.Get("detail.birthday", new { birthday }),
                i18n.Get("directory.count", new { count = events.Count }),
                i18n.Get("directory.watched-count", new { count = events.Count(entry => Game1.player.eventsSeen.Contains(entry.EventId)) }), ""];
            for (int i = 0; i < profile.Length; i++) DrawRow(b, profile[i], i + 1);
            return;
        }
        DrawHearts(b, Bounds(GallerySpreadLayout.LeftRowBounds(1)), friendship?.Points ?? 0, data?.CanBeRomanced == true);
        string[] lines =
        [
            i18n.Get("detail.birthday", new { birthday }),
            i18n.Get("detail.gifts", new { count = friendship?.GiftsThisWeek ?? 0, today = friendship?.GiftsToday > 0 ? i18n.Get("common.yes") : i18n.Get("common.no") }),
            i18n.Get(friendship?.TalkedToToday == true ? "detail.talked" : "detail.not-talked"),
            i18n.Get("detail.seen", new { seen = events.Count(entry => Game1.player.eventsSeen.Contains(entry.EventId)), total = events.Count, relationship })
        ];
        for (int i = 0; i < lines.Length; i++)
            DrawRow(b, lines[i], i + 2);
    }

    private static void DrawRow(SpriteBatch b, string text, int row)
    {
        Rectangle bounds = Bounds(GallerySpreadLayout.LeftRowBounds(row));
        bounds.Inflate(-32, 0);
        GalleryMenu.DrawCentered(b, text, bounds);
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
