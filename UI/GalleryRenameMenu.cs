using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace StardewGallery;

/// <summary>
/// Compact <c>720x340</c> parchment nameplate rename modal centered over the darkened <c>1672x941</c> leather book:
/// displays the original default title &amp; Event ID reference, an unobstructed dual-state recessed input box, and
/// three tri-state wood buttons (Back / Restore Default Name / Save Name).
/// </summary>
internal sealed class GalleryRenameMenu : GalleryToolMenu
{
    private const int BookWidth = GalleryDrawing.MenuWidth, BookHeight = GalleryDrawing.MenuHeight;
    private const int ModalWidth = 720, ModalHeight = 340;
    private const int InputComponentId = RowId; // 100
    private const int BackButtonId = 0, SaveButtonId = 1, RestoreButtonId = 2;

    private static Rectangle ModalBounds => new((BookWidth - ModalWidth) / 2, (BookHeight - ModalHeight) / 2, ModalWidth, ModalHeight);
    private static Rectangle PlaqueBounds => new(ModalBounds.Center.X - 270, ModalBounds.Y - 18, 540, 42);
    private static Rectangle OriginalTitleBounds => new(ModalBounds.X + 40, ModalBounds.Y + 42, ModalWidth - 80, 32);
    private static Rectangle EventIdRefBounds => new(ModalBounds.X + 40, ModalBounds.Y + 76, ModalWidth - 80, 30);
    private static Rectangle InputSlotBounds => new(ModalBounds.X + 40, ModalBounds.Y + 128, ModalWidth - 80, 54);
    private static Rectangle ErrorBounds => new(ModalBounds.X + 40, ModalBounds.Y + 190, ModalWidth - 80, 30);
    private static Rectangle ModalBackBounds => new(ModalBounds.X + 36, ModalBounds.Bottom - 84, 192, 52);
    private static Rectangle ModalRestoreBounds => new(ModalBounds.X + 242, ModalBounds.Bottom - 84, 236, 52);
    private static Rectangle ModalSaveBounds => new(ModalBounds.X + 492, ModalBounds.Bottom - 84, 192, 52);

    private readonly TextBox input;
    private readonly EventIdentity identity;
    private string? errorMessage;
    private float modalScale = 1f;
    private int modalOffsetX, modalOffsetY, lastViewportW, lastViewportH;
    private bool modalKeyboardFocus;
    private Point modalLastMouse;

    protected override string Title => Context.I18n.Get("name.edit");
    protected override string? ApplyLabel => Context.I18n.Get("name.save");
    protected override string? ExtraLabel => Context.I18n.Get("name.restore");
    public override bool IsSearchSelected => input.Selected;

    internal GalleryRenameMenu(GalleryViewContext context, GalleryPageState state) : base(context, state)
    {
        identity = state.Event!.Value;
        input = new GalleryTextBox
        {
            DrawFrame = false,
            X = InputSlotBounds.X + 4,
            Y = InputSlotBounds.Y + 4,
            Width = InputSlotBounds.Width - 8,
            Height = InputSlotBounds.Height - 6,
            Text = state.RenameText ?? context.Names?.Get(identity) ?? ""
        };
        input.OnEnterPressed += _ => Apply();
        Rows.Add(new("ID " + identity.EventId, SelectInput));
        RefreshRows(state.Focus >= 0 ? state.Focus : InputComponentId);
        if (Game1.uiViewport.Width > 0 && Game1.uiViewport.Height > 0)
        {
            RebuildModalLayout();
            FocusModal(state.Focus >= 0 ? state.Focus : InputComponentId);
        }
    }

    private void SelectInput() => GalleryTextBox.SelectForInput(input);

    private bool CanRestoreDefault
        => !string.IsNullOrWhiteSpace(Context.Names?.Get(identity)) || !string.IsNullOrEmpty(input.Text);

    public override void update(GameTime time)
    {
        base.update(time);
        input.Text = GallerySearchInput.CleanText(input.Text);
        State.RenameText = input.Text;
    }

    public override void DeselectSearch()
    {
        State.RenameText = input.Text;
        input.Selected = false;
        if (Game1.keyboardDispatcher?.Subscriber == input)
            Game1.keyboardDispatcher.Subscriber = null;
    }

    public override void HandleControllerBack()
    {
        if (input.Selected)
        {
            DeselectSearch();
            Game1.playSound("bigDeSelect");
        }
        else
        {
            DeselectSearch();
            Game1.playSound("bigDeSelect");
            Context.Navigation.Back();
        }
    }

    protected override void Apply()
    {
        if (Context.Names?.Set(identity, input.Text) == true)
        {
            errorMessage = null;
            State.RenameText = null;
            DeselectSearch();
            Game1.playSound("coin");
            Context.Navigation.Back();
        }
        else
        {
            errorMessage = Context.I18n.Get("name.failed");
            FocusModal(SaveButtonId);
        }
    }

    protected override void Extra()
    {
        if (!CanRestoreDefault)
            return;
        input.Text = "";
        Apply();
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
    {
        base.gameWindowSizeChanged(oldBounds, newBounds);
        RebuildModalLayout();
    }

    public override void populateClickableComponentList() => BuildModalComponents();
    public override void snapToDefaultClickableComponent() => FocusModal(InputComponentId);

    public override void performHoverAction(int x, int y)
    {
        if (modalLastMouse != new Point(x, y))
        {
            if (modalLastMouse != default)
                modalKeyboardFocus = false;
            modalLastMouse = new Point(x, y);
        }
    }

    public override void receiveRightClick(int x, int y, bool playSound = true) => HandleControllerBack();
    public override void receiveScrollWheelAction(int direction) { }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        EnsureModalLayout();
        (int lx, int ly) = ToModalLogical(x, y);
        if (upperRightCloseButton?.bounds.Contains(lx, ly) == true)
        {
            DeselectSearch();
            Context.Navigation.Close();
            return;
        }
        if (InputSlotBounds.Contains(lx, ly))
        {
            FocusModal(InputComponentId);
            SelectInput();
            return;
        }
        DeselectSearch();
        if (ModalBackBounds.Contains(lx, ly))
        {
            FocusModal(BackButtonId);
            HandleControllerBack();
            return;
        }
        if (ModalRestoreBounds.Contains(lx, ly))
        {
            if (CanRestoreDefault)
            {
                FocusModal(RestoreButtonId);
                Extra();
            }
            return;
        }
        if (ModalSaveBounds.Contains(lx, ly))
        {
            FocusModal(SaveButtonId);
            Apply();
            return;
        }
        if (!ModalBounds.Contains(lx, ly))
        {
            HandleControllerBack();
        }
    }

    public override void receiveKeyPress(Keys key)
    {
        if (input.Selected)
        {
            if (key == Keys.Escape)
            {
                DeselectSearch();
                Game1.playSound("bigDeSelect");
            }
            return;
        }
        if (key is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Enter)
            modalKeyboardFocus = true;
        if (key == Keys.Escape || Game1.options.menuButton.Any(binding => binding.key == key))
        {
            HandleControllerBack();
            return;
        }
        if (key == Keys.Enter)
        {
            ActivateModal(currentlySnappedComponent?.myID ?? InputComponentId);
            return;
        }
        if (key is Keys.Up or Keys.Right or Keys.Down or Keys.Left)
        {
            applyMovementKey(key switch { Keys.Up => 0, Keys.Right => 1, Keys.Down => 2, _ => 3 });
        }
    }

    public override void receiveGamePadButton(Buttons button)
    {
        if (button == Buttons.B)
        {
            HandleControllerBack();
            return;
        }
        if (button == Buttons.A && Game1.options.snappyMenus)
        {
            ActivateModal(currentlySnappedComponent?.myID ?? InputComponentId);
            return;
        }
        base.receiveGamePadButton(button);
    }

    public override void applyMovementKey(int direction)
    {
        if (input.Selected || direction is < 0 or > 3)
            return;
        int current = currentlySnappedComponent?.myID ?? InputComponentId;
        if (current == InputComponentId)
        {
            if (direction == 2)
                FocusModal(SaveButtonId);
            return;
        }
        if (direction == 0)
        {
            FocusModal(InputComponentId);
            return;
        }
        if (direction is 1 or 3)
        {
            int[] row = CanRestoreDefault ? [BackButtonId, RestoreButtonId, SaveButtonId] : [BackButtonId, SaveButtonId];
            int idx = Array.IndexOf(row, current);
            if (idx < 0) idx = row.Length - 1;
            int nextIdx = Math.Clamp(idx + (direction == 1 ? 1 : -1), 0, row.Length - 1);
            FocusModal(row[nextIdx]);
        }
    }

    private void ActivateModal(int id)
    {
        if (id == InputComponentId) SelectInput();
        else if (id == BackButtonId) HandleControllerBack();
        else if (id == RestoreButtonId && CanRestoreDefault) Extra();
        else if (id == SaveButtonId) Apply();
    }

    public override void draw(SpriteBatch b)
    {
        EnsureModalLayout();
        b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * 0.55f);

        (int mx, int my) = ToModalLogical(Game1.getMouseX(true), Game1.getMouseY(true));
        bool showFocus = Game1.options.snappyMenus && Game1.options.gamepadControls || modalKeyboardFocus;
        int focusedId = currentlySnappedComponent?.myID ?? InputComponentId;

        GalleryDrawing.BeginScaled(b, modalScale, modalOffsetX, modalOffsetY);

        // Darkened leather book backdrop underneath the centered nameplate modal
        Texture2D? bookTex = Context.Textures?.Detail ?? Context.Textures?.Home;
        if (bookTex is not null)
        {
            b.Draw(bookTex, new Rectangle(0, 0, BookWidth, BookHeight), new Color(150, 136, 120));
            b.Draw(Game1.fadeToBlackRect, new Rectangle(20, 22, BookWidth - 40, BookHeight - 39), Color.Black * 0.34f);
        }

        // Centered 720x340 parchment nameplate modal with walnut border, brass trim, and corner rivets
        Rectangle modal = ModalBounds;
        b.Draw(Game1.staminaRect, new Rectangle(modal.X + 8, modal.Y + 10, modal.Width, modal.Height), Color.Black * 0.42f);
        b.Draw(Game1.staminaRect, modal, new Color(72, 38, 18));
        b.Draw(Game1.staminaRect, GalleryDrawing.Inset(modal, 5, 5), new Color(188, 142, 78));
        b.Draw(Game1.staminaRect, GalleryDrawing.Inset(modal, 9, 9), new Color(246, 233, 205));
        GalleryBookChrome.DrawBorder(b, GalleryDrawing.Inset(modal, 15, 15), new Color(204, 174, 128) * 0.75f, 1);
        GalleryUiSkin.DrawFocusCorners(b, GalleryDrawing.Inset(modal, 3, 3), 14);

        // Top riveted title plaque
        GalleryUiSkin.DrawTitlePlaque(b, PlaqueBounds, Title, false);

        // Original event title & Event ID reference
        string defaultTitle = ResolveOriginalDefaultTitle();
        float refScale = Math.Min(0.92f, 26f / Math.Max(1f, Game1.smallFont.LineSpacing));
        GalleryDrawing.DrawEllipsized(b, defaultTitle, OriginalTitleBounds, refScale);
        string idLine = Context.I18n.Get("event-detail.event-id", new { id = identity.EventId });
        GalleryDrawing.DrawEllipsized(b, idLine, EventIdRefBounds, Math.Min(0.86f, refScale));

        // Unobstructed dual-state recessed input box (zero overlapping magnifier or thick border)
        Rectangle slot = InputSlotBounds;
        bool inputFocused = showFocus && focusedId == InputComponentId;
        bool inputHot = input.Selected || inputFocused || slot.Contains(mx, my);
        b.Draw(Game1.staminaRect, slot, input.Selected ? new Color(214, 158, 56) : new Color(138, 102, 64));
        b.Draw(Game1.staminaRect, new Rectangle(slot.X + 2, slot.Y + 2, slot.Width - 4, slot.Height - 4),
            input.Selected ? new Color(164, 116, 42) : new Color(108, 76, 46));
        b.Draw(Game1.staminaRect, new Rectangle(slot.X + 4, slot.Y + 4, slot.Width - 8, slot.Height - 7),
            inputHot ? new Color(255, 249, 232) : new Color(235, 220, 190));
        if (inputFocused && !input.Selected)
            GalleryUiSkin.DrawFocusCorners(b, GalleryDrawing.Inset(slot, -4, -4), 10);

        input.Draw(b);
        if (input.Text.Length == 0 && !input.Selected)
            GalleryDrawing.DrawLeftFitted(b, defaultTitle, GalleryDrawing.Inset(slot, 16, 10), GallerySpreadDrawing.LightInk);

        if (!string.IsNullOrWhiteSpace(errorMessage))
            GalleryDrawing.DrawCentered(b, errorMessage, ErrorBounds, new Color(164, 42, 36));

        // Three tri-state wood buttons: Back / Restore Default Name / Save Name
        DrawModalButton(b, BackButtonId, ModalBackBounds, Context.I18n.Get("nav.back"), true, mx, my, showFocus, focusedId);
        DrawModalButton(b, RestoreButtonId, ModalRestoreBounds, Context.I18n.Get("name.restore"), CanRestoreDefault, mx, my, showFocus, focusedId);
        DrawModalButton(b, SaveButtonId, ModalSaveBounds, Context.I18n.Get("name.save"), true, mx, my, showFocus, focusedId);

        upperRightCloseButton?.draw(b);
        GalleryDrawing.EndScaled(b);
        drawMouse(b);
    }

    private static void DrawModalButton(SpriteBatch b, int id, Rectangle bounds, string text, bool enabled, int mx, int my, bool showFocus, int focusedId)
    {
        bool focused = showFocus && focusedId == id;
        bool hot = enabled && (focused || bounds.Contains(mx, my));
        GalleryDrawing.DrawButton(b, bounds, text, hot, enabled);
        if (focused)
            GalleryUiSkin.DrawFocusCorners(b, GalleryDrawing.Inset(bounds, -4, -4), 10);
    }

    private string ResolveOriginalDefaultTitle()
    {
        if (Context.Catalog?.Find(identity) is { } entry)
        {
            string? owner = entry.Ownership.Owners.Count == 1 ? entry.Ownership.Owners[0].Name : null;
            return GalleryCardTitleFormatter.FormatStoryCardTitle(
                entry,
                null,
                Context.Locations.Get(entry.LocationName),
                GalleryDrawing.TextSeparator,
                Context.IsParallelBranch(entry),
                owner,
                h => Context.I18n.Get("event.hearts", new { hearts = h }),
                k => Context.I18n.Get(k));
        }
        if (Context.Catalog?.FindPrerequisite(identity.EventId) is { } prereq && prereq.Identity == identity)
        {
            var translated = Context.I18n.Get("prerequisite.name." + prereq.EventId);
            return translated.HasValue() ? translated.ToString() : "ID " + prereq.EventId;
        }
        return "ID " + identity.EventId;
    }

    private void EnsureModalLayout()
    {
        if (lastViewportW != Game1.uiViewport.Width || lastViewportH != Game1.uiViewport.Height)
            RebuildModalLayout();
    }

    private void RebuildModalLayout()
    {
        width = BookWidth;
        height = BookHeight;
        xPositionOnScreen = yPositionOnScreen = 0;
        lastViewportW = Game1.uiViewport.Width;
        lastViewportH = Game1.uiViewport.Height;
        modalScale = (float)GalleryLayout.ScaleToFit(lastViewportW, lastViewportH, width, height, 24);
        modalOffsetX = (int)Math.Round((lastViewportW - width * modalScale) / 2f);
        modalOffsetY = (int)Math.Round((lastViewportH - height * modalScale) / 2f);
        Scale = modalScale;
        OffsetX = modalOffsetX;
        OffsetY = modalOffsetY;

        input.X = InputSlotBounds.X + 4;
        input.Y = InputSlotBounds.Y + 4;
        input.Width = InputSlotBounds.Width - 8;
        input.Height = InputSlotBounds.Height - 6;

        initializeUpperRightCloseButton();
        GalleryBookChrome.PositionCloseButton(upperRightCloseButton, modalScale, modalOffsetX, modalOffsetY, lastViewportW, lastViewportH);
        BuildModalComponents();
    }

    private void BuildModalComponents()
    {
        int previous = currentlySnappedComponent?.myID ?? InputComponentId;
        allClickableComponents =
        [
            new ClickableComponent(ToModalScreen(InputSlotBounds), "rename-input") { myID = InputComponentId },
            new ClickableComponent(ToModalScreen(ModalBackBounds), "back") { myID = BackButtonId },
            new ClickableComponent(ToModalScreen(ModalSaveBounds), "save") { myID = SaveButtonId }
        ];
        if (CanRestoreDefault)
            allClickableComponents.Add(new ClickableComponent(ToModalScreen(ModalRestoreBounds), "restore") { myID = RestoreButtonId });
        currentlySnappedComponent = allClickableComponents.FirstOrDefault(c => c.myID == previous)
            ?? allClickableComponents[0];
        State.Focus = currentlySnappedComponent.myID;
    }

    private void FocusModal(int id)
    {
        BuildModalComponents();
        currentlySnappedComponent = allClickableComponents.FirstOrDefault(c => c.myID == id) ?? allClickableComponents[0];
        State.Focus = currentlySnappedComponent.myID;
        if (Game1.options.snappyMenus && Game1.options.gamepadControls)
            snapCursorToCurrentSnappedComponent();
    }

    private Rectangle ToModalScreen(Rectangle bounds) => GalleryDrawing.ScaleRectangle(bounds, modalScale, modalOffsetX, modalOffsetY);
    private (int X, int Y) ToModalLogical(int x, int y) => ((int)Math.Round((x - modalOffsetX) / modalScale), (int)Math.Round((y - modalOffsetY) / modalScale));
}
