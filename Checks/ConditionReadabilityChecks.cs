using System.Text.Json;
using StardewGallery;

internal static class ConditionReadabilityChecks
{
    private static readonly string[] RequiredM3bKeys =
    [
        "condition.story-flag",
        "condition.story-flag-not",
        "condition.mail-titled",
        "condition.mail-titled-not",
        "condition.quest",
        "item.unknown"
    ];

    internal static void Run()
    {
        ConditionParser parser = new(key => key.Split('/', StringSplitOptions.RemoveEmptyEntries), GameQuerySplit.Arguments);
        string i18nDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../i18n"));
        var en = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(i18nDir, "default.json")))!;
        var zh = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(i18nDir, "zh.json")))!;

        CheckMailVsStoryFlags(parser, en, zh);
        CheckNativeDisplayResolvers(parser, en, zh);
        CheckLocationFallbacks();
        CheckGsqPrefixStripping(parser, en);
        CheckFontSanitization();
        CheckM3bLocales(i18nDir);

        Console.WriteLine("M3b condition readability & font safety checks passed.");
    }

    private static void CheckMailVsStoryFlags(ConditionParser parser, Dictionary<string, string> en, Dictionary<string, string> zh)
    {
        Dictionary<string, string> fakeMail = new(StringComparer.Ordinal)
        {
            ["RobinGift"] = "Here is some wood! Secret body spoiler.[#]Gift from Robin",
            ["IslandPassOut"] = "Secret body text without title marker.",
            ["EmptyTitle"] = "Secret body text with empty title.[#]   "
        };

        ConditionDisplayResolver resolver = new(
            npc => npc,
            item => item == "(O)24" ? "Parsnip" : null,
            (group, value) => en.TryGetValue($"{group}.{value.ToLowerInvariant()}", out string? found) ? found : value,
            time => time.ToString())
        {
            Mail = id => fakeMail.TryGetValue(id, out string? raw)
                ? ConditionDescriber.ExtractMailInfo(raw)
                : new MailDisplayInfo(MailLookupKind.HiddenStoryFlag)
        };

        Check(ConditionDescriber.ExtractMailInfo(null).Kind == MailLookupKind.HiddenStoryFlag, "missing mail key -> HiddenStoryFlag");
        Check(ConditionDescriber.ExtractMailInfo(fakeMail["IslandPassOut"]) is { Kind: MailLookupKind.MailNoTitle, Title: null }, "no [#] -> MailNoTitle");
        Check(ConditionDescriber.ExtractMailInfo(fakeMail["EmptyTitle"]) is { Kind: MailLookupKind.MailNoTitle, Title: null }, "empty [#] -> MailNoTitle");
        Check(ConditionDescriber.ExtractMailInfo(fakeMail["RobinGift"]) is { Kind: MailLookupKind.MailTitled, Title: "Gift from Robin" }, "[#] -> MailTitled");

        string Fmt(Dictionary<string, string> dict, string seg, ConditionDisplayResolver? r = null)
        {
            ConditionTextSpec spec = ConditionDescriber.Describe(parser.ParseSegment(seg));
            return ConditionTextFormatter.Format(spec, (key, args) => Translate(dict, key, args), r ?? resolver);
        }

        // Titled mail: positive & negative, MailCondition & PLAYER_HAS_MAIL
        string titledPos = Fmt(en, "n RobinGift");
        string titledNeg = Fmt(en, "l RobinGift");
        string titledGsqPos = Fmt(en, "G PLAYER_HAS_MAIL Current RobinGift");
        string titledGsqNeg = Fmt(en, "G !PLAYER_HAS_MAIL Current RobinGift Received");
        string titledGsqTomorrow = Fmt(en, "G PLAYER_HAS_MAIL Host RobinGift Tomorrow");
        Check(titledPos.Contains("Gift from Robin") && !titledPos.Contains("Secret body spoiler"), "MailCondition titled pos hides body");
        Check(titledNeg.Contains("Gift from Robin") && titledNeg.Contains("Hasn't received mail"), "MailCondition titled neg");
        Check(titledGsqPos == titledPos, "PLAYER_HAS_MAIL default current/any uses mail-titled");
        Check(titledGsqNeg == titledNeg, "negated PLAYER_HAS_MAIL uses mail-titled-not without double negation");
        Check(titledGsqTomorrow.Contains("Host") && titledGsqTomorrow.Contains("Gift from Robin") && titledGsqTomorrow.Contains("tomorrow"), "PLAYER_HAS_MAIL non-default keeps query-mail with title");
        Check(Fmt(zh, "n RobinGift").Contains("《Gift from Robin》"), "zh mail-titled uses title");

        // No-title mail: keeps ID-only, NEVER leaks mail body
        string noTitlePos = Fmt(en, "n IslandPassOut");
        string noTitleEmpty = Fmt(en, "G PLAYER_HAS_MAIL Current EmptyTitle");
        Check(noTitlePos.Contains("IslandPassOut") && !noTitlePos.Contains("Secret body"), "no-title mail keeps ID and blocks body leak");
        Check(noTitleEmpty.Contains("EmptyTitle") && !noTitleEmpty.Contains("Secret body"), "empty-title mail keeps ID and blocks body leak");

        // Absent key -> HiddenStoryFlag
        string flagPos = Fmt(en, "n SVE_StoryNode1");
        string flagNeg = Fmt(en, "l SVE_StoryNode1");
        string flagGsqPos = Fmt(en, "G PLAYER_HAS_MAIL Current SVE_StoryNode1");
        string flagGsqNeg = Fmt(en, "G !PLAYER_HAS_MAIL Current SVE_StoryNode1");
        Check(flagPos == "Completed prerequisite story flag: SVE_StoryNode1", "MailCondition hidden story flag pos");
        Check(flagNeg == "Hasn't completed prerequisite story flag: SVE_StoryNode1", "MailCondition hidden story flag neg");
        Check(flagGsqPos == flagPos, "PLAYER_HAS_MAIL hidden story flag pos");
        Check(flagGsqNeg == flagNeg, "negated PLAYER_HAS_MAIL hidden story flag uses natural negative without condition.not");

        // LegacySendMailCondition: null resolver keeps legacy; non-null formats safely
        ConditionDisplayResolver nullMailResolver = resolver with { Mail = null };
        Check(Fmt(en, "x RobinGift", nullMailResolver) == "Special mail condition", "LegacySendMail with null Mail keeps legacy output");
        Check(Fmt(en, "x RobinGift", resolver) == "Legacy event action: send mail Gift from Robin (display only)", "LegacySendMail with titled mail uses title");
        Check(Fmt(en, "x IslandPassOut", resolver) == "Legacy event action: send mail IslandPassOut (display only)", "LegacySendMail with no-title keeps ID");
        Check(Fmt(en, "x SVE_StoryNode1", resolver) == "Special mail condition", "LegacySendMail with absent key stays safe");
    }

    private static void CheckNativeDisplayResolvers(ConditionParser parser, Dictionary<string, string> en, Dictionary<string, string> zh)
    {
        ConditionDisplayResolver resolver = new(
            npc => npc,
            id => id == "(O)24" ? "Parsnip" : id == "(BC)130" ? "Chest" : null,
            (group, value) => en.TryGetValue($"{group}.{value.ToLowerInvariant()}", out string? found) ? found : value,
            time => time.ToString())
        {
            Recipe = (name, isCooking) => isCooking && name == "Fried Egg" ? "Fried Egg (Localized)" : !isCooking && name == "Wood Fence" ? "Wood Fence (Localized)" : null,
            Building = id => id == "Junimo Hut" ? "Junimo Hut (Localized)" : null,
            Monster = id => id == "Green Slime" ? "Green Slime (Localized)" : null,
            Quest = id => id == "1" ? "Meet the Wizard" : null
        };

        string Fmt(Dictionary<string, string> dict, ConditionExpression expr)
            => ConditionTextFormatter.Format(ConditionDescriber.Describe(expr), (key, args) => Translate(dict, key, args), resolver);

        // Item fallback across HasItem, Shipped, PLAYER_HAS_ITEM
        Check(Fmt(en, parser.ParseSegment("i Missing.Item")) == "Has item: Unknown item (Missing.Item)", "HasItem uses item.unknown");
        Check(Fmt(en, parser.ParseSegment("s Missing.Item 3")).Contains("Unknown item (Missing.Item) × 3"), "Shipped uses item.unknown");
        Check(Fmt(en, parser.ParseSegment("G PLAYER_HAS_ITEM Current Missing.Item 2")).Contains("Unknown item (Missing.Item)"), "PLAYER_HAS_ITEM uses item.unknown");
        Check(Fmt(zh, parser.ParseSegment("i Missing.Item")).Contains("未知物品 (Missing.Item)"), "zh item.unknown");
        Check(Fmt(en, parser.ParseSegment("i (O)24")) == "Has item: Parsnip", "known item displays name");

        // Recipe, Building, Monster, Quest extractors & formatters
        Check(ConditionDescriber.ExtractRecipeDisplayName("388 50/Home/130/true/null/", false, id => id == "(BC)130" ? "Chest" : null) == "Chest", "crafting BigCraftable fallback");
        Check(ConditionDescriber.ExtractRecipeDisplayName("-5 1/10 10/24/default/Egg Dish", true, _ => null) == "Egg Dish", "cooking explicit displayName field [4]");
        Check(Fmt(en, parser.ParseSegment("G PLAYER_HAS_COOKING_RECIPE Current Fried Egg")).Contains("Fried Egg (Localized)"), "cooking recipe resolver");
        Check(Fmt(en, parser.ParseSegment("G PLAYER_HAS_CRAFTING_RECIPE Current Wood Fence")).Contains("Wood Fence (Localized)"), "crafting recipe resolver");

        Check(Fmt(en, parser.ParseSegment("G BUILDINGS_CONSTRUCTED Farm \"Junimo Hut\" 1")).Contains("Junimo Hut (Localized)"), "building resolver");
        Check(Fmt(en, parser.ParseSegment("G BUILDINGS_CONSTRUCTED Farm All 1")).Contains("buildings"), "building All keeps term");

        Check(ConditionDescriber.ExtractMonsterDisplayName("24/5/0/0/false/1000/a/1/b/4/0/.0/true/3/绿色史莱姆", "Green Slime") == "绿色史莱姆", "monster [14] extractor");
        Check(ConditionDescriber.ExtractMonsterDisplayName("short/entry", "Fallback") == "Fallback", "monster missing [14] fallback");
        Check(Fmt(en, parser.ParseSegment("G PLAYER_KILLED_MONSTERS Current \"Green Slime\" 10")).Contains("Green Slime (Localized)"), "monster resolver");

        Check(ConditionDescriber.ExtractQuestTitle("Location/会见法师/Desc/Obj/WizardHouse/-1/0/-1/false") == "会见法师", "quest [1] extractor");
        Check(ConditionDescriber.ExtractQuestTitle("Location") is null, "quest short entry returns null");
        Check(Fmt(en, new QuestCondition("1", ConditionSource.Synthetic, "Quest 1", false)) == "Active quest: Meet the Wizard", "QuestCondition formats via condition.quest");

        // Critical SDV 1.6.15 q parity check
        Check(parser.ParseSegment("q 101") is ChoseDialogueAnswersCondition, "q precondition strictly remains ChoseDialogueAnswersCondition");
        Check(ConditionParser.SupportedCanonicalNames.Count == 41 && ConditionParser.SupportedAliases.Count == 48, "41 canonicals and 48 aliases preserved");
    }

    private static void CheckLocationFallbacks()
    {
        Check(GalleryLocationName.Resolve("Custom_Foo_BarBaz", null, _ => null) == "Foo Bar Baz", "Custom_Foo_BarBaz");
        Check(GalleryLocationName.Resolve("Custom_Area2ZoneB_Test", null, _ => null) == "Area 2 Zone B Test", "Custom_Area2ZoneB_Test");
        Check(GalleryLocationName.Resolve("Custom_RSVCliff", null, _ => null) == "RSV Cliff", "Custom_RSVCliff acronym boundary");
        Check(GalleryLocationName.Resolve("Custom__Foo__Bar_", null, _ => null) == "Foo Bar", "Custom__Foo__Bar_ collapses spaces");
        Check(GalleryLocationName.Resolve("Custom_NewHouse", null, _ => null) == "New House", "Custom_NewHouse");
        Check(GalleryLocationName.Resolve("Custom_", null, _ => null) == "Custom_", "Custom_ falls back to id when empty");
    }

    private static void CheckGsqPrefixStripping(ConditionParser parser, Dictionary<string, string> en)
    {
        Check(ConditionDescriber.StripCustomQueryPrefixes("FlashShifter.SVE_HasItem Current 1") == "HasItem Current 1", "UniqueID_ prefix stripped");
        Check(ConditionDescriber.StripCustomQueryPrefixes("!FlashShifter.SVE_HasItem Current 1") == "!HasItem Current 1", "negated UniqueID_ prefix stripped");
        Check(ConditionDescriber.StripCustomQueryPrefixes("Author.Mod/CustomCheck \"arg with spaces\"") == "CustomCheck \"arg with spaces\"", "Author.Mod/ prefix stripped preserving quotes");
        Check(ConditionDescriber.StripCustomQueryPrefixes("SEASON spring, !Author.Mod_Flag Current, Other.Mod/Check 2") == "SEASON spring, !Flag Current, Check 2", "multi-clause GSQ stripped");
        Check(ConditionDescriber.StripCustomQueryPrefixes("CustomMod.Query Value") == "CustomMod.Query Value", "no _ or / kept intact");
        Check(ConditionDescriber.StripCustomQueryPrefixes("Author.Mod_Query \"unclosed") == "Author.Mod_Query \"unclosed", "unclosed quote untouched");

        ConditionExpression expr = parser.ParseSegment("G Author.Mod_Check Current 1");
        NativeQueryCondition nq = (NativeQueryCondition)expr;
        Check(nq.Query == "Author.Mod_Check Current 1", "NativeQueryCondition.Query itself is never mutated");
        Check(SafeGameQuery.UnknownReasonKey(nq.Query) == "event-detail.unknown.query-custom", "SafeGameQuery.UnknownReasonKey still detects custom mod query");
        ConditionDisplayResolver r = new(n => n, i => i, (g, v) => v, t => t.ToString());
        string described = ConditionTextFormatter.Format(ConditionDescriber.Describe(expr), (k, a) => Translate(en, k, a), r);
        Check(described == "Game condition: Check Current 1", "condition.native-query displays stripped query");
    }

    private static void CheckFontSanitization()
    {
        HashSet<char> ascii = [.. "ABCabc123 ?"];
        Check(GalleryTextFit.SanitizeForCharacterSet(null, null, ascii.Contains) == "", "null -> empty");
        Check(GalleryTextFit.SanitizeForCharacterSet("", null, ascii.Contains) == "", "empty -> empty");

        string valid = "Abc\r\n123";
        Check(ReferenceEquals(valid, GalleryTextFit.SanitizeForCharacterSet(valid, null, ascii.Contains)), "all-supported fast path is zero-alloc");

        string withDefault = "Abc中文";
        Check(ReferenceEquals(withDefault, GalleryTextFit.SanitizeForCharacterSet(withDefault, '?', ascii.Contains)), "non-null DefaultCharacter is a no-op");

        Check(GalleryTextFit.SanitizeForCharacterSet("Ab\r\nc中", null, ascii.Contains) == "Ab\r\nc?", "missing BMP char maps to '?' preserving CR/LF");
        Check(GalleryTextFit.SanitizeForCharacterSet("A\uD83D\uDE00B", null, ascii.Contains) == "A?B", "surrogate pair collapses into single '?'");

        HashSet<char> noQuestionMark = [.. "AB "];
        Check(GalleryTextFit.SanitizeForCharacterSet("A中B", null, noQuestionMark.Contains) == "A B", "falls back to space when '?' absent");

        HashSet<char> minimal = [.. "AB"];
        Check(GalleryTextFit.SanitizeForCharacterSet("A中B", null, minimal.Contains) == "AB", "omits unsupported char when '?' and ' ' both absent");
    }

    private static void CheckM3bLocales(string i18nDir)
    {
        string[] files = Directory.GetFiles(i18nDir, "*.json");
        Check(files.Length == 12, "12 locale files present");
        foreach (string file in files)
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file))!;
            foreach (string key in RequiredM3bKeys)
            {
                Check(dict.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value),
                    $"{Path.GetFileName(file)} missing or empty M3b key {key}");
            }
        }
    }

    private static string Translate(Dictionary<string, string> dict, string key, IReadOnlyDictionary<string, string> args)
    {
        Check(dict.TryGetValue(key, out string? template), "missing key: " + key);
        string text = template!;
        foreach (var (k, v) in args)
            text = text.Replace("{{" + k + "}}", v);
        Check(!text.Contains("{{", StringComparison.Ordinal), $"unresolved token in {key}: {text}");
        return text;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("M3b check failed: " + message);
    }
}
