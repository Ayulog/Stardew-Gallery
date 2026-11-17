namespace StardewGallery.Appearance;

internal sealed record HdPortraitAnimation(
    int HFrames,
    int VFrames,
    IReadOnlyList<int> Delays,
    int FallbackDelayMs = 200);

internal sealed record HdPortraitEntry(
    int Width,
    int Height,
    string? TexturePath = null,
    HdPortraitAnimation? Animation = null)
{
    internal bool IsValidDimensions => Width is > 0 and <= 8192 && Height is > 0 and <= 8192;

    internal bool IsAnimated => Animation is { } anim
        && anim.HFrames >= 1
        && anim.VFrames >= 1
        && (long)anim.HFrames * anim.VFrames > 1
        && (long)anim.HFrames * anim.VFrames <= 1024;

    /// <summary>
    /// HD Portraits 1.6 returns a default unconfigured <c>Size = 64</c> record for characters without an HD pack;
    /// treat those defaults as unconfigured so variant fallbacks and DDFC defaults keep working.
    /// </summary>
    internal bool IsConfigured => IsValidDimensions
        && (Width != 64 || Height != 64 || !string.IsNullOrWhiteSpace(TexturePath) || IsAnimated);

    internal PortraitFrame? SelectFrame(double elapsedMilliseconds, int? textureWidth = null, int? textureHeight = null)
    {
        if (!IsValidDimensions)
            return null;

        string? normalizedPath = string.IsNullOrWhiteSpace(TexturePath) ? null : TexturePath.Trim();
        if (!IsAnimated)
            return new PortraitFrame(normalizedPath, X: 0, Y: 0, Width: Width, Height: Height);

        HdPortraitAnimation anim = Animation!;
        int totalFrames = anim.HFrames * anim.VFrames;
        int defaultDelay = anim.FallbackDelayMs > 0 ? anim.FallbackDelayMs : 200;

        long cycleMs = 0;
        for (int i = 0; i < totalFrames; i++)
        {
            int frameDelay = i < anim.Delays.Count && anim.Delays[i] > 0 ? anim.Delays[i] : defaultDelay;
            cycleMs += frameDelay;
        }

        int frameIndex = 0;
        if (cycleMs > 0 && double.IsFinite(elapsedMilliseconds))
        {
            long elapsed = (long)Math.Floor(Math.Max(0d, elapsedMilliseconds)) % cycleMs;
            long cursor = 0;
            for (int i = 0; i < totalFrames; i++)
            {
                int frameDelay = i < anim.Delays.Count && anim.Delays[i] > 0 ? anim.Delays[i] : defaultDelay;
                cursor += frameDelay;
                if (elapsed < cursor)
                {
                    frameIndex = i;
                    break;
                }
            }
        }

        long x = (long)(frameIndex % anim.HFrames) * Width;
        long y = (long)(frameIndex / anim.HFrames) * Height;
        if (x < 0 || y < 0 || x + Width > int.MaxValue || y + Height > int.MaxValue)
        {
            x = 0;
            y = 0;
        }
        else if (textureWidth is > 0 && textureHeight is > 0
            && (x + Width > textureWidth.Value || y + Height > textureHeight.Value)
            && Width <= textureWidth.Value && Height <= textureHeight.Value)
        {
            x = 0;
            y = 0;
        }

        return new PortraitFrame(normalizedPath, (int)x, (int)y, Width, Height);
    }
}

internal static class HdPortraitSelection
{
    internal static HdPortraitEntry? Resolve(IReadOnlyDictionary<string, HdPortraitEntry?> entries, IEnumerable<string> keys)
    {
        foreach (string key in keys)
        {
            if (string.IsNullOrWhiteSpace(key))
                continue;
            if (entries.TryGetValue(key, out HdPortraitEntry? candidate) && candidate is { IsConfigured: true })
                return candidate;
        }
        return null;
    }
}
