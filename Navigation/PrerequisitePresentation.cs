using StardewModdingAPI;
using StardewValley;

namespace StardewGallery;

internal static class PrerequisitePresentation
{
    internal static string Title(PrerequisiteEvent entry, GalleryViewContext context)
    {
        if (context.Names?.Get(entry.Identity) is string name) return name;
        string key = "prerequisite.name." + entry.EventId;
        return context.I18n.Get(key).HasValue() ? context.I18n.Get(key).ToString() : entry.EventId;
    }
    internal static ConditionTruth Truth(IReadOnlyList<ConditionDisplayItem> rows)
        => rows.Any(row => ConditionRowPresentation.Status(row.Evaluation) == ConditionStatusIcon.Cross) ? ConditionTruth.False
        : rows.Any(row => ConditionRowPresentation.Status(row.Evaluation) == ConditionStatusIcon.Unknown) ? ConditionTruth.Unknown : ConditionTruth.True;

    internal static ConditionTruth RuleTruth(MarkerAction source, ITranslationHelper i18n)
    {
        if (source.HostOnly && !Game1.IsMasterGame || source.Player == "Host" && !Game1.IsMasterGame) return ConditionTruth.Unknown;
        if (Game1.player.triggerActionsRun.Contains(source.RuleId)) return ConditionTruth.False;
        if (source.SkipCondition is not null)
        {
            ConditionTruth skip = Truth(GalleryConditionPresentation.Query(source.SkipCondition, i18n));
            if (skip == ConditionTruth.True) return ConditionTruth.False;
            if (skip == ConditionTruth.Unknown) return ConditionTruth.Unknown;
        }
        if (source.Triggers.Any(trigger => !new[] { "DayEnding", "DayStarted", "LocationChanged", "Manual" }.Contains(trigger, StringComparer.OrdinalIgnoreCase)))
            return ConditionTruth.Unknown;
        return Truth(GalleryConditionPresentation.Query(source.Condition, i18n));
    }
    internal static ConditionTruth Truth(PrerequisiteEvent entry, ITranslationHelper i18n)
    {
        // Multiple writers can cancel or overwrite one another. Never predict their final result from one matching rule.
        if (entry.Sources.Count != 1 || !entry.Sources[0].SetsMarker) return ConditionTruth.Unknown;
        return RuleTruth(entry.Sources[0], i18n);
    }
}
