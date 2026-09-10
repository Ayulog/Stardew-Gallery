using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace StardewGallery.Appearance;

internal sealed class CharacterAppearance : ICharacterAppearance
{
    private sealed record Visual(Texture2D Texture, Texture2D Original, string Context, PortraitFrame? Portrait,
        AnimatedSprite? Sprite, DetailedSprite? Detailed)
    {
        internal long Used { get; set; }
    }
    private readonly IGameContentHelper content;
    private readonly IMonitor monitor;
    private readonly Texture2D placeholder;
    private readonly DialoguePortraits dialogue;
    private readonly ScaleUpSprites scaleUp;
    private readonly PortraiturePortraits portraiture = new();
    private readonly Dictionary<(string Name, CharacterVisual Kind), Visual> visuals = [];
    private readonly Dictionary<(string Name, CharacterVisual Kind), double> failed = [];
    private readonly HashSet<string> warnings = [];
    private long clock;

    internal CharacterAppearance(IGameContentHelper content, IModRegistry registry, IMonitor monitor, Texture2D placeholder)
    {
        this.content = content; this.monitor = monitor;
        this.placeholder = placeholder;
        dialogue = new(content, registry); scaleUp = new(registry);
    }

    internal void Invalidate()
    {
        visuals.Clear(); failed.Clear(); dialogue.Invalidate();
    }

    internal bool UsesAsset(IAssetName name) => name.IsEquivalentTo("Data/Characters")
        || name.Name.StartsWith("Portraits/", StringComparison.OrdinalIgnoreCase)
        || name.Name.StartsWith("Characters/", StringComparison.OrdinalIgnoreCase)
        || name.IsEquivalentTo(DialoguePortraits.AssetName)
        || name.Name.StartsWith("Arborsm.ScaleUpUnofficial/", StringComparison.OrdinalIgnoreCase)
        || visuals.Values.Any(v => v.Texture.Name is { Length: > 0 } asset && name.IsEquivalentTo(asset)
            || v.Original.Name is { Length: > 0 } original && name.IsEquivalentTo(original));

    public void Prepare(string name, CharacterVisual kind)
    {
        var key = (name, kind);
        if (failed.TryGetValue(key, out double retryAt) && Game1.currentGameTime.TotalGameTime.TotalMilliseconds < retryAt)
        {
            if (kind == CharacterVisual.Sprite) Prepare(name, CharacterVisual.Portrait);
            return;
        }
        failed.Remove(key);
        try
        {
            NPC? npc = Game1.getCharacterFromName(name);
            Texture2D? original = null;
            try { original = kind == CharacterVisual.Portrait ? npc?.Portrait : npc?.Sprite?.Texture; }
            catch (Exception error) { Warn("source:" + name + kind, $"Current appearance could not be read for {name}: {error.Message}"); }
            if ((original is null || original.IsDisposed) && kind == CharacterVisual.Portrait)
            {
                string asset = "Portraits/" + NPC.getTextureNameForCharacter(name);
                original = content.Load<Texture2D>(asset);
            }
            if (original is null || original.IsDisposed) { Failed(name, kind); return; }
            string context = (npc?.LastAppearanceId ?? "") + "|" + npc?.currentLocation?.NameOrUniqueName
                + "|" + original.Name;
            if (kind == CharacterVisual.Sprite) context += "|" + npc?.Sprite?.SpriteWidth + "|" + npc?.Sprite?.SpriteHeight;
            DetailedSprite? detailed = kind == CharacterVisual.Sprite ? scaleUp.Get(original.Name) : null;
            PortraitureImage? selected = kind == CharacterVisual.Portrait ? portraiture.Read(original) : null;
            if (visuals.TryGetValue(key, out var cached) && ReferenceEquals(cached.Original, original)
                && cached.Context == context && cached.Detailed == detailed && !cached.Texture.IsDisposed
                && (selected is null || ReferenceEquals(cached.Texture, selected.Texture) && cached.Portrait == selected.Frame))
            {
                cached.Used = ++clock;
                Animate(cached.Sprite);
                return;
            }
            Texture2D texture = selected?.Texture ?? original;
            PortraitFrame? portrait = selected?.Frame;
            AnimatedSprite? sprite = null;
            if (kind == CharacterVisual.Portrait && selected is null)
            {
                try
                {
                    portrait = dialogue.Get(npc, name, original);
                    if (portrait?.TexturePath is { Length: > 0 } path) texture = content.Load<Texture2D>(path);
                    if (portrait is not null && !portrait.Disabled && !portrait.TryRegion(texture.Width, texture.Height, out _))
                    {
                        Warn("portrait-region:" + name, $"Invalid portrait region for {name}; using the game portrait.");
                        portrait = null; texture = original;
                    }
                }
                catch (Exception error) { Warn("dialogue:" + name, $"Portrait metadata unavailable for {name}: {error.Message}"); portrait = null; texture = original; }
            }
            else if (kind == CharacterVisual.Sprite && npc?.Sprite is { } live)
            {
                sprite = live.Clone();
                // Clone omits this field; retain temporary outfits without sharing animation callbacks.
                sprite.overrideTextureName = live.overrideTextureName;
                sprite.loadedTexture = live.loadedTexture;
                sprite.spriteTexture = original;
                sprite.CurrentAnimation = null;
                sprite.ignoreSourceRectUpdates = false;
                sprite.currentFrame = 0;
                sprite.UpdateSourceRect();
                Animate(sprite);
            }
            if (visuals.Count >= 40 && !visuals.ContainsKey(key))
                visuals.Remove(visuals.MinBy(pair => pair.Value.Used).Key);
            visuals[key] = new(texture, original, context, portrait, sprite, detailed) { Used = ++clock };
        }
        catch (Exception error)
        {
            Failed(name, kind);
            Warn("prepare:" + name + kind, $"Appearance unavailable for {name}: {error.Message}");
        }
    }

    public void Draw(SpriteBatch batch, string name, CharacterVisual kind, Rectangle bounds, Color tint)
    {
        if (TryDraw(batch, name, kind, bounds, tint)) return;
        if (kind == CharacterVisual.Sprite && TryDraw(batch, name, CharacterVisual.Portrait, bounds, tint)) return;
        var fit = AppearanceGeometry.Fit(bounds.X, bounds.Y, bounds.Width, bounds.Height, placeholder.Width, placeholder.Height);
        batch.Draw(placeholder, new Rectangle((int)fit.X, (int)fit.Y, (int)fit.Width, (int)fit.Height), tint);
    }

    private bool TryDraw(SpriteBatch batch, string name, CharacterVisual kind, Rectangle bounds, Color tint)
    {
        if (!visuals.TryGetValue((name, kind), out var visual) || visual.Texture.IsDisposed) return false;
        try
        {
            if (kind == CharacterVisual.Portrait)
            {
                if (visual.Portrait?.Disabled == true) return false;
                var frame = visual.Portrait ?? new PortraitFrame(null, Width: Math.Min(64, visual.Texture.Width), Height: Math.Min(64, visual.Texture.Height));
                if (frame.Alpha <= 0 || !frame.TryRegion(visual.Texture.Width, visual.Texture.Height, out var r)) return false;
                var fit = AppearanceGeometry.Fit(bounds.X, bounds.Y, bounds.Width, bounds.Height, r.Width, r.Height);
                batch.Draw(visual.Texture, new Rectangle((int)fit.X, (int)fit.Y, (int)fit.Width, (int)fit.Height),
                    new Rectangle(r.X, r.Y, r.Width, r.Height), tint * Math.Clamp(frame.Alpha, 0, 1));
            }
            else if (visual.Sprite is { } sprite)
            {
                var p = AppearanceGeometry.SpritePlacement(bounds.X, bounds.Y, bounds.Width, bounds.Height, sprite.SourceRect.Width, sprite.SourceRect.Height, visual.Detailed);
                Vector2 position = new(p.X, p.Y);
                if (visual.Detailed is null) sprite.drawShadow(batch, position, p.Scale, .45f);
                else batch.Draw(Game1.shadowTexture, new Vector2(bounds.Center.X, bounds.Y + MathF.Round(bounds.Height * .76f)),
                    null, tint * .35f, 0, new Vector2(Game1.shadowTexture.Width / 2f, Game1.shadowTexture.Height / 2f),
                    p.Scale, SpriteEffects.None, .45f);
                sprite.draw(batch, position, .9f, 0, 0, tint, false, p.Scale);
            }
            else return false;
            return true;
        }
        catch (Exception error)
        {
            visuals.Remove((name, kind));
            failed[(name, kind)] = Game1.currentGameTime.TotalGameTime.TotalMilliseconds + 2000;
            Warn("draw:" + name + kind, $"Appearance drawing failed for {name}: {error.Message}");
            return false;
        }
    }

    private void Failed(string name, CharacterVisual kind)
    {
        visuals.Remove((name, kind));
        failed[(name, kind)] = Game1.currentGameTime.TotalGameTime.TotalMilliseconds + 2000;
        if (kind == CharacterVisual.Sprite) Prepare(name, CharacterVisual.Portrait);
    }

    private static void Animate(AnimatedSprite? sprite)
    {
        if (sprite is null) return;
        switch ((int)(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 1800) % 4)
        {
            case 0: sprite.AnimateDown(Game1.currentGameTime); break;
            case 1: sprite.AnimateLeft(Game1.currentGameTime); break;
            case 2: sprite.AnimateUp(Game1.currentGameTime); break;
            default: sprite.AnimateRight(Game1.currentGameTime); break;
        }
    }

    private void Warn(string key, string message)
    {
        if (warnings.Add(key)) monitor.Log(message, LogLevel.Warn);
    }
}
