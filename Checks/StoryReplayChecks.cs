using StardewGallery;

internal static class StoryReplayChecks
{
    internal static void Run()
    {
        foreach (StoryKind kind in Enum.GetValues<StoryKind>())
        {
            Check(!ReplayAccessPolicy.Evaluate(kind, true, true, true, protectionReady: false).Allowed, "protection required");
            Check(!ReplayAccessPolicy.Evaluate(kind, true, true, true, multiplayer: true).Allowed, "single player only");
            Check(!ReplayAccessPolicy.Evaluate(kind, true, true, true, active: true).Allowed, "one replay at a time");
            Check(!ReplayAccessPolicy.Evaluate(kind, false, false, true).Allowed, "unseen requires unlock");
        }
        Check(ReplayAccessPolicy.Evaluate(StoryKind.Heart, true, false, false).Allowed, "heart replay independent of ordinary option");
        Check(!ReplayAccessPolicy.Evaluate(StoryKind.Ordinary, true, true, false).Allowed, "ordinary disabled even when unlocked");
        Check(ReplayAccessPolicy.Evaluate(StoryKind.Ordinary, true, false, true).Allowed, "seen ordinary replay");
        Check(ReplayAccessPolicy.Evaluate(StoryKind.Ordinary, false, true, true).Allowed, "unlocked ordinary replay");
        Check(!ReplayAccessPolicy.Evaluate(StoryKind.Internal, true, true, true).Allowed, "internal is never playable");
        Check(ReplayAccessPolicy.Evaluate(StoryKind.Heart, true, false, false, saveFileReady: false).ReasonKey == "replay.no-save-file",
            "a save without a backup folder explains the first-night requirement");
        Check(ReplayAccessPolicy.Evaluate(StoryKind.Heart, false, false, false, saveFileReady: false).ReasonKey == "event.locked",
            "locked events keep the locked reason before save-file checks");
        Check(ReplayAccessPolicy.Evaluate(StoryKind.Heart, true, false, false, festivalLocation: true).ReasonKey == "replay.festival-location",
            "today's festival venue cannot be used for replay");
        Console.WriteLine("Story replay access checks passed.");
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("Story replay: " + message);
    }
}
