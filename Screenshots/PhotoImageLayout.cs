namespace StardewGallery;

internal static class PhotoImageLayout
{
    internal static (int Width, int Height) Fit(int width, int height, int maxWidth, int maxHeight)
    {
        double scale = Math.Min(1, Math.Min(maxWidth / (double)Math.Max(1, width), maxHeight / (double)Math.Max(1, height)));
        return (Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
    }

    internal static (int X, int Y, int Width, int Height) Crop(int width, int height, int targetWidth, int targetHeight)
    {
        double aspect = targetWidth / (double)targetHeight;
        if (width / (double)height > aspect)
        {
            int cropWidth = Math.Max(1, (int)Math.Round(height * aspect));
            return ((width - cropWidth) / 2, 0, cropWidth, height);
        }
        int cropHeight = Math.Max(1, (int)Math.Round(width / aspect));
        return (0, (height - cropHeight) / 2, width, cropHeight);
    }
}
