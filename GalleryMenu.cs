using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using static StardewGallery.GalleryDrawing;
using StardewGallery.Appearance;

namespace StardewGallery;

internal sealed class GalleryMenu : IClickableMenu, IGallerySearchMenu
{
    private const int Columns = 6;
    private const int VisibleRows = 3;
    private const int PageSize = Columns * VisibleRows;
    private const int SearchComponentId = 1000;
    private const int UnlockComponentId = 1001;
    private const int YesComponentId = 1002;
    private const int NoComponentId = 1003;
    private const int VolumeComponentId = 1004;
    private const int VolumeDropdownMaxVisible = 8;
    private const int VolumeDropdownItemHeight = 44;

    private sealed record VolumeChoice(string? Key, string Label);

    private readonly GalleryViewContext context;
    private readonly GalleryPageState state;
    private readonly GalleryCatalog baseCatalog;
    private GalleryCatalog catalog;
    private readonly bool hasVolumeSelector;
    private readonly VolumeChoice[] volumeAllChoices = [];
    private readonly Dictionary<string, string> characterVolumeKeys = new(StringComparer.Ordinal);
    private readonly ITranslationHelper i18n;
    private readonly Texture2D background;
    private readonly Func<bool> isUnlocked;
    private readonly TextBox search;
    private readonly GallerySearchFilter searchFilter = new();
    private string validatedSearchText = "";
    private List<GalleryCharacter> filtered = [];
    private int ResultCount => filtered.Count;
    private int currentPage;
    private bool confirming;
    private bool keyboardFocus;
    private Point lastMouse;
    private Rectangle searchBounds;
    private Rectangle volumeBounds;
    private Rectangle unlockBounds;
    private Rectangle yesBounds;
    private Rectangle noBounds;
    private int viewportWidth;
    private int viewportHeight;
    private float menuScale = 1f;
    private int drawOffsetX;
    private int drawOffsetY;
    private readonly List<ClickableComponent> cardComponents = [];
    private ClickableComponent? searchComponent;
    private ClickableComponent? volumeComponent;
    private ClickableComponent? unlockComponent;

    // In-book parchment dropdown overlay state for home volume selector.
    private bool volumeDropdownOpen;
    private VolumeChoice[] volumeChoices = [];
    private int volumeDropdownScroll;
    private int volumeDropdownSelectedIndex;
    private bool volumeDropdownSearchFocused;
    private bool draggingVolumeScroll;
    private ulong inputGeneration = 1;
    private ulong? overlayConsumedInputGeneration;
    private ulong? suppressLeftClickGeneration;
    private ulong? suppressRightClickGeneration;
    private Dictionary<Keys, ulong>? suppressedMappedKeyGenerations;
    private TextBox? volumeDropdownSearch;
    private string lastVolumeDropdownSearch = "";

    public bool IsSearchSelected => search.Selected || volumeDropdownSearch?.Selected == true;
    internal bool IsConfirming => confirming;

    internal GalleryMenu(GalleryViewContext context, GalleryPageState state)
        : base(0, 0, MenuWidth, MenuHeight, true)
    {
        this.context = context;
        this.state = state;
        HashSet<string> heartOwners = context.Catalog.Events
            .Where(entry => entry.Kind == StoryKind.Heart && !entry.IsControllerEvent)
            .SelectMany(entry => entry.Ownership.Owners.Select(owner => owner.Name))
            .ToHashSet(StringComparer.Ordinal);
        baseCatalog = new GalleryCatalog(
            context.Catalog.Characters.Where(character => heartOwners.Contains(character.Name)).ToArray(),
            context.Catalog.Events.Where(entry => entry.Kind == StoryKind.Heart && !entry.IsControllerEvent).ToArray(),
            []);
        catalog = baseCatalog;
        i18n = context.I18n;
        background = context.Textures.Home;
        isUnlocked = context.IsUnlocked;

        List<ShelfVolume> modVolumes = [];
        bool anyVanilla = false;
        bool anyUnknown = false;
        foreach (GalleryCharacter character in baseCatalog.Characters)
        {
            if (context.Catalog.CharacterVolumes.TryGetValue(character.Name, out ShelfVolume? vol))
            {
                if (vol.IsVanilla)
                {
                    anyVanilla = true;
                    characterVolumeKeys[character.Name] = StoryQuerySource.GameKey;
                }
                else
                {
                    modVolumes.Add(vol);
                    characterVolumeKeys[character.Name] = vol.Key;
                }
            }
            else
            {
                anyUnknown = true;
                characterVolumeKeys[character.Name] = StoryQuerySource.UnknownKey;
            }
        }
        ShelfVolume[] distinctMods = modVolumes.DistinctBy(v => v.Key, StringComparer.OrdinalIgnoreCase).ToArray();
        hasVolumeSelector = distinctMods.Length > 0;
        if (hasVolumeSelector)
        {
            HashSet<string> duplicateDisplayNames = distinctMods
                .GroupBy(v => v.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            List<VolumeChoice> choices = [new VolumeChoice(null, i18n.Get("home.volume-all"))];
            if (anyVanilla)
                choices.Add(new VolumeChoice(StoryQuerySource.GameKey, i18n.Get("home.volume-vanilla")));
            foreach (ShelfVolume modVol in distinctMods.OrderBy(v => v.DisplayName, StringComparer.CurrentCulture).ThenBy(v => v.Key, StringComparer.OrdinalIgnoreCase))
            {
                string label = duplicateDisplayNames.Contains(modVol.DisplayName)
                    && modVol.Key.StartsWith("mod:", StringComparison.OrdinalIgnoreCase)
                    ? $"{modVol.DisplayName} ({modVol.Key[4..]})"
                    : modVol.DisplayName;
                choices.Add(new VolumeChoice(modVol.Key, label));
            }
            if (anyUnknown)
                choices.Add(new VolumeChoice(StoryQuerySource.UnknownKey, i18n.Get("source.unknown")));

            volumeAllChoices = choices.ToArray();
            volumeChoices = volumeAllChoices;
            if (state.HomeVolumeKey is not null
                && !volumeAllChoices.Any(c => string.Equals(c.Key, state.HomeVolumeKey, StringComparison.OrdinalIgnoreCase)))
            {
                state.HomeVolumeKey = null;
            }
            ApplyVolumeFilter();
        }
        else
        {
            state.HomeVolumeKey = null;
        }

        search = new GalleryTextBox { DrawFrame = GalleryUiSkin.Get(GalleryUiSkin.SearchBoxAsset) is null };
        search.OnEnterPressed += _ => FinishSearch();
        search.Text = state.SearchText;
        RecalculateLayout();
        RefreshFilter();
        currentPage = GalleryPagination.ClampPage(state.Scroll, ResultCount, PageSize);
        PreparePortraits();
        BuildClickableComponents();
        currentlySnappedComponent = allClickableComponents.FirstOrDefault(component => component.myID == state.Focus)
            ?? cardComponents.FirstOrDefault() ?? searchComponent;
        if (Game1.options.snappyMenus && Game1.options.gamepadControls)
            snapCursorToCurrentSnappedComponent();
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
    {
        base.gameWindowSizeChanged(oldBounds, newBounds);
        RecalculateLayout();
    }

    public override void performHoverAction(int x, int y)
    {
        if (lastMouse != new Point(x, y))
        {
            if (lastMouse != default) keyboardFocus = false;
            lastMouse = new Point(x, y);
            if (volumeDropdownOpen)
            {
                (int lx, int ly) = ToLogical(x, y);
                var layout = ComputeVolumeDropdownLayout();
                int visCount = Math.Min(VolumeDropdownMaxVisible, volumeChoices.Length);
                for (int i = 0; i < visCount; i++)
                {
                    if (VolumeDropdownItemRect(layout, i).Contains(lx, ly))
                    {
                        volumeDropdownSelectedIndex = volumeDropdownScroll + i;
                        volumeDropdownSearchFocused = false;
                        break;
                    }
                }
            }
        }
        base.performHoverAction(x, y);
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (ConsumeLeftClickSuppression())
            return;
        (x, y) = ToLogical(x, y);
        if (upperRightCloseButton?.bounds.Contains(x, y) == true)
        {
            CloseVolumeDropdown(playSound: false);
            SaveState();
            DeselectSearch();
            context.Navigation.Close();
            return;
        }
        if (confirming)
        {
            if (yesBounds.Contains(x, y))
            {
                SaveState();
                context.Navigation.ToggleUnlock();
                confirming = false;
                BuildClickableComponents();
                SnapForGamepad();
                ulong frameGen = CurrentFrameSuppressionGeneration();
                suppressRightClickGeneration = frameGen;
                SuppressMappedKey(Buttons.Y, frameGen);
                Game1.playSound("coin");
            }
            else if (noBounds.Contains(x, y))
            {
                confirming = false;
                BuildClickableComponents();
                SnapForGamepad();
                ulong frameGen = CurrentFrameSuppressionGeneration();
                suppressRightClickGeneration = frameGen;
                SuppressMappedKey(Buttons.Y, frameGen);
                Game1.playSound("bigDeSelect");
            }
            return;
        }

        if (volumeDropdownOpen)
        {
            HandleVolumeDropdownLeftClick(x, y);
            return;
        }

        if (GalleryBookChrome.TryClickRibbonTab(x, y, GalleryBookTab.Villagers, context.Navigation, () =>
            {
                SaveState();
                DeselectSearch();
            }))
        {
            return;
        }

        if (unlockBounds.Contains(x, y))
        {
            confirming = true;
            DeselectSearch();
            BuildClickableComponents();
            SnapForGamepad();
            ulong frameGen = CurrentFrameSuppressionGeneration();
            suppressRightClickGeneration = frameGen;
            SuppressMappedKey(Buttons.Y, frameGen);
            Game1.playSound("smallSelect");
            return;
        }
        if (hasVolumeSelector && volumeBounds.Contains(x, y))
        {
            DeselectSearch();
            OpenVolumeDropdown();
            if (volumeDropdownOpen)
            {
                ulong frameGen = CurrentFrameSuppressionGeneration();
                suppressRightClickGeneration = frameGen;
                SuppressMappedKey(Buttons.Y, frameGen);
            }
            return;
        }
        if (searchBounds.Contains(x, y))
        {
            GalleryTextBox.SelectForInput(search);
            if (search.Selected)
            {
                ulong frameGen = CurrentFrameSuppressionGeneration();
                suppressRightClickGeneration = frameGen;
                SuppressMappedKey(Buttons.Y, frameGen);
            }
            return;
        }
        DeselectSearch();

        if (GalleryBookChrome.ToRect(GallerySpreadLayout.SpreadPrevBounds).Contains(x, y))
        {
            TurnPage(-1);
            return;
        }
        if (GalleryBookChrome.ToRect(GallerySpreadLayout.SpreadNextBounds).Contains(x, y))
        {
            TurnPage(1);
            return;
        }

        int first = GalleryPagination.FirstIndex(currentPage, ResultCount, PageSize);
        int visible = GalleryPagination.VisibleCount(currentPage, ResultCount, PageSize);
        for (int slot = 0; slot < visible; slot++)
        {
            if (!Card(slot).Contains(x, y))
                continue;
            GalleryCharacter character = filtered[first + slot];
            if (!character.IsMet && !isUnlocked())
                return;
            Game1.playSound("smallSelect");
            SaveState(slot);
            context.Navigation.OpenCharacter(character.Name);
            return;
        }
    }

    public override void leftClickHeld(int x, int y)
    {
        base.leftClickHeld(x, y);
        if (!volumeDropdownOpen || !draggingVolumeScroll)
            return;
        (_, int ly) = ToLogical(x, y);
        ScrollVolumeDropdownToMouseY(ly);
    }

    public override void releaseLeftClick(int x, int y)
    {
        base.releaseLeftClick(x, y);
        draggingVolumeScroll = false;
    }

    public override void receiveScrollWheelAction(int direction)
    {
        if (volumeDropdownOpen)
        {
            int maxScroll = Math.Max(0, volumeChoices.Length - VolumeDropdownMaxVisible);
            int next = Math.Clamp(volumeDropdownScroll + (direction < 0 ? 1 : -1), 0, maxScroll);
            if (next != volumeDropdownScroll)
            {
                volumeDropdownScroll = next;
                Game1.playSound("shiny4");
            }
            return;
        }
        TurnPage(direction < 0 ? 1 : -1);
    }

    public override void receiveKeyPress(Keys key)
    {
        if (ConsumeSuppressedMappedKey(key))
            return;
        if (volumeDropdownOpen)
        {
            if (volumeDropdownSearch?.Selected == true)
            {
                if (key == Keys.Escape)
                    CloseVolumeDropdown(playSound: true);
                else if (key == Keys.Down && volumeChoices.Length > 0)
                {
                    DeselectSearch();
                    volumeDropdownSearchFocused = false;
                    volumeDropdownSelectedIndex = 0;
                    EnsureVolumeDropdownVisible();
                }
                return;
            }
            if (key == Keys.Escape || Game1.options.menuButton?.Any(binding => binding.key == key) == true)
            {
                CloseVolumeDropdown(playSound: true);
                return;
            }
            if (key is Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown or Keys.Enter)
            {
                keyboardFocus = true;
                HandleVolumeDropdownKey(key);
                return;
            }
            if (Game1.options.moveUpButton is { } upBindings && Game1.options.doesInputListContain(upBindings, key))
            {
                keyboardFocus = true;
                HandleVolumeDropdownKey(Keys.Up);
                return;
            }
            if (Game1.options.moveDownButton is { } downBindings && Game1.options.doesInputListContain(downBindings, key))
            {
                keyboardFocus = true;
                HandleVolumeDropdownKey(Keys.Down);
                return;
            }
            return;
        }

        if (search.Selected)
        {
            if (key == Keys.Escape)
                DeselectSearch();
            return;
        }
        if (key == Keys.Escape || Game1.options.menuButton.Any(binding => binding.key == key))
        {
            HandleControllerBack();
            return;
        }
        if (key is Keys.PageUp or Keys.PageDown)
        {
            keyboardFocus = true;
            TurnPage(key == Keys.PageUp ? -1 : 1);
            return;
        }
        if (key is Keys.Up or Keys.Right or Keys.Down or Keys.Left)
        {
            keyboardFocus = true;
            applyMovementKey(key switch { Keys.Up => 0, Keys.Right => 1, Keys.Down => 2, _ => 3 });
            RefreshFilter();
            return;
        }
        if (key == Keys.Enter)
        {
            keyboardFocus = true;
            RefreshFilter();
            ActivateFocused();
            return;
        }
        base.receiveKeyPress(key);
        RefreshFilter();
    }

    public void HandleControllerBack()
    {
        if (volumeDropdownOpen)
        {
            CloseVolumeDropdown(playSound: true);
            return;
        }
        if (search.Selected)
        {
            DeselectSearch();
            Game1.playSound("bigDeSelect");
            return;
        }
        if (confirming)
        {
            confirming = false;
            BuildClickableComponents();
            SnapForGamepad();
            Game1.playSound("bigDeSelect");
            return;
        }
        SaveState();
        context.Navigation.Back();
        Game1.playSound("bigDeSelect");
    }

    public override void update(GameTime time)
    {
        PurgeStaleInputSuppressions();
        inputGeneration++;
        base.update(time);
        RefreshFilter();
        RefreshVolumeDropdownSearch();
        PreparePortraits();
        SaveState();
    }

    public override void receiveRightClick(int x, int y, bool playSound = true)
    {
        if (ConsumeRightClickSuppression())
            return;
        HandleControllerBack();
    }

    public override void receiveGamePadButton(Buttons button)
    {
        if (overlayConsumedInputGeneration == inputGeneration)
        {
            SuppressMappedKey(button);
            if (button == Buttons.A)
                suppressLeftClickGeneration = inputGeneration;
            else if (button == Buttons.X)
                suppressRightClickGeneration = inputGeneration;
            return;
        }
        if (volumeDropdownOpen)
        {
            if (button is Buttons.B or Buttons.Y)
            {
                SuppressMappedKey(button);
                CloseVolumeDropdown(playSound: true);
                overlayConsumedInputGeneration = inputGeneration;
                return;
            }
            if (button == Buttons.A)
            {
                suppressLeftClickGeneration = inputGeneration;
                SuppressMappedKey(button);
                ActivateVolumeDropdownSelection();
                overlayConsumedInputGeneration = inputGeneration;
                return;
            }
            if (button is Buttons.LeftShoulder or Buttons.RightShoulder)
            {
                SuppressMappedKey(button);
                MoveVolumeDropdownSelection(button == Buttons.LeftShoulder ? -VolumeDropdownMaxVisible : VolumeDropdownMaxVisible);
                return;
            }
            if (button is Buttons.DPadUp or Buttons.LeftThumbstickUp)
            {
                SuppressMappedKey(button);
                if (volumeDropdownSearch?.Selected != true)
                {
                    keyboardFocus = true;
                    HandleVolumeDropdownKey(Keys.Up);
                }
                return;
            }
            if (button is Buttons.DPadDown or Buttons.LeftThumbstickDown)
            {
                SuppressMappedKey(button);
                if (volumeDropdownSearch?.Selected == true)
                {
                    if (volumeChoices.Length > 0)
                    {
                        DeselectSearch();
                        volumeDropdownSearchFocused = false;
                        volumeDropdownSelectedIndex = 0;
                        EnsureVolumeDropdownVisible();
                    }
                }
                else
                {
                    keyboardFocus = true;
                    HandleVolumeDropdownKey(Keys.Down);
                }
                return;
            }
            suppressRightClickGeneration = inputGeneration;
            SuppressMappedKey(button);
            return;
        }
        if (button == Buttons.B)
        {
            bool wasConfirming = confirming;
            bool wasSearchSelected = search.Selected;
            SuppressMappedKey(button);
            HandleControllerBack();
            if ((wasConfirming && !confirming) || (wasSearchSelected && !search.Selected))
            {
                overlayConsumedInputGeneration = inputGeneration;
                suppressLeftClickGeneration = inputGeneration;
                suppressRightClickGeneration = inputGeneration;
            }
            return;
        }
        if (!confirming && button is Buttons.LeftShoulder or Buttons.RightShoulder)
        {
            SuppressMappedKey(button);
            DeselectSearch();
            TurnPage(button == Buttons.LeftShoulder ? -1 : 1);
            return;
        }
        base.receiveGamePadButton(button);
    }

    private ulong CurrentFrameSuppressionGeneration()
        => inputGeneration > 0 ? inputGeneration - 1 : 0;

    private void SuppressMappedKey(Buttons button, ulong? generation = null)
    {
        Keys mapped = Utility.mapGamePadButtonToKey(button);
        if (mapped != Keys.None)
            (suppressedMappedKeyGenerations ??= new Dictionary<Keys, ulong>())[mapped] = generation ?? inputGeneration;
    }

    private bool ConsumeLeftClickSuppression()
    {
        bool active = suppressLeftClickGeneration is { } gen && gen + 1 >= inputGeneration;
        suppressLeftClickGeneration = null;
        return active;
    }

    private bool ConsumeRightClickSuppression()
    {
        bool active = suppressRightClickGeneration is { } gen && gen + 1 >= inputGeneration;
        suppressRightClickGeneration = null;
        return active;
    }

    private bool ConsumeSuppressedMappedKey(Keys key)
    {
        if (suppressedMappedKeyGenerations is null || !suppressedMappedKeyGenerations.TryGetValue(key, out ulong gen))
            return false;
        return gen + 1 >= inputGeneration;
    }

    private void PurgeStaleInputSuppressions()
    {
        if (overlayConsumedInputGeneration is { } overlayGen && overlayGen < inputGeneration)
            overlayConsumedInputGeneration = null;
        if (suppressLeftClickGeneration is { } leftGen && leftGen < inputGeneration)
            suppressLeftClickGeneration = null;
        if (suppressRightClickGeneration is { } rightGen && rightGen < inputGeneration)
            suppressRightClickGeneration = null;
        if (suppressedMappedKeyGenerations is not { Count: > 0 })
            return;
        List<Keys>? expired = null;
        foreach (KeyValuePair<Keys, ulong> pair in suppressedMappedKeyGenerations)
        {
            if (pair.Value < inputGeneration)
                (expired ??= []).Add(pair.Key);
        }
        if (expired is not null)
        {
            foreach (Keys key in expired)
                suppressedMappedKeyGenerations.Remove(key);
        }
    }

    public override void applyMovementKey(int direction)
    {
        if (volumeDropdownOpen)
        {
            if (volumeDropdownSearch?.Selected == true || direction is < 0 or > 3)
                return;
            if (direction == 0)
                HandleVolumeDropdownKey(Keys.Up);
            else if (direction == 2)
                HandleVolumeDropdownKey(Keys.Down);
            return;
        }
        base.applyMovementKey(direction);
    }

    // Game1 repopulates components by reflection when input switches to a controller; rebuild the gallery's own instead.
    public override void populateClickableComponentList() => BuildClickableComponents();

    public override void snapCursorToCurrentSnappedComponent()
    {
        if (Game1.options.snappyMenus && Game1.options.gamepadControls)
            base.snapCursorToCurrentSnappedComponent();
    }

    public override void snapToDefaultClickableComponent()
    {
        currentlySnappedComponent = confirming
            ? allClickableComponents?.FirstOrDefault(component => component.myID == YesComponentId)
            : cardComponents.FirstOrDefault() ?? searchComponent;
        snapCursorToCurrentSnappedComponent();
    }

    public override void draw(SpriteBatch b)
    {
        EnsureLayout();
        b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * .45f);
        if (confirming)
            b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * .7f);

        (int mouseX, int mouseY) = ToLogical(Game1.getMouseX(true), Game1.getMouseY(true));
        bool showFocus = Game1.options.snappyMenus && Game1.options.gamepadControls || keyboardFocus;
        int focusedId = currentlySnappedComponent?.myID ?? -1;
        bool overlayActive = confirming || volumeDropdownOpen;

        BeginScaled(b, menuScale, drawOffsetX, drawOffsetY);
        GalleryBookChrome.DrawCleanHomeSpread(b, background, width, height);
        GalleryBookChrome.DrawRibbonTabs(b, GalleryBookTab.Villagers, focusedId, showFocus && !overlayActive,
            overlayActive ? new Point(-999, -999) : new Point(mouseX, mouseY), i18n, out string? hoverTooltip);
        GalleryBookChrome.DrawSpreadTitle(b, i18n.Get("home.title"), menuScale, drawOffsetX, drawOffsetY);

        bool unlockHot = !overlayActive && (unlockBounds.Contains(mouseX, mouseY) || showFocus && focusedId == UnlockComponentId);
        DrawButton(b, unlockBounds, i18n.Get(isUnlocked() ? "menu.restore-locks" : "menu.unlock-all"), unlockHot);

        bool searchHot = !overlayActive && (search.Selected || searchBounds.Contains(mouseX, mouseY) || showFocus && focusedId == SearchComponentId);
        GalleryUiSkin.DrawSearchBox(b, searchBounds, searchHot);
        search.Draw(b);
        if (search.Text.Length == 0 && !search.Selected)
            DrawLeftFitted(b, i18n.Get("home.search"), GalleryDrawing.Inset(GalleryUiSkin.SearchTextBounds(searchBounds), 4, 10), GallerySpreadDrawing.LightInk);

        if (hasVolumeSelector)
        {
            bool volumeHot = !overlayActive && (volumeBounds.Contains(mouseX, mouseY) || showFocus && focusedId == VolumeComponentId);
            GalleryUiSkin.DrawFilterRow(b, volumeBounds, volumeHot || volumeDropdownOpen);
            if (showFocus && !overlayActive && focusedId == VolumeComponentId)
                GalleryUiSkin.DrawFocusCorners(b, GalleryDrawing.Inset(volumeBounds, -4, -4), 12);
            int arrowW = GalleryUiSkin.FilterRowArrowWidth(volumeBounds);
            float vScale = Math.Min(0.92f, (volumeBounds.Height - 10f) / Game1.smallFont.LineSpacing);
            string volText = i18n.Get("home.volume-label", new { volume = ActiveVolumeDisplayName() });
            GalleryDrawing.DrawEllipsized(b, volText, new Rectangle(volumeBounds.X + 18, volumeBounds.Y, Math.Max(40, volumeBounds.Width - 18 - arrowW), volumeBounds.Height), vScale);
        }

        int first = GalleryPagination.FirstIndex(currentPage, ResultCount, PageSize);
        int visible = GalleryPagination.VisibleCount(currentPage, ResultCount, PageSize);
        for (int slot = 0; slot < visible; slot++)
        {
            Rectangle card = Card(slot);
            GalleryCharacter character = filtered[first + slot];
            bool hot = !overlayActive && (card.Contains(mouseX, mouseY) || showFocus && focusedId == slot);
            DrawCharacter(b, slot, character, hot);
            if (hot)
            {
                string? cardTip = CharacterTooltip(character);
                if (cardTip is not null)
                    hoverTooltip = cardTip;
            }
        }
        if (ResultCount == 0 && search.Text.Trim().Length > 0)
            DrawCentered(b, GalleryDrawing.ParseTextSafe(i18n.Get("query.home-empty"), Game1.smallFont, 540), R(185, 290, 480, 180));

        GalleryBookChrome.DrawPager(
            b,
            GallerySpreadLayout.SpreadPrevBounds,
            GallerySpreadLayout.SpreadIndicatorBounds,
            GallerySpreadLayout.SpreadNextBounds,
            currentPage,
            ResultCount,
            PageSize,
            focusedId,
            showFocus && !overlayActive,
            overlayActive ? null : new Point(mouseX, mouseY));

        if (volumeDropdownOpen)
            DrawVolumeDropdownOverlay(b, mouseX, mouseY, showFocus);

        upperRightCloseButton?.draw(b);
        if (confirming)
            DrawConfirmation(b, mouseX, mouseY, showFocus, focusedId);
        EndScaled(b);
        if (!overlayActive && hoverTooltip is not null)
            GalleryDrawing.DrawHoverTextSafe(b, GalleryDrawing.ParseTextSafe(hoverTooltip, Game1.smallFont, Math.Max(200, Math.Min(520, viewportWidth - 80))), Game1.smallFont);
        drawMouse(b);
    }

    protected override void cleanupBeforeExit()
    {
        DeselectSearch();
        base.cleanupBeforeExit();
    }

    private void TurnPage(int delta)
    {
        var (nextPage, changed) = GalleryPagination.Turn(currentPage, delta, ResultCount, PageSize);
        if (!changed)
            return;
        currentPage = nextPage;
        PreparePortraits();
        BuildClickableComponents();
        if (Game1.options.snappyMenus && Game1.options.gamepadControls)
            snapCursorToCurrentSnappedComponent();
        Game1.playSound("shwip");
    }

    private void RecalculateLayout()
    {
        width = MenuWidth;
        height = MenuHeight;
        xPositionOnScreen = 0;
        yPositionOnScreen = 0;
        menuScale = (float)GalleryLayout.ScaleToFit(Game1.uiViewport.Width, Game1.uiViewport.Height, width, height, 24);
        drawOffsetX = (int)Math.Round((Game1.uiViewport.Width - width * menuScale) / 2f);
        drawOffsetY = (int)Math.Round((Game1.uiViewport.Height - height * menuScale) / 2f);
        viewportWidth = Game1.uiViewport.Width;
        viewportHeight = Game1.uiViewport.Height;
        searchBounds = GalleryBookChrome.ToRect(GallerySpreadLayout.HomeSearchBounds);
        volumeBounds = GalleryBookChrome.ToRect(GallerySpreadLayout.HomeVolumeBounds);
        UpdateVolumeDropdownSearchBounds();
        Rectangle textArea = GalleryUiSkin.SearchTextBounds(searchBounds);
        search.X = textArea.X - 12;
        search.Y = searchBounds.Y;
        search.Width = textArea.Width + 12;
        search.Height = searchBounds.Height;
        unlockBounds = GalleryBookChrome.ToRect(GallerySpreadLayout.HomeUnlockBounds);
        yesBounds = R(650, 540, 160, 56);
        noBounds = R(860, 540, 160, 56);
        initializeUpperRightCloseButton();
        GalleryBookChrome.PositionCloseButton(upperRightCloseButton, menuScale, drawOffsetX, drawOffsetY, viewportWidth, viewportHeight);
        BuildClickableComponents();
    }

    private static Rectangle Card(int slot) => GalleryBookChrome.ToRect(GallerySpreadLayout.HomeCardBounds(slot));

    /// <summary>Grid position of a page slot: columns 0-2 on the left page, 3-5 on the right page.</summary>
    private static (int Column, int Row) GridOf(int slot) => slot < 9 ? (slot % 3, slot / 3) : (3 + (slot - 9) % 3, (slot - 9) / 3);

    private static int SlotAt(int column, int row) => column < 3 ? row * 3 + column : 9 + row * 3 + column - 3;

    private void DrawCharacter(SpriteBatch b, int slot, GalleryCharacter character, bool hot)
    {
        bool known = character.IsMet || isUnlocked();
        Rectangle portraitBounds = GalleryBookChrome.ToRect(GallerySpreadLayout.HomePortraitBounds(slot));
        Rectangle card = Card(slot);

        context.Appearance.Draw(b, character.Name, CharacterVisual.Portrait,
            portraitBounds, known ? Color.White : new Color(92, 60, 36) * 0.92f);
        if (hot)
            GalleryUiSkin.DrawFocusCorners(b, GalleryDrawing.Inset(portraitBounds, -8, -8), 16);
        string name = known ? character.DisplayName : i18n.Get("home.unmet").ToString();
        DrawCentered(b, name, new Rectangle(card.X, portraitBounds.Bottom + 4, card.Width, 28), known ? Game1.textColor : GallerySpreadDrawing.LightInk);

        if (!known)
            return;

        StoryNodeProgress progress = context.CharacterProgress(character.Name);
        string progressLabel = $"{progress.CollectedNodes} / {progress.TotalNodes}";
        Rectangle progressBounds = new(card.X + 20, portraitBounds.Bottom + 32, card.Width - 40, 24);
        DrawCentered(b, progressLabel, progressBounds, GallerySpreadDrawing.Ink);
        if (progress.IsFullyCollected)
        {
            Vector2 size = GalleryDrawing.MeasureStringSafe(Game1.smallFont, progressLabel);
            float scale = GalleryTextFit.Scale(size.X, size.Y, progressBounds.Width, progressBounds.Height);
            int textLeft = progressBounds.Center.X - (int)(size.X * scale / 2);
            GalleryBookChrome.DrawGoldStarBadge(b, new Rectangle(textLeft - 26, progressBounds.Center.Y - 11, 22, 22));
        }

        EncounterClue clue = context.CharacterClue(character.Name);
        if (GalleryUiSkin.ClueBadge(clue) is { } badge)
            GalleryUiSkin.DrawBadge(b, badge, new Rectangle(portraitBounds.Right - 12, portraitBounds.Y - 12, 30, 30));
    }

    private string? CharacterTooltip(GalleryCharacter character)
    {
        if (!character.IsMet && !isUnlocked())
            return null;

        List<string> lines = [];
        StoryNodeProgress progress = context.CharacterProgress(character.Name);
        if (progress.IsFullyCollected)
            lines.Add(i18n.Get("home.full-collection", new { name = character.DisplayName }));

        EncounterClue clue = context.CharacterClue(character.Name);
        if (clue == EncounterClue.AvailableToday)
            lines.Add(i18n.Get("clue.available-today-hint"));
        else if (clue == EncounterClue.ReadyForTiming)
            lines.Add(i18n.Get("clue.ready-for-timing-hint"));

        return lines.Count > 0 ? string.Join("\n", lines) : null;
    }

    private void PreparePortraits()
    {
        int first = GalleryPagination.FirstIndex(currentPage, ResultCount, PageSize);
        int visible = GalleryPagination.VisibleCount(currentPage, ResultCount, PageSize);
        for (int i = first; i < first + visible; i++)
            context.Appearance.Prepare(filtered[i].Name, CharacterVisual.Portrait);
    }

    private void RefreshFilter()
    {
        if (search.Text != validatedSearchText)
        {
            search.Text = GallerySearchInput.CleanText(search.Text);
            validatedSearchText = search.Text;
        }
        bool changed = searchFilter.Update(catalog, search.Text, i18n.Locale,
            LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.zh, context.Pinyin);
        filtered = searchFilter.Results;
        if (!changed)
            return;
        currentPage = 0;
        PreparePortraits();
        BuildClickableComponents();
    }

    public void FinishSearch()
    {
        RefreshFilter();
        DeselectSearch();
        // Show the matching characters with the first one focused; a second Enter or A opens it.
        currentlySnappedComponent = cardComponents.FirstOrDefault() ?? searchComponent;
        if (Game1.options.snappyMenus && Game1.options.gamepadControls)
            snapCursorToCurrentSnappedComponent();
    }

    private void ActivateFocused()
    {
        int id = currentlySnappedComponent?.myID ?? -1;
        if (confirming)
        {
            if (id == YesComponentId)
            {
                SaveState();
                context.Navigation.ToggleUnlock();
                confirming = false;
                BuildClickableComponents();
                Game1.playSound("coin");
            }
            else if (id == NoComponentId)
            {
                confirming = false;
                BuildClickableComponents();
                Game1.playSound("bigDeSelect");
            }
            return;
        }

        if (id >= GalleryBookChrome.RibbonTabBaseId && id < GalleryBookChrome.RibbonTabBaseId + GallerySpreadLayout.RibbonTabCount)
        {
            SaveState();
            DeselectSearch();
            Game1.playSound("shwip");
            context.Navigation.SwitchTab((GalleryBookTab)(id - GalleryBookChrome.RibbonTabBaseId));
            return;
        }
        if (id == GalleryBookChrome.PrevPageComponentId) { TurnPage(-1); return; }
        if (id == GalleryBookChrome.NextPageComponentId) { TurnPage(1); return; }
        if (id == UnlockComponentId)
        {
            confirming = true;
            DeselectSearch();
            BuildClickableComponents();
            Game1.playSound("smallSelect");
            return;
        }
        if (id == SearchComponentId)
        {
            GalleryTextBox.SelectForInput(search);
            return;
        }
        if (hasVolumeSelector && id == VolumeComponentId)
        {
            DeselectSearch();
            OpenVolumeDropdown();
            return;
        }

        int slot = id is >= 0 and < PageSize ? id : 0;
        int index = GalleryPagination.FirstIndex(currentPage, ResultCount, PageSize) + slot;
        if (index >= ResultCount)
            return;
        GalleryCharacter character = filtered[index];
        if (!character.IsMet && !isUnlocked())
            return;
        SaveState(slot);
        DeselectSearch();
        context.Navigation.OpenCharacter(character.Name);
    }

    private void SaveState(int? focus = null)
    {
        state.SearchText = search.Text;
        state.Scroll = currentPage;
        state.Focus = focus ?? currentlySnappedComponent?.myID ?? -1;
    }

    private void BuildClickableComponents()
    {
        int previousId = currentlySnappedComponent?.myID ?? -1;
        allClickableComponents = [];
        cardComponents.Clear();
        volumeComponent = null;
        if (confirming)
        {
            ClickableComponent yes = new(ToScreen(yesBounds), "yes") { myID = YesComponentId, rightNeighborID = NoComponentId, fullyImmutable = true };
            ClickableComponent no = new(ToScreen(noBounds), "no") { myID = NoComponentId, leftNeighborID = YesComponentId, fullyImmutable = true };
            allClickableComponents.Add(yes);
            allClickableComponents.Add(no);
        }
        else
        {
            currentPage = GalleryPagination.ClampPage(currentPage, ResultCount, PageSize);
            int visible = GalleryPagination.VisibleCount(currentPage, ResultCount, PageSize);
            int SlotOrNone(int column, int row)
            {
                if (column < 0 || column > 5 || row < 0 || row > 2) return -1;
                int s = SlotAt(column, row);
                return s < visible ? s : -1;
            }
            // Lowest visible card in a column, used when moving up from the ribbons, pager or unlock button.
            int LowestIn(int column)
            {
                for (int row = 2; row >= 0; row--)
                    if (SlotOrNone(column, row) is >= 0 and var s) return s;
                return visible > 0 ? (column < 3 ? 0 : Math.Min(9, visible - 1)) : (hasVolumeSelector && column >= 3 ? VolumeComponentId : SearchComponentId);
            }

            GalleryBookChrome.AddRibbonTabComponents(allClickableComponents, ToScreen, GalleryBookChrome.PrevPageComponentId,
                upNeighborId: LowestIn(0));
            foreach (ClickableComponent ribbon in allClickableComponents)
                ribbon.upNeighborID = LowestIn(ribbon.myID - GalleryBookChrome.RibbonTabBaseId);

            searchComponent = new ClickableComponent(ToScreen(searchBounds), "search")
            {
                myID = SearchComponentId,
                rightNeighborID = hasVolumeSelector ? VolumeComponentId : visible > 9 ? 9 : upperRightCloseButton?.myID ?? -1,
                downNeighborID = visible > 0 ? 0 : GalleryBookChrome.RibbonTabBaseId,
                fullyImmutable = true
            };
            if (hasVolumeSelector)
            {
                volumeComponent = new ClickableComponent(ToScreen(volumeBounds), "volume")
                {
                    myID = VolumeComponentId,
                    leftNeighborID = SearchComponentId,
                    rightNeighborID = upperRightCloseButton?.myID ?? -1,
                    downNeighborID = visible > 9 ? 9 : visible > 0 ? 0 : UnlockComponentId,
                    upNeighborID = -1,
                    fullyImmutable = true
                };
            }
            unlockComponent = new ClickableComponent(ToScreen(unlockBounds), "unlock")
            {
                myID = UnlockComponentId,
                leftNeighborID = GalleryBookChrome.NextPageComponentId,
                rightNeighborID = -1,
                upNeighborID = LowestIn(5),
                downNeighborID = -1,
                fullyImmutable = true
            };
            allClickableComponents.Add(searchComponent);
            if (volumeComponent is not null)
                allClickableComponents.Add(volumeComponent);
            allClickableComponents.Add(unlockComponent);
            allClickableComponents.Add(new ClickableComponent(ToScreen(GalleryBookChrome.ToRect(GallerySpreadLayout.SpreadPrevBounds)), "prev-page")
            {
                myID = GalleryBookChrome.PrevPageComponentId,
                upNeighborID = LowestIn(3),
                leftNeighborID = GalleryBookChrome.RibbonTabBaseId + 2,
                rightNeighborID = GalleryBookChrome.NextPageComponentId,
                fullyImmutable = true
            });
            allClickableComponents.Add(new ClickableComponent(ToScreen(GalleryBookChrome.ToRect(GallerySpreadLayout.SpreadNextBounds)), "next-page")
            {
                myID = GalleryBookChrome.NextPageComponentId,
                upNeighborID = LowestIn(4),
                leftNeighborID = GalleryBookChrome.PrevPageComponentId,
                rightNeighborID = UnlockComponentId,
                fullyImmutable = true
            });

            for (int slot = 0; slot < visible; slot++)
            {
                (int col, int row) = GridOf(slot);
                int left = SlotOrNone(col - 1, row);
                if (left < 0 && col == 3) left = SlotOrNone(2, row) is >= 0 and var l ? l : LowestIn(2);
                int right = SlotOrNone(col + 1, row);
                if (right < 0 && col == 2) right = SlotOrNone(3, row) is >= 0 and var r ? r : (visible > 9 ? 9 : -1);
                int down = SlotOrNone(col, row + 1);
                if (down < 0)
                    down = col < 3 ? GalleryBookChrome.RibbonTabBaseId + col : col == 3 ? GalleryBookChrome.PrevPageComponentId
                        : col == 4 ? GalleryBookChrome.NextPageComponentId : UnlockComponentId;
                int up = SlotOrNone(col, row - 1);
                if (up < 0)
                    up = hasVolumeSelector && col >= 3 ? VolumeComponentId : SearchComponentId;
                ClickableComponent card = new(ToScreen(Card(slot)), $"character-{slot}")
                {
                    myID = slot,
                    leftNeighborID = left,
                    rightNeighborID = right,
                    upNeighborID = up,
                    downNeighborID = down,
                    fullyImmutable = true
                };
                cardComponents.Add(card);
                allClickableComponents.Add(card);
            }
        }
        if (upperRightCloseButton is not null)
        {
            upperRightCloseButton.leftNeighborID = confirming ? YesComponentId : hasVolumeSelector ? VolumeComponentId : SearchComponentId;
            upperRightCloseButton.downNeighborID = confirming ? YesComponentId : cardComponents.ElementAtOrDefault(9)?.myID ?? UnlockComponentId;
            allClickableComponents.Add(new ClickableComponent(ToScreen(upperRightCloseButton.bounds), upperRightCloseButton.name)
            {
                myID = upperRightCloseButton.myID,
                leftNeighborID = upperRightCloseButton.leftNeighborID,
                downNeighborID = upperRightCloseButton.downNeighborID,
                fullyImmutable = true
            });
        }
        currentlySnappedComponent = allClickableComponents.FirstOrDefault(component => component.myID == previousId)
            ?? cardComponents.FirstOrDefault()
            ?? searchComponent;
    }

    private void SnapForGamepad()
    {
        if (Game1.options.snappyMenus && Game1.options.gamepadControls)
            snapToDefaultClickableComponent();
    }

    public void DeselectSearch()
    {
        search.Selected = false;
        if (Game1.keyboardDispatcher.Subscriber == search)
            Game1.keyboardDispatcher.Subscriber = null;
        if (volumeDropdownSearch is not null)
        {
            volumeDropdownSearch.Selected = false;
            if (Game1.keyboardDispatcher.Subscriber == volumeDropdownSearch)
                Game1.keyboardDispatcher.Subscriber = null;
        }
        currentlySnappedComponent = volumeDropdownOpen ? volumeComponent ?? searchComponent : searchComponent;
        if (Game1.options.snappyMenus && Game1.options.gamepadControls)
            snapCursorToCurrentSnappedComponent();
    }

    private void ApplyVolumeFilter()
    {
        if (!hasVolumeSelector || state.HomeVolumeKey is null)
        {
            catalog = baseCatalog;
            return;
        }
        GalleryCharacter[] matchingChars = baseCatalog.Characters
            .Where(c => characterVolumeKeys.TryGetValue(c.Name, out string? key)
                && string.Equals(key, state.HomeVolumeKey, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        HashSet<string> allowed = matchingChars.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
        GalleryEvent[] matchingEvents = baseCatalog.Events
            .Where(e => e.Ownership.Owners.Any(o => allowed.Contains(o.Name)))
            .ToArray();
        catalog = new GalleryCatalog(matchingChars, matchingEvents, []);
    }

    private string ActiveVolumeDisplayName()
        => volumeAllChoices.FirstOrDefault(c => string.Equals(c.Key, state.HomeVolumeKey, StringComparison.OrdinalIgnoreCase))?.Label
            ?? i18n.Get("home.volume-all");

    private void OpenVolumeDropdown()
    {
        if (!hasVolumeSelector || volumeAllChoices.Length == 0)
            return;
        DeselectSearch();
        volumeDropdownOpen = true;
        volumeChoices = volumeAllChoices;
        lastVolumeDropdownSearch = "";
        draggingVolumeScroll = false;
        volumeDropdownSearchFocused = false;

        if (volumeAllChoices.Length > VolumeDropdownMaxVisible)
        {
            volumeDropdownSearch ??= CreateVolumeDropdownSearchBox();
            volumeDropdownSearch.Text = "";
        }
        else
        {
            volumeDropdownSearch = null;
        }

        int currentIdx = Array.FindIndex(volumeChoices, c => string.Equals(c.Key, state.HomeVolumeKey, StringComparison.OrdinalIgnoreCase));
        volumeDropdownSelectedIndex = Math.Max(0, currentIdx);
        volumeDropdownScroll = 0;
        EnsureVolumeDropdownVisible();
        UpdateVolumeDropdownSearchBounds();
        Game1.playSound("shwip");
    }

    private void CloseVolumeDropdown(bool playSound)
    {
        if (!volumeDropdownOpen)
            return;
        DeselectSearch();
        volumeDropdownOpen = false;
        volumeChoices = volumeAllChoices;
        volumeDropdownSearchFocused = false;
        draggingVolumeScroll = false;
        currentlySnappedComponent = volumeComponent ?? searchComponent;
        if (Game1.options.snappyMenus && Game1.options.gamepadControls)
            snapCursorToCurrentSnappedComponent();
        if (playSound)
            Game1.playSound("bigDeSelect");
    }

    private TextBox CreateVolumeDropdownSearchBox()
    {
        var box = new GalleryTextBox
        {
            DrawFrame = GalleryUiSkin.Get(GalleryUiSkin.SearchBoxAsset) is null,
            Height = 44
        };
        box.OnEnterPressed += _ =>
        {
            RefreshVolumeDropdownSearch(force: true);
            box.Selected = false;
            if (Game1.keyboardDispatcher.Subscriber == box)
                Game1.keyboardDispatcher.Subscriber = null;
            volumeDropdownSearchFocused = false;
            volumeDropdownSelectedIndex = volumeChoices.Length > 1 ? 1 : 0;
            EnsureVolumeDropdownVisible();
        };
        return box;
    }

    private void RefreshVolumeDropdownSearch(bool force = false)
    {
        if (!volumeDropdownOpen || volumeDropdownSearch is null)
            return;
        volumeDropdownSearch.Text = GallerySearchInput.CleanText(volumeDropdownSearch.Text);
        if (!force && lastVolumeDropdownSearch == volumeDropdownSearch.Text)
            return;
        lastVolumeDropdownSearch = volumeDropdownSearch.Text;
        FilterVolumeDropdownChoices();
    }

    private void FilterVolumeDropdownChoices()
    {
        if (volumeAllChoices.Length == 0)
        {
            volumeChoices = [];
            return;
        }
        string query = volumeDropdownSearch?.Text.Trim() ?? "";
        if (query.Length == 0)
        {
            volumeChoices = volumeAllChoices;
        }
        else
        {
            string? spelling = context.Pinyin is null ? null : PinyinTable.Spelling(query);
            List<VolumeChoice> matches = [volumeAllChoices[0]];
            for (int i = 1; i < volumeAllChoices.Length; i++)
            {
                VolumeChoice c = volumeAllChoices[i];
                if (c.Label.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || c.Key?.Contains(query, StringComparison.OrdinalIgnoreCase) == true
                    || spelling is not null && context.Pinyin!.Matches(c.Label, spelling))
                {
                    matches.Add(c);
                }
            }
            volumeChoices = matches.ToArray();
        }
        volumeDropdownScroll = 0;
        volumeDropdownSelectedIndex = volumeChoices.Length > 1 && query.Length > 0 ? 1 : 0;
    }

    private void HandleVolumeDropdownKey(Keys key)
    {
        if (key == Keys.Enter)
        {
            ActivateVolumeDropdownSelection();
            return;
        }
        if (key == Keys.Up)
        {
            if (volumeDropdownSearch is not null && !volumeDropdownSearchFocused && volumeDropdownSelectedIndex <= 0)
            {
                volumeDropdownSearchFocused = true;
                Game1.playSound("shiny4");
            }
            else if (!volumeDropdownSearchFocused)
                MoveVolumeDropdownSelection(-1);
        }
        else if (key == Keys.Down)
        {
            if (volumeDropdownSearchFocused)
            {
                volumeDropdownSearchFocused = false;
                volumeDropdownSelectedIndex = 0;
                EnsureVolumeDropdownVisible();
                Game1.playSound("shiny4");
            }
            else
                MoveVolumeDropdownSelection(1);
        }
        else if (key is Keys.PageUp or Keys.PageDown)
        {
            volumeDropdownSearchFocused = false;
            MoveVolumeDropdownSelection(key == Keys.PageUp ? -VolumeDropdownMaxVisible : VolumeDropdownMaxVisible);
        }
    }

    private void MoveVolumeDropdownSelection(int delta)
    {
        if (volumeChoices.Length == 0)
            return;
        int next = Math.Clamp(volumeDropdownSelectedIndex + delta, 0, volumeChoices.Length - 1);
        if (next != volumeDropdownSelectedIndex)
        {
            volumeDropdownSelectedIndex = next;
            EnsureVolumeDropdownVisible();
            Game1.playSound("shiny4");
        }
    }

    private void EnsureVolumeDropdownVisible()
    {
        int maxScroll = Math.Max(0, volumeChoices.Length - VolumeDropdownMaxVisible);
        if (volumeDropdownSelectedIndex < volumeDropdownScroll)
            volumeDropdownScroll = volumeDropdownSelectedIndex;
        else if (volumeDropdownSelectedIndex >= volumeDropdownScroll + VolumeDropdownMaxVisible)
            volumeDropdownScroll = volumeDropdownSelectedIndex - VolumeDropdownMaxVisible + 1;
        volumeDropdownScroll = Math.Clamp(volumeDropdownScroll, 0, maxScroll);
    }

    private void ActivateVolumeDropdownSelection()
    {
        if (volumeDropdownSearchFocused && volumeDropdownSearch is not null)
        {
            UpdateVolumeDropdownSearchBounds();
            GalleryTextBox.SelectForInput(volumeDropdownSearch);
            return;
        }
        if (volumeDropdownSelectedIndex >= 0 && volumeDropdownSelectedIndex < volumeChoices.Length)
            SelectVolumeChoice(volumeDropdownSelectedIndex);
    }

    private void SelectVolumeChoice(int choiceIndex)
    {
        if (!volumeDropdownOpen || choiceIndex < 0 || choiceIndex >= volumeChoices.Length)
            return;
        string? nextKey = volumeChoices[choiceIndex].Key;
        bool changed = !string.Equals(state.HomeVolumeKey, nextKey, StringComparison.OrdinalIgnoreCase);
        state.HomeVolumeKey = nextKey;
        CloseVolumeDropdown(playSound: false);
        if (changed)
        {
            ApplyVolumeFilter();
            currentPage = 0;
            searchFilter.Update(catalog, search.Text, i18n.Locale,
                LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.zh, context.Pinyin);
            filtered = searchFilter.Results;
            PreparePortraits();
            BuildClickableComponents();
            SaveState(VolumeComponentId);
        }
        currentlySnappedComponent = volumeComponent ?? cardComponents.FirstOrDefault() ?? searchComponent;
        if (Game1.options.snappyMenus && Game1.options.gamepadControls)
            snapCursorToCurrentSnappedComponent();
        Game1.playSound("smallSelect");
    }

    private void HandleVolumeDropdownLeftClick(int x, int y)
    {
        var layout = ComputeVolumeDropdownLayout();
        if (!layout.Popup.Contains(x, y))
        {
            CloseVolumeDropdown(playSound: true);
            return;
        }

        if (volumeDropdownSearch is not null && layout.Search.Contains(x, y))
        {
            volumeDropdownSearchFocused = true;
            UpdateVolumeDropdownSearchBounds();
            GalleryTextBox.SelectForInput(volumeDropdownSearch);
            if (volumeDropdownSearch.Selected)
            {
                ulong frameGen = CurrentFrameSuppressionGeneration();
                suppressRightClickGeneration = frameGen;
                SuppressMappedKey(Buttons.Y, frameGen);
            }
            return;
        }

        DeselectSearch();
        volumeDropdownSearchFocused = false;

        if (layout.HasScroll && layout.ScrollTrack.Contains(x, y))
        {
            draggingVolumeScroll = true;
            ScrollVolumeDropdownToMouseY(y);
            return;
        }

        int visCount = Math.Min(VolumeDropdownMaxVisible, volumeChoices.Length);
        for (int i = 0; i < visCount; i++)
        {
            if (VolumeDropdownItemRect(layout, i).Contains(x, y))
            {
                SelectVolumeChoice(volumeDropdownScroll + i);
                return;
            }
        }
    }

    private void ScrollVolumeDropdownToMouseY(int logicalY)
    {
        var layout = ComputeVolumeDropdownLayout();
        int maxScroll = Math.Max(0, volumeChoices.Length - VolumeDropdownMaxVisible);
        if (!layout.HasScroll || maxScroll <= 0 || layout.ScrollTrack.Height <= layout.ScrollThumbHeight)
            return;
        float ratio = Math.Clamp((logicalY - layout.ScrollTrack.Y - layout.ScrollThumbHeight / 2f)
            / Math.Max(1f, layout.ScrollTrack.Height - layout.ScrollThumbHeight), 0f, 1f);
        volumeDropdownScroll = (int)Math.Round(ratio * maxScroll);
    }

    private readonly record struct VolumeDropdownLayout(
        Rectangle Popup,
        Rectangle Search,
        int ListTop,
        int ItemWidth,
        bool HasScroll,
        Rectangle ScrollTrack,
        int ScrollThumbHeight);

    private VolumeDropdownLayout ComputeVolumeDropdownLayout()
    {
        bool hasSearch = volumeDropdownSearch is not null;
        int visCount = Math.Clamp(volumeChoices.Length, 1, VolumeDropdownMaxVisible);
        int listHeight = visCount * VolumeDropdownItemHeight;
        int searchBlock = hasSearch ? 54 : 0;
        int popupHeight = 24 + searchBlock + listHeight;
        int preferredY = Math.Min(volumeBounds.Bottom + 4, 844 - popupHeight);
        Rectangle popup = new(volumeBounds.X, Math.Max(112, preferredY), volumeBounds.Width, popupHeight);
        Rectangle searchRect = hasSearch ? new Rectangle(popup.X + 14, popup.Y + 12, popup.Width - 28, 44) : Rectangle.Empty;
        int listTop = popup.Y + 12 + searchBlock;
        bool hasScroll = volumeChoices.Length > VolumeDropdownMaxVisible;
        int itemWidth = popup.Width - 28 - (hasScroll ? 22 : 0);
        Rectangle scrollTrack = hasScroll ? new Rectangle(popup.Right - 28, listTop + 2, 14, Math.Max(24, listHeight - 4)) : Rectangle.Empty;
        int thumbHeight = hasScroll
            ? Math.Max(28, (int)Math.Round(scrollTrack.Height * (VolumeDropdownMaxVisible / (float)volumeChoices.Length)))
            : 0;
        return new VolumeDropdownLayout(popup, searchRect, listTop, itemWidth, hasScroll, scrollTrack, thumbHeight);
    }

    private static Rectangle VolumeDropdownItemRect(VolumeDropdownLayout layout, int slot)
        => new(layout.Popup.X + 14, layout.ListTop + slot * VolumeDropdownItemHeight, layout.ItemWidth, VolumeDropdownItemHeight - 2);

    private void UpdateVolumeDropdownSearchBounds()
    {
        if (!volumeDropdownOpen || volumeDropdownSearch is null)
            return;
        var layout = ComputeVolumeDropdownLayout();
        Rectangle textArea = GalleryUiSkin.SearchTextBounds(layout.Search);
        volumeDropdownSearch.X = textArea.X - 12;
        volumeDropdownSearch.Y = layout.Search.Y;
        volumeDropdownSearch.Width = textArea.Width + 12;
        volumeDropdownSearch.Height = layout.Search.Height;
    }

    private void DrawVolumeDropdownOverlay(SpriteBatch b, int mouseX, int mouseY, bool showFocus)
    {
        var layout = ComputeVolumeDropdownLayout();
        Rectangle popup = layout.Popup;

        b.Draw(Game1.staminaRect, new Rectangle(popup.X + 6, popup.Y + 8, popup.Width, popup.Height), Color.Black * 0.34f);
        b.Draw(Game1.staminaRect, popup, new Color(76, 42, 20));
        b.Draw(Game1.staminaRect, GalleryDrawing.Inset(popup, 4, 4), new Color(188, 142, 78));
        b.Draw(Game1.staminaRect, GalleryDrawing.Inset(popup, 7, 7), new Color(247, 234, 206));
        GalleryUiSkin.DrawFocusCorners(b, GalleryDrawing.Inset(popup, 2, 2), 12);

        if (volumeDropdownSearch is not null)
        {
            UpdateVolumeDropdownSearchBounds();
            bool dsHot = volumeDropdownSearch.Selected || volumeDropdownSearchFocused || layout.Search.Contains(mouseX, mouseY);
            GalleryUiSkin.DrawSearchBox(b, layout.Search, dsHot);
            volumeDropdownSearch.Draw(b);
            if (volumeDropdownSearch.Text.Length == 0 && !volumeDropdownSearch.Selected)
                DrawLeftFitted(b, i18n.Get("home.search"),
                    GalleryDrawing.Inset(GalleryUiSkin.SearchTextBounds(layout.Search), 4, 8), GallerySpreadDrawing.LightInk);
        }

        int visCount = Math.Min(VolumeDropdownMaxVisible, volumeChoices.Length);
        float itemScale = Math.Min(0.92f, 32f / Math.Max(1f, Game1.smallFont.LineSpacing));
        for (int i = 0; i < visCount; i++)
        {
            int choiceIdx = volumeDropdownScroll + i;
            VolumeChoice choice = volumeChoices[choiceIdx];
            Rectangle itemRect = VolumeDropdownItemRect(layout, i);
            bool isCurrent = string.Equals(choice.Key, state.HomeVolumeKey, StringComparison.OrdinalIgnoreCase);
            bool isSelected = !volumeDropdownSearchFocused && choiceIdx == volumeDropdownSelectedIndex;
            bool isHovered = itemRect.Contains(mouseX, mouseY);

            if (isSelected || isHovered)
                b.Draw(Game1.staminaRect, itemRect, new Color(232, 194, 124) * 0.82f);
            else if (isCurrent)
                b.Draw(Game1.staminaRect, itemRect, new Color(228, 208, 168) * 0.55f);

            if (i + 1 < visCount)
                b.Draw(Game1.staminaRect, new Rectangle(itemRect.X + 8, itemRect.Bottom, itemRect.Width - 16, 1), new Color(204, 178, 138) * 0.55f);

            if (showFocus && isSelected)
                GalleryUiSkin.DrawFocusCorners(b, GalleryDrawing.Inset(itemRect, 1, 1), 10);

            int textX = itemRect.X + 16;
            int textW = itemRect.Width - (isCurrent ? 56 : 28);
            DrawEllipsized(b, choice.Label, new Rectangle(textX, itemRect.Y, Math.Max(40, textW), itemRect.Height), itemScale);

            if (isCurrent)
            {
                b.Draw(context.Textures.ConditionIcons,
                    new Rectangle(itemRect.Right - 32, itemRect.Center.Y - 10, 20, 20),
                    GalleryBookChrome.ToRect(GallerySpreadLayout.ConditionCheckSource),
                    Color.White);
            }
        }

        if (layout.HasScroll)
        {
            Rectangle track = layout.ScrollTrack;
            b.Draw(Game1.staminaRect, track, new Color(196, 168, 126));
            int maxScroll = Math.Max(1, volumeChoices.Length - VolumeDropdownMaxVisible);
            int thumbY = track.Y + (int)Math.Round((track.Height - layout.ScrollThumbHeight) * (volumeDropdownScroll / (float)maxScroll));
            DrawScrollbar(b, new Rectangle(track.X + 1, thumbY, track.Width - 2, layout.ScrollThumbHeight));
        }
    }

    private void EnsureLayout()
    {
        if (GalleryLayout.Changed(viewportWidth, viewportHeight, Game1.uiViewport.Width, Game1.uiViewport.Height))
            RecalculateLayout();
    }

    private void DrawConfirmation(SpriteBatch b, int mouseX, int mouseY, bool showFocus, int focusedId)
    {
        Rectangle panel = R(460, 330, 750, 300);
        IClickableMenu.drawTextureBox(b, panel.X, panel.Y, panel.Width, panel.Height, Color.White);
        string message = GalleryDrawing.ParseTextSafe(i18n.Get(isUnlocked() ? "confirm.restore" : "confirm.unlock"), Game1.smallFont, panel.Width - 100);
        GalleryDrawing.DrawStringSafe(b, Game1.smallFont, message, new Vector2(panel.X + 50, panel.Y + 55), Game1.textColor);
        DrawButton(b, yesBounds, i18n.Get("common.yes"), yesBounds.Contains(mouseX, mouseY) || showFocus && focusedId == YesComponentId);
        DrawButton(b, noBounds, i18n.Get("common.no"), noBounds.Contains(mouseX, mouseY) || showFocus && focusedId == NoComponentId);
    }

    private Rectangle R(int x, int y, int w, int h) => new(xPositionOnScreen + x, yPositionOnScreen + y, w, h);

    private Rectangle ToScreen(Rectangle bounds) => ScaleRectangle(bounds, menuScale, drawOffsetX, drawOffsetY);

    private (int X, int Y) ToLogical(int x, int y)
        => ((int)Math.Round((x - drawOffsetX) / menuScale), (int)Math.Round((y - drawOffsetY) / menuScale));
}
