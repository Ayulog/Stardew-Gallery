using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace StardewGallery;

internal sealed record GalleryToolRow(string Text, Action? Activate = null, ConditionStatusIcon? Status = null);

internal abstract class GalleryToolMenu : IClickableMenu, IGallerySearchMenu
{
    protected const int RowId = 100;
    protected readonly GalleryViewContext Context;
    protected readonly GalleryPageState State;
    protected readonly List<GalleryToolRow> Rows = [];
    protected const int ToolWidth = 1180, ToolHeight = 850;
    private readonly List<Rectangle> rowBounds = [];
    private static readonly RasterizerState Clip = new() { ScissorTestEnable = true };
    protected float Scale;
    protected int OffsetX, OffsetY;
    private int viewportWidth, viewportHeight, scroll, contentHeight;
    private bool dragging;
    private int dragOffset;
    private Rectangle thumb;
    protected virtual Rectangle ContentBounds => new(52, 148, 1050, 588);
    private Rectangle Track => new(1124, ContentBounds.Y, 20, ContentBounds.Height);
    protected Rectangle BackBounds => new(52, 768, 280, 52);
    protected Rectangle ExtraBounds => new(368, 768, 360, 52);
    protected Rectangle ApplyBounds => new(774, 768, 354, 52);
    protected abstract string Title { get; }
    protected virtual string? ApplyLabel => null;
    protected virtual string? ExtraLabel => null;
    public virtual bool IsSearchSelected => false;
    private int MaxScroll => Math.Max(0, contentHeight - ContentBounds.Height);
    protected int FocusId => currentlySnappedComponent?.myID ?? 0;

    protected GalleryToolMenu(GalleryViewContext context, GalleryPageState state) : base(0, 0, ToolWidth, ToolHeight, true)
    { Context = context; State = state; scroll = state.Scroll; }
    protected void RefreshRows(int focus = RowId)
    {
        rowBounds.Clear();
        int y = ContentBounds.Y;
        foreach (var row in Rows)
        {
            string wrapped = Game1.parseText(row.Text, Game1.smallFont, ContentBounds.Width - 94);
            int size = Math.Max(58, (int)Math.Ceiling(Game1.smallFont.MeasureString(wrapped).Y) + 24);
            rowBounds.Add(new(ContentBounds.X, y, ContentBounds.Width, size)); y += size;
        }
        contentHeight = y - ContentBounds.Y;
        scroll = Math.Clamp(scroll, 0, MaxScroll);
        Layout(); Focus(focus);
    }
    private void Layout()
    {
        width = ToolWidth; height = ToolHeight; xPositionOnScreen = yPositionOnScreen = 0;
        viewportWidth = Game1.uiViewport.Width; viewportHeight = Game1.uiViewport.Height;
        Scale = (float)GalleryLayout.ScaleToFit(viewportWidth, viewportHeight, width, height, 24);
        OffsetX = (int)Math.Round((viewportWidth - width * Scale) / 2); OffsetY = (int)Math.Round((viewportHeight - height * Scale) / 2);
        initializeUpperRightCloseButton(); BuildComponents();
    }
    protected Rectangle Screen(Rectangle bounds) => GalleryDrawing.ScaleRectangle(bounds, Scale, OffsetX, OffsetY);
    protected (int X, int Y) Logical(int x, int y) => ((int)Math.Round((x - OffsetX) / Scale), (int)Math.Round((y - OffsetY) / Scale));
    private Rectangle RowBounds(int index) { var bounds = rowBounds[index]; bounds.Y -= scroll; return bounds; }
    private void BuildComponents()
    {
        int old = FocusId;
        allClickableComponents = [new(Screen(BackBounds), "back") { myID = 0 }];
        if (ApplyLabel is not null) allClickableComponents.Add(new(Screen(ApplyBounds), "apply") { myID = 1 });
        if (ExtraLabel is not null) allClickableComponents.Add(new(Screen(ExtraBounds), "extra") { myID = 2 });
        for (int i = 0; i < Rows.Count; i++)
        {
            Rectangle visible = Rectangle.Intersect(RowBounds(i), ContentBounds);
            if (visible.Height > 0) allClickableComponents.Add(new(Screen(visible), "row") { myID = RowId + i });
        }
        currentlySnappedComponent = allClickableComponents.FirstOrDefault(c => c.myID == old) ?? allClickableComponents[0];
        int h = Math.Max(42, ContentBounds.Height * ContentBounds.Height / Math.Max(ContentBounds.Height, contentHeight));
        thumb = new(Track.X, Track.Y + (MaxScroll == 0 ? 0 : (Track.Height - h) * scroll / MaxScroll), Track.Width, h);
    }
    protected void Focus(int id)
    {
        if (id >= RowId && id - RowId < Rows.Count)
        {
            Rectangle r = RowBounds(id - RowId);
            scroll = Math.Clamp(scroll + (r.Top < ContentBounds.Top ? r.Top - ContentBounds.Top
                : r.Bottom > ContentBounds.Bottom ? Math.Min(r.Top - ContentBounds.Top, r.Bottom - ContentBounds.Bottom) : 0), 0, MaxScroll);
        }
        BuildComponents();
        currentlySnappedComponent = allClickableComponents.FirstOrDefault(c => c.myID == id) ?? allClickableComponents[0];
        State.Focus = FocusId; State.Scroll = scroll;
        if (Game1.options.snappyMenus && Game1.options.gamepadControls) snapCursorToCurrentSnappedComponent();
    }
    private void Scroll(int amount) { scroll = Math.Clamp(scroll + amount, 0, MaxScroll); BuildComponents(); State.Scroll = scroll; }
    protected virtual void Apply() { }
    protected virtual void Extra() { }
    protected virtual void Adjust(int direction) { }
    protected virtual void Activate(int id)
    {
        if (id == 0) HandleControllerBack(); else if (id == 1) Apply(); else if (id == 2) Extra();
        else if (id >= RowId && id - RowId < Rows.Count) Rows[id - RowId].Activate?.Invoke();
    }
    public virtual void HandleControllerBack() { DeselectSearch(); Context.Navigation.Back(); }
    public virtual void DeselectSearch() { }
    public virtual void OpenFirstMatch() => Apply();
    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        (x, y) = Logical(x, y);
        if (upperRightCloseButton.bounds.Contains(x, y)) { HandleControllerBack(); return; }
        if (MaxScroll > 0 && thumb.Contains(x, y)) { dragging = true; dragOffset = y - thumb.Y; return; }
        if (MaxScroll > 0 && Track.Contains(x, y)) { Scroll(y < thumb.Y ? -ContentBounds.Height : ContentBounds.Height); return; }
        int sx = Screen(new(x, y, 1, 1)).X, sy = Screen(new(x, y, 1, 1)).Y;
        var component = allClickableComponents.FirstOrDefault(c => c.containsPoint(sx, sy));
        if (component is not null) { Focus(component.myID); Activate(component.myID); }
    }
    public override void receiveRightClick(int x, int y, bool playSound = true) => HandleControllerBack();
    public override void receiveScrollWheelAction(int direction) => Scroll(direction < 0 ? 96 : -96);
    public override void leftClickHeld(int x, int y)
    {
        if (!dragging || MaxScroll == 0) return;
        (_, y) = Logical(x, y);
        int value = (int)Math.Round((double)Math.Clamp(y - dragOffset - Track.Y, 0, Track.Height - thumb.Height) / (Track.Height - thumb.Height) * MaxScroll);
        Scroll(value - scroll);
    }
    public override void releaseLeftClick(int x, int y) => dragging = false;
    public override void receiveGamePadButton(Buttons button)
    {
        if (button == Buttons.A) Activate(FocusId);
        else if (button == Buttons.B) HandleControllerBack();
        else if (button is Buttons.LeftShoulder or Buttons.RightShoulder)
        {
            int delta = button == Buttons.LeftShoulder ? -8 : 8;
            Focus(RowId + Math.Clamp(Math.Max(0, FocusId - RowId) + delta, 0, Math.Max(0, Rows.Count - 1)));
        }
        else base.receiveGamePadButton(button);
    }
    public override void receiveKeyPress(Keys key)
    {
        if (IsSearchSelected) { if (key == Keys.Escape) DeselectSearch(); return; }
        if (key == Keys.Escape) HandleControllerBack();
        else if (key == Keys.Enter) Activate(FocusId);
        else if (key is Keys.Up or Keys.Down or Keys.Left or Keys.Right)
            applyMovementKey(key switch { Keys.Up => 0, Keys.Right => 1, Keys.Down => 2, _ => 3 });
        else if (key is Keys.PageUp or Keys.PageDown) receiveGamePadButton(key == Keys.PageUp ? Buttons.LeftShoulder : Buttons.RightShoulder);
        else base.receiveKeyPress(key);
    }
    public override void applyMovementKey(int direction)
    {
        if (IsSearchSelected) return;
        if (FocusId >= RowId)
        {
            if (direction is 1 or 3) { Adjust(direction == 1 ? 1 : -1); return; }
            int next = FocusId - RowId + (direction == 0 ? -1 : 1);
            Focus(next < 0 || next >= Rows.Count ? 0 : RowId + next);
        }
        else if (direction is 0 or 2) Focus(Rows.Count == 0 ? 0 : direction == 0 ? RowId + Rows.Count - 1 : RowId);
        else
        {
            int[] ids = new[] { 0, 2, 1 }.Where(id => allClickableComponents.Any(c => c.myID == id)).ToArray();
            int index = Array.IndexOf(ids, FocusId);
            Focus(ids[Math.Clamp(index + (direction == 1 ? 1 : -1), 0, ids.Length - 1)]);
        }
    }
    public override void snapToDefaultClickableComponent() => Focus(Rows.Count > 0 ? RowId : 0);
    protected virtual void DrawTop(SpriteBatch b) { }
    public override void draw(SpriteBatch b)
    {
        if (viewportWidth != Game1.uiViewport.Width || viewportHeight != Game1.uiViewport.Height) Layout();
        b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * .55f);
        GalleryDrawing.BeginScaled(b, Scale, OffsetX, OffsetY);
        drawTextureBox(b, 0, 0, width, height, Color.White);
        GalleryDrawing.DrawCentered(b, Title, new(64, 36, width - 128, 54)); DrawTop(b);
        Rectangle oldClip = b.GraphicsDevice.ScissorRectangle;
        b.End(); b.GraphicsDevice.ScissorRectangle = Rectangle.Intersect(Screen(ContentBounds), b.GraphicsDevice.Viewport.Bounds);
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, Clip, null,
            Matrix.CreateScale(Scale) * Matrix.CreateTranslation(OffsetX, OffsetY, 0));
        var (mx, my) = Logical(Game1.getMouseX(true), Game1.getMouseY(true));
        for (int i = 0; i < Rows.Count; i++)
        {
            Rectangle r = RowBounds(i);
            if (!r.Intersects(ContentBounds)) continue;
            if (FocusId == RowId + i || r.Contains(mx, my)) b.Draw(Game1.staminaRect, r, new Color(204, 221, 187) * .6f);
            b.DrawString(Game1.smallFont, Game1.parseText(Rows[i].Text, Game1.smallFont, r.Width - 94), new(r.X + 14, r.Y + 12), Game1.textColor);
            if (Rows[i].Status is ConditionStatusIcon icon)
            {
                var bounds = icon switch { ConditionStatusIcon.Check => GallerySpreadLayout.ConditionCheckSource,
                    ConditionStatusIcon.Cross => GallerySpreadLayout.ConditionCrossSource, _ => GallerySpreadLayout.ConditionQuestionSource };
                b.Draw(Context.Textures.ConditionIcons, new Rectangle(r.Right - 54, r.Y + 12, 36, 36), new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height), Color.White);
            }
            else if (Rows[i].Activate is not null) GalleryDrawing.DrawCentered(b, ">", new(r.Right - 48, r.Y + 12, 32, 34));
            b.Draw(Game1.staminaRect, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), Game1.textColor * .16f);
        }
        b.End(); b.GraphicsDevice.ScissorRectangle = oldClip;
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null,
            Matrix.CreateScale(Scale) * Matrix.CreateTranslation(OffsetX, OffsetY, 0));
        if (MaxScroll > 0) { GalleryDrawing.DrawScrollbarTrack(b, Context.Textures.Scrollbar, Track); GalleryDrawing.DrawScrollbar(b, thumb); }
        GalleryDrawing.DrawButton(b, BackBounds, Context.I18n.Get("nav.back"));
        if (ApplyLabel is not null) GalleryDrawing.DrawButton(b, ApplyBounds, ApplyLabel);
        if (ExtraLabel is not null) GalleryDrawing.DrawButton(b, ExtraBounds, ExtraLabel);
        foreach (int id in new[] { 0, 1, 2 })
            if (id == FocusId) { var r = id == 0 ? BackBounds : id == 1 ? ApplyBounds : ExtraBounds; b.Draw(Game1.staminaRect, new Rectangle(r.X + 16, r.Bottom - 8, r.Width - 32, 3), new Color(66, 112, 76)); }
        upperRightCloseButton.draw(b); GalleryDrawing.EndScaled(b); drawMouse(b);
    }
    protected override void cleanupBeforeExit() { DeselectSearch(); base.cleanupBeforeExit(); }
}
