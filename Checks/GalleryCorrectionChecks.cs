using StardewGallery;

internal static class GalleryCorrectionChecks
{
    internal static void Run()
    {
        // XNA key codes: F1 112, F3 114, G 71, K 75, Enter 13, Escape 27, V 86, LeftControl 162, LeftWindows 91.
        GalleryTypingKeys typing = new();
        int[] held = [112, 114, 71, 13];
        Check(typing.Update(false, held, false).Hide.Length == 0, "nothing is hidden outside a text field");
        Check(typing.Update(true, held, false) is { Paste: false, Escape: false } start && start.Hide.ToHashSet().SetEquals(held),
            "keys held when typing starts are hidden without acting");
        Check(typing.Update(true, held, false).Hide.Length == 0, "a held key is hidden once, not again every tick");
        Check(typing.Update(true, [.. held, 75], false) is { Hide: [75], Paste: false, Escape: false }, "a letter pressed while typing is hidden");
        Check(typing.Update(true, [75], false).Hide.Length == 0 && typing.Update(true, [75, 71], false) is { Hide: [71] },
            "a key released and pressed again is hidden again");
        typing.Update(true, [], false);
        Check(typing.Update(true, [162], false) is { Hide: [162], Paste: false } && typing.Update(true, [162, 86], false) is { Hide: [86], Paste: true },
            "Ctrl, then V pastes, and both stay hidden");
        Check(!typing.Update(true, [162, 86], false).Paste, "holding Ctrl+V pastes once");
        typing.Update(true, [], false);
        Check(typing.Update(true, [86], false) is { Hide: [86], Paste: false }, "V alone is typing, not pasting");
        typing.Update(true, [], true);
        Check(typing.Update(true, [91, 86], true).Paste, "Cmd+V pastes on macOS");
        typing.Update(true, [], true);
        Check(!typing.Update(true, [162, 86], true).Paste, "Ctrl+V doesn't paste on macOS, as in the game");
        typing.Update(true, [], false);
        Check(typing.Update(true, [27], false) is { Hide: [27], Escape: true }, "Escape ends typing and is hidden");
        Check(typing.Update(false, [27, 75], false).Hide.Length == 0 && typing.Update(true, [27, 75], false) is { Escape: false },
            "after typing ends nothing new is hidden; keys held into the next field are hidden without acting");
        GalleryTypingKeys otherScreen = new();
        Check(otherScreen.Update(true, [75], false) is { Hide: [75], Escape: false }, "each screen keeps its own typing state");

        string unicode = "Abigail \u963f\u6bd4\u76d6\u5c14 \u30a2\u30d3\u30b2\u30a4\u30eb e\u0301 \ud83d\ude00";
        Check(ReferenceEquals(unicode, GallerySearchInput.CleanText(unicode)), "Unicode and combining text are preserved without allocation");
        Check(GallerySearchInput.CleanText("A\0B\r\nC\tD\u2028E\u2029F") == "AB  C D E F", "control characters removed, line breaks become spaces");

        Check(GalleryLocationName.Resolve("Custom_JenkinsHouse", "Custom_JenkinsHouse", key => key == "location.custom_jenkinshouse" ? "Jenkins' House" : null)
            == "Jenkins' House", "known raw location uses localized fallback");
        Check(GalleryLocationName.Resolve("Custom_JenkinsHouse", "Provided localized name", _ => "Fallback")
            == "Provided localized name", "provider display name takes priority");
        Check(GalleryLocationName.Resolve("Custom_NewHouse", null, _ => null) == "New House", "unknown custom ID becomes readable");

        DayOfWeek[] weekdays = Enum.GetValues<DayOfWeek>();
        for (int mask = 1; mask < 1 << weekdays.Length; mask++)
        {
            DayOfWeek[] excluded = weekdays.Where((_, index) => (mask & (1 << index)) != 0).ToArray();
            DayOfWeekCondition condition = new(excluded, ConditionSource.LegacyEventPrecondition, "weekday fixture", true);
            ConditionTextSpec spec = ConditionDescriber.Describe(condition);
            HashSet<string> shown = ((ListTextValue)spec.Arguments["days"]).Values.Cast<TermTextValue>().Select(term => term.Value).ToHashSet();
            foreach (DayOfWeek day in weekdays)
                Check(!excluded.Contains(day) == (spec.Negated ? !shown.Contains(day.ToString()) : shown.Contains(day.ToString())), "weekday complement preserves all seven outcomes");
            Check(condition.Negated && condition.RawSegment == "weekday fixture", "original weekday AST untouched");
        }
        DayOfWeekCondition tuesday = new(weekdays.Where(day => day != DayOfWeek.Tuesday).ToArray(), ConditionSource.LegacyEventPrecondition, "d Mon Wed Thu Fri Sat Sun", true);
        ConditionTextSpec tuesdaySpec = ConditionDescriber.Describe(tuesday);
        Check(!tuesdaySpec.Negated && ((ListTextValue)tuesdaySpec.Arguments["days"]).Values is [TermTextValue { Value: "Tuesday" }], "six excluded days display Tuesday");

        // Preconditions.Season (1.6.15) compares only the first listed season, for both Season and its negations.
        string[] seasons = ["spring", "summer", "fall", "winter"];
        ConditionEvaluator seasonEvaluator = new();
        ConditionEvaluationContext seasonContext = new(null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
        foreach (string[] listed in Enumerable.Range(1, (1 << seasons.Length) - 1)
            .Select(mask => seasons.Where((_, index) => (mask & (1 << index)) != 0).ToArray())
            .SelectMany(listed => new[] { listed, listed.Reverse().ToArray() }))
        foreach (bool negated in new[] { false, true })
        {
            SeasonCondition condition = new(listed, ConditionSource.LegacyEventPrecondition, "season fixture", negated);
            ConditionTextSpec spec = ConditionDescriber.Describe(condition);
            bool namesFirst = spec.Negated == negated && ((ListTextValue)spec.Arguments["seasons"]).Values is [TermTextValue { Value: var shown }] && shown == listed[0];
            bool listsIgnored = listed.Length == 1
                ? spec.LocalizationKey == "condition.season" && !spec.Arguments.ContainsKey("ignored")
                : spec.LocalizationKey == "condition.season-first-only"
                    && ((ListTextValue)spec.Arguments["ignored"]).Values.Cast<TermTextValue>().Select(term => term.Value).SequenceEqual(listed.Skip(1));
            Check(namesFirst && listsIgnored, "season text names only the first season and lists the ignored ones");
            foreach (string current in seasons)
                Check(seasonEvaluator.Evaluate(condition, seasonContext with { Season = current }).Truth
                    == ((current == listed[0]) != negated ? ConditionTruth.True : ConditionTruth.False), "season evaluation follows the game's first-season rule");
        }
        ConditionTextSpec repeated = ConditionDescriber.Describe(new SeasonCondition(["SPRING", "spring", "Summer", "summer"], ConditionSource.LegacyEventPrecondition, "z spring spring summer summer", true));
        Check(repeated.Negated && repeated.NaturalNegativeKey == "condition.season-first-only-not"
            && ((ListTextValue)repeated.Arguments["ignored"]).Values is [TermTextValue { Value: "Summer" }], "repeated seasons are ignored without case duplicates");

        Check(GalleryUiRules.ResolveReturnPosition(89, 12, 6, 3, 120) == (12, 17), "late character restores home scroll and visible slot");
        Check(GalleryUiRules.ResolveReturnPosition(15, 5, 2, 3, 20) == (5, 5), "late event restores album scroll and visible slot");
        Check(GalleryUiRules.ResolveReturnPosition(15, 0, 2, 3, 20) == (5, 5), "stale scroll still keeps selected event visible");
        float scale = GalleryTextFit.Scale(600, 90, 328, 56);
        Check(scale * 600 <= 328.001f && scale * 90 <= 56.001f, "long labels fit both width and height with button padding");
        string shortTitle = "14 hearts ID 1724099";
        Check(ReferenceEquals(shortTitle, GalleryTextFit.Ellipsize(shortTitle, 100, text => text.Length)), "short titles retain their full text");
        string longTitle = "14 hearts ID Parrot.RomRas_14HeartPart2";
        Check(GalleryTextFit.Ellipsize(longTitle, 22, text => text.Length) == "14 hearts ID Parrot...", "long ID truncates without changing title scale");
        Check(GalleryTextFit.Ellipsize("e\u0301abcdef", 5, text => text.Length) == "e\u0301...", "ellipsis does not split a combining text element");
        Check(GalleryTextFit.Ellipsize("\ud83d\ude00abcdef", 4, text => text.Length) == "...", "ellipsis does not leave an unmatched surrogate");
        ActiveDialogueEventCondition notActive = new("haleyCakewalk2", ConditionSource.LegacyEventPrecondition, "!ActiveDialogueEvent haleyCakewalk2", true);
        ConditionTextSpec notActiveText = ConditionDescriber.Describe(notActive);
        Check(notActiveText.Negated && notActiveText.NaturalNegativeKey == "condition.dialogue-event-not", "negated dialogue uses a requirement description, not a failed status label");
        ConditionEvaluationContext absent = new(null, null, null, null, null, null, null, null, null, null, null, null, null, null, null)
            { Details = new() { ActiveDialogues = new HashSet<string>() } };
        ConditionEvaluator evaluator = new(null);
        Check(ConditionRowPresentation.Status(evaluator.Evaluate(notActive, absent)) == ConditionStatusIcon.Check,
            "inactive dialogue satisfies the negative requirement");
        Check(ConditionRowPresentation.Status(evaluator.Evaluate(notActive, absent with { Details = new() { ActiveDialogues = new HashSet<string> { "haleyCakewalk2" } } })) == ConditionStatusIcon.Cross,
            "active dialogue fails the negative requirement");
        Console.WriteLine("Gallery 2.1.2 correction checks passed.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("2.1.2: " + message);
    }
}
