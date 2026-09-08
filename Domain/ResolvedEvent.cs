namespace StardewGallery;

internal sealed record ResolvedEvent(
    EventIdentity Identity,
    string LocationName,
    string RawEventKey,
    string ResolvedScript,
    EventFragments Fragments,
    string RootDefinitionHash,
    string RootScriptHash
)
{
    public bool HasLocationContext { get; init; } = true;

    public string AssetName => Identity.AssetName;

    public string EventId => Identity.EventId;
}
