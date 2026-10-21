using System.Runtime.CompilerServices;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using Steamworks;

namespace StardewGallery;

/// <summary>
/// Steam's on-screen keyboard, which Steam shows on Steam Deck and in Big Picture mode. Unlike the game's own
/// on-screen keyboard it types Chinese. Where Steam can't show it, callers use the game's keyboard instead.
/// </summary>
internal static class SteamTextInput
{
    private static readonly SButton[] KeyboardButtons =
        [SButton.ControllerA, SButton.ControllerB, SButton.ControllerX, SButton.ControllerY, SButton.ControllerStart, SButton.ControllerBack];
    private static TextBox? target;
    private static bool enterPending, unavailable;

    internal static Action<string, LogLevel>? Log { get; set; }

    /// <summary>Show Steam's keyboard for a selected field; false means the caller should show the game's keyboard.</summary>
    internal static bool TryShow(TextBox box)
    {
        // Steam's keyboard has no idea which split screen player is typing.
        if (unavailable || Context.IsSplitScreen)
            return false;
        if (target is not null)
        {
            // Steam said it showed the last keyboard, yet the page got input before it closed: it isn't on screen.
            unavailable = true;
            target = null;
            Log?.Invoke("Steam's keyboard didn't close after opening; using the game's keyboard.", LogLevel.Debug);
            return false;
        }
        try
        {
            if (!Show(box))
                return false;
        }
        catch (Exception error)
        {
            // Not a Steam copy, or Steam isn't running for this one.
            unavailable = true;
            Log?.Invoke($"Steam's keyboard isn't available; using the game's keyboard: {error.Message}", LogLevel.Debug);
            return false;
        }
        target = box;
        return true;
    }

    /// <summary>
    /// Press Enter on the field once the controller's buttons are up. Steam's keyboard takes the press that closes it,
    /// so a button still held then would reach the page as a new press and open the first result.
    /// </summary>
    internal static void Update(Func<SButton, bool> isDown)
    {
        if (!enterPending || KeyboardButtons.Any(isDown))
            return;
        TextBox box = target!;
        target = null;
        enterPending = false;
        // Enter as if typed: searches show their results, the name page saves.
        if (IsTyping(box))
            box.RecieveCommandInput('\r');
    }

    // Kept apart so that a copy without Steamworks fails inside TryShow's catch rather than where this class loads.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool Show(TextBox box) => SteamKeyboard.Show(box.Text, box.textLimit < 0 ? 100u : (uint)box.textLimit, Closed);

    private static void Closed(string? text)
    {
        if (target is null)
            return;
        try
        {
            // Cancelling is like closing the game's keyboard: the field stays selected with its text.
            if (text is null || !IsTyping(target))
            {
                if (text is not null)
                    Log?.Invoke("Text from Steam's keyboard was dropped because its field is no longer selected.", LogLevel.Debug);
                target = null;
                return;
            }
            target.Text = text;
            enterPending = true;
        }
        catch (Exception error)
        {
            target = null;
            Log?.Invoke($"Text from Steam's keyboard couldn't be entered: {error}", LogLevel.Warn);
        }
    }

    private static bool IsTyping(TextBox box) => box.Selected && Game1.keyboardDispatcher.Subscriber == box;
}

/// <summary>The only code that uses Steamworks, which ships with the game.</summary>
internal static class SteamKeyboard
{
    // Keeps the callback registered; typed as object so loading this class never needs Steamworks.
    private static object? dismissed;
    private static Action<string?>? closed;

    internal static bool Show(string text, uint limit, Action<string?> onClosed)
    {
        dismissed ??= Callback<GamepadTextInputDismissed_t>.Create(OnDismissed);
        if (!SteamUtils.ShowGamepadTextInput(EGamepadTextInputMode.k_EGamepadTextInputModeNormal,
            EGamepadTextInputLineMode.k_EGamepadTextInputLineModeSingleLine, "", limit, text))
            return false;
        closed = onClosed;
        return true;
    }

    // Steam runs this from the game's update (SteamHelper.Update). The game registers the same callback for its own
    // keyboard, which 1.6.15 never opens, and ignores closings it didn't start.
    private static void OnDismissed(GamepadTextInputDismissed_t result)
    {
        Action<string?>? onClosed = closed;
        closed = null;
        if (onClosed is null)
            return;
        string? text = null;
        try
        {
            // The length is in bytes; one more leaves room for the terminator however Steam counts it.
            if (result.m_bSubmitted && SteamUtils.GetEnteredGamepadTextInput(out string entered, SteamUtils.GetEnteredGamepadTextLength() + 1))
                text = entered;
        }
        finally { onClosed(text); }
    }
}
