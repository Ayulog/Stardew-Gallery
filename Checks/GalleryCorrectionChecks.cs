using StardewGallery;

internal static class GalleryCorrectionChecks
{
    internal static void Run()
    {
        GalleryKeyboardCapture capture = new();
        int[] keys = [112, 114, 71, 162, 86];
        Check(capture.Update(false, keys.ToHashSet()).Length == 0, "normal gameplay not captured");
        Check(capture.Update(true, keys.ToHashSet()).ToHashSet().SetEquals(keys), "F1/F3, letters and paste chord captured");
        Check(capture.Update(false, keys.ToHashSet()).ToHashSet().SetEquals(keys), "held keys remain captured after leaving search");
        Check(capture.Update(false, new HashSet<int>()).ToHashSet().SetEquals(keys), "release events remain captured");
        Check(!capture.HasCapturedKeys && capture.Update(false, new HashSet<int> { 114 }).Length == 0, "fresh shortcuts work after release");
        GalleryKeyboardCapture otherInput = new();
        capture.Update(true, keys.ToHashSet());
        Check(!otherInput.HasCapturedKeys, "capture state belongs to one input instance");
        capture.Reset();
        Check(!capture.HasCapturedKeys, "failed guard can release its local capture state");

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

        string[] seasons = ["spring", "summer", "fall", "winter"];
        for (int mask = 1; mask < 1 << seasons.Length; mask++)
        {
            string[] excluded = seasons.Where((_, index) => (mask & (1 << index)) != 0).ToArray();
            ConditionTextSpec spec = ConditionDescriber.Describe(new SeasonCondition(excluded, ConditionSource.LegacyEventPrecondition, "season fixture", true));
            HashSet<string> shown = ((ListTextValue)spec.Arguments["seasons"]).Values.Cast<TermTextValue>().Select(term => term.Value).ToHashSet();
            foreach (string season in seasons)
                Check(!excluded.Contains(season) == (spec.Negated ? !shown.Contains(season) : shown.Contains(season)), "season complement preserves all four outcomes");
        }
        ConditionTextSpec winter = ConditionDescriber.Describe(new SeasonCondition(["SPRING", "summer", "Fall", "fall"], ConditionSource.LegacyEventPrecondition, "z spring summer fall", true));
        Check(!winter.Negated && ((ListTextValue)winter.Arguments["seasons"]).Values is [TermTextValue { Value: "winter" }], "three excluded seasons display winter, ignoring case and duplicates");
        ConditionTextSpec notWinter = ConditionDescriber.Describe(new SeasonCondition(["winter"], ConditionSource.LegacyEventPrecondition, "z winter", true));
        Check(notWinter.Negated, "short negative is not expanded to three seasons");

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
