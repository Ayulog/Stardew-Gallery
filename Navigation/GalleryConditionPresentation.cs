using StardewModdingAPI;
using StardewValley;

namespace StardewGallery;

internal static class GalleryConditionPresentation
{
    internal static IReadOnlyList<ConditionDisplayItem> Build(GalleryEvent entry, ITranslationHelper i18n, CurrentStateSnapshot? state = null, string? locationName = null)
    {
        ConditionParser parser = ConditionProduction.CreateParser(Event.SplitPreconditions, ArgUtility.SplitBySpaceQuoteAware);
        ConditionPresentationBuilder presentation = new(
            parser,
            ConditionProduction.CreateEvaluator(null),
            (key, arguments) => i18n.Get(key, arguments),
            new ConditionDisplayResolver(NPC.GetDisplayName, id => ItemRegistry.GetData(id)?.DisplayName,
                (key, value) => Term(key, value, i18n), Game1.getTimeOfDayString));
        GameLocation? location = Game1.getLocationFromName(entry.LocationName);
        CurrentStateSnapshot current = RuntimeStateReader.ForLocation(state ?? RuntimeStateReader.Capture(), location)
            with { Details = ConditionStateReader.Capture(parser.ParseRawKey(entry.EventKey), location) };
        return presentation.Build(entry.EventKey, current,
            locationName ?? Location(entry, i18n));
    }

    internal static string Location(GalleryEvent entry, ITranslationHelper i18n)
        => new GalleryLocationNames(i18n).Get(entry.LocationName);

    internal static IReadOnlyList<ConditionDisplayItem> Query(string raw, ITranslationHelper i18n)
    {
        var parser = ConditionProduction.CreateParser(Event.SplitPreconditions, ArgUtility.SplitBySpaceQuoteAware);
        var set = new ConditionSet(string.IsNullOrWhiteSpace(raw) ? [] : GameStateQuery.SplitRaw(raw)
            .Select(clause => (ConditionExpression)new NativeQueryCondition(clause, ConditionSource.GameStateQuery, clause, false)).ToArray());
        var state = RuntimeStateReader.Capture() with { Details = ConditionStateReader.Capture(set, Game1.currentLocation) };
        var presentation = new ConditionPresentationBuilder(parser, new ConditionEvaluator(null), (key, args) => i18n.Get(key, args),
            new ConditionDisplayResolver(NPC.GetDisplayName, id => ItemRegistry.GetData(id)?.DisplayName, (key, value) => Term(key, value, i18n), Game1.getTimeOfDayString));
        return presentation.Build(set, state);
    }

    internal static string InternalStep(StoryDependencyResult dependency, string eventId, ITranslationHelper i18n)
    {
        string heading = i18n.Get("nav.internal-step", new { id = eventId });
        List<string> lines = [];
        foreach (GalleryEvent step in dependency.InternalSteps)
        {
            IReadOnlyList<ConditionDisplayItem> conditions = Build(step, i18n);
            string requirements = conditions.Count == 0 ? i18n.Get("condition.none")
                : string.Join("; ", conditions.Select(item => item.Description.Replace("≥", ">=").Replace("≤", "<=").Replace("≠", "!=")));
            lines.Add(Location(step, i18n) + ": " + requirements);
        }
        return lines.Count == 0 ? heading : heading + "\n" + string.Join("\n", lines.Distinct(StringComparer.Ordinal));
    }

    private static string Term(string group, string value, ITranslationHelper i18n)
    {
        if (group == "location") return new GalleryLocationNames(i18n).Get(value);
        if (group == "relationship") group = "status";
        if (group == "festival")
        {
            try
            {
                if (Utility.TryGetPassiveFestivalData(value, out var data) && !string.IsNullOrWhiteSpace(data.DisplayName))
                    return StardewValley.TokenizableStrings.TokenParser.ParseText(data.DisplayName);
            }
            catch { }
        }
        string key = $"{group}.{value.ToLowerInvariant()}";
        return i18n.Get(key).HasValue() ? i18n.Get(key).ToString() : value;
    }
}
