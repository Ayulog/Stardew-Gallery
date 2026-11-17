using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewGallery.Appearance;

namespace StardewGallery;

internal sealed class ModEntry : Mod
{
    private const int CollectionsGalleryComponentId = 7099;
    private const string SaveDataKey = "gallery-state";
    private GalleryCatalogCache catalog = null!;
    // Split-screen players each have their own gallery pages, photos and unlock choice.
    private readonly PerScreen<bool> unlockAll = new();
    private readonly PerScreen<bool> pendingOpen = new();
    private readonly PerScreen<GalleryTypingKeys> typingKeys = new(() => new GalleryTypingKeys());
    private readonly PerScreen<TheaterHudState> theaterHud = new(() => new TheaterHudState());
    private readonly PerScreen<SocialClueCache> socialClueCache = new(() => new SocialClueCache());
    private PerScreen<AutoMementoTracker> autoMemento = null!;
    private PerScreen<GalleryApplication> application = null!;
    private PerScreen<GalleryPhotos> photos = null!;
    private Event? ActiveCapturableEvent => replay.IsActive ? replay.PlayingEvent : Game1.CurrentEvent;
    private Texture2D tabIcon = null!;
    private ReplayCoordinator replay = null!;
    private ReplayService replayService = null!;
    private bool replayProtectionReady;
    private CharacterAppearance appearance = null!;
    private EventDefinitionSources eventDefinitions = null!;

    internal ModConfig Config { get; private set; } = new();

    public override void Entry(IModHelper helper)
    {
        try
        {
            Config = helper.ReadConfig<ModConfig>();
        }
        catch (Exception error)
        {
            Monitor.Log(helper.Translation.Get("log.config-invalid", new { error = error.Message }), LogLevel.Error);
        }

        eventDefinitions = new EventDefinitionSources(helper, Monitor);
        helper.ConsoleCommands.Add("gallery_event_source", "Inspect event definition sources: gallery_event_source <location> <event-id> | status | rescan",
            new EventSourceDiagnostics(eventDefinitions, Monitor, () => catalog.Invalidate()).Run);
        helper.Events.Content.AssetsInvalidated += (_, e) =>
        {
            // CP config/reload can change referenced files, targets and event or trigger-action IDs during a save.
            if (e.NamesWithoutLocale.Any(name => name.StartsWith("Data/Events") || name.IsEquivalentTo("Data/TriggerActions")))
                eventDefinitions.Reset();
        };
        helper.Events.GameLoop.ReturnedToTitle += (_, _) => eventDefinitions.Reset();

        catalog = new GalleryCatalogCache(Monitor, () => Config.DebugDiagnostics,
            identity => eventDefinitions.Read(identity.AssetName, identity.EventId));
        photos = new(() => new GalleryPhotos(helper, Monitor, (key, error) =>
            Game1.addHUDMessage(new HUDMessage(helper.Translation.Get(key), error ? HUDMessage.error_type : HUDMessage.newQuest_type))));
        SteamTextInput.Log = Monitor.Log;
        try
        {
            PhotoCapturePatches.Apply(helper, () => photos.Value.Pending);
        }
        catch (Exception error)
        {
            Monitor.Log($"Replay photos at 100% zoom are unavailable: {error}", LogLevel.Warn);
        }
        replay = new ReplayCoordinator(Monitor, helper, () => Config.AutoAdvanceDialogue, () => Config.DebugDiagnostics);
        try
        {
            ReplaySaveGuard.Apply(helper, Monitor, replay);
            ReplaySpeedPatches.Apply(helper, replay);
            replayProtectionReady = true;
        }
        catch (Exception error)
        {
            Monitor.Log($"回放存档保护无法启用，本次运行已禁用回放：{error}", LogLevel.Error);
        }
        // Pre-scaled copies of the tab art (tools/Optimize-Assets.ps1): 2x the Collections button, and a portrait placeholder.
        tabIcon = helper.ModContent.Load<Texture2D>("assets/GalleryTabIcon-104x80.png");
        replayService = new ReplayService(helper, Monitor, replay, () => replayProtectionReady,
            () => Config.EnableOrdinaryEventReplay, () => unlockAll.Value, () => Config.ShowRollbackWarning,
            entry => Game1.player is not null && catalog.Get().IsParallelBranchUnlocked(entry, Game1.player.eventsSeen));
        appearance = new CharacterAppearance(helper.GameContent, helper.ModRegistry, Monitor, helper.ModContent.Load<Texture2D>("assets/GalleryTabIcon-640x490.png"));
        // Custom names and favorites are shared by every save and screen on this computer, so all pages use one store.
        string userDataPath = Path.Combine(helper.DirectoryPath, "user-data");
        var names = new EventNameStore(userDataPath, warning => Monitor.Log(warning, LogLevel.Warn));
        var favorites = new FavoriteEventStore(userDataPath, warning => Monitor.Log(warning, LogLevel.Warn));
        autoMemento = new(() => new AutoMementoTracker(Monitor, catalog, replay, replayService, () => Config.AutoCaptureMemories));
        Func<ResolvedEvent, EventSourceDisplay> sourceDetails = new EventSourceDetails(eventDefinitions, helper.Translation).Read;
        application = new(() => new GalleryApplication(helper, Monitor, catalog, photos.Value, replayService, () => unlockAll.Value, ToggleUnlock,
            appearance, sourceDetails, names, favorites));
        helper.Events.GameLoop.SaveLoaded += (_, _) => appearance.Invalidate();
        helper.Events.GameLoop.DayStarted += (_, _) =>
        {
            appearance.Invalidate();
            socialClueCache.Value.Invalidate();
            ShowMorningEncounterHintIfAny();
        };
        helper.Events.GameLoop.SaveLoaded += (_, _) => { catalog.Invalidate(); socialClueCache.Value.Invalidate(); replayService.ResetWarnings(); application.Value.Reset(); };
        helper.Events.GameLoop.SaveLoaded += (_, _) => photos.Value.Load(new SaveProfileKey(Game1.uniqueIDForThisGame, Game1.player.UniqueMultiplayerID));
        helper.Events.GameLoop.GameLaunched += (_, _) => RegisterGmcm();
        helper.Events.GameLoop.SaveLoaded += (_, _) => unlockAll.Value = ReadUnlock();
        helper.Events.GameLoop.ReturnedToTitle += (_, _) =>
        {
            replay.OnReturnedToTitle();
            catalog.Invalidate();
            replayService.ResetWarnings();
            // Returning to the title ends every split-screen session too.
            foreach ((_, GalleryApplication screen) in application.GetActiveValues()) screen.Reset();
            foreach ((_, GalleryPhotos screen) in photos.GetActiveValues()) screen.Dispose();
            foreach ((_, AutoMementoTracker tracker) in autoMemento.GetActiveValues()) tracker.Reset();
            unlockAll.ResetAllScreens();
            pendingOpen.ResetAllScreens();
            theaterHud.ResetAllScreens();
            socialClueCache.ResetAllScreens();
            autoMemento.ResetAllScreens();
            appearance.Invalidate();
        };
        helper.Events.Content.LocaleChanged += (_, _) => catalog.Invalidate();
        helper.Events.Content.AssetsInvalidated += (_, e) =>
        {
            if (e.NamesWithoutLocale.Any(appearance.UsesAsset)) appearance.Invalidate();
            // Prerequisite records come from Data/TriggerActions.
            if (e.NamesWithoutLocale.Any(name => name.IsEquivalentTo("Data/Characters") || name.IsEquivalentTo("Data/TriggerActions")
                || name.Name.StartsWith("Data/Events/", StringComparison.OrdinalIgnoreCase)))
                catalog.Invalidate();
        };
        helper.Events.Input.ButtonPressed += OnButtonPressed;
        helper.Events.GameLoop.UpdateTicking += (_, _) => GalleryTyping.Update(typingKeys.Value,
            Game1.activeClickableMenu as IGallerySearchMenu, GalleryTyping.PressedKeys, GalleryTyping.Clipboard);
        var frames = new GalleryFrameProbe(Monitor, () => Config.DebugDiagnostics);
        helper.Events.GameLoop.UpdateTicking += (_, _) => frames.BeginUpdate();
        helper.Events.GameLoop.UpdateTicked += (_, _) => frames.EndUpdate();
        helper.Events.Display.RenderingActiveMenu += (_, _) => frames.BeginDraw();
        helper.Events.Display.RenderedActiveMenu += (_, _) => frames.EndDraw();
        helper.Events.Display.RenderedActiveMenu += OnRenderedActiveMenu;
        helper.Events.Display.RenderedHud += OnRenderedHud;
        helper.Events.Display.RenderingWorld += (_, _) => photos.Value.PrepareCapture(ActiveCapturableEvent);
        helper.Events.Display.RenderedWorld += (_, e) => photos.Value.Capture(e.SpriteBatch, ActiveCapturableEvent);
        helper.Events.Display.WindowResized += (_, _) =>
        {
            foreach ((_, GalleryPhotos screen) in photos.GetActiveValues()) screen.ClearTextures();
        };
        helper.Events.Display.MenuChanged += (_, e) =>
        {
            if (e.OldMenu is GalleryPhotoMenu)
                photos.Value.ReleasePreviews();
            if (!GalleryApplication.OwnsMenu(e.NewMenu))
                photos.Value.ClearTextures();
        };
        Func<SButton, bool> buttonDown = helper.Input.IsDown;
        helper.Events.GameLoop.UpdateTicked += (_, _) =>
        {
            SteamTextInput.Update(buttonDown);
            replay.Update();
            photos.Value.Update(ActiveCapturableEvent);
            autoMemento.Value.Update(photos.Value);
            if (replay.IsActive && Game1.CurrentEvent is not null && !replay.IsScenePaused)
                theaterHud.Value.Tick(Game1.getMouseX(true), Game1.getMouseY(true));
            else if (!replay.IsActive)
                theaterHud.Value.Reset();
            if (!pendingOpen.Value)
                return;
            pendingOpen.Value = false;
            application.Value.Open();
        };

        Monitor.Log(helper.Translation.Get("log.loaded", new { version = ModManifest.Version }), LogLevel.Info);
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        // The game's on-screen keyboard owns all input while it is open.
        if (Game1.textEntry is not null)
            return;
        if (replay.ShouldHideSceneUi)
        {
            if (replayService.ActiveIdentity is { } pausedIdentity && replay.PlayingEvent is Event pausedPlaying
                && Config.ScreenshotKeys.JustPressed())
            {
                Helper.Input.SuppressActiveKeybinds(Config.ScreenshotKeys);
                Helper.Input.Suppress(e.Button);
                photos.Value.Request(pausedIdentity, pausedPlaying);
                return;
            }
            if (Config.PauseSceneKeys.JustPressed()
                || e.Button is SButton.MouseLeft or SButton.MouseRight or SButton.ControllerA or SButton.ControllerB or SButton.Escape
                || IsMenuButton(e.Button))
            {
                if (Config.PauseSceneKeys.JustPressed())
                    Helper.Input.SuppressActiveKeybinds(Config.PauseSceneKeys);
                Helper.Input.Suppress(e.Button);
                replay.SetScenePaused(false);
                theaterHud.Value.NotifyActivity();
                return;
            }
            if (!IsPendingChordModifier(e.Button))
                Helper.Input.Suppress(e.Button);
            return;
        }
        bool galleryPressed = Config.GalleryKeys.JustPressed();
        bool hudVisible = theaterHud.Value.IsVisible;
        if (replayService.ActiveIdentity is { } identity && replay.PlayingEvent is Event playing
            && (Config.ScreenshotKeys.JustPressed()
                || e.Button == SButton.MouseLeft && hudVisible && GetScreenshotButton().Contains(Game1.getMouseX(true), Game1.getMouseY(true))))
        {
            theaterHud.Value.NotifyActivity();
            Helper.Input.SuppressActiveKeybinds(Config.ScreenshotKeys);
            Helper.Input.Suppress(e.Button);
            photos.Value.Request(identity, playing);
            return;
        }
        if (replay.PlayingEvent is not null
            && (Config.PauseSceneKeys.JustPressed()
                || e.Button == SButton.MouseLeft && hudVisible && GetPauseSceneButton().Contains(Game1.getMouseX(true), Game1.getMouseY(true))))
        {
            theaterHud.Value.NotifyActivity();
            if (Config.PauseSceneKeys.JustPressed())
                Helper.Input.SuppressActiveKeybinds(Config.PauseSceneKeys);
            Helper.Input.Suppress(e.Button);
            replay.ToggleScenePause();
            return;
        }
        if (Config.ReplaySpeedKeys.JustPressed() && replay.IsActive)
        {
            theaterHud.Value.NotifyActivity();
            Helper.Input.SuppressActiveKeybinds(Config.ReplaySpeedKeys);
            replay.CycleSpeed();
            return;
        }
        if (e.Button == SButton.MouseLeft && replay.IsActive && hudVisible && GetReplaySpeedButton().Contains(Game1.getMouseX(true), Game1.getMouseY(true)))
        {
            theaterHud.Value.NotifyActivity();
            Helper.Input.Suppress(e.Button);
            replay.CycleSpeed();
            return;
        }
        bool searchSelected = Game1.activeClickableMenu is IGallerySearchMenu { IsSearchSelected: true };
        if (GalleryUiRules.ShouldCloseFromShortcut(galleryPressed, searchSelected)
            && GalleryApplication.OwnsMenu(Game1.activeClickableMenu))
        {
            Helper.Input.SuppressActiveKeybinds(Config.GalleryKeys);
            application.Value.Close();
            Game1.playSound("bigDeSelect");
            return;
        }
        if (e.Button == SButton.ControllerB && Game1.activeClickableMenu is IGallerySearchMenu homeMenu)
        {
            Helper.Input.Suppress(e.Button);
            homeMenu.HandleControllerBack();
            return;
        }
        if (e.Button == SButton.ControllerB && GalleryApplication.OwnsMenu(Game1.activeClickableMenu))
        {
            Helper.Input.Suppress(e.Button);
            application.Value.Back();
            return;
        }
        // Keys typed into the field are hidden before they get here (GalleryTyping); this covers the rest, such as a
        // controller, or a key pressed in the same tick the field was selected.
        if (Game1.activeClickableMenu is IGallerySearchMenu { IsSearchSelected: true } home)
        {
            if (e.Button == SButton.Escape)
                home.DeselectSearch();
            else if (IsControllerNavigation(e.Button))
            {
                home.DeselectSearch();
                return;
            }
            if (e.Button is not SButton.MouseLeft and not SButton.MouseRight)
            {
                Helper.Input.Suppress(e.Button);
                return;
            }
        }
        if (e.Button is SButton.MouseLeft or SButton.ControllerA && IsCollectionsPageOpen())
        {
            Vector2 cursor = new(Game1.getMouseX(true), Game1.getMouseY(true));
            if (GetCollectionsButton().Contains(cursor))
            {
                Helper.Input.Suppress(e.Button);
                pendingOpen.Value = true;
                return;
            }
        }
        if (e.Button == SButton.MouseLeft && Config.ShowSocialPageClueBadges && Context.IsWorldReady && !replay.IsActive
            && SocialPageBridge.TryInspectActiveMenu(out GameMenu badgeGameMenu, out SocialPage badgeSocialPage, out _)
            && SocialPageBridge.TryHitBadge(badgeSocialPage, GetSocialClues(badgeSocialPage), Game1.getMouseX(true), Game1.getMouseY(true), out string? badgeVillager))
        {
            Helper.Input.Suppress(e.Button);
            application.Value.OpenFromSocialPage(badgeVillager, SocialPageBridge.CreateRestoreCallback(badgeGameMenu, badgeSocialPage));
            return;
        }
        if (galleryPressed && Context.IsWorldReady && !replay.IsActive
            && SocialPageBridge.TryInspectActiveMenu(out GameMenu socialGameMenu, out SocialPage socialPage, out string? pointedVillager))
        {
            Helper.Input.SuppressActiveKeybinds(Config.GalleryKeys);
            Helper.Input.Suppress(e.Button);
            application.Value.OpenFromSocialPage(pointedVillager, SocialPageBridge.CreateRestoreCallback(socialGameMenu, socialPage));
            return;
        }
        if (!galleryPressed || !Context.IsWorldReady || !Context.IsPlayerFree || Game1.activeClickableMenu is not null)
            return;

        Helper.Input.SuppressActiveKeybinds(Config.GalleryKeys);
        application.Value.Open();
    }

    private void OnRenderedActiveMenu(object? sender, RenderedActiveMenuEventArgs e)
    {
        if (IsCollectionsPageOpen())
        {
            EnsureCollectionsControllerEntry();
            Rectangle button = GetCollectionsButton();
            Vector2 cursor = new(Game1.getMouseX(true), Game1.getMouseY(true));
            Color tint = button.Contains(cursor) ? new Color(255, 244, 207) : Color.White;
            e.SpriteBatch.Draw(Game1.mouseCursors, new Vector2(button.X, button.Bottom), new Rectangle(16, 368, 16, 16), tint,
                -MathHelper.PiOver2, Vector2.Zero, 4f, SpriteEffects.None, 1f);
            e.SpriteBatch.Draw(tabIcon, new Rectangle(button.Center.X - 24, button.Center.Y - 20, 52, 40), tint);
            if (button.Contains(cursor))
                IClickableMenu.drawHoverText(e.SpriteBatch, Helper.Translation.Get("menu.open"), Game1.smallFont);
            return;
        }

        if (Config.ShowSocialPageClueBadges && Context.IsWorldReady && !replay.IsActive
            && SocialPageBridge.TryInspectActiveMenu(out _, out SocialPage socialPage, out _))
        {
            DrawSocialPageClueBadges(e.SpriteBatch, socialPage);
        }
    }

    private void ShowMorningEncounterHintIfAny()
    {
        if (!Config.ShowMorningEncounterHint || !Context.IsWorldReady || Game1.player is null || replay.IsActive)
            return;
        try
        {
            GalleryCatalog cat = catalog.Get();
            CurrentStateSnapshot shared = RuntimeStateReader.Capture();
            var charNames = new GalleryCharacterNames(cat, Helper.Translation);
            List<string> availableVillagers = [];

            foreach (GalleryCharacter character in cat.Characters)
            {
                if (!character.IsMet && Game1.player.friendshipData?.ContainsKey(character.Name) != true)
                    continue;
                bool hasAvailable = false;
                foreach (GalleryEvent story in cat.HeartEventsFor(character.Name))
                {
                    if (GalleryConditionPresentation.Clue(story, cat, shared) == EncounterClue.AvailableToday)
                    {
                        hasAvailable = true;
                        break;
                    }
                }
                if (hasAvailable)
                    availableVillagers.Add(charNames.Get(character.Name));
            }

            string? message = SocialPageBridge.FormatMorningEncounterHint(
                availableVillagers,
                Helper.Translation.Get("clue.name-separator"),
                (key, args) => Helper.Translation.Get(key, args));
            if (!string.IsNullOrWhiteSpace(message))
                Game1.addHUDMessage(new HUDMessage(message, HUDMessage.newQuest_type));
        }
        catch (Exception error)
        {
            Monitor.Log($"Skipped morning encounter hint: {error.Message}", LogLevel.Debug);
        }
    }

    private IReadOnlyDictionary<string, EncounterClue> GetSocialClues(SocialPage socialPage)
    {
        SocialClueCache cache = socialClueCache.Value;
        int totalDays = Game1.Date?.TotalDays ?? -1;
        int timeBucket = Game1.timeOfDay / 10;
        string locationName = Game1.currentLocation?.NameOrUniqueName ?? "";
        int seenCount = Game1.player?.eventsSeen?.Count ?? 0;

        if (ReferenceEquals(cache.Page, socialPage)
            && cache.TotalDays == totalDays
            && cache.TimeBucket == timeBucket
            && cache.LocationName == locationName
            && cache.SeenCount == seenCount
            && cache.Clues is not null)
        {
            return cache.Clues;
        }

        Dictionary<string, EncounterClue> computed = new(StringComparer.Ordinal);
        try
        {
            GalleryCatalog cat = catalog.Get();
            CurrentStateSnapshot shared = RuntimeStateReader.Capture();
            foreach (GalleryCharacter character in cat.Characters)
            {
                if (!character.IsMet && Game1.player?.friendshipData?.ContainsKey(character.Name) != true)
                    continue;
                EncounterClue best = EncounterClue.None;
                foreach (GalleryEvent story in cat.HeartEventsFor(character.Name))
                {
                    EncounterClue c = GalleryConditionPresentation.Clue(story, cat, shared);
                    if (c > best)
                        best = c;
                    if (best == EncounterClue.AvailableToday)
                        break;
                }
                if (best != EncounterClue.None)
                    computed[character.Name] = best;
            }
        }
        catch (Exception error)
        {
            Monitor.Log($"Skipped SocialPage clue badge scan: {error.Message}", LogLevel.Debug);
        }

        cache.Page = socialPage;
        cache.TotalDays = totalDays;
        cache.TimeBucket = timeBucket;
        cache.LocationName = locationName;
        cache.SeenCount = seenCount;
        cache.Clues = computed;
        return computed;
    }

    private void DrawSocialPageClueBadges(SpriteBatch b, SocialPage socialPage)
    {
        IReadOnlyDictionary<string, EncounterClue> clues = GetSocialClues(socialPage);
        if (clues.Count == 0)
            return;
        if (!GalleryUiSkin.IsLoaded)
            GalleryUiSkin.Load(path => Helper.ModContent.Load<Texture2D>(path), msg => Monitor.Log(msg, LogLevel.Warn));

        IReadOnlyList<SocialPageBridge.VisibleSlotBadge> visible = SocialPageBridge.CollectVisibleBadges(socialPage, clues);
        if (visible.Count == 0)
            return;

        int mx = Game1.getMouseX(true), my = Game1.getMouseY(true);
        SocialPageBridge.VisibleSlotBadge? hovered = null;
        foreach (SocialPageBridge.VisibleSlotBadge item in visible)
        {
            bool isHover = item.BadgeBounds.Contains(mx, my);
            Rectangle drawRect = isHover ? GalleryDrawing.Inset(item.BadgeBounds, -2, -2) : item.BadgeBounds;
            GalleryUiSkin.DrawBadge(b,
                item.Clue == EncounterClue.AvailableToday ? UiBadge.AvailableToday : UiBadge.ClueReady,
                drawRect);
            if (isHover)
                hovered = item;
        }

        if (hovered is { } hit)
        {
            string keyLabel = Config.GalleryKeys.ToString();
            string tipKey = hit.Clue == EncounterClue.AvailableToday ? "clue.social-badge-available" : "clue.social-badge-ready";
            string tip = Helper.Translation.Get(tipKey, new { name = hit.DisplayName, key = keyLabel });
            IClickableMenu.drawHoverText(b,
                Game1.parseText(tip, Game1.smallFont, Math.Max(200, Math.Min(520, Game1.uiViewport.Width - 80))),
                Game1.smallFont);
            socialPage.drawMouse(b);
        }
    }

    private static bool IsCollectionsPageOpen()
        => Game1.activeClickableMenu is GameMenu menu
            && menu.GetCurrentPage() is CollectionsPage { letterviewerSubMenu: null };

    private static Rectangle GetCollectionsButton()
    {
        if (Game1.activeClickableMenu is not GameMenu menu || menu.GetCurrentPage() is not CollectionsPage page || page.sideTabs.Count == 0)
            return Rectangle.Empty;
        int x = page.sideTabs.Values.Min(tab => tab.bounds.X);
        int y = page.sideTabs.Values.Max(tab => tab.bounds.Bottom);
        return new Rectangle(x, y, 64, 64);
    }

    private static void EnsureCollectionsControllerEntry()
    {
        if (Game1.activeClickableMenu is not GameMenu menu || menu.GetCurrentPage() is not CollectionsPage page
            || page.sideTabs.Count == 0 || page.allClickableComponents is null)
            return;
        Rectangle bounds = GetCollectionsButton();
        ClickableComponent? component = page.allClickableComponents.FirstOrDefault(value => value.myID == CollectionsGalleryComponentId);
        ClickableTextureComponent previous = page.sideTabs.Values.MaxBy(value => value.bounds.Y)!;
        previous.downNeighborID = CollectionsGalleryComponentId;
        if (component is null)
        {
            component = new ClickableComponent(bounds, "gallery")
            {
                myID = CollectionsGalleryComponentId,
                upNeighborID = previous.myID,
                rightNeighborID = 0
            };
            page.allClickableComponents.Add(component);
        }
        else
            component.bounds = bounds;
    }

    private static bool IsControllerNavigation(SButton button) => button is
        SButton.DPadUp or SButton.DPadDown or SButton.DPadLeft or SButton.DPadRight
        or SButton.LeftThumbstickUp or SButton.LeftThumbstickDown or SButton.LeftThumbstickLeft or SButton.LeftThumbstickRight;

    private static bool IsMenuButton(SButton button)
        => button is SButton.ControllerStart or SButton.ControllerY
            || Game1.options?.menuButton?.Any(binding => binding.ToSButton() == button) == true;

    private bool IsPendingChordModifier(SButton button)
        => button is SButton.LeftShift or SButton.RightShift or SButton.LeftControl or SButton.RightControl or SButton.LeftAlt or SButton.RightAlt
            && (Config.ScreenshotKeys.Keybinds.Any(kb => kb.Buttons.Length > 1 && kb.Buttons.Contains(button))
                || Config.PauseSceneKeys.Keybinds.Any(kb => kb.Buttons.Length > 1 && kb.Buttons.Contains(button)));

    private void OnRenderedHud(object? sender, RenderedHudEventArgs e)
    {
        if (!replay.IsActive || Game1.CurrentEvent is null || replay.IsScenePaused)
            return;
        float alpha = theaterHud.Value.Opacity;
        if (alpha <= 0f)
            return;
        if (!GalleryUiSkin.IsLoaded)
            GalleryUiSkin.Load(path => Helper.ModContent.Load<Texture2D>(path), msg => Monitor.Log(msg, LogLevel.Warn));

        int mouseX = Game1.getMouseX(true), mouseY = Game1.getMouseY(true);
        Rectangle speedButton = GetReplaySpeedButton();
        bool speedHot = speedButton.Contains(mouseX, mouseY);
        DrawHudSpeedButton(e.SpriteBatch, speedButton, Helper.Translation.Get("replay.speed", new { speed = replay.SpeedMultiplier }), speedHot, alpha);
        string? hoverText = null;
        if (replay.PlayingEvent is not null)
        {
            Rectangle pause = GetPauseSceneButton();
            bool pauseHot = pause.Contains(mouseX, mouseY);
            GalleryUiSkin.DrawWoodButton(e.SpriteBatch, pause, "", pauseHot ? UiButtonState.Hover : UiButtonState.Normal, alpha);
            if (GalleryUiSkin.HasHudIcons)
            {
                GalleryUiSkin.DrawHudIcon(e.SpriteBatch, UiHudIcon.Pause, new Rectangle(pause.Center.X - 15, pause.Center.Y - 15, 30, 30), Color.White * alpha);
            }
            else
            {
                Color barColor = Game1.textColor * alpha;
                e.SpriteBatch.Draw(Game1.staminaRect, new Rectangle(pause.X + 19, pause.Y + 14, 6, 20), barColor);
                e.SpriteBatch.Draw(Game1.staminaRect, new Rectangle(pause.X + 31, pause.Y + 14, 6, 20), barColor);
            }
            if (pauseHot)
                hoverText = Helper.Translation.Get("replay.pause-scene");

            if (replayService.ActiveIdentity is not null)
            {
                Rectangle camera = GetScreenshotButton();
                bool camHot = camera.Contains(mouseX, mouseY);
                bool pending = photos.Value.Pending;
                GalleryUiSkin.DrawWoodButton(e.SpriteBatch, camera, "",
                    pending ? UiButtonState.Disabled : camHot ? UiButtonState.Hover : UiButtonState.Normal, alpha);
                GalleryUiSkin.DrawHudIcon(e.SpriteBatch, UiHudIcon.Camera, new Rectangle(camera.Center.X - 15, camera.Center.Y - 15, 30, 30),
                    (pending ? Color.LightGray : Color.White) * alpha);
                if (camHot)
                    hoverText = Helper.Translation.Get("photo.capture");
            }
        }
        if (GalleryMusicNames.ResolveActiveReplayTitle(replay.StaticMusicCue) is { Length: > 0 } bgmTitle)
        {
            string rawLabel = Helper.Translation.Get("replay.bgm", new { bgm = bgmTitle });
            bool hasNoteIcon = GalleryUiSkin.HasHudIcons;
            string cleanLabel = rawLabel.Trim();
            if (!hasNoteIcon && !cleanLabel.StartsWith("BGM:", StringComparison.OrdinalIgnoreCase))
                cleanLabel = "BGM: " + cleanLabel;
            int iconSpace = hasNoteIcon ? 26 : 0;
            int maxLabelWidth = Math.Max(180, Math.Min(420, Game1.uiViewport.Width - 80));
            string fitted = GalleryTextFit.Ellipsize(cleanLabel, maxLabelWidth - 28 - iconSpace, value => Game1.smallFont.MeasureString(value).X);
            Vector2 textSize = Game1.smallFont.MeasureString(fitted);
            int pillWidth = Math.Min(maxLabelWidth, (int)Math.Ceiling(textSize.X) + 28 + iconSpace);
            Rectangle bgmBounds = new(speedButton.Right - pillWidth, speedButton.Bottom + 8, pillWidth, 38);
            e.SpriteBatch.Draw(Game1.fadeToBlackRect, bgmBounds, new Color(36, 24, 16) * (0.76f * alpha));
            int textX = bgmBounds.X + 12;
            if (hasNoteIcon)
            {
                GalleryUiSkin.DrawHudIcon(e.SpriteBatch, UiHudIcon.Music, new Rectangle(textX, bgmBounds.Center.Y - 11, 22, 22), Color.Wheat * alpha);
                textX += 26;
            }
            e.SpriteBatch.DrawString(Game1.smallFont, fitted,
                new Vector2(textX, bgmBounds.Center.Y - textSize.Y / 2f), Color.Wheat * alpha);
        }
        if (hoverText is not null && alpha > 0.5f)
            IClickableMenu.drawHoverText(e.SpriteBatch, hoverText, Game1.smallFont);
    }

    private static void DrawHudSpeedButton(SpriteBatch b, Rectangle bounds, string text, bool hot, float alpha)
    {
        GalleryUiSkin.DrawWoodButton(b, bounds, "", hot ? UiButtonState.Hover : UiButtonState.Normal, alpha);
        bool hasIcon = GalleryUiSkin.HasHudIcons;
        int iconSize = hasIcon ? 24 : 0;
        float scale = Math.Min(0.86f, (bounds.Height - 14f) / Game1.smallFont.LineSpacing);
        int innerWidth = bounds.Width - 32;
        string fitted = GalleryTextFit.Ellipsize(text, Math.Max(24, innerWidth - (hasIcon ? iconSize + 6 : 0)) / scale, s => Game1.smallFont.MeasureString(s).X);
        Vector2 size = Game1.smallFont.MeasureString(fitted) * scale;
        int totalW = (hasIcon ? iconSize + 6 : 0) + (int)Math.Ceiling(size.X);
        int startX = bounds.Center.X - totalW / 2;
        if (hasIcon)
        {
            GalleryUiSkin.DrawHudIcon(b, UiHudIcon.FastForward, new Rectangle(startX, bounds.Center.Y - iconSize / 2, iconSize, iconSize), Color.White * alpha);
            startX += iconSize + 6;
        }
        b.DrawString(Game1.smallFont, fitted, new Vector2(startX, bounds.Center.Y - size.Y / 2f),
            new Color(62, 32, 12) * alpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private static Rectangle GetReplaySpeedButton() => new(Game1.uiViewport.Width - 212, 24, 172, 48);
    private static Rectangle GetPauseSceneButton() => new(Game1.uiViewport.Width - 280, 24, 56, 48);
    private static Rectangle GetScreenshotButton() => new(Game1.uiViewport.Width - 348, 24, 56, 48);

    // Split-screen players share the host's save data; the main player keeps the original key.
    private static string UnlockKey => Context.IsMainPlayer ? SaveDataKey : SaveDataKey + "-" + Game1.player.UniqueMultiplayerID;

    private bool ReadUnlock()
    {
        // A remote farmhand can't read the host's save data (SMAPI throws), so its unlock lasts for this session only.
        if (!Context.IsOnHostComputer) return false;
        try { return Helper.Data.ReadSaveData<GallerySaveData>(UnlockKey)?.UnlockAll == true; }
        catch (InvalidOperationException error)
        {
            Monitor.Log($"The unlock choice can't be read from this save; it applies to this session only. {error.Message}", LogLevel.Debug);
            return false;
        }
    }

    private void ToggleUnlock()
    {
        if (!Context.IsWorldReady || replay.IsActive) return;
        unlockAll.Value = !unlockAll.Value;
        if (!Context.IsOnHostComputer) return;
        try { Helper.Data.WriteSaveData(UnlockKey, new GallerySaveData { UnlockAll = unlockAll.Value }); }
        catch (InvalidOperationException error)
        {
            Monitor.Log($"The unlock choice can't be saved to this save; it applies to this session only. {error.Message}", LogLevel.Debug);
        }
    }

    private void RegisterGmcm()
    {
        IGenericModConfigMenuApi? gmcm = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (gmcm is null)
        {
            Monitor.Log("未检测到 Generic Mod Config Menu；仍可直接编辑 config.json。", LogLevel.Debug);
            return;
        }

        gmcm.Register(ModManifest, () => Config = new ModConfig(), () => Helper.WriteConfig(Config));
        gmcm.AddBoolOption(ModManifest, () => Config.AutoCaptureMemories, value => Config.AutoCaptureMemories = value,
            () => Helper.Translation.Get("config.auto-photo.name"), () => Helper.Translation.Get("config.auto-photo.tooltip"));
        gmcm.AddBoolOption(ModManifest, () => Config.ShowMorningEncounterHint, value => Config.ShowMorningEncounterHint = value,
            () => Helper.Translation.Get("config.morning-hint.name"), () => Helper.Translation.Get("config.morning-hint.tooltip"));
        gmcm.AddBoolOption(ModManifest, () => Config.ShowSocialPageClueBadges, value => Config.ShowSocialPageClueBadges = value,
            () => Helper.Translation.Get("config.social-badge.name"), () => Helper.Translation.Get("config.social-badge.tooltip"));
        gmcm.AddBoolOption(ModManifest, () => Config.EnableOrdinaryEventReplay, value => Config.EnableOrdinaryEventReplay = value,
            () => Helper.Translation.Get("config.ordinary-replay.name"), () => Helper.Translation.Get("config.ordinary-replay.tooltip"));
        gmcm.AddKeybindList(ModManifest, () => Config.GalleryKeys, value => Config.GalleryKeys = value,
            () => Helper.Translation.Get("config.key.name"), () => Helper.Translation.Get("config.key.tooltip"));
        gmcm.AddKeybindList(ModManifest, () => Config.ReplaySpeedKeys, value => Config.ReplaySpeedKeys = value,
            () => Helper.Translation.Get("config.speed-key.name"), () => Helper.Translation.Get("config.speed-key.tooltip"));
        gmcm.AddKeybindList(ModManifest, () => Config.ScreenshotKeys, value => Config.ScreenshotKeys = value,
            () => Helper.Translation.Get("config.photo-key.name"), () => Helper.Translation.Get("config.photo-key.tooltip"));
        gmcm.AddKeybindList(ModManifest, () => Config.PauseSceneKeys, value => Config.PauseSceneKeys = value,
            () => Helper.Translation.Get("config.pause-key.name"), () => Helper.Translation.Get("config.pause-key.tooltip"));
        gmcm.AddBoolOption(ModManifest, () => Config.ShowRollbackWarning, value => Config.ShowRollbackWarning = value,
            () => Helper.Translation.Get("config.warning.name"), () => Helper.Translation.Get("config.warning.tooltip"));
        gmcm.AddBoolOption(ModManifest, () => Config.AutoAdvanceDialogue, value => Config.AutoAdvanceDialogue = value,
            () => Helper.Translation.Get("config.auto-dialogue.name"), () => Helper.Translation.Get("config.auto-dialogue.tooltip"));
        gmcm.AddBoolOption(ModManifest, () => Config.DebugDiagnostics, value => { Config.DebugDiagnostics = value; catalog.Invalidate(); },
            () => Helper.Translation.Get("config.debug.name"),
            () => Helper.Translation.Get("config.debug.tooltip", new { path = GalleryDiagnostics.DirectoryPath }));
        Monitor.Log("GMCM 配置已注册。", LogLevel.Debug);
    }

    private sealed class GallerySaveData
    {
        public bool UnlockAll { get; set; }
    }

    private sealed class SocialClueCache
    {
        internal SocialPage? Page;
        internal int TotalDays = -1;
        internal int TimeBucket = -1;
        internal string LocationName = "";
        internal int SeenCount = -1;
        internal IReadOnlyDictionary<string, EncounterClue>? Clues;

        internal void Invalidate()
        {
            Page = null;
            Clues = null;
        }
    }
}
