using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace StardewGallery;

internal sealed class GalleryEventPickerMenu : IClickableMenu
{
    private const int LogicalWidth = 1200, LogicalHeight = 780, PageSize = 6;
    private readonly IReadOnlyList<GalleryEvent> entries;
    private readonly GalleryPhotos photos;
    private readonly Texture2D fallback;
    private readonly ITranslationHelper i18n;
    private readonly string title;
    private readonly string emptyMessage;
    private readonly string[] names, places;
    private readonly Action<GalleryEvent, Action> choose;
    private readonly Action back;
    private int selected;
    private float scale;
    private int offsetX, offsetY, oldWidth, oldHeight;
    private int Page => selected / PageSize;
    private static Rectangle Row(int slot) => new(40, 100 + slot * 90, 1120, 86);
    private static Rectangle BackBounds => new(40, 688, 280, 64);
    private static Rectangle Previous => new(470, 698, 48, 44);
    private static Rectangle Next => new(900, 698, 48, 44);

    internal GalleryEventPickerMenu(IReadOnlyList<GalleryEvent> entries, GalleryCatalog catalog, GalleryPhotos photos,
        Texture2D fallback, ITranslationHelper i18n, string title, Action<GalleryEvent, Action> choose, Action back, string? emptyMessage = null)
        : base(0, 0, LogicalWidth, LogicalHeight, true)
    {
        this.entries = entries; this.photos = photos; this.fallback = fallback; this.i18n = i18n;
        this.title = title; this.choose = choose; this.back = back;
        this.emptyMessage = emptyMessage ?? i18n.Get("nav.empty");
        names = entries.Select(entry => GalleryEventNavigation.Owner(catalog, entry)?.DisplayName ?? "").ToArray();
        var ambiguous = entries.GroupBy(entry => entry.EventId).Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet();
        places = entries.Select(entry => GalleryConditionPresentation.Location(entry, i18n)
            + (ambiguous.Contains(entry.EventId) ? " | " + entry.AssetName : "")).ToArray();
        Layout();
        snapToDefaultClickableComponent();
    }

    public override void draw(SpriteBatch batch)
    {
        if (oldWidth != Game1.uiViewport.Width || oldHeight != Game1.uiViewport.Height) Layout();
        batch.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * .65f);
        GalleryMenu.BeginScaled(batch, scale, offsetX, offsetY);
        IClickableMenu.drawTextureBox(batch, 0, 0, LogicalWidth, LogicalHeight, Color.White);
        GalleryMenu.DrawCentered(batch, title, new Rectangle(60, 24, 1080, 56));
        if (entries.Count == 0)
            GalleryMenu.DrawCentered(batch, emptyMessage, new Rectangle(60, 160, 1080, 460));
        for (int slot = 0; slot < PageSize && Page * PageSize + slot < entries.Count; slot++)
        {
            int index = Page * PageSize + slot;
            GalleryEvent entry = entries[index];
            Rectangle bounds = Row(slot);
            bool hovered = ToScreen(bounds).Contains(Game1.getMouseX(true), Game1.getMouseY(true));
            if (hovered || index == selected && Game1.options.gamepadControls)
                batch.Draw(Game1.staminaRect, bounds, new Color(255, 241, 190) * .8f);
            GalleryPhotos.DrawCover(batch, photos.Cover(entry.Resolved.Identity) ?? fallback, new(bounds.X + 8, bounds.Y + 7, 128, 72), Color.White);
            GalleryMenu.DrawLeftFitted(batch, $"{names[index]} | ID {entry.EventId}", new(bounds.X + 158, bounds.Y + 7, bounds.Width - 192, 34));
            GalleryMenu.DrawLeftFitted(batch, places[index], new(bounds.X + 158, bounds.Y + 44, bounds.Width - 192, 34));
            batch.Draw(Game1.staminaRect, new Rectangle(bounds.X, bounds.Bottom, bounds.Width, 1), new Color(128, 86, 31) * .35f);
        }
        GalleryMenu.DrawButton(batch, BackBounds, i18n.Get("nav.back"));
        batch.Draw(Game1.mouseCursors, Previous, new Rectangle(352, 495, 12, 11), Page > 0 ? Color.White : Color.Gray);
        batch.Draw(Game1.mouseCursors, Next, new Rectangle(365, 495, 12, 11), (Page + 1) * PageSize < entries.Count ? Color.White : Color.Gray);
        GalleryMenu.DrawCentered(batch, $"{(entries.Count == 0 ? 0 : Page + 1)} / {(entries.Count + PageSize - 1) / PageSize}", new Rectangle(550, 698, 320, 44));
        upperRightCloseButton?.draw(batch);
        GalleryMenu.EndScaled(batch);
        drawMouse(batch);
    }
    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        Point point = new((int)Math.Round((x - offsetX) / scale), (int)Math.Round((y - offsetY) / scale));
        if (BackBounds.Contains(point) || upperRightCloseButton?.bounds.Contains(point) == true) { Return(); return; }
        if (Previous.Contains(point)) { Select((Page - 1) * PageSize); return; }
        if (Next.Contains(point)) { Select((Page + 1) * PageSize); return; }
        for (int slot = 0; slot < PageSize && Page * PageSize + slot < entries.Count; slot++)
            if (Row(slot).Contains(point)) { Select(Page * PageSize + slot); Open(); return; }
    }
    public override void receiveScrollWheelAction(int direction) => Select(Math.Clamp(selected + (direction < 0 ? PageSize : -PageSize), 0, Math.Max(0, entries.Count - 1)));
    public override void receiveRightClick(int x, int y, bool playSound = true) => Return();
    public override void receiveKeyPress(Keys key)
    {
        if (key == Keys.Escape) Return();
        else if (key == Keys.Enter) { if (currentlySnappedComponent?.myID == 1000) Return(); else Open(); }
        else if (key is Keys.Up or Keys.Right or Keys.Down or Keys.Left)
            applyMovementKey(key switch { Keys.Up => 0, Keys.Right => 1, Keys.Down => 2, _ => 3 });
        else base.receiveKeyPress(key);
    }
    internal void HandleControllerBack() => Return();
    public override void receiveGamePadButton(Buttons button)
    {
        if (button == Buttons.B) Return();
        else if (button == Buttons.A) { if (currentlySnappedComponent?.myID == 1000) Return(); else Open(); }
        else base.receiveGamePadButton(button);
    }
    public override void applyMovementKey(int direction)
    {
        if (currentlySnappedComponent?.myID == 1000) { if (direction == 0) FocusSelected(); return; }
        if (direction == 2 && (selected % PageSize == PageSize - 1 || selected == entries.Count - 1))
        {
            currentlySnappedComponent = allClickableComponents.First(component => component.myID == 1000);
            Snap();
        }
        else Select(Math.Clamp(selected + (direction switch { 0 => -1, 2 => 1, 1 => PageSize, 3 => -PageSize, _ => 0 }), 0, Math.Max(0, entries.Count - 1)));
    }
    public override void snapToDefaultClickableComponent() => FocusSelected();
    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds) { base.gameWindowSizeChanged(oldBounds, newBounds); Layout(); }
    private void Open()
    {
        if (entries.Count == 0) return;
        choose(entries[selected], () => { Game1.activeClickableMenu = this; Layout(); FocusSelected(); });
    }
    private void Select(int index)
    {
        if (index < 0 || index >= entries.Count) return;
        selected = index; BuildComponents(); FocusSelected();
    }
    private void FocusSelected()
    {
        currentlySnappedComponent = allClickableComponents.FirstOrDefault(component => component.myID == selected % PageSize) ?? allClickableComponents[0];
        Snap();
    }
    private void Snap() { if (Game1.options.snappyMenus && Game1.options.gamepadControls) snapCursorToCurrentSnappedComponent(); }
    private void Return() { back(); Game1.playSound("bigDeSelect"); }
    private void Layout()
    {
        width = LogicalWidth; height = LogicalHeight; xPositionOnScreen = yPositionOnScreen = 0;
        oldWidth = Game1.uiViewport.Width; oldHeight = Game1.uiViewport.Height;
        scale = (float)GalleryLayout.ScaleToFit(oldWidth, oldHeight, width, height, 24);
        offsetX = (int)Math.Round((oldWidth - width * scale) / 2); offsetY = (int)Math.Round((oldHeight - height * scale) / 2);
        initializeUpperRightCloseButton(); BuildComponents();
    }
    private void BuildComponents()
    {
        int previous = currentlySnappedComponent?.myID ?? selected % PageSize;
        allClickableComponents = [new(ToScreen(BackBounds), "back") { myID = 1000 }];
        for (int slot = 0; slot < PageSize && Page * PageSize + slot < entries.Count; slot++)
            allClickableComponents.Add(new(ToScreen(Row(slot)), "event") { myID = slot });
        currentlySnappedComponent = allClickableComponents.FirstOrDefault(component => component.myID == previous) ?? allClickableComponents[0];
    }
    private Rectangle ToScreen(Rectangle bounds) => GalleryMenu.ScaleRectangle(bounds, scale, offsetX, offsetY);
}
