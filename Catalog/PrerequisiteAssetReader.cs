using StardewModdingAPI;
using StardewValley;

namespace StardewGallery;

internal static class PrerequisiteAssetReader
{
    internal static IReadOnlyList<PrerequisiteEvent> Read(GalleryCatalog catalog, IMonitor monitor)
    {
        try
        {
            var rules = DataLoader.TriggerActions(Game1.content).Where(rule => !string.IsNullOrWhiteSpace(rule.Id) && !string.IsNullOrWhiteSpace(rule.Trigger))
                .Select(rule => new PrerequisiteRule(rule.Id, ArgUtility.SplitBySpace(rule.Trigger), rule.Condition ?? "",
                    rule.HostOnly, rule.MarkActionApplied, (string.IsNullOrWhiteSpace(rule.Action) ? [] : new[] { rule.Action })
                        .Concat(rule.Actions ?? []).Where(action => !string.IsNullOrWhiteSpace(action)).ToArray(), rule.SkipPermanentlyCondition));
            return PrerequisiteCatalog.Build(rules, catalog, ArgUtility.SplitBySpaceQuoteAware,
                new ConditionParser(Event.SplitPreconditions, ArgUtility.SplitBySpaceQuoteAware), raw => Event.ParseCommands(raw));
        }
        catch (Exception error)
        { monitor.Log("Prerequisite data could not be read: " + error.Message, LogLevel.Warn); return []; }
    }
}
