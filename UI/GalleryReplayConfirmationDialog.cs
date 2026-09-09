using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace StardewGallery;

internal sealed class GalleryReplayConfirmationDialog : ConfirmationDialog
{
    private readonly string originalMessage;
    private Rectangle textBounds;
    private float textScale = 1f;
    private int viewportWidth, viewportHeight;
    private bool finished;

    internal GalleryReplayConfirmationDialog(string text, behavior confirm, behavior cancel)
        : base("", confirm, cancel)
    {
        originalMessage = text;
        onConfirm = who => { if (finished) return; finished = true; confirm(who); };
        onCancel = who => { if (finished) return; finished = true; cancel(who); };
        Reflow();
    }

    private void Reflow()
    {
        viewportWidth = Game1.uiViewport.Width;
        viewportHeight = Game1.uiViewport.Height;
        int margin = Math.Min(24, Math.Max(8, Math.Min(viewportWidth, viewportHeight) / 20));
        width = Math.Max(160, Math.Min(960, viewportWidth - 2 * margin));
        int padding = Math.Min(32, width / 12);
        int textWidth = width - padding * 2;
        int availableHeight = Math.Max(24, viewportHeight - margin * 2 - padding * 2 - 84);
        (string Text, Vector2 Size) Measure(float scale)
        {
            string wrapped = Game1.parseText(originalMessage, Game1.dialogueFont, (int)(textWidth / scale));
            return (wrapped, Game1.dialogueFont.MeasureString(wrapped) * scale);
        }
        bool Fits(Vector2 size) => size.X <= textWidth && size.Y <= availableHeight;
        textScale = 1f;
        var content = Measure(textScale);
        if (!Fits(content.Size))
        {
            float low = .05f, high = 1f;
            for (int i = 0; i < 16; i++)
            {
                float middle = (low + high) / 2;
                if (Fits(Measure(middle).Size)) low = middle; else high = middle;
            }
            textScale = low;
            content = Measure(textScale);
        }
        message = content.Text;
        int textHeight = (int)Math.Ceiling(content.Size.Y);
        height = padding * 2 + textHeight + 84;
        (xPositionOnScreen, yPositionOnScreen) = GalleryLayout.Center(viewportWidth, viewportHeight, width, height);
        textBounds = new(xPositionOnScreen + padding, yPositionOnScreen + padding, textWidth, textHeight);
        int buttonY = yPositionOnScreen + height - padding - 64;
        okButton.bounds = new(xPositionOnScreen + width - padding - 148, buttonY, 64, 64);
        cancelButton.bounds = new(xPositionOnScreen + width - padding - 64, buttonY, 64, 64);
        int focus = currentlySnappedComponent?.myID ?? region_cancelButton;
        allClickableComponents = [okButton, cancelButton];
        currentlySnappedComponent = focus == region_okButton ? okButton : cancelButton;
        if (Game1.options.SnappyMenus && Game1.options.gamepadControls) snapCursorToCurrentSnappedComponent();
    }

    private void EnsureLayout()
    {
        if (viewportWidth != Game1.uiViewport.Width || viewportHeight != Game1.uiViewport.Height) Reflow();
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds) => Reflow();

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (finished) return;
        EnsureLayout();
        base.receiveLeftClick(x, y, playSound);
    }

    public override void receiveKeyPress(Keys key)
    {
        if (finished) return;
        EnsureLayout();
        base.receiveKeyPress(key);
    }

    public override void receiveGamePadButton(Buttons button)
    {
        if (finished) return;
        EnsureLayout();
        if (button == Buttons.B) { cancel(); return; }
        if (button == Buttons.A)
        {
            Rectangle target = (currentlySnappedComponent ?? cancelButton).bounds;
            receiveLeftClick(target.Center.X, target.Center.Y);
            return;
        }
        base.receiveGamePadButton(button);
    }

    public override void draw(SpriteBatch b)
    {
        if (finished) return;
        EnsureLayout();
        b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * .5f);
        drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);
        b.DrawString(Game1.dialogueFont, message, new Vector2(textBounds.X, textBounds.Y), Game1.textColor,
            0, Vector2.Zero, textScale, SpriteEffects.None, 0);
        okButton.draw(b);
        cancelButton.draw(b);
        drawMouse(b);
    }
}
