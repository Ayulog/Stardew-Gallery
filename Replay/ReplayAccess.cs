namespace StardewGallery;

internal sealed record ReplayAccess(bool Allowed, string? ReasonKey = null);

internal static class ReplayAccessPolicy
{
    internal static ReplayAccess Evaluate(StoryKind kind, bool seen, bool unlockAll, bool ordinaryEnabled,
        bool protectionReady = true, bool multiplayer = false, bool active = false,
        bool saveFileReady = true, bool festivalLocation = false)
    {
        if (kind == StoryKind.Internal) return new(false, "replay.internal");
        if (!protectionReady) return new(false, "replay.protection-failed");
        if (multiplayer) return new(false, "replay.multiplayer");
        if (active) return new(false, "replay.already-running");
        if (kind == StoryKind.Ordinary && !ordinaryEnabled) return new(false, "replay.ordinary-disabled");
        if (!seen && !unlockAll) return new(false, "event.locked");
        // A brand-new save has no folder to back up until the first night's save.
        if (!saveFileReady) return new(false, "replay.no-save-file");
        // Entering today's festival venue would start, block or end the real festival.
        if (festivalLocation) return new(false, "replay.festival-location");
        return new(true);
    }
}
