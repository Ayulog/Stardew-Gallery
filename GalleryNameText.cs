using System.Diagnostics.CodeAnalysis;

namespace StardewGallery;

internal static class GalleryNameText
{
    internal static bool IsMissing([NotNullWhen(false)] string? text) => string.IsNullOrWhiteSpace(text)
        || text.Contains("no translation:", StringComparison.OrdinalIgnoreCase)
        || text.Contains("{{i18n:", StringComparison.OrdinalIgnoreCase)
        || text.StartsWith("[LocalizedText", StringComparison.OrdinalIgnoreCase);
}
