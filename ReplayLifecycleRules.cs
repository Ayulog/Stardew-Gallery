namespace StardewGallery;

internal static class ReplayLifecycleRules
{
    internal static bool ShouldRestore(bool eventObserved, int quietTicks, int totalTicks, int timeoutTicks)
        => eventObserved ? quietTicks >= 15 : totalTicks >= timeoutTicks;

    internal static bool CanFinishRestore(bool locationMatches, bool transitionPending, bool fading, int stableTicks)
        => locationMatches && !transitionPending && !fading && stableTicks >= 2;

    internal static bool CanApplyRestore(bool transitionPending, bool fading) => !transitionPending && !fading;

    internal static int NextSpeed(int speed) => speed switch { 1 => 2, 2 => 4, _ => 1 };

    internal static bool IsTransitionBlocking(float fadeAlpha, bool globalFade, bool nonWarpFade, bool locationPending)
        => fadeAlpha > 0f || globalFade || nonWarpFade || locationPending;

    internal static bool BlocksReplaySpeed(bool transitionBlocking, bool dialogueOpen)
        => transitionBlocking && !dialogueOpen;

    internal static bool IsSecondaryEvent(bool observed, object? original, object? current)
        => observed && original is not null && current is not null && !ReferenceEquals(original, current);
}
