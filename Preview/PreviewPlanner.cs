namespace StardewGallery;

internal sealed record CurrentStateSnapshot(
    string? Season,
    string? Weather,
    int? DayOfMonth,
    int? Year,
    int? Time,
    int? DaysPlayed,
    IReadOnlyDictionary<string, int>? Friendship,
    IReadOnlySet<string>? EventsSeen,
    IReadOnlySet<string>? LocalMail,
    IReadOnlySet<string>? HostMail,
    IReadOnlySet<string>? HostOrLocalMail,
    IReadOnlySet<string>? Dating,
    IReadOnlySet<string>? Spouse,
    bool? Roommate,
    IReadOnlySet<string>? WorldState
)
{
    internal ConditionEvaluationContext ToConditionContext()
        => new(
            Season,
            DayOfMonth,
            Year,
            Time,
            Weather,
            Friendship,
            EventsSeen,
            LocalMail,
            HostMail,
            HostOrLocalMail,
            Dating,
            Spouse,
            Roommate,
            DaysPlayed,
            WorldState);
}

/// <summary>
/// Pure planner: given the current state and an event's parsed conditions, decide whether
/// the event is currently available, what is missing, what is unknown, and what a safe
/// preview would need to override. Never mutates game state.
/// </summary>
internal sealed class PreviewPlanner
{
    // Only these kinds are safely snapshot-able and restorable by the existing replay snapshot.
    // Weather is not captured by the snapshot; relationship/world-state injection is not proven.
    private static readonly HashSet<PreviewOverrideKind> Restorable = new()
    {
        PreviewOverrideKind.Friendship,
        PreviewOverrideKind.EventSeen,
        PreviewOverrideKind.Mail,
        PreviewOverrideKind.Season,
        PreviewOverrideKind.DayOfMonth,
        PreviewOverrideKind.Year,
        PreviewOverrideKind.Time
    };

    private readonly ConditionParser parser;
    private readonly ConditionEvaluator evaluator;

    internal PreviewPlanner(Func<string, string[]> splitPreconditions, Func<string, string[]> splitArguments,
        Func<string, bool>? checkNativeQuery)
    {
        parser = new ConditionParser(splitPreconditions, splitArguments);
        evaluator = new ConditionEvaluator(checkNativeQuery);
    }

    internal EventConditionStatus Analyze(GalleryEvent entry, CurrentStateSnapshot state)
    {
        ConditionExpression[] conditions = parser.ParseRawKey(entry.EventKey).Conditions.ToArray();
        List<string> missing = [];
        List<string> unknown = [];
        List<string> readable = [];

        foreach (ConditionExpression condition in conditions)
        {
            ConditionEvaluation evaluation = evaluator.Evaluate(condition, state.ToConditionContext());
            string summary = Describe(ConditionDescriber.Describe(condition));
            readable.Add(summary);
            if (evaluation.Knowledge == ConditionKnowledge.Known)
            {
                if (evaluation.Truth == ConditionTruth.True)
                    continue;
                missing.Add(summary);
            }
            else
            {
                unknown.Add(summary);
            }
        }

        PreviewCapability capability = ComputeCapability(conditions, state);
        return new EventConditionStatus(
            IsCurrentlyAvailable: missing.Count == 0 && unknown.Count == 0,
            RequiredCount: conditions.Length,
            MissingCount: missing.Count,
            UnknownCount: unknown.Count,
            Capability: capability,
            MissingSummaries: missing,
            UnknownSummaries: unknown,
            ReadableRequired: readable);
    }

    internal PreviewPlan Plan(GalleryEvent entry, CurrentStateSnapshot state)
    {
        ConditionExpression[] conditions = parser.ParseRawKey(entry.EventKey).Conditions.ToArray();
        PreviewCapability capability = ComputeCapability(conditions, state);
        PreviewState suggestion = BuildSuggestion(conditions);
        List<string> unsupported = [];
        List<PreviewWarning> warnings = [];
        foreach (ConditionExpression condition in conditions)
        {
            ConditionEvaluation evaluation = evaluator.Evaluate(condition, state.ToConditionContext());
            string summary = Describe(ConditionDescriber.Describe(condition));
            if (evaluation.Knowledge == ConditionKnowledge.Known && evaluation.Truth == ConditionTruth.True)
                continue;
            if (evaluation.Knowledge != ConditionKnowledge.Known)
            {
                unsupported.Add(summary);
                warnings.Add(new PreviewWarning("preview.warning.unknown", EmptyArguments(), summary));
            }
            else if (!TryBuildOverride(condition, out _))
            {
                unsupported.Add(summary);
                warnings.Add(new PreviewWarning("preview.warning.unsupported", EmptyArguments(), summary));
            }
        }

        EventPlayback playback = EventPlayback.ForCurrent(entry.Resolved);
        return new PreviewPlan(
            entry.Resolved.Identity,
            playback,
            capability,
            suggestion,
            CollectOverrides(suggestion),
            unsupported,
            warnings);
    }

    private PreviewCapability ComputeCapability(IReadOnlyList<ConditionExpression> conditions, CurrentStateSnapshot state)
    {
        bool anyUnknown = false;
        bool anyMissing = false;
        bool anyUnrestorableMissing = false;
        foreach (ConditionExpression condition in conditions)
        {
            ConditionEvaluation evaluation = evaluator.Evaluate(condition, state.ToConditionContext());
            if (evaluation.Knowledge != ConditionKnowledge.Known)
            {
                anyUnknown = true;
                continue;
            }
            if (evaluation.Truth != ConditionTruth.False)
                continue;
            anyMissing = true;
            if (!TryBuildOverride(condition, out PreviewOverride? overridden)
                || overridden is null || !Restorable.Contains(overridden.Kind))
                anyUnrestorableMissing = true;
        }
        if (!anyMissing && !anyUnknown)
            return PreviewCapability.DirectReplay;
        if (anyUnknown)
            return anyMissing ? PreviewCapability.PreviewPartiallySupported : PreviewCapability.AnalysisOnly;
        return anyUnrestorableMissing
            ? PreviewCapability.PreviewPartiallySupported
            : PreviewCapability.PreviewSupported;
    }

    private static PreviewState BuildSuggestion(IReadOnlyList<ConditionExpression> conditions)
    {
        string? season = null;
        int? day = null;
        int? year = null;
        int? time = null;
        Dictionary<string, int>? friendship = null;
        HashSet<string>? seen = null;
        HashSet<string>? mail = null;
        foreach (ConditionExpression condition in conditions)
        {
            switch (condition)
            {
                case FriendshipCondition leaf:
                    friendship ??= new Dictionary<string, int>(StringComparer.Ordinal);
                    friendship[leaf.Npc] = leaf.Points;
                    break;
                case SawEventCondition leaf:
                    seen ??= new HashSet<string>(StringComparer.Ordinal);
                    seen.Add(leaf.EventId);
                    break;
                case MailCondition leaf:
                    mail ??= new HashSet<string>(StringComparer.Ordinal);
                    mail.Add(leaf.MailId);
                    break;
                case SeasonCondition leaf when leaf.Seasons.Count > 0:
                    season = leaf.Seasons[0];
                    break;
                case DayOfMonthCondition leaf when leaf.Days.Count > 0:
                    day = leaf.Days[0];
                    break;
                case YearCondition { Negated: false } leaf:
                    year = leaf.Min;
                    break;
                case TimeCondition { Negated: false } leaf:
                    time = leaf.Min ?? 600;
                    break;
            }
        }
        return new PreviewState(
            Season: season,
            DayOfMonth: day,
            Year: year,
            Time: time,
            Friendship: friendship is null ? null : friendship,
            EventsSeen: seen is null ? null : seen,
            Mail: mail is null ? null : mail);
    }

    private static bool TryBuildOverride(ConditionExpression condition, out PreviewOverride? overridden)
    {
        overridden = null;
        if (condition.Negated)
            return false;
        switch (condition)
        {
            case FriendshipCondition leaf:
                overridden = new PreviewOverride(PreviewOverrideKind.Friendship, leaf.Npc, leaf.Points, leaf.Npc);
                return true;
            case SawEventCondition leaf:
                overridden = new PreviewOverride(PreviewOverrideKind.EventSeen, leaf.EventId, null, leaf.EventId);
                return true;
            case MailCondition leaf:
                overridden = new PreviewOverride(PreviewOverrideKind.Mail, leaf.MailId, null, leaf.MailId);
                return true;
            case SeasonCondition leaf when leaf.Seasons.Count > 0:
                overridden = new PreviewOverride(PreviewOverrideKind.Season, null, null, leaf.Seasons[0]);
                return true;
            case DayOfMonthCondition leaf when leaf.Days.Count > 0:
                overridden = new PreviewOverride(PreviewOverrideKind.DayOfMonth, null, leaf.Days[0], null);
                return true;
            case YearCondition leaf:
                overridden = new PreviewOverride(PreviewOverrideKind.Year, null, leaf.Min, null);
                return true;
            case TimeCondition leaf:
                overridden = new PreviewOverride(PreviewOverrideKind.Time, null, leaf.Min ?? 600, null);
                return true;
            default:
                // Weather/relationship/world-state are analyze-only and not restorable.
                return false;
        }
    }

    private static IReadOnlyList<PreviewOverride> CollectOverrides(PreviewState suggestion)
    {
        List<PreviewOverride> overrides = [];
        if (suggestion.Season is not null)
            overrides.Add(new PreviewOverride(PreviewOverrideKind.Season, null, null, suggestion.Season));
        if (suggestion.DayOfMonth is not null)
            overrides.Add(new PreviewOverride(PreviewOverrideKind.DayOfMonth, null, suggestion.DayOfMonth, null));
        if (suggestion.Year is not null)
            overrides.Add(new PreviewOverride(PreviewOverrideKind.Year, null, suggestion.Year, null));
        if (suggestion.Time is not null)
            overrides.Add(new PreviewOverride(PreviewOverrideKind.Time, null, suggestion.Time, null));
        if (suggestion.Friendship is not null)
            foreach ((string npc, int points) in suggestion.Friendship)
                overrides.Add(new PreviewOverride(PreviewOverrideKind.Friendship, npc, points, npc));
        if (suggestion.EventsSeen is not null)
            foreach (string id in suggestion.EventsSeen)
                overrides.Add(new PreviewOverride(PreviewOverrideKind.EventSeen, id, null, id));
        if (suggestion.Mail is not null)
            foreach (string id in suggestion.Mail)
                overrides.Add(new PreviewOverride(PreviewOverrideKind.Mail, id, null, id));
        return overrides;
    }

    private static string Describe(ReadableCondition condition)
        => condition.LocalizationKey is null ? condition.RawFallback ?? "" : condition.LocalizationKey;

    private static IReadOnlyDictionary<string, string> EmptyArguments()
        => new Dictionary<string, string>();
}
