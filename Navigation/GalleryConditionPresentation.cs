using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Buildings;
using StardewValley.Monsters;
using StardewValley.TokenizableStrings;

namespace StardewGallery;

internal static class GalleryConditionPresentation
{
    private static LocalizedContentManager.LanguageCode? cachedLanguage;
    private static Dictionary<string, string>? mailData;
    private static Dictionary<string, string>? craftingRecipes;
    private static Dictionary<string, string>? cookingRecipes;
    private static Dictionary<string, BuildingData>? buildingsData;
    private static Dictionary<string, string>? monstersData;
    private static Dictionary<string, string>? questsData;

    internal static void InvalidateCache()
    {
        cachedLanguage = null;
        mailData = null;
        craftingRecipes = null;
        cookingRecipes = null;
        buildingsData = null;
        monstersData = null;
        questsData = null;
    }

    private static void EnsureCacheLocale()
    {
        try
        {
            LocalizedContentManager.LanguageCode current = LocalizedContentManager.CurrentLanguageCode;
            if (cachedLanguage != current)
            {
                InvalidateCache();
                cachedLanguage = current;
            }
        }
        catch
        {
        }
    }

    internal static IReadOnlyList<ConditionDisplayItem> Build(GalleryEvent entry, ITranslationHelper i18n, CurrentStateSnapshot? state = null, string? locationName = null)
    {
        EnsureCacheLocale();
        GalleryLocationNames locationNames = new(i18n);
        ConditionParser parser = ConditionProduction.CreateParser(Event.SplitPreconditions, ArgUtility.SplitBySpaceQuoteAware);
        ConditionPresentationBuilder presentation = new(
            parser,
            ConditionProduction.CreateEvaluator(null),
            (key, arguments) => i18n.Get(key, arguments),
            CreateResolver(i18n, locationNames));
        GameLocation? location = Game1.getLocationFromName(entry.LocationName);
        CurrentStateSnapshot current = RuntimeStateReader.ForLocation(state ?? RuntimeStateReader.Capture(), location)
            with { Details = ConditionStateReader.Capture(parser.ParseRawKey(entry.EventKey), location) };
        return presentation.Build(entry.EventKey, current,
            locationName ?? locationNames.Get(entry.LocationName));
    }

    internal static EncounterClue Clue(GalleryEvent entry, GalleryCatalog catalog, CurrentStateSnapshot? sharedState = null)
    {
        if (entry.Kind == StoryKind.Internal)
            return EncounterClue.None;
        CurrentStateSnapshot shared = sharedState ?? RuntimeStateReader.Capture();
        if (catalog.IsParallelBranchUnlocked(entry, shared.EventsSeen))
            return EncounterClue.None;
        ConditionParser parser = ConditionProduction.CreateParser(Event.SplitPreconditions, ArgUtility.SplitBySpaceQuoteAware);
        ConditionSet parsed = parser.ParseRawKey(entry.EventKey);
        GameLocation? location = Game1.getLocationFromName(entry.LocationName);
        CurrentStateSnapshot current = RuntimeStateReader.ForLocation(shared, location)
            with { Details = ConditionStateReader.Capture(parsed, location) };
        return EncounterClueClassifier.Classify(entry, parsed, current, ConditionProduction.CreateEvaluator(null),
            parallelBranchUnlocked: false, locationAvailable: location is not null);
    }

    internal static string Location(GalleryEvent entry, ITranslationHelper i18n)
        => new GalleryLocationNames(i18n).Get(entry.LocationName);

    internal static IReadOnlyList<ConditionDisplayItem> Query(string raw, ITranslationHelper i18n)
    {
        EnsureCacheLocale();
        GalleryLocationNames locationNames = new(i18n);
        var parser = ConditionProduction.CreateParser(Event.SplitPreconditions, ArgUtility.SplitBySpaceQuoteAware);
        var set = new ConditionSet(string.IsNullOrWhiteSpace(raw) ? [] : GameStateQuery.SplitRaw(raw)
            .Select(clause => (ConditionExpression)new NativeQueryCondition(clause, ConditionSource.GameStateQuery, clause, false)).ToArray());
        var state = RuntimeStateReader.Capture() with { Details = ConditionStateReader.Capture(set, Game1.currentLocation) };
        var presentation = new ConditionPresentationBuilder(parser, new ConditionEvaluator(null), (key, args) => i18n.Get(key, args),
            CreateResolver(i18n, locationNames));
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

    private static ConditionDisplayResolver CreateResolver(ITranslationHelper i18n, GalleryLocationNames locationNames)
        => new(
            NPC.GetDisplayName,
            ResolveItem,
            (key, value) => Term(key, value, i18n, locationNames),
            Game1.getTimeOfDayString)
        {
            Mail = ResolveMail,
            Recipe = ResolveRecipe,
            Building = ResolveBuilding,
            Monster = ResolveMonster,
            Quest = ResolveQuest
        };

    private static string ParseTokenTextSafe(string text)
    {
        try
        {
            return TokenParser.ParseText(text);
        }
        catch
        {
            return text;
        }
    }

    private static Dictionary<string, string>? GetMailData()
    {
        if (mailData is not null)
            return mailData;
        try { return mailData = DataLoader.Mail(Game1.content); }
        catch { return null; }
    }

    private static Dictionary<string, string>? GetCraftingRecipes()
    {
        if (craftingRecipes is not null)
            return craftingRecipes;
        try { return craftingRecipes = DataLoader.CraftingRecipes(Game1.content); }
        catch { return null; }
    }

    private static Dictionary<string, string>? GetCookingRecipes()
    {
        if (cookingRecipes is not null)
            return cookingRecipes;
        try { return cookingRecipes = DataLoader.CookingRecipes(Game1.content); }
        catch { return null; }
    }

    private static Dictionary<string, BuildingData>? GetBuildingsData()
    {
        if (buildingsData is not null)
            return buildingsData;
        try { return buildingsData = DataLoader.Buildings(Game1.content); }
        catch { return null; }
    }

    private static Dictionary<string, string>? GetMonstersData()
    {
        if (monstersData is not null)
            return monstersData;
        try { return monstersData = DataLoader.Monsters(Game1.content); }
        catch { return null; }
    }

    private static Dictionary<string, string>? GetQuestsData()
    {
        if (questsData is not null)
            return questsData;
        try { return questsData = DataLoader.Quests(Game1.content); }
        catch { return null; }
    }

    private static MailDisplayInfo ResolveMail(string mailId)
    {
        Dictionary<string, string>? mail = GetMailData();
        if (mail is null)
            return new MailDisplayInfo(MailLookupKind.Unknown);
        if (!mail.TryGetValue(mailId, out string? raw))
            return new MailDisplayInfo(MailLookupKind.HiddenStoryFlag);
        return ConditionDescriber.ExtractMailInfo(raw, ParseTokenTextSafe);
    }

    private static string? ResolveItem(string id)
    {
        try
        {
            string? qualified = ItemRegistry.QualifyItemId(id);
            if (!string.IsNullOrWhiteSpace(qualified))
            {
                string? qualifiedName = ItemRegistry.GetData(qualified)?.DisplayName;
                if (!string.IsNullOrWhiteSpace(qualifiedName))
                    return qualifiedName;
            }
            string? directName = ItemRegistry.GetData(id)?.DisplayName;
            return string.IsNullOrWhiteSpace(directName) ? null : directName;
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolveRecipe(string name, bool isCooking)
    {
        Dictionary<string, string>? recipes = isCooking ? GetCookingRecipes() : GetCraftingRecipes();
        if (recipes is null || !recipes.TryGetValue(name, out string? raw))
            return null;
        try
        {
            string display = new CraftingRecipe(name, isCooking).DisplayName;
            if (!string.IsNullOrWhiteSpace(display))
                return display;
        }
        catch
        {
        }
        return ConditionDescriber.ExtractRecipeDisplayName(raw, isCooking, ResolveItem);
    }

    private static string? ResolveBuilding(string id)
    {
        Dictionary<string, BuildingData>? buildings = GetBuildingsData();
        if (buildings is null)
            return null;
        if (!buildings.TryGetValue(id, out BuildingData? data))
            data = buildings.FirstOrDefault(pair => pair.Key.Equals(id, StringComparison.OrdinalIgnoreCase)).Value;
        if (data is null || string.IsNullOrWhiteSpace(data.Name))
            return null;
        string parsed = ParseTokenTextSafe(data.Name).Trim();
        return string.IsNullOrWhiteSpace(parsed) ? null : parsed;
    }

    private static string? ResolveMonster(string id)
    {
        try
        {
            string display = Monster.GetDisplayName(id);
            if (!string.IsNullOrWhiteSpace(display) && !string.Equals(display, id, StringComparison.Ordinal))
                return display;
        }
        catch
        {
        }
        Dictionary<string, string>? monsters = GetMonstersData();
        if (monsters is not null && monsters.TryGetValue(id, out string? raw))
        {
            string extracted = ConditionDescriber.ExtractMonsterDisplayName(raw, id);
            if (!string.Equals(extracted, id, StringComparison.Ordinal))
                return extracted;
        }
        return null;
    }

    private static string? ResolveQuest(string id)
    {
        Dictionary<string, string>? quests = GetQuestsData();
        if (quests is not null && quests.TryGetValue(id, out string? raw) && ConditionDescriber.ExtractQuestTitle(raw) is { } title)
        {
            string parsed = ParseTokenTextSafe(title).Trim();
            return string.IsNullOrWhiteSpace(parsed) ? title : parsed;
        }
        return null;
    }

    private static string Term(string group, string value, ITranslationHelper i18n, GalleryLocationNames locationNames)
    {
        if (group == "location") return locationNames.Get(value);
        if (group == "relationship") group = "status";
        if (group == "festival")
        {
            try
            {
                if (Utility.TryGetPassiveFestivalData(value, out var data) && !string.IsNullOrWhiteSpace(data.DisplayName))
                    return TokenParser.ParseText(data.DisplayName);
            }
            catch { }
        }
        string key = $"{group}.{value.ToLowerInvariant()}";
        return i18n.Get(key).HasValue() ? i18n.Get(key).ToString() : value;
    }
}
