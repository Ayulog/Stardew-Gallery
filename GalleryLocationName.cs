using System.Text.RegularExpressions;

namespace StardewGallery;

internal static class GalleryLocationName
{
    internal static string Resolve(string id, string? displayName, Func<string, string?> translation)
    {
        if (!string.IsNullOrWhiteSpace(displayName) && !string.Equals(displayName, id, StringComparison.OrdinalIgnoreCase)
            && !displayName.StartsWith("Custom_", StringComparison.OrdinalIgnoreCase))
            return displayName;
        string? translated = translation("location." + id.ToLowerInvariant());
        if (!string.IsNullOrWhiteSpace(translated))
            return translated;
        string readable = id.StartsWith("Custom_", StringComparison.OrdinalIgnoreCase) ? id[7..] : id;
        readable = Regex.Replace(readable, @"([a-z0-9])([A-Z])", "$1 $2");
        return readable.Replace('_', ' ').Trim();
    }
}
