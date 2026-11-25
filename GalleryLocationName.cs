using System.Text.RegularExpressions;

namespace StardewGallery;

internal static class GalleryLocationName
{
    internal static string Resolve(string id, string? displayName, Func<string, string?> translation)
    {
        if (!GalleryNameText.IsMissing(displayName) && !string.Equals(displayName, id, StringComparison.OrdinalIgnoreCase)
            && !displayName.StartsWith("Custom_", StringComparison.OrdinalIgnoreCase))
            return displayName;
        string? translated = translation("location." + id.ToLowerInvariant());
        if (!GalleryNameText.IsMissing(translated))
            return translated;
        string readable = id.StartsWith("Custom_", StringComparison.OrdinalIgnoreCase) ? id[7..] : id;
        readable = readable.Replace('_', ' ');
        readable = Regex.Replace(readable, @"([A-Z]+)([A-Z][a-z])", "$1 $2");
        readable = Regex.Replace(readable, @"([a-z])([A-Z])", "$1 $2");
        readable = Regex.Replace(readable, @"([A-Za-z])([0-9])", "$1 $2");
        readable = Regex.Replace(readable, @"([0-9])([A-Za-z])", "$1 $2");
        readable = Regex.Replace(readable, @"\s+", " ").Trim();
        return readable.Length == 0 ? id : readable;
    }
}
