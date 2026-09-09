using StardewModdingAPI;
using StardewValley;

namespace StardewGallery;

internal static class GalleryConditionPresentation
{
    internal static IReadOnlyList<ConditionDisplayItem> Build(GalleryEvent entry, ITranslationHelper i18n, CurrentStateSnapshot? state = null)
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
            Location(entry, i18n));
    }

    internal static string Location(GalleryEvent entry, ITranslationHelper i18n)
        => GalleryLocationName.Resolve(entry.LocationName, Game1.getLocationFromName(entry.LocationName)?.DisplayName,
            key => i18n.Get(key).HasValue() ? i18n.Get(key).ToString() : null);

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
