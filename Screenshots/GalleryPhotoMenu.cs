using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace StardewGallery;

internal sealed class GalleryPhotoMenu : IClickableMenu
{
    private const int LogicalWidth = 1200, LogicalHeight = 780;
    private readonly GalleryPhotos photos;
    private readonly EventIdentity identity;
    private readonly ITranslationHelper i18n;
    private readonly GalleryViewContext context;
    private readonly GalleryPageState state;
    private EventPhotoSet set;
    private int selected;
    private float scale;
    private int offsetX, offsetY, oldWidth, oldHeight;
    private static Rectangle Preview => new(40, 112, 840, 473);
    private static Rectangle Previous => new(40, 607, 48, 44);
    private static Rectangle Next => new(832, 607, 48, 44);
    private static Rectangle Footer(int index) => new(40 + index * 280, 688, 260, 64);
    private static Rectangle Slot(int index) => new(920, 112 + index * 162, 240, 135);
    private EventPhoto? Selected => selected >= 0 && selected < set.Photos.Count ? set.Photos[selected] : null;
    private bool CanSelectCover => !set.ReadOnly && Selected is { } photo && photos.Image(identity, photo.Id, preview: true) is not null;

    internal GalleryPhotoMenu(GalleryViewContext context, GalleryPageState state)
        : base(0, 0, LogicalWidth, LogicalHeight, true)
    {
        this.context = context; this.state = state;
        photos = context.Photos; i18n = context.I18n;
        identity = state.Event ?? throw new ArgumentException("An event identity is required.", nameof(state));
        set = photos.Read(identity);
        selected = state.Focus < 0 ? Math.Max(0, set.Photos.Count - 1) : Math.Clamp(state.Scroll, 0, Math.Max(0, set.Photos.Count - 1));
        int initialFocus = state.Focus;
        Layout();
        Focus(initialFocus >= 0 ? initialFocus : Selected is null ? 100 : 300 + selected % 3);
        if (Game1.options.snappyMenus && Game1.options.gamepadControls)
            snapCursorToCurrentSnappedComponent();
    }

    public override void draw(SpriteBatch batch)
    {
        if (oldWidth != Game1.uiViewport.Width || oldHeight != Game1.uiViewport.Height)
            Layout();
        batch.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * .65f);
        GalleryDrawing.BeginScaled(batch, scale, offsetX, offsetY);
        IClickableMenu.drawTextureBox(batch, 0, 0, LogicalWidth, LogicalHeight, Color.White);
        GalleryDrawing.DrawCentered(batch, i18n.Get("photo.title"), new Rectangle(80, 22, 1040, 52));
        GalleryDrawing.DrawLeftFitted(batch, i18n.Get("event-detail.event-id", new { id = identity.EventId }), new Rectangle(40, 78, 840, 28));
        batch.Draw(Game1.staminaRect, Preview, new Color(31, 35, 31));
        Texture2D? image = Selected is { } photo ? photos.Image(identity, photo.Id, preview: true) : null;
        if (image is not null)
            GalleryPhotos.DrawContained(batch, image, Preview);
        else
        {
            batch.Draw(Game1.staminaRect, Preview, new Color(239, 219, 174));
            GalleryDrawing.DrawCentered(batch, i18n.Get(Selected is null ? "photo.empty" : "photo.image-unavailable"), Preview);
        }
        for (int slot = 0; slot < 3; slot++)
        {
            int index = selected / 3 * 3 + slot;
            if (index >= set.Photos.Count)
                break;
            EventPhoto item = set.Photos[index];
            Rectangle bounds = Slot(slot);
            Texture2D? thumb = photos.Image(identity, item.Id, preview: false);
            batch.Draw(Game1.staminaRect, bounds, new Color(31, 35, 31));
            if (thumb is not null)
                GalleryPhotos.DrawCover(batch, thumb, bounds, Color.White);
            if (index == selected)
                Border(batch, bounds, new Color(225, 172, 52));
            if (set.CoverId == item.Id)
            {
                Rectangle badge = new(bounds.X, bounds.Bottom - 32, bounds.Width, 32);
                batch.Draw(Game1.staminaRect, badge, new Color(239, 227, 184));
                GalleryDrawing.DrawCentered(batch, i18n.Get("photo.current-cover"), badge);
            }
        }
        batch.Draw(Game1.mouseCursors, Previous, new Rectangle(352, 495, 12, 11), selected > 0 ? Color.White : Color.Gray);
        batch.Draw(Game1.mouseCursors, Next, new Rectangle(365, 495, 12, 11), selected + 1 < set.Photos.Count ? Color.White : Color.Gray);
        GalleryDrawing.DrawCentered(batch, $"{(set.Photos.Count == 0 ? 0 : selected + 1)} / {set.Photos.Count}", new Rectangle(285, 607, 350, 44));
        for (int index = 0; index < 4; index++)
        {
            Rectangle bounds = Footer(index);
            GalleryDrawing.DrawButton(batch, bounds, i18n.Get(new[] { "photo.back", "photo.set-cover", "photo.default", "photo.remove" }[index]));
            if (!Enabled(index))
                batch.Draw(Game1.staminaRect, bounds, Color.Gray * .4f);
            if (currentlySnappedComponent?.myID == 100 + index && Game1.options.snappyMenus && Game1.options.gamepadControls)
                Border(batch, bounds, new Color(225, 172, 52));
        }
        upperRightCloseButton?.draw(batch);
        GalleryDrawing.EndScaled(batch);
        drawMouse(batch);
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        Point cursor = new((int)Math.Round((x - offsetX) / scale), (int)Math.Round((y - offsetY) / scale));
        if (upperRightCloseButton?.bounds.Contains(cursor) == true)
        {
            SaveState(); photos.ReleasePreviews(); context.Navigation.Close(); return;
        }
        for (int index = 0; index < 4; index++)
            if (Footer(index).Contains(cursor)) { Activate(100 + index); return; }
        if (Previous.Contains(cursor)) { Select(selected - 1); return; }
        if (Next.Contains(cursor)) { Select(selected + 1); return; }
        for (int slot = 0; slot < 3; slot++)
            if (Slot(slot).Contains(cursor)) { Select(selected / 3 * 3 + slot); return; }
    }

    public override void receiveScrollWheelAction(int direction) => Select(selected + (direction < 0 ? 1 : -1));
    public override void receiveRightClick(int x, int y, bool playSound = true) => Return();
    public override void receiveKeyPress(Keys key)
    {
        if (key == Keys.Escape || Game1.options.menuButton.Any(binding => binding.key == key)) Return();
        else base.receiveKeyPress(key);
    }
    public override void update(GameTime time) { base.update(time); SaveState(); }
    public override void receiveGamePadButton(Buttons button)
    {
        if (button == Buttons.B) Return();
        else if (button == Buttons.A) Activate(currentlySnappedComponent?.myID ?? 100);
        else base.receiveGamePadButton(button);
    }
    public override void applyMovementKey(int direction)
    {
        int id = currentlySnappedComponent?.myID ?? 100;
        if (id is >= 100 and <= 103)
        {
            if (direction == 0 && Selected is not null) Focus(300 + selected % 3);
            else if (direction is 1 or 3)
            {
                int step = direction == 1 ? 1 : -1;
                int next = id - 100 + step;
                while (next is >= 0 and < 4 && !Enabled(next)) next += step;
                if (next is >= 0 and < 4) Focus(100 + next);
            }
        }
        else if (direction == 2 && (selected % 3 == 2 || selected + 1 == set.Photos.Count)) Focus(Enabled(1) ? 101 : 100);
        else if (direction is 0 or 2) Select(selected + (direction == 0 ? -1 : 1));
        else if (direction == 3) Select(selected - 1);
        else if (direction == 1) Select(selected + 1);
    }
    public override void snapToDefaultClickableComponent() => Focus(Selected is null ? 100 : 300 + selected % 3);
    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds) { base.gameWindowSizeChanged(oldBounds, newBounds); Layout(); }

    private void Select(int index)
    {
        if (index < 0 || index >= set.Photos.Count || index == selected) return;
        selected = index;
        BuildComponents();
        Focus(300 + selected % 3);
        Game1.playSound("shwip");
    }
    private bool Enabled(int index) => index switch
    {
        0 => true,
        1 => CanSelectCover && set.CoverId != Selected?.Id,
        2 => !set.ReadOnly && set.CoverId is not null,
        3 => !set.ReadOnly && Selected is not null,
        _ => false
    };
    private void Activate(int id)
    {
        if (id >= 300) { Select(selected / 3 * 3 + id - 300); return; }
        int action = id - 100;
        if (!Enabled(action)) return;
        if (action == 0) { Return(); return; }
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
    private void Layout()
    {
        width = LogicalWidth; height = LogicalHeight;
        xPositionOnScreen = yPositionOnScreen = 0;
        oldWidth = Game1.uiViewport.Width; oldHeight = Game1.uiViewport.Height;
        scale = (float)GalleryLayout.ScaleToFit(oldWidth, oldHeight, width, height, 24);
        offsetX = (int)Math.Round((oldWidth - width * scale) / 2); offsetY = (int)Math.Round((oldHeight - height * scale) / 2);
        initializeUpperRightCloseButton();
        BuildComponents();
    }
    private void BuildComponents()
    {
        int oldId = currentlySnappedComponent?.myID ?? 100;
        allClickableComponents = [];
        for (int index = 0; index < 4; index++)
            if (Enabled(index)) allClickableComponents.Add(new(ToScreen(Footer(index)), "photo-action") { myID = 100 + index });
        for (int slot = 0; slot < 3 && selected / 3 * 3 + slot < set.Photos.Count; slot++)
            allClickableComponents.Add(new(ToScreen(Slot(slot)), "photo") { myID = 300 + slot });
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
    private static void Border(SpriteBatch batch, Rectangle bounds, Color color)
    {
        batch.Draw(Game1.staminaRect, new Rectangle(bounds.X, bounds.Y, bounds.Width, 3), color);
        batch.Draw(Game1.staminaRect, new Rectangle(bounds.X, bounds.Bottom - 3, bounds.Width, 3), color);
        batch.Draw(Game1.staminaRect, new Rectangle(bounds.X, bounds.Y, 3, bounds.Height), color);
        batch.Draw(Game1.staminaRect, new Rectangle(bounds.Right - 3, bounds.Y, 3, bounds.Height), color);
    }
}
