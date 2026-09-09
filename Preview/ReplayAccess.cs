namespace StardewGallery;

internal sealed record ReplayAccess(bool Allowed, string? ReasonKey = null);

internal static class ReplayAccessPolicy
{
    internal static ReplayAccess Evaluate(StoryKind kind, bool seen, bool unlockAll, bool ordinaryEnabled,
        bool protectionReady = true, bool multiplayer = false, bool active = false)
    {
        if (kind == StoryKind.Internal) return new(false, "replay.internal");
        if (!protectionReady) return new(false, "replay.protection-failed");
        if (multiplayer) return new(false, "replay.multiplayer");
        if (active) return new(false, "replay.already-running");
        if (kind == StoryKind.Ordinary && !ordinaryEnabled) return new(false, "replay.ordinary-disabled");
        return seen || unlockAll ? new(true) : new(false, "event.locked");
    }
}
