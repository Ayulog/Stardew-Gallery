using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace StardewGallery;

/// <summary>
/// Chapter 3 ("Treasured Moments") of the leather-bound gallery journal: showcases all current-save spouse
/// Wedding Memento cards alongside all cross-save favorited stories in an 8-card keepsake spread with whole-page turning.
/// </summary>
internal sealed class GalleryMomentsMenu : IClickableMenu
{
    private const int Columns = 4;
    private const int VisibleRows = 2;
    private const int PageSize = Columns * VisibleRows; // 8 keepsake cards per spread (2x2 left + 2x2 right)
    private const int DefaultEmptyFocusId = GalleryBookChrome.RibbonTabBaseId + 2;
    private const int CardComponentBase = 2000;

    private sealed record MomentItem(EventIdentity Identity, GalleryEvent? Story, WeddingDateRecord? WeddingDate)
    {
        internal bool IsWedding => Story is null;
    }

    private readonly GalleryViewContext context;
    private readonly GalleryPageState state;
    private readonly ITranslationHelper i18n;
    private readonly List<MomentItem> items = [];
    private int currentPage;
    private bool keyboardFocus;
    private Point lastMouse;
    private float menuScale = 1f;
    private int drawOffsetX, drawOffsetY, viewportWidth, viewportHeight;

    internal GalleryMomentsMenu(GalleryViewContext context, GalleryPageState state)
        : base(0, 0, GalleryDrawing.MenuWidth, GalleryDrawing.MenuHeight, true)
    {
        this.context = context;
        this.state = state;
        i18n = context.I18n;
        RebuildItems();

        int initialFocus = state.Focus;
        currentPage = initialFocus >= CardComponentBase && initialFocus < GalleryBookChrome.RibbonTabBaseId && items.Count > 0
            ? GalleryPagination.PageForIndex((initialFocus - CardComponentBase) / 2, items.Count, PageSize)
            : GalleryPagination.ClampPage(state.Scroll, items.Count, PageSize);

        RecalculateLayout();
        int firstIndex = GalleryPagination.FirstIndex(currentPage, items.Count, PageSize);
        Focus(initialFocus >= 0 ? initialFocus : items.Count > 0 ? CardComponentBase + firstIndex * 2 : DefaultEmptyFocusId);
    }

    private void RebuildItems()
    {
        items.Clear();
        if (context.Photos is not null)
        {
            foreach ((EventIdentity weddingId, EventPhotoSet set) in context.Photos.ReadWeddingMemories())
                items.Add(new MomentItem(weddingId, null, set.WeddingDate));
        }

        if (context.Favorites is not null && context.Catalog is not null)
        {
            foreach (EventIdentity favId in context.Favorites.All())
            {
                if (context.Catalog.Find(favId) is { Kind: not StoryKind.Internal } entry)
                    items.Add(new MomentItem(entry.Resolved.Identity, entry, null));
            }
        }
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
        }
        base.performHoverAction(x, y);
    }

    public override void update(GameTime time)
    {
        base.update(time);
        SaveState();
    }

    public override void receiveScrollWheelAction(int direction) => TurnPage(direction < 0 ? 1 : -1);
    public override void receiveRightClick(int x, int y, bool playSound = true) => Return();

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        (x, y) = ToLogical(x, y);
        if (upperRightCloseButton?.bounds.Contains(x, y) == true)
        {
            SaveState();
            context.Navigation.Close();
            return;
        }
        if (GalleryBookChrome.TryClickRibbonTab(x, y, GalleryBookTab.Moments, context.Navigation, SaveState))
            return;
        if (GalleryBookChrome.ToRect(GallerySpreadLayout.IndexPrevBounds).Contains(x, y))
        {
            TurnPage(-1);
            return;
        }
        if (GalleryBookChrome.ToRect(GallerySpreadLayout.IndexNextBounds).Contains(x, y))
        {
            TurnPage(1);
            return;
        }

        int first = GalleryPagination.FirstIndex(currentPage, items.Count, PageSize);
        int visible = GalleryPagination.VisibleCount(currentPage, items.Count, PageSize);
        for (int slot = 0; slot < visible; slot++)
        {
            if (CardBounds(slot).Contains(x, y))
            {
                OpenItem(first + slot);
                return;
            }
        }
    }

    public override void receiveKeyPress(Keys key)
    {
        if (key is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Enter or Keys.PageUp or Keys.PageDown)
            keyboardFocus = true;
        if (key == Keys.Escape || Game1.options.menuButton.Any(binding => binding.key == key))
        {
            Return();
            return;
        }
        if (key is Keys.PageUp or Keys.PageDown)
        {
            TurnPage(key == Keys.PageUp ? -1 : 1);
            return;
        }
        if (key == Keys.Enter)
        {
            Activate(currentlySnappedComponent?.myID ?? DefaultEmptyFocusId);
            return;
        }
        if (key is Keys.Up or Keys.Right or Keys.Down or Keys.Left)
        {
            applyMovementKey(key switch { Keys.Up => 0, Keys.Right => 1, Keys.Down => 2, _ => 3 });
            return;
        }
        base.receiveKeyPress(key);
    }

    public override void receiveGamePadButton(Buttons button)
    {
        if (button == Buttons.B)
        {
            Return();
            return;
        }
        if (button is Buttons.LeftShoulder or Buttons.RightShoulder)
        {
            TurnPage(button == Buttons.LeftShoulder ? -1 : 1);
            return;
        }
        base.receiveGamePadButton(button);
    }

    private void Activate(int id)
    {
        if (id >= GalleryBookChrome.RibbonTabBaseId && id < GalleryBookChrome.RibbonTabBaseId + GallerySpreadLayout.RibbonTabCount)
        {
            SaveState();
            Game1.playSound("shwip");
            context.Navigation.SwitchTab((GalleryBookTab)(id - GalleryBookChrome.RibbonTabBaseId));
            return;
        }
        if (id == GalleryBookChrome.PrevPageComponentId) { TurnPage(-1); return; }
        if (id == GalleryBookChrome.NextPageComponentId) { TurnPage(1); return; }
        if (id >= CardComponentBase)
            OpenItem((id - CardComponentBase) / 2);
    }

    private static (int Column, int Row) GridOf(int slot) => slot < 4 ? (slot % 2, slot / 2) : (2 + (slot - 4) % 2, (slot - 4) / 2);
    private static int SlotAt(int column, int row) => column < 2 ? row * 2 + column : 4 + row * 2 + column - 2;

    /// <summary>
    /// Lowest visible card in <paramref name="preferredColumn"/>; when that column has no cards on a partial page,
    /// prefers another column on the same page before falling back to the lowest row on the left page.
    /// </summary>
    private static int LowestIn(int preferredColumn, int first, int visible)
    {
        if (visible <= 0)
            return DefaultEmptyFocusId;
        int clampedCol = Math.Clamp(preferredColumn, 0, Columns - 1);
        for (int row = VisibleRows - 1; row >= 0; row--)
        {
            int s = SlotAt(clampedCol, row);
            if (s >= 0 && s < visible)
                return CardComponentBase + (first + s) * 2;
        }
        if (clampedCol >= 2)
        {
            int siblingRightCol = clampedCol == 2 ? 3 : 2;
            for (int row = VisibleRows - 1; row >= 0; row--)
            {
                int s = SlotAt(siblingRightCol, row);
                if (s >= 0 && s < visible)
                    return CardComponentBase + (first + s) * 2;
            }
        }
        for (int row = VisibleRows - 1; row >= 0; row--)
        {
            for (int col = 1; col >= 0; col--)
            {
                int s = SlotAt(col, row);
                if (s >= 0 && s < visible)
                    return CardComponentBase + (first + s) * 2;
            }
        }
        return CardComponentBase + (first + visible - 1) * 2;
    }

    public override void applyMovementKey(int direction)
    {
        if (direction is < 0 or > 3) return;
        int id = currentlySnappedComponent?.myID ?? DefaultEmptyFocusId;
        int first = GalleryPagination.FirstIndex(currentPage, items.Count, PageSize);
        int vis = GalleryPagination.VisibleCount(currentPage, items.Count, PageSize);

        if (id >= GalleryBookChrome.RibbonTabBaseId && id < GalleryBookChrome.RibbonTabBaseId + GallerySpreadLayout.RibbonTabCount)
        {
            int tab = id - GalleryBookChrome.RibbonTabBaseId;
            if (direction == 3 && tab > 0) Focus(GalleryBookChrome.RibbonTabBaseId + tab - 1);
            else if (direction == 1 && tab + 1 < GallerySpreadLayout.RibbonTabCount) Focus(GalleryBookChrome.RibbonTabBaseId + tab + 1);
            else if (direction == 1) Focus(GalleryBookChrome.PrevPageComponentId);
            else if (direction == 0 && vis > 0) Focus(LowestIn(Math.Min(tab, 1), first, vis));
            return;
        }

        if (id is GalleryBookChrome.PrevPageComponentId or GalleryBookChrome.NextPageComponentId)
        {
            if (direction == 3)
                Focus(id == GalleryBookChrome.NextPageComponentId ? GalleryBookChrome.PrevPageComponentId : GalleryBookChrome.RibbonTabBaseId + 2);
            else if (direction == 1 && id == GalleryBookChrome.PrevPageComponentId)
                Focus(GalleryBookChrome.NextPageComponentId);
            else if (direction == 0 && vis > 0)
                Focus(LowestIn(id == GalleryBookChrome.NextPageComponentId ? 3 : 2, first, vis));
            return;
        }

        if (id >= CardComponentBase && vis > 0)
        {
            int itemIndex = (id - CardComponentBase) / 2;
            int slot = Math.Clamp(itemIndex - first, 0, Math.Max(0, vis - 1));
            (int col, int row) = GridOf(slot);

            int SlotOrNone(int c, int r)
            {
                if (c < 0 || c > 3 || r < 0 || r > 1) return -1;
                int s = SlotAt(c, r);
                return s < vis ? s : -1;
            }

            if (direction == 1)
            {
                if (SlotOrNone(col + 1, row) is >= 0 and var nextRight)
                    Focus(CardComponentBase + (first + nextRight) * 2);
                else if (col == 1 && vis > 4)
                    Focus(CardComponentBase + (first + 4) * 2);
            }
            else if (direction == 3)
            {
                if (SlotOrNone(col - 1, row) is >= 0 and var nextLeft)
                    Focus(CardComponentBase + (first + nextLeft) * 2);
                else if (col == 2 && vis > 1)
                    Focus(CardComponentBase + (first + Math.Min(1, vis - 1)) * 2);
            }
            else if (direction == 0)
            {
                if (SlotOrNone(col, row - 1) is >= 0 and var nextUp)
                    Focus(CardComponentBase + (first + nextUp) * 2);
            }
            else if (direction == 2)
            {
                if (SlotOrNone(col, row + 1) is >= 0 and var nextDown)
                    Focus(CardComponentBase + (first + nextDown) * 2);
                else
                    Focus(col < 2 ? GalleryBookChrome.RibbonTabBaseId + Math.Min(col, 2)
                        : col == 2 ? GalleryBookChrome.PrevPageComponentId : GalleryBookChrome.NextPageComponentId);
            }
        }
    }

    public override void populateClickableComponentList() => BuildComponents();

    public override void snapToDefaultClickableComponent()
    {
        int first = GalleryPagination.FirstIndex(currentPage, items.Count, PageSize);
        Focus(items.Count > 0 ? CardComponentBase + first * 2 : DefaultEmptyFocusId);
    }

    public override void draw(SpriteBatch b)
    {
        if (GalleryLayout.Changed(viewportWidth, viewportHeight, Game1.uiViewport.Width, Game1.uiViewport.Height))
            RecalculateLayout();

        b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * .45f);
        (int mouseX, int mouseY) = ToLogical(Game1.getMouseX(true), Game1.getMouseY(true));
        bool showFocus = Game1.options.snappyMenus && Game1.options.gamepadControls || keyboardFocus;
        int focusedId = currentlySnappedComponent?.myID ?? DefaultEmptyFocusId;

        GalleryDrawing.BeginScaled(b, menuScale, drawOffsetX, drawOffsetY);
        GalleryBookChrome.DrawCleanHomeSpread(b, context.Textures.Home, width, height);
        GalleryBookChrome.DrawRibbonTabs(b, GalleryBookTab.Moments, focusedId, showFocus, new Point(mouseX, mouseY), i18n, out string? tooltip);
        GalleryBookChrome.DrawSpreadTitle(b, i18n.Get("tab.moments"), menuScale, drawOffsetX, drawOffsetY);

        if (items.Count == 0)
        {
            DrawEmptyState(b);
        }
        else
        {
            int first = GalleryPagination.FirstIndex(currentPage, items.Count, PageSize);
            int visible = GalleryPagination.VisibleCount(currentPage, items.Count, PageSize);
            for (int slot = 0; slot < visible; slot++)
            {
                int index = first + slot;
                DrawMomentCard(b, items[index], slot, index, mouseX, mouseY, showFocus, focusedId, ref tooltip);
            }
        }

        GalleryBookChrome.DrawPager(
            b,
            GallerySpreadLayout.IndexPrevBounds,
            GallerySpreadLayout.IndexIndicatorBounds,
            GallerySpreadLayout.IndexNextBounds,
            currentPage,
            items.Count,
            PageSize,
            focusedId,
            showFocus,
            new Point(mouseX, mouseY));

        upperRightCloseButton?.draw(b);
        GalleryDrawing.EndScaled(b);

        if (tooltip is not null)
            GalleryDrawing.DrawHoverTextSafe(b, GalleryDrawing.ParseTextSafe(tooltip, Game1.smallFont, Math.Min(600, Math.Max(200, viewportWidth - 80))), Game1.smallFont);
        drawMouse(b);
    }

    private void DrawEmptyState(SpriteBatch b)
    {
        Rectangle leftPhoto = new(268, 280, 265, 149);
        Rectangle leftText = new(168, 456, 468, 56);
        Rectangle rightBox = new(900, 260, 580, 260);
        GalleryBookChrome.DrawLockedStoryEnvelope(b, leftPhoto, i18n.Get("tab.moments"), i18n.Get("favorite.add"));
        GalleryUiSkin.DrawCardFrame(b, leftPhoto, false);
        GalleryDrawing.DrawCentered(b, i18n.Get("moments.empty-title"), leftText, GallerySpreadDrawing.Ink);
        string hint = GalleryDrawing.ParseTextSafe(i18n.Get("moments.empty-hint"), Game1.smallFont, rightBox.Width - 40);
        GalleryDrawing.DrawCentered(b, hint, rightBox, GallerySpreadDrawing.Ink);
    }

    private void DrawMomentCard(
        SpriteBatch b,
        MomentItem item,
        int slot,
        int index,
        int mouseX,
        int mouseY,
        bool showFocus,
        int focusedId,
        ref string? tooltip)
    {
        Rectangle card = CardBounds(slot);
        Rectangle photo = CardPhotoBounds(slot);
        bool cardHovered = card.Contains(mouseX, mouseY);
        bool cardFocused = showFocus && focusedId >= CardComponentBase && focusedId < GalleryBookChrome.RibbonTabBaseId
            && (focusedId - CardComponentBase) / 2 == index;
        bool hot = cardHovered || cardFocused;

        Texture2D? cover = context.Photos.Cover(item.Identity);
        string plaqueTitle;
        string hoverSubtitle;

        if (item.IsWedding)
        {
            string spouseName = WeddingMemento.TryGetSpouseName(item.Identity) ?? item.Identity.StorageEventId;
            string spouseDisplay = context.Characters.Get(spouseName);
            plaqueTitle = spouseDisplay + GalleryDrawing.TextSeparator + i18n.Get("pill.wedding");
            string datePillLabel;
            if (item.WeddingDate is { } date)
            {
                string seasonKey = "season." + (WeddingDateRecord.NormalizeSeason(date.Season) ?? "spring");
                hoverSubtitle = i18n.Get("wedding.subtitle", new { year = date.Year, season = i18n.Get(seasonKey), day = date.DayOfMonth });
                datePillLabel = i18n.Get("wedding.album-card-dated", new { year = date.Year, season = i18n.Get(seasonKey), day = date.DayOfMonth });
            }
            else
            {
                hoverSubtitle = i18n.Get("wedding.album-card");
                datePillLabel = i18n.Get("pill.wedding");
            }

            GalleryPhotos.DrawCover(b, cover ?? context.Textures.Thumbnail, photo, Color.White);
            GalleryUiSkin.DrawCardFrame(b, photo, hot);
            GalleryUiSkin.DrawTitlePlaque(b, new Rectangle(photo.X - 8, photo.Y - 48, photo.Width + 16, 30), plaqueTitle, hot);
            GalleryUiSkin.DrawPill(b, new Point(photo.X + 8, photo.Y + 8), 26, datePillLabel, UiPill.Amber, UiBadge.Rings, maxWidth: 210);
        }
        else
        {
            GalleryEvent entry = item.Story!;
            bool unlocked = context.IsStoryUnlocked(entry);
            bool parallel = context.IsParallelBranch(entry);
            bool seen = Game1.player?.eventsSeen.Contains(entry.EventId) == true;

            if (cover is not null)
                GalleryPhotos.DrawCover(b, cover, photo, unlocked ? Color.White : new Color(215, 210, 198));
            else if (!seen)
                GalleryBookChrome.DrawLockedStoryEnvelope(b, photo, context.CardPrimaryLead(entry), context.Locations.Get(entry.LocationName), parallel && unlocked);
            else
                GalleryPhotos.DrawCover(b, context.Textures.Thumbnail, photo, Color.White);

            GalleryUiSkin.DrawCardFrame(b, photo, hot);

            string? ownerName = entry.Ownership.Owners.Count == 1 ? entry.Ownership.Owners[0].Name
                : entry.RelatedNpcNames.Count == 1 ? entry.RelatedNpcNames[0] : null;
            string storyTitle = context.CardTitle(entry, ownerName);
            plaqueTitle = ownerName is not null
                ? context.Characters.Get(ownerName) + GalleryDrawing.TextSeparator + storyTitle
                : storyTitle;
            string npcText = string.Join(", ", entry.RelatedNpcNames.Select(context.Characters.Get).Where(s => s.Length > 0));
            hoverSubtitle = npcText.Length > 0 ? npcText + GalleryDrawing.TextSeparator + context.Locations.Get(entry.LocationName) : context.Locations.Get(entry.LocationName);

            GalleryUiSkin.DrawTitlePlaque(b, new Rectangle(photo.X - 8, photo.Y - 48, photo.Width + 16, 30), plaqueTitle, hot);

            Point pillAt = new(photo.X + 8, photo.Y + 8);
            EncounterClue clue = context.EventClue(entry);
            if (clue == EncounterClue.AvailableToday)
                GalleryUiSkin.DrawPill(b, pillAt, 26, i18n.Get("clue.available-today"), UiPill.Amber, maxWidth: 180);
            else if (parallel)
                GalleryUiSkin.DrawPill(b, pillAt, 26, i18n.Get("pill.parallel"), UiPill.Amber, UiBadge.Branch, maxWidth: 180);
            else if (!unlocked)
                GalleryUiSkin.DrawPill(b, pillAt, 26, i18n.Get("pill.locked"), UiPill.Wood, maxWidth: 180);
            else if (!seen)
                GalleryUiSkin.DrawPill(b, pillAt, 26, i18n.Get("pill.unseen"), UiPill.Wood, maxWidth: 180);

            GalleryUiSkin.DrawFavoriteRibbon(b, photo);
        }

        if (hot)
        {
            List<string> lines = [plaqueTitle, hoverSubtitle];
            if (item.IsWedding)
                lines.Add(i18n.Get("wedding.card-hint"));
            else if (item.Story is { } st && context.StoryQuote(st) is { Length: > 0 } quote)
                lines.Add(i18n.Get("event-detail.quote", new { quote }));
            tooltip = string.Join("\n", lines);
        }
    }

    private static Rectangle CardPhotoBounds(int slot)
        => GalleryBookChrome.ToRect(GallerySpreadLayout.MomentPhotoBounds(slot));

    private static Rectangle CardBounds(int slot)
    {
        Rectangle photo = CardPhotoBounds(slot);
        return new Rectangle(photo.X - 16, photo.Y - 48, photo.Width + 32, 211);
    }

    private void OpenItem(int index)
    {
        if (index < 0 || index >= items.Count) return;
        MomentItem item = items[index];
        Focus(CardComponentBase + index * 2);
        SaveState();
        if (item.IsWedding)
        {
            context.Navigation.OpenPhotos(item.Identity);
            return;
        }
        context.Navigation.OpenEvent(item.Identity);
    }

    private void TurnPage(int delta)
    {
        var (nextPage, changed) = GalleryPagination.Turn(currentPage, delta, items.Count, PageSize);
        if (!changed)
            return;
        int prevId = currentlySnappedComponent?.myID ?? DefaultEmptyFocusId;
        currentPage = nextPage;
        BuildComponents();
        if (prevId >= CardComponentBase && prevId < GalleryBookChrome.RibbonTabBaseId && items.Count > 0)
            Focus(CardComponentBase + GalleryPagination.FirstIndex(currentPage, items.Count, PageSize) * 2);
        Game1.playSound("shwip");
    }

    private void RecalculateLayout()
    {
        width = GalleryDrawing.MenuWidth;
        height = GalleryDrawing.MenuHeight;
        xPositionOnScreen = yPositionOnScreen = 0;
        viewportWidth = Game1.uiViewport.Width;
        viewportHeight = Game1.uiViewport.Height;
        menuScale = (float)GalleryLayout.ScaleToFit(viewportWidth, viewportHeight, width, height, 24);
        drawOffsetX = (int)Math.Round((viewportWidth - width * menuScale) / 2f);
        drawOffsetY = (int)Math.Round((viewportHeight - height * menuScale) / 2f);
        initializeUpperRightCloseButton();
        GalleryBookChrome.PositionCloseButton(upperRightCloseButton, menuScale, drawOffsetX, drawOffsetY, viewportWidth, viewportHeight);
        BuildComponents();
    }

    private void BuildComponents()
    {
        int previous = currentlySnappedComponent?.myID ?? DefaultEmptyFocusId;
        currentPage = GalleryPagination.ClampPage(currentPage, items.Count, PageSize);
        int first = GalleryPagination.FirstIndex(currentPage, items.Count, PageSize);
        int visible = GalleryPagination.VisibleCount(currentPage, items.Count, PageSize);

        allClickableComponents = [];
        GalleryBookChrome.AddRibbonTabComponents(allClickableComponents, ToScreen, GalleryBookChrome.PrevPageComponentId,
            upNeighborId: visible > 0 ? LowestIn(0, first, visible) : -1);
        foreach (ClickableComponent ribbon in allClickableComponents.Where(c => c.myID >= GalleryBookChrome.RibbonTabBaseId && c.myID < GalleryBookChrome.RibbonTabBaseId + GallerySpreadLayout.RibbonTabCount))
            ribbon.upNeighborID = visible > 0 ? LowestIn(Math.Min(ribbon.myID - GalleryBookChrome.RibbonTabBaseId, 1), first, visible) : -1;
        allClickableComponents.Add(new ClickableComponent(ToScreen(GalleryBookChrome.ToRect(GallerySpreadLayout.IndexPrevBounds)), "prev-page")
        {
            myID = GalleryBookChrome.PrevPageComponentId,
            leftNeighborID = GalleryBookChrome.RibbonTabBaseId + 2,
            rightNeighborID = GalleryBookChrome.NextPageComponentId,
            upNeighborID = visible > 0 ? LowestIn(2, first, visible) : -1,
            fullyImmutable = true
        });
        allClickableComponents.Add(new ClickableComponent(ToScreen(GalleryBookChrome.ToRect(GallerySpreadLayout.IndexNextBounds)), "next-page")
        {
            myID = GalleryBookChrome.NextPageComponentId,
            leftNeighborID = GalleryBookChrome.PrevPageComponentId,
            rightNeighborID = -1,
            upNeighborID = visible > 0 ? LowestIn(3, first, visible) : -1,
            fullyImmutable = true
        });

        for (int slot = 0; slot < visible; slot++)
        {
            int index = first + slot;
            allClickableComponents.Add(new ClickableComponent(ToScreen(CardBounds(slot)), $"moment-{index}")
            {
                myID = CardComponentBase + index * 2
            });
        }

        currentlySnappedComponent = allClickableComponents.FirstOrDefault(c => c.myID == previous)
            ?? (visible > 0 ? allClickableComponents.FirstOrDefault(c => c.myID == CardComponentBase + first * 2) : null)
            ?? allClickableComponents.FirstOrDefault(c => c.myID == DefaultEmptyFocusId)
            ?? allClickableComponents[0];
        SaveState();
    }

    private void Focus(int id)
    {
        currentlySnappedComponent = allClickableComponents.FirstOrDefault(c => c.myID == id)
            ?? allClickableComponents.FirstOrDefault(c => c.myID == DefaultEmptyFocusId)
            ?? allClickableComponents[0];
        SaveState();
        if (Game1.options.snappyMenus && Game1.options.gamepadControls)
            snapCursorToCurrentSnappedComponent();
    }

    private void SaveState()
    {
        state.Scroll = currentPage;
        state.Focus = currentlySnappedComponent?.myID ?? DefaultEmptyFocusId;
    }

    private void Return()
    {
        Game1.playSound("bigDeSelect");
        SaveState();
        context.Navigation.Back();
    }

    private Rectangle ToScreen(Rectangle bounds) => GalleryDrawing.ScaleRectangle(bounds, menuScale, drawOffsetX, drawOffsetY);
    private (int X, int Y) ToLogical(int x, int y) => ((int)Math.Round((x - drawOffsetX) / menuScale), (int)Math.Round((y - drawOffsetY) / menuScale));
}
