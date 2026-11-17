namespace StardewGallery;

/// <summary>
/// Tracks idle ticks and smooth opacity fade for the top-right replay theater HUD.
/// After ~3 seconds (180 ticks at 60 fps) with no mouse movement and no theater key input,
/// the HUD smoothly fades to 0 and immediately restores to full opacity on input.
/// </summary>
internal sealed class TheaterHudState
{
    internal const int IdleDelayTicks = 180;
    internal const int FadeDurationTicks = 30;

    private int idleTicks;
    private int lastMouseX;
    private int lastMouseY;
    private bool mouseInitialized;

    internal int IdleTicks => idleTicks;

    internal float Opacity
    {
        get
        {
            if (idleTicks <= IdleDelayTicks)
                return 1f;
            if (idleTicks >= IdleDelayTicks + FadeDurationTicks)
                return 0f;
            return 1f - (idleTicks - IdleDelayTicks) / (float)FadeDurationTicks;
        }
    }

    internal bool IsVisible => Opacity > 0f;

    internal void Reset()
    {
        idleTicks = 0;
        mouseInitialized = false;
    }

    internal void NotifyActivity()
    {
        idleTicks = 0;
    }

    internal void Tick(int mouseX, int mouseY)
    {
        if (!mouseInitialized)
        {
            mouseInitialized = true;
            lastMouseX = mouseX;
            lastMouseY = mouseY;
            return;
        }

        if (mouseX != lastMouseX || mouseY != lastMouseY)
        {
            lastMouseX = mouseX;
            lastMouseY = mouseY;
            idleTicks = 0;
            return;
        }

        if (idleTicks < IdleDelayTicks + FadeDurationTicks)
            idleTicks++;
    }
}
