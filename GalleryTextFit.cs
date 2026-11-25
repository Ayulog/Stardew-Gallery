using System.Globalization;
using System.Text;

namespace StardewGallery;

internal static class GalleryTextFit
{
    internal static string SanitizeForCharacterSet(string? text, char? defaultCharacter, Func<char, bool> hasCharacter)
    {
        if (string.IsNullOrEmpty(text))
            return text ?? string.Empty;
        if (defaultCharacter.HasValue)
            return text;

        bool allSupported = true;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c is not ('\r' or '\n') && !hasCharacter(c))
            {
                allSupported = false;
                break;
            }
        }
        if (allSupported)
            return text;

        char? fallback = hasCharacter('?') ? '?' : hasCharacter(' ') ? ' ' : null;
        StringBuilder sb = new(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c is '\r' or '\n' || hasCharacter(c))
            {
                sb.Append(c);
                continue;
            }
            if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                i++;
            if (fallback is { } fb)
                sb.Append(fb);
        }
        return sb.ToString();
    }

    internal static float Scale(float textWidth, float textHeight, float width, float height)
        => Math.Min(1f, Math.Min(Math.Max(0, width) / Math.Max(1, textWidth), Math.Max(0, height) / Math.Max(1, textHeight)));

    internal static string Ellipsize(string text, float width, Func<string, float> measure)
    {
        if (measure(text) <= width) return text;
        const string suffix = "...";
        if (measure(suffix) > width) return "";
        int[] starts = StringInfo.ParseCombiningCharacters(text);
        int low = 0, high = starts.Length;
        while (low < high)
        {
            int middle = (low + high + 1) / 2;
            int end = middle == starts.Length ? text.Length : starts[middle];
            if (measure(text[..end] + suffix) <= width) low = middle;
            else high = middle - 1;
        }
        return text[..(low == starts.Length ? text.Length : starts[low])] + suffix;
    }
}
