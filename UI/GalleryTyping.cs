using Microsoft.Xna.Framework.Input;
using StardewValley;

namespace StardewGallery;

/// <summary>
/// Hides the keys typed into a gallery text field with the game's own <see cref="InputState.IgnoreKeys"/>, which the
/// game also calls when its window regains focus. SMAPI reads the keyboard through the game's input state, so hidden
/// keys reach neither SMAPI's input events (other mods' keybinds included) nor the game. The typed text still arrives,
/// because it comes through the window's text input rather than the key state.
/// </summary>
internal static class GalleryTyping
{
    /// <summary>
    /// Run from SMAPI's UpdateTicking: SMAPI has read this tick's keys (as the game captured them last tick), and the
    /// game hasn't yet handled what was typed this tick, such as an Enter that ends typing.
    /// </summary>
    internal static void Update(GalleryTypingKeys keys, IGallerySearchMenu? menu, Func<Keys[]> keyboard, Func<string?> clipboard)
    {
        if (menu is not { IsSearchSelected: true })
        {
            keys.Update(false, [], false);
            return;
        }
        GalleryTypingAction action = keys.Update(true, keyboard().Select(key => (int)key).ToArray(), OperatingSystem.IsMacOS());
        if (action.Hide.Length > 0)
            Game1.input.IgnoreKeys(action.Hide.Select(code => (Keys)code).ToArray());
        // The game pastes when it sees Ctrl+V (Cmd+V on macOS) in the key state, which is hidden now; paste as it would.
        if (action.Paste && clipboard() is { Length: > 0 } text)
            Game1.keyboardDispatcher.Subscriber?.RecieveTextInput(text);
        if (action.Escape)
            menu.DeselectSearch();
    }

    /// <summary>The keyboard as MonoGame reports it now, including keys pressed this tick.</summary>
    internal static Keys[] PressedKeys() => Keyboard.GetState().GetPressedKeys();

    /// <summary>The clipboard text, read the way the game reads it for pasting.</summary>
    internal static string? Clipboard()
    {
        string? text = null;
        DesktopClipboard.GetText(ref text);
        return text;
    }
}
