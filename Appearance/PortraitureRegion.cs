namespace StardewGallery.Appearance;

internal static class PortraitureRegion
{
    internal static PortraitFrame? Create(float scale, int logicalWidth, int logicalHeight,
        (int X, int Y, int Width, int Height)? forced)
    {
        if (forced is { } area)
            return area.X >= 0 && area.Y >= 0 && area.Width > 0 && area.Height > 0
                ? new(null, area.X, area.Y, area.Width, area.Height) : null;
        double width = logicalWidth * (double)scale, height = logicalHeight * (double)scale;
        if (!float.IsFinite(scale) || scale <= 0 || width < 1 || height < 1 || width > int.MaxValue || height > int.MaxValue)
            return null;
        return new(null, 0, 0, (int)width, (int)height);
    }
}
