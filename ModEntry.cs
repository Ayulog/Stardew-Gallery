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
    private PerScreen<GalleryApplication> application = null!;
    private PerScreen<GalleryPhotos> photos = null!;
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
            // CP config/reload can change referenced files, targets and event IDs during a save.
            if (e.NamesWithoutLocale.Any(name => name.StartsWith("Data/Events")))
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
            () => Config.EnableOrdinaryEventReplay, () => unlockAll.Value, () => Config.ShowRollbackWarning);
        appearance = new CharacterAppearance(helper.GameContent, helper.ModRegistry, Monitor, helper.ModContent.Load<Texture2D>("assets/GalleryTabIcon-640x490.png"));
        // Custom names are shared by every save and screen on this computer, so all pages use one store.
        var names = new EventNameStore(Path.Combine(helper.DirectoryPath, "user-data"), warning => Monitor.Log(warning, LogLevel.Warn));
        Func<ResolvedEvent, EventSourceDisplay> sourceDetails = new EventSourceDetails(eventDefinitions, helper.Translation).Read;
        application = new(() => new GalleryApplication(helper, Monitor, catalog, photos.Value, replayService, () => unlockAll.Value, ToggleUnlock,
            appearance, sourceDetails, names));
        helper.Events.GameLoop.SaveLoaded += (_, _) => appearance.Invalidate();
        helper.Events.GameLoop.DayStarted += (_, _) => appearance.Invalidate();
        helper.Events.GameLoop.SaveLoaded += (_, _) => { catalog.Invalidate(); replayService.ResetWarnings(); application.Value.Reset(); };
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
            unlockAll.ResetAllScreens();
            pendingOpen.ResetAllScreens();
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
        helper.Events.Display.RenderingWorld += (_, _) => photos.Value.PrepareCapture(replay.PlayingEvent);
        helper.Events.Display.RenderedWorld += (_, e) => photos.Value.Capture(e.SpriteBatch, replay.PlayingEvent);
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
            photos.Value.Update(replay.PlayingEvent);
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
        bool galleryPressed = Config.GalleryKeys.JustPressed();
        if (replayService.ActiveIdentity is { } identity && replay.PlayingEvent is Event playing
            && (Config.ScreenshotKeys.JustPressed() || e.Button == SButton.MouseLeft && GetScreenshotButton().Contains(Game1.getMouseX(true), Game1.getMouseY(true))))
        {
            Helper.Input.SuppressActiveKeybinds(Config.ScreenshotKeys);
            Helper.Input.Suppress(e.Button);
            photos.Value.Request(identity, playing);
            return;
        }
        if (Config.ReplaySpeedKeys.JustPressed() && replay.IsActive)
        {
            Helper.Input.SuppressActiveKeybinds(Config.ReplaySpeedKeys);
            replay.CycleSpeed();
            return;
        }
        if (e.Button == SButton.MouseLeft && replay.IsActive && GetReplaySpeedButton().Contains(Game1.getMouseX(true), Game1.getMouseY(true)))
        {
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
        if (!galleryPressed || !Context.IsWorldReady || !Context.IsPlayerFree || Game1.activeClickableMenu is not null)
            return;

        Helper.Input.SuppressActiveKeybinds(Config.GalleryKeys);
        application.Value.Open();
    }

    private void OnRenderedActiveMenu(object? sender, RenderedActiveMenuEventArgs e)
    {
        if (!IsCollectionsPageOpen())
            return;
        EnsureCollectionsControllerEntry();
        Rectangle button = GetCollectionsButton();
        Vector2 cursor = new(Game1.getMouseX(true), Game1.getMouseY(true));
        Color tint = button.Contains(cursor) ? new Color(255, 244, 207) : Color.White;
        e.SpriteBatch.Draw(Game1.mouseCursors, new Vector2(button.X, button.Bottom), new Rectangle(16, 368, 16, 16), tint,
            -MathHelper.PiOver2, Vector2.Zero, 4f, SpriteEffects.None, 1f);
        e.SpriteBatch.Draw(tabIcon, new Rectangle(button.Center.X - 24, button.Center.Y - 20, 52, 40), tint);
        if (button.Contains(cursor))
            IClickableMenu.drawHoverText(e.SpriteBatch, Helper.Translation.Get("menu.open"), Game1.smallFont);
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

    private void OnRenderedHud(object? sender, RenderedHudEventArgs e)
    {
        if (!replay.IsActive || Game1.CurrentEvent is null)
            return;
        Rectangle button = GetReplaySpeedButton();
        GalleryDrawing.DrawButton(e.SpriteBatch, button, Helper.Translation.Get("replay.speed", new { speed = replay.SpeedMultiplier }));
        if (replayService.ActiveIdentity is not null && replay.PlayingEvent is not null)
        {
            Rectangle camera = GetScreenshotButton();
            IClickableMenu.drawTextureBox(e.SpriteBatch, camera.X, camera.Y, camera.Width, camera.Height, Color.White);
            e.SpriteBatch.Draw(Game1.mouseCursors2, new Rectangle(camera.X + 10, camera.Y + 8, 36, 32), new Rectangle(72, 31, 18, 16), photos.Value.Pending ? Color.Gray : Color.White);
            if (camera.Contains(Game1.getMouseX(true), Game1.getMouseY(true)))
                IClickableMenu.drawHoverText(e.SpriteBatch, Helper.Translation.Get("photo.capture"), Game1.smallFont);
        }
    }

    private static Rectangle GetReplaySpeedButton() => new(Game1.uiViewport.Width - 190, 24, 150, 48);
    private static Rectangle GetScreenshotButton() => new(Game1.uiViewport.Width - 258, 24, 56, 48);

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
        gmcm.AddBoolOption(ModManifest, () => Config.EnableOrdinaryEventReplay, value => Config.EnableOrdinaryEventReplay = value,
            () => Helper.Translation.Get("config.ordinary-replay.name"), () => Helper.Translation.Get("config.ordinary-replay.tooltip"));
        gmcm.AddKeybindList(ModManifest, () => Config.GalleryKeys, value => Config.GalleryKeys = value,
            () => Helper.Translation.Get("config.key.name"), () => Helper.Translation.Get("config.key.tooltip"));
        gmcm.AddKeybindList(ModManifest, () => Config.ReplaySpeedKeys, value => Config.ReplaySpeedKeys = value,
            () => Helper.Translation.Get("config.speed-key.name"), () => Helper.Translation.Get("config.speed-key.tooltip"));
        gmcm.AddKeybindList(ModManifest, () => Config.ScreenshotKeys, value => Config.ScreenshotKeys = value,
            () => Helper.Translation.Get("config.photo-key.name"), () => Helper.Translation.Get("config.photo-key.tooltip"));
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
}
