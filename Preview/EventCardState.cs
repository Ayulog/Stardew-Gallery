namespace StardewGallery;

/// <summary>
/// Strict two-state event card model.
/// State A (Unlocked): the event was seen by this player or the gallery unlock-all switch is on.
/// State B (Locked): the event isn't available for playback in the gallery.
/// The record is pure (no UI types) so it may be unit tested; the caller maps the keys to
/// label/color/button text.
/// </summary>
internal sealed record EventCardState(
    bool Unlocked,
    string StatusKey
);

internal static class EventCardStateResolver
{
    internal static EventCardState Resolve(bool seenByPlayer, bool galleryUnlocked)
    {
        bool unlocked = seenByPlayer || galleryUnlocked;
        return unlocked
            ? new EventCardState(true, "event.state-unlocked")
            : new EventCardState(false, "event.state-locked");
    }
}
