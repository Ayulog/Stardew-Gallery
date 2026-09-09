using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace StardewGallery;

internal sealed class ModEntry : Mod
{
    private const int CollectionsGalleryComponentId = 7099;
    private GalleryCatalogCache catalog = null!;
    private readonly PreviewPlanner planner = new(Event.SplitPreconditions, ArgUtility.SplitBySpaceQuoteAware, null);
    private bool unlockAll;
    private bool pendingOpen;
    private Texture2D tabIcon = null!;
    private ReplayCoordinator replay = null!;
    private ReplayService replayService = null!;
    private GalleryApplication application = null!;
    private bool replayProtectionReady;
    private GalleryPhotos photos = null!;

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

        catalog = new GalleryCatalogCache(Monitor, () => Config.DebugDiagnostics);
        photos = new GalleryPhotos(helper, Monitor, (key, error) =>
            Game1.addHUDMessage(new HUDMessage(helper.Translation.Get(key), error ? HUDMessage.error_type : HUDMessage.newQuest_type)));
        try
        {
            GallerySearchInputGuard.Apply(helper, Monitor);
        }
        catch (Exception error)
        {
            Monitor.Log($"Search input isolation unavailable; search is disabled: {error}", LogLevel.Error);
        }
        replay = new ReplayCoordinator(Monitor, helper, planner, () => Config.AutoAdvanceDialogue, () => Config.DebugDiagnostics);
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
        tabIcon = helper.ModContent.Load<Texture2D>("assets/GalleryTabIcon-horizontal-v5.png");
        replayService = new ReplayService(helper, Monitor, replay, () => replayProtectionReady,
            () => Config.EnableOrdinaryEventReplay, () => unlockAll, () => Config.ShowRollbackWarning);
        application = new GalleryApplication(helper, Monitor, catalog, photos, replayService, () => unlockAll, ToggleUnlock);
        helper.Events.GameLoop.SaveLoaded += (_, _) => { catalog.Invalidate(); replayService.ResetWarnings(); application.Reset(); };
        helper.Events.GameLoop.SaveLoaded += (_, _) => photos.Load(new SaveProfileKey(Game1.uniqueIDForThisGame, Game1.player.UniqueMultiplayerID));
        helper.Events.GameLoop.GameLaunched += (_, _) => RegisterGmcm();
        helper.Events.GameLoop.SaveLoaded += (_, _) => unlockAll = helper.Data.ReadSaveData<GallerySaveData>("gallery-state")?.UnlockAll == true;
        helper.Events.GameLoop.ReturnedToTitle += (_, _) =>
        {
            replay.OnReturnedToTitle();
            catalog.Invalidate();
            unlockAll = false;
            replayService.ResetWarnings();
            application.Reset();
            photos.Dispose();
        };
        helper.Events.Content.LocaleChanged += (_, _) => catalog.Invalidate();
        helper.Events.Content.AssetsInvalidated += (_, e) =>
        {
            if (e.NamesWithoutLocale.Any(name =>
                name.IsEquivalentTo("Data/Characters") || name.Name.StartsWith("Data/Events/", StringComparison.OrdinalIgnoreCase)))
                catalog.Invalidate();
        };
        helper.Events.Input.ButtonPressed += OnButtonPressed;
        helper.Events.Display.RenderedActiveMenu += OnRenderedActiveMenu;
        helper.Events.Display.RenderedHud += OnRenderedHud;
        helper.Events.Display.RenderedWorld += (_, e) => photos.Capture(e.SpriteBatch, replay.PlayingEvent);
        helper.Events.Display.WindowResized += (_, _) => photos.ClearTextures();
        helper.Events.Display.MenuChanged += (_, e) =>
        {
            if (e.OldMenu is GalleryPhotoMenu)
                photos.ReleasePreviews();
            if (!GalleryApplication.OwnsMenu(e.NewMenu))
                photos.ClearTextures();
        };
        helper.Events.GameLoop.UpdateTicked += (_, _) =>
        {
            replay.Update();
            photos.Update(replay.PlayingEvent);
            if (!pendingOpen)
                return;
            pendingOpen = false;
            application.Open();
        };

        Monitor.Log(helper.Translation.Get("log.loaded", new { version = ModManifest.Version }), LogLevel.Info);
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        bool galleryPressed = Config.GalleryKeys.JustPressed();
        if (replayService.ActiveIdentity is { } identity && replay.PlayingEvent is Event playing
            && (Config.ScreenshotKeys.JustPressed() || e.Button == SButton.MouseLeft && GetScreenshotButton().Contains(Game1.getMouseX(true), Game1.getMouseY(true))))
        {
            Helper.Input.SuppressActiveKeybinds(Config.ScreenshotKeys);
            Helper.Input.Suppress(e.Button);
            photos.Request(identity, playing);
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
            application.Close();
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
            application.Back();
            return;
        }
        if (Game1.activeClickableMenu is IGallerySearchMenu { IsSearchSelected: true } home)
        {
            if (e.Button == SButton.Escape)
                home.DeselectSearch();
            else if (e.Button == SButton.Enter)
                home.OpenFirstMatch();
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
                pendingOpen = true;
                return;
            }
        }
        if (!galleryPressed || !Context.IsWorldReady || !Context.IsPlayerFree || Game1.activeClickableMenu is not null)
            return;

        Helper.Input.SuppressActiveKeybinds(Config.GalleryKeys);
        application.Open();
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
            e.SpriteBatch.Draw(Game1.mouseCursors2, new Rectangle(camera.X + 10, camera.Y + 8, 36, 32), new Rectangle(72, 31, 18, 16), photos.Pending ? Color.Gray : Color.White);
            if (camera.Contains(Game1.getMouseX(true), Game1.getMouseY(true)))
                IClickableMenu.drawHoverText(e.SpriteBatch, Helper.Translation.Get("photo.capture"), Game1.smallFont);
        }
    }

    private static Rectangle GetReplaySpeedButton() => new(Game1.uiViewport.Width - 190, 24, 150, 48);
    private static Rectangle GetScreenshotButton() => new(Game1.uiViewport.Width - 258, 24, 56, 48);

    private void ToggleUnlock()
    {
        if (!Context.IsWorldReady || replay.IsActive) return;
        unlockAll = !unlockAll;
        Helper.Data.WriteSaveData("gallery-state", new GallerySaveData { UnlockAll = unlockAll });
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
