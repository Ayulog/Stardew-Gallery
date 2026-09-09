using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace StardewGallery;

internal sealed class GalleryRenameMenu : GalleryToolMenu
{
    private readonly TextBox input;
    private readonly EventIdentity identity;
    private static Rectangle InputBounds => new(66, 124, 1034, 72);
    protected override Rectangle ContentBounds => new(52, 224, 1050, 512);
    protected override string Title => Context.I18n.Get("name.edit");
    protected override string? ApplyLabel => Context.I18n.Get("name.save");
    protected override string? ExtraLabel => Context.I18n.Get("name.restore");
    public override bool IsSearchSelected => input.Selected;
    internal GalleryRenameMenu(GalleryViewContext context, GalleryPageState state) : base(context, state)
    {
        identity = state.Event!.Value;
        input = new GalleryTextBox
            { X = InputBounds.X, Y = InputBounds.Y, Width = InputBounds.Width, Height = InputBounds.Height, Text = context.Names?.Get(identity) ?? "" };
        input.OnEnterPressed += _ => Apply();
        Rows.Add(new("ID " + identity.EventId, SelectInput));
        RefreshRows();
    }
    private void SelectInput() { if (GallerySearchInputGuard.Ready) input.SelectMe(); }
    public override void update(GameTime time) { base.update(time); input.Text = GallerySearchInput.CleanText(input.Text); }
    protected override void DrawTop(SpriteBatch b) => input.Draw(b);
    public override void DeselectSearch()
    {
        input.Selected = false;
        if (Game1.keyboardDispatcher.Subscriber == input) Game1.keyboardDispatcher.Subscriber = null;
    }
    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        var (lx, ly) = Logical(x, y);
        if (InputBounds.Contains(lx, ly)) { SelectInput(); return; }
        DeselectSearch(); base.receiveLeftClick(x, y, playSound);
    }
    public override void HandleControllerBack() { if (input.Selected) DeselectSearch(); else base.HandleControllerBack(); }
    protected override void Apply()
    {
        if (Context.Names?.Set(identity, input.Text) == true) { DeselectSearch(); Context.Navigation.Back(); }
        else { Rows.Add(new(Context.I18n.Get("name.failed"))); RefreshRows(1); }
    }
    protected override void Extra() { input.Text = ""; Apply(); }
}
