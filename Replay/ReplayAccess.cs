namespace StardewGallery;

internal sealed record ReplayAccess(bool Allowed, string? ReasonKey = null);

internal static class ReplayAccessPolicy
{
    internal static bool IsUnlockedForViewing(StoryKind kind, bool seen, bool unlockAll, bool parallelBranchUnlocked = false)
        => kind != StoryKind.Internal && (seen || unlockAll || kind == StoryKind.Heart && parallelBranchUnlocked);

    internal static ReplayAccess EvaluateWedding() => new(false, "replay.wedding-unavailable");

    internal static ReplayAccess Evaluate(StoryKind kind, bool seen, bool unlockAll, bool ordinaryEnabled,
        bool protectionReady = true, bool multiplayer = false, bool active = false,
        bool saveFileReady = true, bool festivalLocation = false, bool parallelBranchUnlocked = false,
        bool isWedding = false, bool isControllerEvent = false)
    {
        if (isWedding) return new(false, "replay.wedding-unavailable");
        if (kind == StoryKind.Internal) return new(false, "replay.internal");
        if (isControllerEvent) return new(false, "replay.controller-unavailable");
        if (!protectionReady) return new(false, "replay.protection-failed");
        if (multiplayer) return new(false, "replay.multiplayer");
        if (active) return new(false, "replay.already-running");
        if (kind == StoryKind.Ordinary && !ordinaryEnabled) return new(false, "replay.ordinary-disabled");
        if (!IsUnlockedForViewing(kind, seen, unlockAll, parallelBranchUnlocked)) return new(false, "event.locked");
        // A brand-new save has no folder to back up until the first night's save.
        if (!saveFileReady) return new(false, "replay.no-save-file");
        // Entering today's festival venue would start, block or end the real festival.
        if (festivalLocation) return new(false, "replay.festival-location");
        return new(true);
    }
}
