namespace StardewGallery.Appearance;

internal sealed record PortraitFrame(string? TexturePath, int X = -1, int Y = -1, int Width = 64, int Height = 64,
    float Alpha = 1, bool Disabled = false)
{
    internal static PortraitFrame? FromGameSheet(int width, int height)
        => width is 64 or 128 && height >= 64 && height % 64 == 0 ? new(TexturePath: null) : null;

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

    /// <summary>
    /// Deterministic meadow stroll for the album scenic window: gentle walk right (frames 4..7) -> front-facing
    /// idle pause (frame 0) -> gentle walk left (frames 12..15) -> front-facing idle pause (frame 0).
    /// Enabled only on standard 4-column x >=4-row sheets; any other layout safely stays at frame 0 with 0 offset.
    /// </summary>
    internal static (int Frame, float OffsetX) ScenicStroll(
        string? name,
        double totalMilliseconds,
        int textureWidth,
        int textureHeight,
        int frameWidth,
        int frameHeight,
        float maxOffset = 28f)
    {
        if (frameWidth <= 0 || frameHeight <= 0
            || textureWidth != frameWidth * 4
            || textureHeight < frameHeight * 4
            || !double.IsFinite(totalMilliseconds)
            || maxOffset <= 0f)
            return (0, 0f);

        const double cycleMs = 14400.0;
        const double stepMs = 200.0;
        double phase = ((totalMilliseconds + StablePhaseMs(name, (int)cycleMs)) % cycleMs + cycleMs) % cycleMs;

        // 0..2200ms: center front-facing pause
        if (phase < 2200.0)
            return (0, 0f);
        // 2200..3800ms (1.6s): walk right 0 -> +maxOffset (row 1: frames 4..7)
        if (phase < 3800.0)
        {
            double elapsed = phase - 2200.0;
            int frame = 4 + ((int)(elapsed / stepMs) % 4);
            float x = (float)(elapsed / 1600.0) * maxOffset;
            return (frame, x);
        }
        // 3800..6200ms (2.4s): right front-facing pause
        if (phase < 6200.0)
            return (0, maxOffset);
        // 6200..9400ms (3.2s): walk left +maxOffset -> -maxOffset (row 3: frames 12..15)
        if (phase < 9400.0)
        {
            double elapsed = phase - 6200.0;
            int frame = 12 + ((int)(elapsed / stepMs) % 4);
            float x = maxOffset - (float)(elapsed / 3200.0) * (maxOffset * 2f);
            return (frame, x);
        }
        // 9400..11800ms (2.4s): left front-facing pause
        if (phase < 11800.0)
            return (0, -maxOffset);
        // 11800..13400ms (1.6s): walk right -maxOffset -> 0 (row 1: frames 4..7)
        if (phase < 13400.0)
        {
            double elapsed = phase - 11800.0;
            int frame = 4 + ((int)(elapsed / stepMs) % 4);
            float x = -maxOffset + (float)(elapsed / 1600.0) * maxOffset;
            return (frame, x);
        }
        // 13400..14400ms: center front-facing pause (joins seamlessly with 0..2200ms)
        return (0, 0f);
    }

    private static int StablePhaseMs(string? name, int modulo)
    {
        if (string.IsNullOrEmpty(name) || modulo <= 0)
            return 0;
        uint hash = 2166136261u;
        foreach (char ch in name)
        {
            hash ^= ch;
            hash *= 16777619u;
        }
        return (int)(hash % (uint)modulo);
    }
}
