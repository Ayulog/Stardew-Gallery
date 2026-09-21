namespace StardewGallery;

/// <summary>The actual asset language and player context of one content request.</summary>
internal readonly struct EventSourceScope : IEquatable<EventSourceScope>
{
    private readonly string? assetName;
    private readonly string? locale;
    private readonly string? contextId;

    internal EventSourceScope(string assetName, string locale, int screenId, string contextId)
    {
        this.assetName = NormalizeAssetName(assetName);
        this.locale = (locale ?? string.Empty).Trim();
        ScreenId = screenId;
        this.contextId = contextId ?? string.Empty;
    }

    public string AssetName => assetName ?? string.Empty;
    public string Locale => locale ?? string.Empty;
    public int ScreenId { get; }
    public string ContextId => contextId ?? string.Empty;

    internal static string NormalizeAssetName(string name) => (name ?? string.Empty).Replace('\\', '/').Trim();

    public bool Equals(EventSourceScope other)
        => StringComparer.OrdinalIgnoreCase.Equals(AssetName, other.AssetName)
            && StringComparer.OrdinalIgnoreCase.Equals(Locale, other.Locale)
            && ScreenId == other.ScreenId
            && StringComparer.Ordinal.Equals(ContextId, other.ContextId);

    public override bool Equals(object? obj) => obj is EventSourceScope other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(
        StringComparer.OrdinalIgnoreCase.GetHashCode(AssetName),
        StringComparer.OrdinalIgnoreCase.GetHashCode(Locale),
        ScreenId,
        StringComparer.Ordinal.GetHashCode(ContextId));
    public static bool operator ==(EventSourceScope left, EventSourceScope right) => left.Equals(right);
    public static bool operator !=(EventSourceScope left, EventSourceScope right) => !left.Equals(right);
}

/// <summary>An observed operation identity, not a claim about copyright or historical authorship.</summary>
internal sealed record EventSourceActor(string UniqueId, string Name, string? Version = null, bool IsGameBase = false)
{
    internal static EventSourceActor GameBase { get; } = new("StardewValley", "Stardew Valley", IsGameBase: true);
}

internal enum EventSourceStatus
{
    Unknown,
    Partial,
    Complete
}

internal enum EventSourceMutationKind
{
    Add,
    Change,
    Delete
}

/// <summary>A visible difference across an operation boundary; reverted writes inside a group are invisible.</summary>
internal sealed record EventSourceMutation(
    long Sequence,
    EventSourceMutationKind Kind,
    EventSourceActor? Actor,
    EventSourceActor? Executor,
    int DefinitionGeneration,
    string? BeforeHash,
    string? AfterHash,
    bool Failed,
    EventSourceActor? PriorProvider = null,
    EventSourceActor? PriorProviderExecutor = null);

/// <summary>Immutable evidence for one exact raw key and script in a completed load.</summary>
internal sealed record EventSourceInfo(
    EventSourceScope Scope,
    string RawEventKey,
    string ScriptHash,
    long LoadId,
    int DefinitionGeneration,
    EventSourceActor? Provider,
    EventSourceActor? ProviderExecutor,
    IReadOnlyList<EventSourceMutation> Mutations,
    EventSourceStatus Status,
    string? Reason,
    bool IsDeleted = false,
    bool ScriptMatches = true);
