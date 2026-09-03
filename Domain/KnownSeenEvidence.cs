namespace StardewGallery;

internal enum KnownSeenSource
{
    SaveEventsSeen,
    LegacyCapturedVariant
}

internal sealed record KnownSeenEvidence(
    string EventId,
    EventIdentity? Identity,
    KnownSeenSource Source
);
