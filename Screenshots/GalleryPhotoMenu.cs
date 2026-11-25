using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace StardewGallery;

/// <summary>
/// Symmetric <c>1672x941</c> leather-bound photo spread (<c>GalleryHome.png</c>):
/// left page holds a large riveted <c>16:9</c> preview frame (backed by <c>CardLockedEnvelope</c> when empty),
/// wood page turner, local folder path and bottom-left chapter bookmarks; right page holds a <c>2x2</c> grid of
/// riveted Polaroid thumbnails above spacious two-row tri-state wood action buttons.
/// </summary>
internal sealed class GalleryPhotoMenu : IClickableMenu
{
    private const int LogicalWidth = GalleryDrawing.MenuWidth, LogicalHeight = GalleryDrawing.MenuHeight;
    private const int FooterButtonCount = 5;
    private const int SlotsPerPage = 4;
    private const int OpenFolderComponentId = 104;

    private readonly GalleryPhotos photos;
    private readonly EventIdentity identity;
    private readonly ITranslationHelper i18n;
    private readonly GalleryViewContext context;
    private readonly GalleryPageState state;
    private EventPhotoSet set;
    private int selected;
    private float scale;
    private int offsetX, offsetY, oldWidth, oldHeight;
    private bool keyboardFocus;
    private Point lastMouse;
    private string? folderStatusNotice;

    private static Rectangle SubheaderLeft => new(164, 132, 592, 34);
    private static Rectangle Preview => new(168, 196, 584, 329);
    private static Rectangle Previous => new(316, 566, 44, 44);
    private static Rectangle PreviewIndicator => new(368, 566, 184, 44);
    private static Rectangle Next => new(560, 566, 44, 44);
    private static Rectangle FolderPathBounds => new(164, 632, 592, 34);
    private static Rectangle FolderNoticeBounds => new(164, 672, 592, 34);

    private static Rectangle RightHeaderBounds => new(896, 132, 600, 34);
    private static Rectangle Slot(int index) => new(912 + (index % 2) * 312, 192 + (index / 2) * 190, 256, 144);

    // Action IDs: 100 = Back, 101 = SetCover, 102 = RestoreDefault, 103 = Remove, 104 = OpenFolder.
    private static Rectangle Footer(int index) => index switch
    {
        1 => new(896, 584, 290, 52),
        2 => new(1206, 584, 290, 52),
        3 => new(896, 650, 290, 52),
        4 => new(1206, 650, 290, 52),
        _ => new(896, 760, 600, 56)
    };

    private EventPhoto? Selected => selected >= 0 && selected < set.Photos.Count ? set.Photos[selected] : null;
    private bool CanSelectCover => !set.ReadOnly && Selected is { } photo && photos.Image(identity, photo.Id, preview: true) is not null;

    internal GalleryPhotoMenu(GalleryViewContext context, GalleryPageState state)
        : base(0, 0, LogicalWidth, LogicalHeight, true)
    {
        this.context = context;
        this.state = state;
        photos = context.Photos;
        i18n = context.I18n;
        identity = state.Event ?? throw new ArgumentException("An event identity is required.", nameof(state));
        set = photos.Read(identity);
        selected = state.Focus < 0 ? Math.Max(0, set.Photos.Count - 1) : Math.Clamp(state.Scroll, 0, Math.Max(0, set.Photos.Count - 1));
        int initialFocus = state.Focus;
        Layout();
        Focus(initialFocus >= 0 ? initialFocus : Selected is null ? 100 : 300 + selected % SlotsPerPage);
        if (Game1.options.snappyMenus && Game1.options.gamepadControls)
            snapCursorToCurrentSnappedComponent();
    }

    public override void draw(SpriteBatch batch)
    {
        if (oldWidth != Game1.uiViewport.Width || oldHeight != Game1.uiViewport.Height)
            Layout();

        batch.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * .45f);
        int mouseX = Game1.getMouseX(true), mouseY = Game1.getMouseY(true);
        Point logicalCursor = new((int)Math.Round((mouseX - offsetX) / scale), (int)Math.Round((mouseY - offsetY) / scale));
        bool showFocus = Game1.options.snappyMenus && Game1.options.gamepadControls || keyboardFocus;
        int focusedId = currentlySnappedComponent?.myID ?? 100;

        GalleryDrawing.BeginScaled(batch, scale, offsetX, offsetY);
        GalleryBookChrome.DrawCleanHomeSpread(batch, context.Textures.Home, LogicalWidth, LogicalHeight);
        GalleryBookChrome.DrawRibbonTabs(batch, state.OriginTab, focusedId, showFocus, logicalCursor, i18n, out string? hoverTooltip);
        GalleryBookChrome.DrawSpreadTitle(batch, HeaderTitle(), scale, offsetX, offsetY);

        GalleryDrawing.DrawEllipsized(batch, HeaderSubtitle(), SubheaderLeft, Math.Min(0.92f, 28f / Math.Max(1f, Game1.smallFont.LineSpacing)));

        Texture2D? image = Selected is { } photo ? photos.Image(identity, photo.Id, preview: true) : null;
        if (image is not null)
        {
            batch.Draw(Game1.staminaRect, Preview, new Color(28, 24, 20));
            GalleryPhotos.DrawContained(batch, image, Preview);
        }
        else
        {
            string notice = i18n.Get(Selected is null ? "photo.empty" : "photo.image-unavailable");
            GalleryBookChrome.DrawLockedStoryEnvelope(batch, Preview, HeaderTitle(), notice);
            GalleryUiSkin.DrawPill(batch, new Point(Preview.X + 14, Preview.Y + 14), 28, notice, UiPill.Wood, maxWidth: 260);
        }
        GalleryUiSkin.DrawCardFrame(batch, Preview, false);

        if (Selected is not null && set.CoverId == Selected.Id)
            GalleryUiSkin.DrawPill(batch, new Point(Preview.X + 14, Preview.Y + 14), 28, i18n.Get("photo.current-cover"), UiPill.Amber, UiBadge.Star, maxWidth: 220);

        bool canPrev = selected > 0;
        bool canNext = selected + 1 < set.Photos.Count;
        GalleryUiSkin.DrawPagerArrow(batch, Previous, false,
            !canPrev ? UiButtonState.Disabled : Previous.Contains(logicalCursor) ? UiButtonState.Hover : UiButtonState.Normal);
        GalleryUiSkin.DrawPagerArrow(batch, Next, true,
            !canNext ? UiButtonState.Disabled : Next.Contains(logicalCursor) ? UiButtonState.Hover : UiButtonState.Normal);
        GalleryDrawing.DrawCentered(batch, $"{(set.Photos.Count == 0 ? 0 : selected + 1)} / {set.Photos.Count}", PreviewIndicator, GallerySpreadDrawing.Ink);

        string relativeFolder = photos.DisplaySubdirectory(identity);
        string folderLabel = i18n.Get("photo.folder-path", new { path = relativeFolder });
        float folderScale = Math.Min(0.88f, 26f / Math.Max(1f, Game1.smallFont.LineSpacing));
        GalleryDrawing.DrawEllipsized(batch, folderLabel, FolderPathBounds, folderScale);
        if (!string.IsNullOrWhiteSpace(folderStatusNotice))
            GalleryDrawing.DrawEllipsized(batch, folderStatusNotice, FolderNoticeBounds, folderScale);

        // Right page: thumbnail gallery + two-row tri-state wood buttons + Back button
        int pageStart = selected / SlotsPerPage * SlotsPerPage;
        int totalPages = GalleryPagination.TotalPages(set.Photos.Count, SlotsPerPage);
        int currentThumbPage = set.Photos.Count == 0 ? 1 : selected / SlotsPerPage + 1;
        GalleryDrawing.DrawEllipsized(batch,
            $"{i18n.Get("photo.title")} ({currentThumbPage} / {totalPages})",
            RightHeaderBounds,
            Math.Min(0.92f, 28f / Math.Max(1f, Game1.smallFont.LineSpacing)));

        if (set.Photos.Count == 0)
        {
            Rectangle emptyRight = new(912, 224, 568, 260);
            bDrawSoftNote(batch, emptyRight, i18n.Get("photo.empty"));
        }
        else
        {
            for (int slot = 0; slot < SlotsPerPage; slot++)
            {
                int index = pageStart + slot;
                if (index >= set.Photos.Count)
                    break;
                EventPhoto item = set.Photos[index];
                Rectangle bounds = Slot(slot);
                bool slotFocused = showFocus && focusedId == 300 + slot;
                bool slotHot = bounds.Contains(logicalCursor) || slotFocused || index == selected;
                Texture2D? thumb = photos.Image(identity, item.Id, preview: false);
                if (thumb is not null)
                {
                    batch.Draw(Game1.staminaRect, bounds, new Color(28, 24, 20));
                    GalleryPhotos.DrawCover(batch, thumb, bounds, Color.White);
                }
                else
                {
                    GalleryBookChrome.DrawLockedStoryEnvelope(batch, bounds, $"#{index + 1}", "");
                }
                GalleryUiSkin.DrawCardFrame(batch, bounds, index == selected || slotFocused);
                if (set.CoverId == item.Id)
                    GalleryUiSkin.DrawPill(batch, new Point(bounds.X + 8, bounds.Y + 8), 26, i18n.Get("photo.current-cover"), UiPill.Amber, maxWidth: 180);
            }
        }

        string[] footerKeys = ["photo.back", "photo.set-cover", "photo.default", "photo.remove", "photo.open-folder"];
        for (int index = 0; index < FooterButtonCount; index++)
        {
            Rectangle bounds = Footer(index);
            bool enabled = Enabled(index);
            bool focused = showFocus && focusedId == 100 + index;
            bool hot = enabled && (bounds.Contains(logicalCursor) || focused);
            GalleryDrawing.DrawButton(batch, bounds, i18n.Get(footerKeys[index]), hot, enabled);
            if (focused)
                GalleryUiSkin.DrawFocusCorners(batch, GalleryDrawing.Inset(bounds, -4, -4), 10);
        }

        upperRightCloseButton?.draw(batch);
        GalleryDrawing.EndScaled(batch);

        if (hoverTooltip is null && (FolderPathBounds.Contains(logicalCursor)
            || Footer(4).Contains(logicalCursor)
            || focusedId == OpenFolderComponentId && showFocus))
        {
            hoverTooltip = !string.IsNullOrWhiteSpace(folderStatusNotice) ? folderStatusNotice : folderLabel;
        }
        if (hoverTooltip is not null)
            GalleryDrawing.DrawHoverTextSafe(batch, GalleryDrawing.ParseTextSafe(hoverTooltip, Game1.smallFont, Math.Max(200, Math.Min(560, Game1.uiViewport.Width - 80))), Game1.smallFont);
        drawMouse(batch);
    }

    private static void bDrawSoftNote(SpriteBatch b, Rectangle area, string message)
    {
        Rectangle card = GalleryDrawing.Inset(area, 20, 36);
        b.Draw(Game1.staminaRect, card, new Color(238, 223, 192) * 0.65f);
        GalleryBookChrome.DrawBorder(b, card, new Color(188, 154, 108) * 0.75f, 2);
        GalleryDrawing.DrawCentered(b, message, GalleryDrawing.Inset(card, 24, 24), GallerySpreadDrawing.Ink);
    }

    private string HeaderTitle()
        => WeddingMemento.TryGetSpouseName(identity) is { } spouseName
            ? i18n.Get("wedding.title", new { spouse = context.Characters.Get(spouseName) })
            : i18n.Get("photo.title");

    private string HeaderSubtitle()
    {
        if (WeddingMemento.TryGetSpouseName(identity) is { } spouseName)
        {
            if (set.WeddingDate is { } date)
            {
                string seasonKey = "season." + (WeddingDateRecord.NormalizeSeason(date.Season) ?? "spring");
                return i18n.Get("wedding.subtitle", new { year = date.Year, season = i18n.Get(seasonKey), day = date.DayOfMonth });
            }
            return context.Characters.Get(spouseName);
        }
        if (context.Catalog?.Find(identity) is { } entry)
            return context.CardTitle(entry) + GalleryDrawing.TextSeparator + i18n.Get("event-detail.event-id", new { id = identity.EventId });
        return i18n.Get("event-detail.event-id", new { id = identity.EventId });
    }

    public override void performHoverAction(int x, int y)
    {
        if (lastMouse != new Point(x, y))
        {
            if (lastMouse != default) keyboardFocus = false;
            lastMouse = new Point(x, y);
        }
        base.performHoverAction(x, y);
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        Point cursor = new((int)Math.Round((x - offsetX) / scale), (int)Math.Round((y - offsetY) / scale));
        if (upperRightCloseButton?.bounds.Contains(cursor) == true)
        {
            SaveState();
            photos.ReleasePreviews();
            context.Navigation.Close();
            return;
        }
        if (GalleryBookChrome.TryClickRibbonTab(cursor.X, cursor.Y, state.OriginTab, context.Navigation, () =>
            {
                SaveState();
                photos.ReleasePreviews();
            }))
        {
            return;
        }
        for (int index = 0; index < FooterButtonCount; index++)
            if (Footer(index).Contains(cursor)) { Activate(100 + index); return; }
        if (Previous.Contains(cursor)) { Select(selected - 1); return; }
        if (Next.Contains(cursor)) { Select(selected + 1); return; }
        for (int slot = 0; slot < SlotsPerPage; slot++)
            if (Slot(slot).Contains(cursor)) { Select(selected / SlotsPerPage * SlotsPerPage + slot); return; }
    }

    public override void receiveScrollWheelAction(int direction) => Select(selected + (direction < 0 ? 1 : -1));
    public override void receiveRightClick(int x, int y, bool playSound = true) => Return();

    public override void receiveKeyPress(Keys key)
    {
        if (key == Keys.Escape || Game1.options.menuButton.Any(binding => binding.key == key))
        {
            Return();
            return;
        }
        if (key == Keys.Enter)
        {
            keyboardFocus = true;
            Activate(currentlySnappedComponent?.myID ?? 100);
            return;
        }
        if (key is Keys.Up or Keys.Right or Keys.Down or Keys.Left)
        {
            keyboardFocus = true;
            applyMovementKey(key switch { Keys.Up => 0, Keys.Right => 1, Keys.Down => 2, _ => 3 });
            return;
        }
        base.receiveKeyPress(key);
    }

    public override void update(GameTime time) { base.update(time); SaveState(); }

    public override void receiveGamePadButton(Buttons button)
    {
        // A/X use Game1's mouse emulation, including free-cursor mode.
        if (button == Buttons.B) Return();
        else if (button is Buttons.LeftShoulder or Buttons.RightShoulder)
            Select(selected + (button == Buttons.LeftShoulder ? -1 : 1));
        else base.receiveGamePadButton(button);
    }

    public override void applyMovementKey(int direction)
    {
        int id = currentlySnappedComponent?.myID ?? 100;
        if (id >= GalleryBookChrome.RibbonTabBaseId && id < GalleryBookChrome.RibbonTabBaseId + GallerySpreadLayout.RibbonTabCount)
        {
            int tab = id - GalleryBookChrome.RibbonTabBaseId;
            if (direction == 3 && tab > 0) Focus(GalleryBookChrome.RibbonTabBaseId + tab - 1);
            else if (direction == 1 && tab + 1 < GallerySpreadLayout.RibbonTabCount) Focus(GalleryBookChrome.RibbonTabBaseId + tab + 1);
            else if (direction == 1) Focus(100);
            else if (direction == 0 && Selected is not null) Focus(300 + selected % SlotsPerPage);
            return;
        }
        if (id is >= 100 and <= 104)
        {
            if (direction == 0)
            {
                if (id == 100)
                {
                    int upBtn = FirstEnabled(103, 104, 101, 102);
                    if (upBtn > 0) Focus(upBtn);
                    else if (Selected is not null) Focus(300 + selected % SlotsPerPage);
                }
                else if (id is 103 or 104)
                {
                    int upBtn = id == 103 ? FirstEnabled(101, 102) : FirstEnabled(102, 101);
                    if (upBtn > 0) Focus(upBtn);
                    else if (Selected is not null) Focus(300 + selected % SlotsPerPage);
                }
                else if (Selected is not null)
                {
                    Focus(300 + selected % SlotsPerPage);
                }
            }
            else if (direction == 2)
            {
                if (id is 101 or 102)
                {
                    int downBtn = id == 101 ? FirstEnabled(103, 104, 100) : FirstEnabled(104, 103, 100);
                    if (downBtn > 0) Focus(downBtn);
                }
                else if (id is 103 or 104)
                {
                    Focus(100);
                }
            }
            else if (direction is 1 or 3)
            {
                if (direction == 3 && id is 100 or 101 or 103 && context.Textures is not null
                    && allClickableComponents.Any(c => c.myID == GalleryBookChrome.RibbonTabBaseId + 2))
                {
                    if (id == 100)
                    {
                        Focus(GalleryBookChrome.RibbonTabBaseId + 2);
                        return;
                    }
                }
                int step = direction == 1 ? 1 : -1;
                int next = id - 100 + step;
                while (next is >= 0 and < FooterButtonCount && !Enabled(next))
                    next += step;
                if (next is >= 0 and < FooterButtonCount)
                    Focus(100 + next);
            }
            return;
        }

        // Focused on thumbnail slot 300..303
        int localSlot = selected % SlotsPerPage;
        if (direction == 3)
        {
            if (selected > 0) Select(selected - 1);
        }
        else if (direction == 1)
        {
            if (selected + 1 < set.Photos.Count) Select(selected + 1);
        }
        else if (direction == 0)
        {
            if (localSlot >= 2 && selected - 2 >= 0) Select(selected - 2);
        }
        else if (direction == 2)
        {
            if (localSlot < 2 && selected + 2 < set.Photos.Count)
                Select(selected + 2);
            else
                Focus(FirstEnabled(101, 102, 103, 104, 100));
        }
    }

    private int FirstEnabled(params int[] buttonIds)
    {
        foreach (int id in buttonIds)
            if (Enabled(id - 100))
                return id;
        return -1;
    }

    // Game1 repopulates components by reflection when input switches to a controller; rebuild the gallery's own instead.
    public override void populateClickableComponentList() => BuildComponents();
    public override void snapToDefaultClickableComponent() => Focus(Selected is null ? 100 : 300 + selected % SlotsPerPage);
    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds) { base.gameWindowSizeChanged(oldBounds, newBounds); Layout(); }

    private void Select(int index)
    {
        if (index < 0 || index >= set.Photos.Count || index == selected) return;
        selected = index;
        BuildComponents();
        Focus(300 + selected % SlotsPerPage);
        Game1.playSound("shwip");
    }

    private bool Enabled(int index) => index switch
    {
        0 => true,
        1 => CanSelectCover && set.CoverId != Selected?.Id,
        2 => !set.ReadOnly && set.CoverId is not null,
        3 => !set.ReadOnly && Selected is not null,
        4 => true,
        _ => false
    };

    private void Activate(int id)
    {
        if (id >= GalleryBookChrome.RibbonTabBaseId && id < GalleryBookChrome.RibbonTabBaseId + GallerySpreadLayout.RibbonTabCount)
        {
            SaveState();
            photos.ReleasePreviews();
            Game1.playSound("shwip");
            context.Navigation.SwitchTab((GalleryBookTab)(id - GalleryBookChrome.RibbonTabBaseId));
            return;
        }
        if (id >= 300) { Select(selected / SlotsPerPage * SlotsPerPage + id - 300); return; }
        int action = id - 100;
        if (!Enabled(action)) return;
        if (action == 0) { Return(); return; }
        if (action == 4) { OpenLocalFolder(); return; }
        try
        {
            if (action == 1) photos.SetCover(identity, Selected!.Id);
            else if (action == 2) photos.SetCover(identity, null);
            else if (action == 3) photos.Archive(identity, Selected!.Id);
            Game1.playSound("smallSelect");
        }
        catch (Exception error) { photos.ReportFailure(error); }
        set = photos.Read(identity);
        selected = Math.Clamp(selected, 0, Math.Max(0, set.Photos.Count - 1));
        BuildComponents();
        Focus(Enabled(action) ? id : 100);
    }

    private void OpenLocalFolder()
    {
        PhotoFolderOpenResult result = PhotoFolderOpener.TryOpen(
            photos.RootDirectory,
            photos.ProfileDirectory,
            photos.EventDirectory(identity));
        string relativePath = photos.DisplaySubdirectory(identity);
        folderStatusNotice = result.Outcome switch
        {
            PhotoFolderOpenOutcome.Opened => i18n.Get("photo.folder-opened", new { path = relativePath }),
            PhotoFolderOpenOutcome.HandheldFallback => i18n.Get("photo.folder-handheld", new { path = relativePath }),
            _ => i18n.Get("photo.folder-fallback", new { path = relativePath })
        };
        Game1.playSound("smallSelect");
        Focus(OpenFolderComponentId);
    }

    private void Layout()
    {
        width = LogicalWidth;
        height = LogicalHeight;
        xPositionOnScreen = yPositionOnScreen = 0;
        oldWidth = Game1.uiViewport.Width;
        oldHeight = Game1.uiViewport.Height;
        scale = (float)GalleryLayout.ScaleToFit(oldWidth, oldHeight, width, height, 24);
        offsetX = (int)Math.Round((oldWidth - width * scale) / 2);
        offsetY = (int)Math.Round((oldHeight - height * scale) / 2);
        initializeUpperRightCloseButton();
        GalleryBookChrome.PositionCloseButton(upperRightCloseButton, scale, offsetX, offsetY, oldWidth, oldHeight);
        BuildComponents();
    }

    private void BuildComponents()
    {
        int oldId = currentlySnappedComponent?.myID ?? 100;
        allClickableComponents = [];
        for (int index = 0; index < FooterButtonCount; index++)
            if (Enabled(index)) allClickableComponents.Add(new(ToScreen(Footer(index)), "photo-action") { myID = 100 + index });
        for (int slot = 0; slot < SlotsPerPage && selected / SlotsPerPage * SlotsPerPage + slot < set.Photos.Count; slot++)
            allClickableComponents.Add(new(ToScreen(Slot(slot)), "photo") { myID = 300 + slot });
        if (context?.Textures is not null)
            GalleryBookChrome.AddRibbonTabComponents(allClickableComponents, ToScreen, 100);
        currentlySnappedComponent = allClickableComponents.FirstOrDefault(item => item.myID == oldId) ?? allClickableComponents[0];
        SaveState();
    }

    private void Focus(int id)
    {
        currentlySnappedComponent = allClickableComponents.FirstOrDefault(item => item.myID == id) ?? allClickableComponents[0];
        SaveState();
        if (Game1.options.snappyMenus && Game1.options.gamepadControls) snapCursorToCurrentSnappedComponent();
    }

    private void SaveState() { state.Scroll = selected; state.Focus = currentlySnappedComponent?.myID ?? 100; }
    private void Return() { SaveState(); photos.ReleasePreviews(); context.Navigation.Back(); Game1.playSound("bigDeSelect"); }
    private Rectangle ToScreen(Rectangle bounds) => GalleryDrawing.ScaleRectangle(bounds, scale, offsetX, offsetY);
}
