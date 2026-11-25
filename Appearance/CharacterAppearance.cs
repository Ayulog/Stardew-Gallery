using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace StardewGallery.Appearance;

internal sealed class CharacterAppearance : ICharacterAppearance
{
    private enum ScenicPhase
    {
        IdleCenter,
        HobbyCenter,
        WalkRight,
        IdleRight,
        WalkLeft,
        IdleLeft,
        WalkReturn
    }

    private sealed class ScenicActorMotion
    {
        internal ScenicPhase Phase = ScenicPhase.IdleCenter;
        internal double PhaseElapsedMs;
        internal float OffsetNativeX;
        internal double LastTotalMs = -1d;
        internal bool HobbyStarted;
    }

    private sealed record Visual(Texture2D Texture, Texture2D Original, string Context, PortraitFrame? Portrait,
        AnimatedSprite? Sprite, DetailedSprite? Detailed, HdPortraitEntry? AnimatedHd = null,
        ScenicParsedAnimation? HobbyAnimation = null, ScenicActorMotion? Motion = null)
    {
        internal long Used { get; set; }
    }

    private readonly IGameContentHelper content;
    private readonly IMonitor monitor;
    private readonly Texture2D placeholder;
    private readonly DialoguePortraits dialogue;
    private readonly HdPortraits hdPortraits;
    private readonly ScaleUpSprites scaleUp;
    private readonly PortraiturePortraits portraiture = new();
    private readonly Dictionary<(string Name, CharacterVisual Kind), Visual> visuals = [];
    private readonly Dictionary<(string Name, CharacterVisual Kind), double> failed = [];
    private readonly HashSet<string> warnings = [];
    private Dictionary<string, string>? animationDescriptions;
    private bool animationDescriptionsAttempted;
    private long clock;
    private Dictionary<string, NPC>? npcs;
    private long npcsReadAt;
    private static double NowMs => Game1.currentGameTime?.TotalGameTime.TotalMilliseconds ?? 0d;

    internal CharacterAppearance(IGameContentHelper content, IModRegistry registry, IMonitor monitor, Texture2D placeholder)
    {
        this.content = content; this.monitor = monitor;
        this.placeholder = placeholder;
        dialogue = new(content, registry);
        hdPortraits = new(content, registry);
        scaleUp = new(registry);
    }

    internal void Invalidate()
    {
        visuals.Clear();
        failed.Clear();
        dialogue.Invalidate();
        hdPortraits.Invalidate();
        npcs = null;
        animationDescriptions = null;
        animationDescriptionsAttempted = false;
    }

    /// <summary>
    /// Game1.getCharacterFromName walks every location (all of them for a name with no NPC). Menus prepare each
    /// visible portrait every frame, so read all villagers in one pass and reuse it for a second.
    /// </summary>
    private NPC? FindNpc(string name)
    {
        long now = Environment.TickCount64;
        if (npcs is null || now - npcsReadAt > 1000)
        {
            // Same choice as getCharacterFromName: the first match in an active location, else the last match.
            Dictionary<string, NPC> found = new(StringComparer.Ordinal);
            HashSet<string> active = new(StringComparer.Ordinal);
            Utility.ForEachCharacter(npc =>
            {
                if (npc.IsVillager && !active.Contains(npc.Name))
                {
                    found[npc.Name] = npc;
                    if (npc.currentLocation?.IsActiveLocation() == true) active.Add(npc.Name);
                }
                return true;
            });
            npcs = found;
            npcsReadAt = now;
        }
        return npcs.GetValueOrDefault(name);
    }

    private IReadOnlyDictionary<string, string>? ReadAnimationDescriptions()
    {
        if (animationDescriptionsAttempted)
            return animationDescriptions;
        animationDescriptionsAttempted = true;
        try
        {
            animationDescriptions = content.Load<Dictionary<string, string>>("Data/AnimationDescriptions");
        }
        catch (Exception error)
        {
            animationDescriptions = null;
            Warn("anim-descriptions", $"Could not read Data/AnimationDescriptions for scenic hobby idles: {error.Message}");
        }
        return animationDescriptions;
    }

    internal bool UsesAsset(IAssetName name)
    {
        string normalized = name.Name.Replace('\\', '/');
        return name.IsEquivalentTo("Data/Characters")
            || name.IsEquivalentTo("Data/AnimationDescriptions")
            || normalized.StartsWith("Portraits/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("Characters/", StringComparison.OrdinalIgnoreCase)
            || name.IsEquivalentTo(DialoguePortraits.AssetName)
            || name.IsEquivalentTo(HdPortraits.DictionaryAssetName)
            || normalized.StartsWith(HdPortraits.AssetPrefix, StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("Arborsm.ScaleUpUnofficial/", StringComparison.OrdinalIgnoreCase)
            || visuals.Values.Any(v => v.Texture.Name is { Length: > 0 } asset && name.IsEquivalentTo(asset)
                || v.Original.Name is { Length: > 0 } original && name.IsEquivalentTo(original));
    }

    public void Prepare(string name, CharacterVisual kind)
    {
        var key = (name, kind);
        if (failed.TryGetValue(key, out double retryAt))
        {
            if (NowMs < retryAt)
            {
                if (kind == CharacterVisual.Sprite) Prepare(name, CharacterVisual.Portrait);
                return;
            }
            // A readable fallback is temporary, not a successful permanent cache entry.
            visuals.Remove(key);
            failed.Remove(key);
        }
        try
        {
            NPC? npc = FindNpc(name);
            Texture2D? original = null;
            try { original = kind == CharacterVisual.Portrait ? npc?.Portrait : npc?.Sprite?.Texture; }
            catch (Exception error) { Warn("source:" + name + kind, $"Current appearance could not be read for {name}: {error.Message}"); }
            if ((original is null || original.IsDisposed) && kind == CharacterVisual.Portrait)
            {
                string asset = "Portraits/" + NPC.getTextureNameForCharacter(name);
                original = content.Load<Texture2D>(asset);
            }
            if (original is null || original.IsDisposed) { Failed(name, kind); return; }
            PortraitureImage? selected = null;
            if (kind == CharacterVisual.Portrait)
            {
                try { selected = portraiture.Read(original); }
                catch (Exception error)
                {
                    // Fall back to the regular portrait sheet before the placeholder; Portraiture is retried later.
                    Warn("portraiture:" + name, $"Portraiture portrait unavailable for {name}; showing the regular portrait: {error.Message}");
                    original = content.Load<Texture2D>("Portraits/" + NPC.getTextureNameForCharacter(name));
                    if (portraiture.IsPortraiture(original)) throw;
                    failed[key] = NowMs + 2000;
                }
            }
            string context = (npc?.LastAppearanceId ?? "") + "|" + npc?.currentLocation?.NameOrUniqueName
                + "|" + original.Name;
            if (kind == CharacterVisual.Sprite) context += "|" + npc?.Sprite?.SpriteWidth + "|" + npc?.Sprite?.SpriteHeight;
            DetailedSprite? detailed = kind == CharacterVisual.Sprite ? scaleUp.Get(original.Name) : null;
            if (visuals.TryGetValue(key, out var cached) && ReferenceEquals(cached.Original, original)
                && cached.Context == context && cached.Detailed == detailed && !cached.Texture.IsDisposed
                && (selected is null || ReferenceEquals(cached.Texture, selected.Texture) && cached.Portrait == selected.Frame))
            {
                if (cached.AnimatedHd is { IsAnimated: true } animHd
                    && animHd.SelectFrame(NowMs, cached.Texture.Width, cached.Texture.Height) is { } nextFrame
                    && nextFrame.TryRegion(cached.Texture.Width, cached.Texture.Height, out _))
                {
                    cached = cached with { Portrait = nextFrame };
                    visuals[key] = cached;
                }
                cached.Used = ++clock;
                // Keep ongoing vanilla AnimatedSprite walk / hobby animation state intact across ticks.
                return;
            }
            Texture2D texture = selected?.Texture ?? original;
            PortraitFrame? portrait = selected?.Frame;
            HdPortraitEntry? animatedHd = null;
            AnimatedSprite? sprite = null;
            ScenicParsedAnimation? hobbyAnimation = null;
            ScenicActorMotion? motion = null;
            if (kind == CharacterVisual.Portrait && selected is null)
            {
                string adapterSource = "DDFC";
                try
                {
                    portrait = dialogue.Get(npc, name, original, includeDefault: false);
                    if (portrait is null)
                    {
                        adapterSource = "HD Portraits";
                        if (hdPortraits.Get(npc, name, original) is { } hdEntry)
                        {
                            portrait = hdEntry.SelectFrame(NowMs);
                            animatedHd = hdEntry.IsAnimated ? hdEntry : null;
                        }
                    }
                    if (portrait is null)
                    {
                        adapterSource = "DDFC";
                        portrait = dialogue.Get(npc, name, original, includeDefault: true);
                    }

                    if (portrait?.TexturePath is { Length: > 0 } path)
                        texture = content.Load<Texture2D>(path);
                    if (animatedHd is not null)
                        portrait = animatedHd.SelectFrame(NowMs, texture.Width, texture.Height);

                    if (portrait is not null && !portrait.Disabled && !portrait.TryRegion(texture.Width, texture.Height, out _))
                    {
                        string requested = $"{portrait.X},{portrait.Y},{portrait.Width}x{portrait.Height}";
                        string actual = $"{texture.Name ?? "unnamed"} ({texture.Width}x{texture.Height})";
                        animatedHd = null;
                        portrait = PortraitFrame.FromGameSheet(original.Width, original.Height);
                        if (portrait is not null)
                        {
                            if (warnings.Add($"native-portrait:{adapterSource}:{name}"))
                                monitor.Log($"Using the current game's 64px portrait for {name}: {adapterSource} frame {requested} does not fit {actual}.", LogLevel.Debug);
                        }
                        else
                        {
                            Warn($"portrait-region:{adapterSource}:{name}", $"Invalid portrait region for {name}: {adapterSource} frame {requested}, texture {actual}; using the gallery placeholder.");
                            portrait = new(null, Width: 0, Height: 0);
                            failed[key] = NowMs + 2000;
                        }
                        texture = original;
                    }
                }
                catch (Exception error)
                {
                    animatedHd = null;
                    Warn($"dialogue:{adapterSource}:{name}", $"Portrait metadata unavailable for {name} ({adapterSource}): {error.Message}");
                    portrait = PortraitFrame.FromGameSheet(original.Width, original.Height) ?? new(null, Width: 0, Height: 0);
                    texture = original;
                    failed[key] = NowMs + 2000;
                }
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
                SetStandingIdle(sprite);

                if (detailed is null
                    && ScenicWindowRules.SelectHobbyAnimationKey(
                        name,
                        ReadAnimationDescriptions(),
                        original.Width,
                        original.Height,
                        sprite.SpriteWidth,
                        sprite.SpriteHeight,
                        out ScenicParsedAnimation parsed) is not null)
                {
                    hobbyAnimation = parsed;
                }
                motion = new ScenicActorMotion();
            }
            if (visuals.Count >= 40 && !visuals.ContainsKey(key))
                visuals.Remove(visuals.MinBy(pair => pair.Value.Used).Key);
            visuals[key] = new(texture, original, context, portrait, sprite, detailed, animatedHd, hobbyAnimation, motion) { Used = ++clock };
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

    /// <summary>
    /// Draws a villager's live sprite inside the unscaled screen-space scenic window at strict integer
    /// <paramref name="layout"/>.<c>PixelZoom</c> (matching in-game <c>Game1.pixelZoom</c> = 4x with zero compression),
    /// driven by Stardew Valley's native <see cref="AnimatedSprite"/> walk/idle methods and safe
    /// <c>Data/AnimationDescriptions</c> hobby animations.
    /// </summary>
    internal void DrawScenicSprite(SpriteBatch batch, string name, Rectangle screenClip, ScenicStageLayout layout, Color tint)
    {
        if (TryDrawScenicNative(batch, name, screenClip, layout, tint))
            return;
        if (TryDraw(batch, name, CharacterVisual.Portrait, screenClip, tint))
            return;
        var fit = AppearanceGeometry.Fit(screenClip.X, screenClip.Y, screenClip.Width, screenClip.Height, placeholder.Width, placeholder.Height);
        batch.Draw(placeholder, new Rectangle((int)fit.X, (int)fit.Y, (int)fit.Width, (int)fit.Height), tint);
    }

    private bool TryDrawScenicNative(SpriteBatch batch, string name, Rectangle screenClip, ScenicStageLayout layout, Color tint)
    {
        if (!visuals.TryGetValue((name, CharacterVisual.Sprite), out var visual)
            || visual.Texture.IsDisposed
            || visual.Sprite is not { } sprite)
            return false;

        try
        {
            ScenicActorMotion motion = visual.Motion ?? new ScenicActorMotion();
            bool standardSheet = sprite.SpriteWidth > 0 && sprite.SpriteHeight > 0
                && visual.Texture.Width == sprite.SpriteWidth * 4
                && visual.Texture.Height >= sprite.SpriteHeight * 4;

            if (visual.Detailed is not null)
            {
                // Scale Up Unofficial HD sprites: preserve its Draw-hook placement semantics inside screenClip.
                (int frame, float strollOffsetX) = AppearanceGeometry.ScenicStroll(
                    name,
                    NowMs,
                    visual.Texture.Width,
                    visual.Texture.Height,
                    sprite.SpriteWidth,
                    sprite.SpriteHeight,
                    maxOffset: Math.Min(24f, Math.Max(0f, screenClip.Width * 0.08f)));
                ApplySpriteFrame(sprite, visual.Texture, frame, ref strollOffsetX);
                var p = AppearanceGeometry.SpritePlacement(screenClip.X, screenClip.Y, screenClip.Width, screenClip.Height,
                    sprite.SourceRect.Width, sprite.SourceRect.Height, visual.Detailed);
                float minX = screenClip.X + 8f;
                float maxX = Math.Max(minX, screenClip.Right - p.Width - 8f);
                float drawX = p.Width + 16f <= screenClip.Width ? Math.Clamp(p.X + strollOffsetX, minX, maxX) : p.X;
                float appliedOffset = drawX - p.X;
                Vector2 position = new(MathF.Round(drawX), MathF.Round(p.Y));
                if (Game1.shadowTexture is not null)
                {
                    batch.Draw(Game1.shadowTexture,
                        new Vector2(MathF.Round(screenClip.Center.X + appliedOffset), MathF.Round(screenClip.Y + screenClip.Height * .76f)),
                        null, tint * .35f, 0f, new Vector2(Game1.shadowTexture.Width / 2f, Game1.shadowTexture.Height / 2f),
                        p.Scale, SpriteEffects.None, .45f);
                }
                sprite.draw(batch, position, .9f, 0, 0, tint, false, p.Scale);
                return true;
            }

            int zoom = layout.PixelZoom;
            int spritePixelW = sprite.SpriteWidth * zoom;
            int spritePixelH = sprite.SpriteHeight * zoom;
            int maxNativeOffset = standardSheet
                ? Math.Clamp((screenClip.Width - spritePixelW) / (2 * Math.Max(1, zoom)) - 6, 0, 16)
                : 0;

            UpdateNativeMotion(sprite, visual.Texture, visual.HobbyAnimation, motion, standardSheet, maxNativeOffset);
            EnsureSourceRectInBounds(sprite, visual.Texture);

            int drawW = sprite.SourceRect.Width * zoom;
            int drawH = sprite.SourceRect.Height * zoom;
            int centerX = screenClip.X + screenClip.Width / 2;
            int drawXInt = centerX - drawW / 2 + (int)Math.Round(motion.OffsetNativeX * zoom);
            int drawYInt = layout.ActorFeetY - drawH;
            Vector2 drawPos = new(drawXInt, drawYInt);

            sprite.drawShadow(batch, drawPos, zoom, .45f);
            sprite.draw(batch, drawPos, .9f, 0, 0, tint, false, zoom);
            return true;
        }
        catch (Exception error)
        {
            visuals.Remove((name, CharacterVisual.Sprite));
            failed[(name, CharacterVisual.Sprite)] = NowMs + 2000;
            Warn("draw:" + name + CharacterVisual.Sprite, $"Appearance drawing failed for {name}: {error.Message}");
            return false;
        }
    }

    private static void UpdateNativeMotion(
        AnimatedSprite sprite,
        Texture2D texture,
        ScenicParsedAnimation? hobbyAnimation,
        ScenicActorMotion motion,
        bool standardSheet,
        int maxNativeOffset)
    {
        if (!standardSheet)
        {
            motion.OffsetNativeX = 0f;
            SetStandingIdle(sprite);
            return;
        }

        double nowMs = NowMs;
        double dtMs = 16.6667;
        if (motion.LastTotalMs >= 0d && nowMs >= motion.LastTotalMs)
            dtMs = Math.Clamp(nowMs - motion.LastTotalMs, 0d, 50d);
        motion.LastTotalMs = nowMs;

        if (dtMs <= 0d)
            return;

        GameTime stepTime = new(TimeSpan.FromMilliseconds(nowMs), TimeSpan.FromMilliseconds(dtMs));
        motion.PhaseElapsedMs += dtMs;

        const float walkNativePxPerSec = 20f;
        float stepNative = (float)(walkNativePxPerSec * dtMs / 1000.0);

        switch (motion.Phase)
        {
            case ScenicPhase.IdleCenter:
                motion.OffsetNativeX = 0f;
                FaceFrontStanding(sprite);
                if (motion.PhaseElapsedMs >= 1600.0)
                {
                    motion.PhaseElapsedMs = 0d;
                    motion.HobbyStarted = false;
                    motion.Phase = hobbyAnimation is not null
                        ? ScenicPhase.HobbyCenter
                        : maxNativeOffset >= 6 ? ScenicPhase.WalkRight : ScenicPhase.IdleCenter;
                }
                break;

            case ScenicPhase.HobbyCenter:
                motion.OffsetNativeX = 0f;
                if (hobbyAnimation is not { } hobby)
                {
                    motion.Phase = maxNativeOffset >= 6 ? ScenicPhase.WalkRight : ScenicPhase.IdleCenter;
                    motion.PhaseElapsedMs = 0d;
                    break;
                }
                if (!motion.HobbyStarted)
                {
                    motion.HobbyStarted = true;
                    sprite.setCurrentAnimation(BuildHobbyFrames(hobby));
                    sprite.loop = false;
                }
                if (sprite.CurrentAnimation is not null)
                {
                    if (sprite.animateOnce(stepTime))
                    {
                        FaceFrontStanding(sprite);
                        motion.PhaseElapsedMs = 0d;
                        motion.Phase = maxNativeOffset >= 6 ? ScenicPhase.WalkRight : ScenicPhase.IdleCenter;
                    }
                }
                else if (motion.PhaseElapsedMs >= 3200.0)
                {
                    FaceFrontStanding(sprite);
                    motion.PhaseElapsedMs = 0d;
                    motion.Phase = maxNativeOffset >= 6 ? ScenicPhase.WalkRight : ScenicPhase.IdleCenter;
                }
                break;

            case ScenicPhase.WalkRight:
                sprite.CurrentAnimation = null;
                sprite.AnimateRight(stepTime, 18, "");
                motion.OffsetNativeX += stepNative;
                if (motion.OffsetNativeX >= maxNativeOffset)
                {
                    motion.OffsetNativeX = maxNativeOffset;
                    FaceFrontStanding(sprite);
                    motion.PhaseElapsedMs = 0d;
                    motion.Phase = ScenicPhase.IdleRight;
                }
                break;

            case ScenicPhase.IdleRight:
                FaceFrontStanding(sprite);
                if (motion.PhaseElapsedMs >= 2200.0)
                {
                    motion.PhaseElapsedMs = 0d;
                    motion.Phase = ScenicPhase.WalkLeft;
                }
                break;

            case ScenicPhase.WalkLeft:
                sprite.CurrentAnimation = null;
                sprite.AnimateLeft(stepTime, 18, "");
                motion.OffsetNativeX -= stepNative;
                if (motion.OffsetNativeX <= -maxNativeOffset)
                {
                    motion.OffsetNativeX = -maxNativeOffset;
                    FaceFrontStanding(sprite);
                    motion.PhaseElapsedMs = 0d;
                    motion.Phase = ScenicPhase.IdleLeft;
                }
                break;

            case ScenicPhase.IdleLeft:
                FaceFrontStanding(sprite);
                if (motion.PhaseElapsedMs >= 2200.0)
                {
                    motion.PhaseElapsedMs = 0d;
                    motion.Phase = ScenicPhase.WalkReturn;
                }
                break;

            case ScenicPhase.WalkReturn:
                sprite.CurrentAnimation = null;
                sprite.AnimateRight(stepTime, 18, "");
                motion.OffsetNativeX += stepNative;
                if (motion.OffsetNativeX >= 0f)
                {
                    motion.OffsetNativeX = 0f;
                    FaceFrontStanding(sprite);
                    motion.PhaseElapsedMs = 0d;
                    motion.Phase = ScenicPhase.IdleCenter;
                }
                break;
        }
    }

    private static List<FarmerSprite.AnimationFrame> BuildHobbyFrames(ScenicParsedAnimation hobby)
    {
        List<FarmerSprite.AnimationFrame> frames = [];
        foreach (int f in hobby.StartFrames)
            frames.Add(new FarmerSprite.AnimationFrame(f, 220));

        // Repeat loop frames so the signature hobby plays naturally for ~2.8-3.4 seconds.
        int loopStepMs = hobby.LoopFrames.Length <= 2 ? 320 : 220;
        int singleLoopMs = Math.Max(loopStepMs, hobby.LoopFrames.Length * loopStepMs);
        int repeats = Math.Clamp((int)Math.Round(2800.0 / singleLoopMs), 1, 8);
        for (int r = 0; r < repeats; r++)
            foreach (int f in hobby.LoopFrames)
                frames.Add(new FarmerSprite.AnimationFrame(f, loopStepMs));

        foreach (int f in hobby.EndFrames)
            frames.Add(new FarmerSprite.AnimationFrame(f, 200));

        return frames;
    }

    private static void FaceFrontStanding(AnimatedSprite sprite)
    {
        sprite.CurrentAnimation = null;
        sprite.faceDirection(2);
        sprite.StopAnimation();
        if (sprite.currentFrame != 0)
        {
            sprite.currentFrame = 0;
            sprite.UpdateSourceRect();
        }
        else if (sprite.SourceRect == Rectangle.Empty)
        {
            sprite.UpdateSourceRect();
        }
    }

    private static void EnsureSourceRectInBounds(AnimatedSprite sprite, Texture2D texture)
    {
        Rectangle src = sprite.SourceRect;
        if (src.Width <= 0 || src.Height <= 0 || src.X < 0 || src.Y < 0
            || src.Right > texture.Width || src.Bottom > texture.Height)
        {
            SetStandingIdle(sprite);
        }
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
                (int frame, float strollOffsetX) = AppearanceGeometry.ScenicStroll(
                    name,
                    NowMs,
                    visual.Texture.Width,
                    visual.Texture.Height,
                    sprite.SpriteWidth,
                    sprite.SpriteHeight);
                ApplySpriteFrame(sprite, visual.Texture, frame, ref strollOffsetX);
                var p = AppearanceGeometry.SpritePlacement(bounds.X, bounds.Y, bounds.Width, bounds.Height, sprite.SourceRect.Width, sprite.SourceRect.Height, visual.Detailed);
                float minX = bounds.X + 8f;
                float maxX = Math.Max(minX, bounds.Right - p.Width - 8f);
                float drawX = p.Width + 16f <= bounds.Width ? Math.Clamp(p.X + strollOffsetX, minX, maxX) : p.X;
                float appliedOffset = drawX - p.X;
                Vector2 position = new(MathF.Round(drawX), MathF.Round(p.Y));
                if (visual.Detailed is null) sprite.drawShadow(batch, position, p.Scale, .45f);
                else batch.Draw(Game1.shadowTexture, new Vector2(MathF.Round(bounds.Center.X + appliedOffset), MathF.Round(bounds.Y + bounds.Height * .76f)),
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
            failed[(name, kind)] = NowMs + 2000;
            Warn("draw:" + name + kind, $"Appearance drawing failed for {name}: {error.Message}");
            return false;
        }
    }

    private void Failed(string name, CharacterVisual kind)
    {
        visuals.Remove((name, kind));
        failed[(name, kind)] = NowMs + 2000;
        if (kind == CharacterVisual.Sprite) Prepare(name, CharacterVisual.Portrait);
    }

    private static void ApplySpriteFrame(AnimatedSprite sprite, Texture2D texture, int frame, ref float offsetX)
    {
        sprite.CurrentAnimation = null;
        if (sprite.currentFrame != frame || sprite.SourceRect == Rectangle.Empty)
        {
            sprite.currentFrame = frame;
            sprite.UpdateSourceRect();
        }
        Rectangle src = sprite.SourceRect;
        if (src.Width <= 0 || src.Height <= 0 || src.X < 0 || src.Y < 0
            || src.Right > texture.Width || src.Bottom > texture.Height)
        {
            offsetX = 0f;
            SetStandingIdle(sprite);
        }
    }

    private static void SetStandingIdle(AnimatedSprite? sprite)
    {
        if (sprite is null)
            return;
        sprite.CurrentAnimation = null;
        if (sprite.currentFrame != 0)
        {
            sprite.currentFrame = 0;
            sprite.UpdateSourceRect();
        }
        else if (sprite.SourceRect == Rectangle.Empty)
        {
            sprite.UpdateSourceRect();
        }
    }

    private void Warn(string key, string message)
    {
        if (warnings.Add(key)) monitor.Log(message, LogLevel.Warn);
    }
}
