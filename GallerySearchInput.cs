using System.Globalization;

namespace StardewGallery;

internal sealed class GalleryKeyboardCapture
{
    private readonly HashSet<int> captured = [];

    internal bool HasCapturedKeys => captured.Count > 0;

    internal void Reset() => captured.Clear();

    internal int[] Update(bool editing, IReadOnlySet<int> down)
    {
        if (editing)
            captured.UnionWith(down);
        int[] blocked = captured.ToArray();
        // Also hide the release transition, including keys held while leaving the textbox.
        captured.RemoveWhere(key => !down.Contains(key));
        return blocked;
    }
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
