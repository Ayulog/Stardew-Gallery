namespace StardewGallery;

internal enum QueryKind { All, Heart, Ordinary, Prerequisite }
internal enum QueryCompletion { Any, Complete, Incomplete }
internal enum QueryConditionState { Any, Met, Unmet, Unknown }

internal sealed record QueryFilter
{
    internal QueryKind Kind { get; init; }
    internal string? Npc { get; init; }
    internal string? Location { get; init; }
    internal string? Source { get; init; }
    internal QueryCompletion Completion { get; init; }
    internal QueryConditionState Conditions { get; init; }
    internal string? Season { get; init; }
    internal string? Weather { get; init; }
    internal int? Time { get; init; }
    internal int? MinimumHearts { get; init; }
    internal int? MaximumHearts { get; init; }
}

/// <summary>A confirmed original provider, or an explicit unresolved category; candidate mods are never treated as confirmed.</summary>
internal sealed record StoryQuerySource(string Key, string Name, string? ModId = null)
{
    internal const string GameKey = "game", UnknownKey = "unknown", AmbiguousKey = "ambiguous";
    internal static StoryQuerySource Unknown { get; } = new(UnknownKey, "Unknown");
    internal static string ModKey(string id) => "mod:" + id;

    internal static StoryQuerySource From(EventOriginMatch? origin)
    {
        if (origin?.Status == EventOriginMatchStatus.Ambiguous)
            return new(AmbiguousKey, "Multiple possible sources");
        if (origin is not { Status: EventOriginMatchStatus.Identified, Provider: { } provider }) return Unknown;
        return provider.IsGameBase ? new(GameKey, provider.Name)
            : new(ModKey(provider.UniqueId), string.IsNullOrWhiteSpace(provider.Name) ? provider.UniqueId : provider.Name, provider.UniqueId);
    }

    internal string Label(Func<string, string>? translate = null)
        => ModId is not null ? $"{Name} ({ModId})" : translate?.Invoke(Key switch
        {
            GameKey => "source.game",
            AmbiguousKey => "source.origin-ambiguous",
            _ => "source.unknown"
        }) ?? Name;
}
