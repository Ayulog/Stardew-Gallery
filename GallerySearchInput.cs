using System.Globalization;

namespace StardewGallery;

/// <summary>
/// Which keyboard keys to hide while a gallery text field is selected. Every key pressed while typing, or already
/// held when typing starts, is hidden until it is released, even after typing ends: a letter bound as a shortcut
/// doesn't fire, and the Enter that ends typing doesn't also act on the page. Codes are XNA <c>Keys</c> values.
/// </summary>
internal sealed class GalleryTypingKeys
{
    internal const int Escape = 27, V = 86, LeftWindows = 91, RightWindows = 92, LeftControl = 162, RightControl = 163;
    private readonly HashSet<int> hidden = [];
    private bool typing;

    /// <summary>Call every tick with the keys down now; returns the keys to start hiding and what a fresh press means.</summary>
    /// <param name="mac">Paste with Cmd (the Windows keys) instead of Ctrl, as the game does on macOS.</param>
    internal GalleryTypingAction Update(bool editing, IReadOnlyCollection<int> down, bool mac)
    {
        if (!editing)
        {
            // Keys already hidden stay hidden until released; the game keeps that list.
            hidden.Clear();
            typing = false;
            return GalleryTypingAction.None;
        }
        hidden.RemoveWhere(key => !down.Contains(key));
        bool started = !typing;
        typing = true;
        int[] hide = down.Where(hidden.Add).ToArray();
        // Keys held when typing starts are only hidden; they weren't pressed in the field.
        if (started || hide.Length == 0)
            return new(hide, false, false);
        bool command = mac ? down.Contains(LeftWindows) || down.Contains(RightWindows) : down.Contains(LeftControl) || down.Contains(RightControl);
        return new(hide, Paste: command && hide.Contains(V), Escape: hide.Contains(Escape));
    }
}

internal readonly record struct GalleryTypingAction(int[] Hide, bool Paste, bool Escape)
{
    internal static GalleryTypingAction None { get; } = new([], false, false);
}

internal static class GallerySearchInput
{
    internal static string CleanText(string text)
    {
        foreach (char character in text)
        {
            if (char.IsControl(character) || char.GetUnicodeCategory(character) is UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator)
                return string.Concat(text.Select(value => char.IsWhiteSpace(value) ? ' ' : value).Where(value => !char.IsControl(value)));
        }
        return text;
    }
}
