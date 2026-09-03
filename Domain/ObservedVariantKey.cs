namespace StardewGallery;

internal readonly record struct ObservedVariantKey(
    EventIdentity Identity,
    string RootDefinitionHash,
    string PlaybackHash
);
