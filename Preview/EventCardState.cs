namespace StardewGallery;

/// <summary>
/// Strict two-state event card model.
/// State A (Unlocked): the event is currently satisfied by the player's real save state.
/// State B (Locked): the event is not currently satisfied and is only previewable through
/// supported temporary state simulation.
/// The record is pure (no UI types) so it may be unit tested; the caller maps the keys to
/// label/color/button text.
/// </summary>
internal sealed record EventCardState(
    bool Unlocked,
    string StatusKey,
    string ButtonKey
);

internal static class EventCardStateResolver
{
    internal static EventCardState Resolve(bool currentlyAvailable, bool seenByPlayer, bool galleryUnlocked)
    {
        bool unlocked = currentlyAvailable || seenByPlayer || galleryUnlocked;
        return unlocked
            ? new EventCardState(true, "event.state-unlocked", "event.replay")
            : new EventCardState(false, "event.state-locked", "event.preview");
    }
}
