namespace StardewGallery;

internal static class GalleryLayout
{
    internal static (int X, int Y) Center(int viewportWidth, int viewportHeight, int menuWidth, int menuHeight)
        => ((viewportWidth - menuWidth) / 2, (viewportHeight - menuHeight) / 2);

    internal static bool Changed(int oldWidth, int oldHeight, int viewportWidth, int viewportHeight)
        => oldWidth != viewportWidth || oldHeight != viewportHeight;

    internal static double ScaleToFit(int viewportWidth, int viewportHeight, int menuWidth, int menuHeight, int margin)
        => Math.Min(1d, Math.Min((viewportWidth - margin * 2d) / menuWidth, (viewportHeight - margin * 2d) / menuHeight));
}
