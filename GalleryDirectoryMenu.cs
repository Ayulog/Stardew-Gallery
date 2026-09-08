using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace StardewGallery;

internal sealed class GalleryDirectoryMenu : IClickableMenu, IGallerySearchMenu
{
    private readonly GalleryCatalog catalog;
    private readonly GalleryCharacter? character;
    private readonly GalleryCharacterPanel? panel;
    private readonly ITranslationHelper i18n;
    private readonly Texture2D background, thumbnail, frame, scrollTexture, replayGlyph;
    private readonly GalleryPhotos photos;
    private readonly Func<bool> isUnlocked;
    private readonly Action back;
    private readonly Action<GalleryEvent, Action> openDetail, replay;
    private readonly IReadOnlyList<GalleryEventGroup> groups;
    private readonly Dictionary<EventIdentity, string> locations;
    private readonly Dictionary<string, string> npcNames;
    private readonly TextBox search;
    private List<GalleryEventGroup> filtered = [];
    private Dictionary<string, IReadOnlyList<GalleryEvent>> scopedSources = [];
    private List<(string Asset, string Label, int Count)> places = [];
    private GalleryEventCategory category;
    private string? asset;
    private string lastQuery = "\0";
    private string validatedSearchText = "\0";
    private int page, placeOffset, sourcePage, returnFocus = 300, listFocus = 300;
    private GalleryEventGroup? sourceGroup;
    private IReadOnlyList<GalleryEvent> sources = [];
    private float scale;
    private int offsetX, offsetY, viewportWidth, viewportHeight;
    private bool dragging;
    private int dragOffset;
    private Rectangle track, thumb;
    private const int PageSize = 6, SourcePageSize = 7;
    private static readonly GalleryEventCategory[] GlobalCategories = [GalleryEventCategory.All, GalleryEventCategory.Heart, GalleryEventCategory.Ordinary, GalleryEventCategory.Flow];
    private static readonly GalleryEventCategory[] CharacterCategories = [GalleryEventCategory.All, GalleryEventCategory.Heart, GalleryEventCategory.Ordinary];
    private Rectangle SearchBounds => new(188, 139, 570, 48);
    private Rectangle BackBounds => character is null ? new(188, 810, 368, 72) : new(735, 810, 368, 72);
    private Rectangle PreviousBounds => character is null ? new(984, 786, 48, 44) : new(1150, 824, 48, 44);
    private Rectangle NextBounds => character is null ? new(1370, 786, 48, 44) : new(1412, 824, 48, 44);
    private int CurrentPage => sourceGroup is null ? page : sourcePage;
    private int MaxPage => Math.Max(0, ((sourceGroup is null ? filtered.Count : sources.Count) - 1) / (sourceGroup is null ? PageSize : SourcePageSize));
    public bool IsSearchSelected => character is null && sourceGroup is null && search.Selected;

    internal GalleryDirectoryMenu(GalleryCatalog catalog, GalleryCharacter? character, ITranslationHelper i18n,
        Texture2D background, Texture2D scene, Texture2D thumbnail, Texture2D frame, Texture2D scrollTexture,
        Texture2D replayGlyph, GalleryPhotos photos, Func<bool> isUnlocked, Action back,
        Action<GalleryEvent, Action> openDetail, Action<GalleryEvent, Action> replay,
        string initialQuery = "", GalleryEventGroup? initialGroup = null)
        : base(0, 0, GalleryMenu.MenuWidth, GalleryMenu.MenuHeight, true)
    {
        this.catalog = catalog; this.character = character; this.i18n = i18n; this.background = background;
        this.thumbnail = thumbnail; this.frame = frame; this.scrollTexture = scrollTexture; this.replayGlyph = replayGlyph;
        this.photos = photos; this.isUnlocked = isUnlocked; this.back = back; this.openDetail = openDetail; this.replay = replay;
        groups = catalog.Groups.Count > 0 ? catalog.Groups : GalleryEventGroup.Build(catalog.Events.Concat(catalog.ExcludedEvents));
        locations = groups.SelectMany(group => group.Sources).DistinctBy(entry => entry.Resolved.Identity)
            .ToDictionary(entry => entry.Resolved.Identity, entry => GalleryConditionPresentation.Location(entry, i18n));
        npcNames = catalog.Characters.ToDictionary(npc => npc.Name, npc => npc.DisplayName, StringComparer.Ordinal);
        if (character is not null)
            panel = new GalleryCharacterPanel(character, groups.Where(group => !group.IsFlow && group.NpcNames.Contains(character.Name)).Select(group => group.Representative).ToArray(), i18n, scene);
        search = new TextBox(Game1.content.Load<Texture2D>("LooseSprites/textBox"), null, Game1.smallFont, Game1.textColor)
            { Text = character is null ? initialQuery : "" };
        if (character is not null && initialQuery.Length > 0 && groups.Any(group => group.IsFlow && group.NpcNames.Contains(character.Name)
            && group.Representative.EventId.Contains(initialQuery, StringComparison.OrdinalIgnoreCase))) search.Text = initialQuery;
        search.OnEnterPressed += _ => OpenFirstMatch();
        RecalculateLayout(); Refresh();
        if (initialGroup is not null) SelectGroup(initialGroup);
        else if (character is not null && initialQuery.Length > 0)
        {
            int index = filtered.FindIndex(group => group.Representative.EventId.Contains(initialQuery, StringComparison.OrdinalIgnoreCase));
            if (index >= 0) { page = index / PageSize; returnFocus = 300 + index % PageSize * 2; }
        }
        Restore();
    }

    private string NpcName(string name) => npcNames.GetValueOrDefault(name, name);
    private string Location(GalleryEvent entry) => locations[entry.Resolved.Identity];
    private GalleryEventCategory[] Categories => character is null ? GlobalCategories : CharacterCategories;

    private void Refresh(bool force = false)
    {
        if (!force && search.Text == validatedSearchText) return;
        search.Text = GallerySearchInput.CleanText(search.Text);
        validatedSearchText = search.Text;
        string query = search.Text.Trim();
        if (!force && query == lastQuery) return;
        lastQuery = query;
        List<GalleryEventGroup> candidates = groups.Where(group => group.Matches(category, character?.Name, null, query, NpcName, Location)).ToList();
        places = candidates.SelectMany(group => group.Sources.Select(entry => (Group: group.Key, Entry: entry)))
            .GroupBy(item => item.Entry.AssetName, StringComparer.OrdinalIgnoreCase)
            .Select(group => (group.Key, Location(group.First().Entry), group.Select(item => item.Group).Distinct().Count()))
            .OrderBy(place => place.Item2, StringComparer.CurrentCultureIgnoreCase).ThenBy(place => place.Key, StringComparer.Ordinal).ToList();
        filtered = candidates.Where(group => asset is null || group.Sources.Any(entry => entry.AssetName.Equals(asset, StringComparison.OrdinalIgnoreCase))).ToList();
        scopedSources = filtered.ToDictionary(group => group.Key, group => asset is null ? group.Sources
            : (IReadOnlyList<GalleryEvent>)group.Sources.Where(entry => entry.AssetName.Equals(asset, StringComparison.OrdinalIgnoreCase)).ToArray(), StringComparer.Ordinal);
        page = 0; placeOffset = Math.Clamp(placeOffset, 0, Math.Max(0, places.Count - 6));
        BuildComponents();
    }

    private bool CanReplay(GalleryEvent entry) => GalleryEventNavigation.IsReplayListed(catalog, entry)
        && (isUnlocked() || Game1.player.eventsSeen.Contains(entry.EventId));
    private void SelectGroup(GalleryEventGroup group)
    {
        sources = group.Sources.Where(entry => asset is null || entry.AssetName.Equals(asset, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (sources.Count == 1) { Open(sources[0], false); return; }
        DeselectSearch(); listFocus = returnFocus; sourceGroup = group; sourcePage = 0; returnFocus = 300; BuildComponents(); Snap(300);
    }
    private void Open(GalleryEvent entry, bool playback)
    {
        DeselectSearch(); returnFocus = currentlySnappedComponent?.myID ?? returnFocus;
        if (playback && CanReplay(entry)) replay(entry, Restore);
        else openDetail(entry, Restore);
    }
    internal void Restore()
    {
        Game1.activeClickableMenu = this;
        RecalculateLayout(); BuildComponents(); Snap(returnFocus);
    }
    public void DeselectSearch()
    {
        search.Selected = false;
        if (Game1.keyboardDispatcher.Subscriber == search) Game1.keyboardDispatcher.Subscriber = null;
    }
    public void OpenFirstMatch()
    {
        Refresh();
        returnFocus = 300;
        DeselectSearch();
        Snap(returnFocus);
        if (sourceGroup is not null) { if (sources.Count > 0) Open(sources[sourcePage * SourcePageSize], false); }
        else if (filtered.Count > 0) SelectGroup(filtered[page * PageSize]);
    }
    public void HandleControllerBack()
    {
        if (IsSearchSelected) { DeselectSearch(); Snap(20); return; }
        if (sourceGroup is not null) { sourceGroup = null; returnFocus = listFocus; BuildComponents(); Snap(returnFocus); return; }
        DeselectSearch(); back();
    }
    public override void receiveKeyPress(Keys key)
    {
        if (IsSearchSelected) { if (key == Keys.Escape) DeselectSearch(); return; }
        if (key == Keys.Escape) HandleControllerBack();
        else if (key == Keys.Enter) ActivateFocused();
        else base.receiveKeyPress(key);
    }
    public override void receiveGamePadButton(Buttons button)
    {
        if (button == Buttons.A) ActivateFocused();
        else if (button != Buttons.B) base.receiveGamePadButton(button);
    }
    private void ActivateFocused()
    {
        if (currentlySnappedComponent is { } component) receiveLeftClick(component.bounds.Center.X, component.bounds.Center.Y);
        else OpenFirstMatch();
    }
    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        Point point = Logical(x, y);
        if (IsSearchSelected && !SearchBounds.Contains(point)) DeselectSearch();
        ClickableComponent? hit = allClickableComponents.FirstOrDefault(component => component.bounds.Contains(x, y));
        if (hit is not null)
        {
            currentlySnappedComponent = hit; int id = hit.myID;
            if (id == 10) HandleControllerBack();
            else if (id == 11 || id == 12) ChangePage(id == 11 ? -1 : 1);
            else if (id == 20 && GallerySearchInputGuard.Ready) search.SelectMe();
            else if (id == 21 || id == 22) { placeOffset = Math.Clamp(placeOffset + (id == 21 ? -6 : 6), 0, Math.Max(0, places.Count - 6)); BuildComponents(); Snap(id); }
            else if (id >= 100 && id < 104) { category = Categories[id - 100]; asset = null; placeOffset = 0; if (character is not null) search.Text = ""; Refresh(true); Snap(id); }
            else if (id == 200) { asset = null; Refresh(true); Snap(id); }
            else if (id > 200 && id < 207) { asset = places[placeOffset + id - 201].Asset; Refresh(true); Snap(id); }
            else if (id >= 300 && id < 314)
            {
                returnFocus = id;
                if (sourceGroup is not null) Open(sources[sourcePage * SourcePageSize + id - 300], false);
                else
                {
                    GalleryEventGroup group = filtered[page * PageSize + (id - 300) / 2];
                    IReadOnlyList<GalleryEvent> selected = scopedSources[group.Key];
                    if ((id - 300) % 2 == 1 && selected.Count == 1) Open(selected[0], true);
                    else SelectGroup(group);
                }
            }
            return;
        }
        if (thumb.Contains(point)) { dragging = true; dragOffset = point.Y - thumb.Y; return; }
        if (track.Contains(point)) { ChangePage(point.Y < thumb.Y ? -1 : 1); return; }
        base.receiveLeftClick(point.X, point.Y, playSound);
    }
    public override void leftClickHeld(int x, int y)
    {
        if (!dragging || MaxPage == 0) return;
        int next = (int)Math.Round(Math.Clamp(Logical(x, y).Y - dragOffset - track.Y, 0, track.Height - thumb.Height) * MaxPage / (double)(track.Height - thumb.Height));
        if (next != CurrentPage) ChangePage(next - CurrentPage);
    }
    public override void releaseLeftClick(int x, int y) { dragging = false; }
    public override void receiveScrollWheelAction(int direction)
    {
        Point cursor = Logical(Game1.getMouseX(true), Game1.getMouseY(true));
        if (character is null && sourceGroup is null && new Rectangle(188, 390, 570, 370).Contains(cursor))
        {
            placeOffset = Math.Clamp(placeOffset + (direction < 0 ? 1 : -1), 0, Math.Max(0, places.Count - 6)); BuildComponents();
        }
        else ChangePage(direction < 0 ? 1 : -1);
    }
    private void ChangePage(int delta)
    {
        if (sourceGroup is null) page = Math.Clamp(page + delta, 0, MaxPage);
        else sourcePage = Math.Clamp(sourcePage + delta, 0, MaxPage);
        BuildComponents();
    }
    public override void update(GameTime time) { base.update(time); if (sourceGroup is null) Refresh(); }
    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds) { RecalculateLayout(); BuildComponents(); }
    protected override void cleanupBeforeExit() { DeselectSearch(); base.cleanupBeforeExit(); }
    public override void snapToDefaultClickableComponent() => Snap(returnFocus);
    private void Snap(int id)
    {
        currentlySnappedComponent = allClickableComponents.FirstOrDefault(component => component.myID == id)
            ?? allClickableComponents.FirstOrDefault(component => component.myID >= 300) ?? allClickableComponents.FirstOrDefault();
        if (Game1.options.snappyMenus && Game1.options.gamepadControls) snapCursorToCurrentSnappedComponent();
    }
    public override void applyMovementKey(int direction)
    {
        if (currentlySnappedComponent is not { } current) { Snap(returnFocus); return; }
        Point center = current.bounds.Center;
        var next = allClickableComponents.Where(component => component != current).Select(component =>
        {
            int dx = component.bounds.Center.X - center.X, dy = component.bounds.Center.Y - center.Y;
            int along = direction switch { 0 => -dy, 1 => dx, 2 => dy, _ => -dx };
            return (component, along, cross: Math.Abs(direction % 2 == 0 ? dx : dy));
        }).Where(item => item.along > 0).OrderBy(item => item.along + item.cross * 3).FirstOrDefault();
        if (next.component is not null) Snap(next.component.myID);
    }
    private Rectangle Card(int slot) => character is null
        ? new(899 + slot % 2 * 312, 208 + slot / 2 * 192, 285, 178)
        : new(755 + slot % 2 * 365, 194 + slot / 2 * 202, 345, 194);
    private Rectangle CardImage(int slot)
    {
        Rectangle card = Card(slot); int height = card.Height - 69, width = height * 16 / 9;
        return new(card.Center.X - width / 2, card.Y + 38, width, height);
    }
    private Rectangle Tab(int index)
    {
        int left = character is null ? 188 : 755, top = character is null ? 262 : 139;
        int width = (character is null ? 570 : 710) / Categories.Length;
        return new(left + index * width, top, width, 44);
    }
    private void RecalculateLayout()
    {
        width = GalleryMenu.MenuWidth; height = GalleryMenu.MenuHeight;
        scale = (float)GalleryLayout.ScaleToFit(Game1.uiViewport.Width, Game1.uiViewport.Height, width, height, 24);
        offsetX = (int)Math.Round((Game1.uiViewport.Width - width * scale) / 2f);
        offsetY = (int)Math.Round((Game1.uiViewport.Height - height * scale) / 2f);
        viewportWidth = Game1.uiViewport.Width; viewportHeight = Game1.uiViewport.Height;
        search.X = SearchBounds.X; search.Y = SearchBounds.Y; search.Width = SearchBounds.Width;
        initializeUpperRightCloseButton();
        track = character is null ? new(1534, 206, 24, 578) : new(1508, 196, 24, 585);
    }
    private void BuildComponents()
    {
        int focused = currentlySnappedComponent?.myID ?? returnFocus;
        allClickableComponents = [];
        void Add(int id, Rectangle bounds) => allClickableComponents.Add(new ClickableComponent(GalleryMenu.ScaleRectangle(bounds, scale, offsetX, offsetY), id.ToString()) { myID = id });
        Add(10, BackBounds); if (CurrentPage > 0) Add(11, PreviousBounds); if (CurrentPage < MaxPage) Add(12, NextBounds);
        if (sourceGroup is null)
        {
            for (int i = 0; i < Categories.Length; i++) Add(100 + i, Tab(i));
            if (character is null)
            {
                Add(20, SearchBounds); Add(200, new(188, 391, 570, 45));
                for (int i = 0; i < 6 && placeOffset + i < places.Count; i++) Add(201 + i, new(188, 444 + i * 53, 570, 45));
                if (placeOffset > 0) Add(21, new(636, 335, 48, 40));
                if (placeOffset + 6 < places.Count) Add(22, new(704, 335, 48, 40));
            }
            for (int slot = 0; slot < PageSize && page * PageSize + slot < filtered.Count; slot++)
            {
                Rectangle card = Card(slot); Add(300 + slot * 2, new(card.X, card.Y, card.Width, 36));
                Add(301 + slot * 2, new(card.X, card.Y + 36, card.Width, card.Height - 36));
            }
        }
        else for (int i = 0; i < SourcePageSize && sourcePage * SourcePageSize + i < sources.Count; i++) Add(300 + i, new(899, 216 + i * 80, 588, 72));
        thumb = new(track.X, track.Y + (MaxPage == 0 ? 0 : (track.Height - 40) * CurrentPage / MaxPage), track.Width, 40);
        currentlySnappedComponent = allClickableComponents.FirstOrDefault(component => component.myID == focused);
    }
    private Point Logical(int x, int y) => new((int)Math.Round((x - offsetX) / scale), (int)Math.Round((y - offsetY) / scale));
    private void Rule(SpriteBatch b, Rectangle bounds, Color? color = null) => b.Draw(Game1.staminaRect, bounds, color ?? GallerySpreadDrawing.LightInk * .35f);
    public override void draw(SpriteBatch b)
    {
        if (GalleryLayout.Changed(viewportWidth, viewportHeight, Game1.uiViewport.Width, Game1.uiViewport.Height)) { RecalculateLayout(); BuildComponents(); }
        b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * .45f);
        GalleryMenu.BeginScaled(b, scale, offsetX, offsetY);
        panel?.DrawPhoto(b); b.Draw(background, new Rectangle(0, 0, width, height), Color.White); panel?.DrawInformation(b);
        GalleryMenu.DrawCentered(b, character is null ? i18n.Get(sourceGroup is null ? "directory.title" : "directory.sources-title") : i18n.Get("directory.character-title", new { name = character.DisplayName }),
            character is null ? new Rectangle(650, 35, 372, 65) : new Rectangle(895, 68, 530, 56));
        if (sourceGroup is null) DrawList(b); else DrawSources(b);
        GalleryMenu.DrawScrollbarTrack(b, scrollTexture, track); if (MaxPage > 0) GalleryMenu.DrawScrollbar(b, thumb);
        GalleryMenu.DrawButton(b, BackBounds, i18n.Get(sourceGroup is null ? "detail.back" : "event-detail.back"));
        if (CurrentPage > 0) DrawArrow(b, PreviousBounds, false);
        if (CurrentPage < MaxPage) DrawArrow(b, NextBounds, true);
        GalleryMenu.DrawCentered(b, $"{((sourceGroup is null ? filtered.Count : sources.Count) == 0 ? 0 : CurrentPage + 1)} / {((sourceGroup is null ? filtered.Count : sources.Count) == 0 ? 0 : MaxPage + 1)}",
            character is null ? new Rectangle(1085, 790, 220, 30) : new Rectangle(1208, 827, 194, 36));
        if (currentlySnappedComponent is { } focused && Game1.options.snappyMenus && Game1.options.gamepadControls)
        {
            Point at = Logical(focused.bounds.X, focused.bounds.Y);
            Rule(b, new(at.X, at.Y, Math.Max(1, (int)(focused.bounds.Width / scale)), 2), GallerySpreadDrawing.Ink);
        }
        upperRightCloseButton?.draw(b); GalleryMenu.EndScaled(b); drawMouse(b);
        DrawTooltip(b);
    }
    private void DrawList(SpriteBatch b)
    {
        if (character is null)
        {
            search.Draw(b);
            if (search.Text.Length == 0 && !search.Selected) GalleryMenu.DrawLeftFitted(b, i18n.Get(GallerySearchInputGuard.Ready ? "directory.search" : "home.search-unavailable"), new(200, 147, 546, 32));
            GalleryMenu.DrawLeftFitted(b, i18n.Get("directory.category"), new(194, 218, 550, 34));
            GalleryMenu.DrawLeftFitted(b, i18n.Get("directory.locations"), new(194, 340, 435, 34));
            if (placeOffset > 0) DrawArrow(b, new(636, 335, 40, 36), false);
            if (placeOffset + 6 < places.Count) DrawArrow(b, new(704, 335, 40, 36), true);
            DrawPlace(b, i18n.Get("directory.all-locations"), null, 393, null);
            for (int i = 0; i < 6 && placeOffset + i < places.Count; i++)
            {
                var place = places[placeOffset + i]; DrawPlace(b, place.Label, place.Asset, 446 + i * 53, place.Count);
            }
            GalleryMenu.DrawLeftFitted(b, i18n.Get("directory." + category.ToString().ToLowerInvariant()), new(900, 139, 350, 36));
            GalleryMenu.DrawCentered(b, i18n.Get("directory.count", new { count = filtered.Count }), new(1270, 139, 225, 36));
        }
        for (int i = 0; i < Categories.Length; i++)
        {
            Rectangle tab = Tab(i);
            GalleryMenu.DrawCentered(b, i18n.Get("directory." + Categories[i].ToString().ToLowerInvariant()), new(tab.X + 8, tab.Y, tab.Width - 16, 36));
            Rule(b, new(tab.X, tab.Bottom - 2, tab.Width, 1));
            if (category == Categories[i]) Rule(b, new(tab.X + 12, tab.Bottom - 4, tab.Width - 24, 3), GallerySpreadDrawing.Ink);
        }
        for (int slot = 0; slot < PageSize && page * PageSize + slot < filtered.Count; slot++)
        {
            GalleryEventGroup group = filtered[page * PageSize + slot]; IReadOnlyList<GalleryEvent> selected = scopedSources[group.Key]; GalleryEvent entry = selected[0];
            Rectangle card = Card(slot), image = CardImage(slot);
            b.Draw(frame, card, Color.White); GalleryPhotos.DrawCover(b, photos.Cover(entry.Resolved.Identity) ?? thumbnail, image, Color.White);
            GalleryMenu.DrawLeftFitted(b, "ID " + entry.EventId, new(card.X + 12, card.Y + 5, card.Width - 126, 28));
            GalleryMenu.DrawCentered(b, selected.Count > 1 ? i18n.Get("directory.sources", new { count = selected.Count }) : i18n.Get("event.details-short"), new(card.Right - 112, card.Y + 5, 100, 28));
            GalleryMenu.DrawCentered(b, selected.Count > 1 ? i18n.Get("directory.multiple-locations") : Location(entry), new(card.X + 12, card.Bottom - 29, card.Width - 24, 24));
            if (selected.Count == 1 && CanReplay(entry)) b.Draw(replayGlyph, new Rectangle(image.Right - 36, image.Bottom - 36, 32, 32), Color.White * .85f);
        }
        if (filtered.Count == 0) GalleryMenu.DrawCentered(b, Game1.parseText(i18n.Get("nav.no-results"), Game1.smallFont, 530), new(910, 350, 530, 180));
    }
    private void DrawPlace(SpriteBatch b, string label, string? value, int y, int? count)
    {
        if (string.Equals(asset, value, StringComparison.OrdinalIgnoreCase)) Rule(b, new(188, y - 2, 4, 45), GallerySpreadDrawing.Ink);
        GalleryMenu.DrawLeftFitted(b, label, new(211, y, 426, 36));
        if (count is int number) GalleryMenu.DrawCentered(b, number.ToString(), new(661, y, 74, 36));
    }
    private void DrawSources(SpriteBatch b)
    {
        GalleryEvent entry = sourceGroup!.Representative;
        if (character is null)
        {
            GalleryPhotos.DrawCover(b, photos.Cover(entry.Resolved.Identity) ?? thumbnail, new(210, 186, 530, 298), Color.White);
            GalleryMenu.DrawCentered(b, "ID " + entry.EventId, new(200, 517, 550, 40));
            GalleryMenu.DrawCentered(b, string.Join(", ", sourceGroup.NpcNames.Select(NpcName)), new(200, 575, 550, 38));
        }
        GalleryMenu.DrawLeftFitted(b, i18n.Get("directory.select-source"), new(911, 155, 570, 42));
        for (int i = 0; i < SourcePageSize && sourcePage * SourcePageSize + i < sources.Count; i++)
        {
            GalleryEvent source = sources[sourcePage * SourcePageSize + i]; int y = 216 + i * 80;
            GalleryMenu.DrawLeftFitted(b, Location(source), new(911, y, 430, 32));
            GalleryMenu.DrawCentered(b, i18n.Get("event.details-short"), new(1370, y, 106, 32));
            GalleryMenu.DrawLeftFitted(b, source.AssetName, new(911, y + 34, 565, 26));
            Rule(b, new(911, y + 71, 565, 1));
        }
    }
    private static void DrawArrow(SpriteBatch b, Rectangle bounds, bool right) => b.Draw(Game1.mouseCursors, bounds, new Rectangle(right ? 365 : 352, 495, 12, 11), Color.White);
    private void DrawTooltip(SpriteBatch b)
    {
        Point mouse = Logical(Game1.getMouseX(true), Game1.getMouseY(true));
        string? text = null;
        if (sourceGroup is null)
        {
            for (int slot = 0; slot < PageSize && page * PageSize + slot < filtered.Count; slot++)
                if (Card(slot).Contains(mouse))
                {
                    GalleryEventGroup group = filtered[page * PageSize + slot];
                    IReadOnlyList<GalleryEvent> selected = scopedSources[group.Key];
                    text = "ID " + group.Representative.EventId + "\n" + string.Join(", ", group.NpcNames.Select(NpcName))
                        + "\n" + (selected.Count > 1 ? i18n.Get("directory.sources", new { count = selected.Count }) : Location(selected[0]));
                }
            if (character is null)
                for (int i = 0; i < 6 && placeOffset + i < places.Count; i++)
                    if (new Rectangle(188, 444 + i * 53, 570, 45).Contains(mouse)) text = places[placeOffset + i].Label + "\n" + places[placeOffset + i].Asset;
        }
        if (!string.IsNullOrWhiteSpace(text)) drawHoverText(b, Game1.parseText(text, Game1.smallFont, 480), Game1.smallFont);
    }
}
