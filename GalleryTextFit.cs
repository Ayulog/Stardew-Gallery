namespace StardewGallery;

internal static class GalleryTextFit
{
    internal static float Scale(float textWidth, float textHeight, float width, float height)
        => Math.Min(1f, Math.Min(Math.Max(0, width) / Math.Max(1, textWidth), Math.Max(0, height) / Math.Max(1, textHeight)));
}
