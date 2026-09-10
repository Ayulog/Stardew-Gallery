namespace StardewGallery.Appearance;

internal sealed record PortraitFrame(string? TexturePath, int X = -1, int Y = -1, int Width = 64, int Height = 64,
    float Alpha = 1, bool Disabled = false)
{
    internal bool TryRegion(int textureWidth, int textureHeight, out (int X, int Y, int Width, int Height) region)
    {
        int x = X >= 0 && Y >= 0 ? X : 0, y = X >= 0 && Y >= 0 ? Y : 0;
        region = (x, y, Width, Height);
        return !Disabled && Width > 0 && Height > 0 && float.IsFinite(Alpha)
            && (long)x + Width <= textureWidth && (long)y + Height <= textureHeight;
    }
}

internal sealed record DetailedSprite(int OriginX = 32, int OriginY = 112);

internal static class AppearanceGeometry
{
    internal static (float X, float Y, float Width, float Height) Fit(int x, int y, int width, int height, int sourceWidth, int sourceHeight)
    {
        float scale = Math.Min(width / (float)Math.Max(1, sourceWidth), height / (float)Math.Max(1, sourceHeight));
        float w = sourceWidth * scale, h = sourceHeight * scale;
        return (x + (width - w) / 2, y + (height - h) / 2, w, h);
    }

    internal static (float X, float Y, float Scale, float Width, float Height) SpritePlacement(
        int x, int y, int width, int height, int frameWidth, int frameHeight, DetailedSprite? detailed)
    {
        int renderedWidth = detailed is null ? frameWidth : 32;
        int renderedHeight = detailed is null ? frameHeight : frameHeight * 2;
        float scale = Math.Min(4f, Math.Min(width * .65f / Math.Max(1, renderedWidth), height * .72f / Math.Max(1, renderedHeight)));
        float w = renderedWidth * scale, h = renderedHeight * scale;
        float left = x + (width - w) / 2, top = y + MathF.Round(height * .76f) - h;
        if (detailed is not null)
        {
            // Scale Up's Draw hook replaces the origin and destination size.
            left += detailed.OriginX * w / (frameWidth * 4f);
            top += (frameHeight <= 16 ? 96 : detailed.OriginY) * h / (frameHeight * 4f);
        }
        return (left, top, scale, w, h);
    }
}
