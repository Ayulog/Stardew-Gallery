namespace StardewGallery;

internal enum QueryKind { All, Heart, Ordinary, Prerequisite }
internal enum QueryCompletion { Any, Complete, Incomplete }
internal enum QueryConditionState { Any, Met, Unmet, Unknown }

internal sealed record QueryFilter
{
    internal QueryKind Kind { get; init; }
    internal string? Npc { get; init; }
    internal string? Location { get; init; }
    internal QueryCompletion Completion { get; init; }
    internal QueryConditionState Conditions { get; init; }
    internal string? Season { get; init; }
    internal string? Weather { get; init; }
    internal int? Time { get; init; }
    internal int? MinimumHearts { get; init; }
    internal int? MaximumHearts { get; init; }
}
