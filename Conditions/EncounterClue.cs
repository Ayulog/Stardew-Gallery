namespace StardewGallery;

internal enum EncounterClue
{
    None,
    ReadyForTiming,
    AvailableToday
}

/// <summary>
/// Pure read-only two-tier "Encounter Clue" classifier for unseen stories that are not already opened If-routes.
/// Never invokes external GSQ delegates or trigger actions; any unknown condition or unmet long-term progression
/// gate yields <see cref="EncounterClue.None"/>.
/// </summary>
internal static class EncounterClueClassifier
{
    private const int DayStartTime = 600;
    private const int DayEndTime = 2600;

    private enum GateKind
    {
        Unknown,
        LongTerm,
        DailyTiming,
        TransientOnSite
    }

    internal static EncounterClue Classify(
        GalleryEvent entry,
        ConditionSet conditions,
        CurrentStateSnapshot state,
        ConditionEvaluator evaluator,
        bool parallelBranchUnlocked = false,
        bool locationAvailable = true)
    {
        if (entry.Kind == StoryKind.Internal
            || parallelBranchUnlocked
            || !locationAvailable
            || state.EventsSeen?.Contains(entry.EventId) == true)
            return EncounterClue.None;

        ConditionEvaluationContext context = state.ToConditionContext();
        bool hasUnmetDailyTiming = false;

        foreach (ConditionExpression expression in conditions.Conditions)
        {
            if (expression is NativeQueryCondition query)
            {
                EncounterClue queryOutcome = ClassifyQuery(query, context);
                if (queryOutcome == EncounterClue.None)
                    return EncounterClue.None;
                if (queryOutcome == EncounterClue.ReadyForTiming)
                    hasUnmetDailyTiming = true;
                continue;
            }

            ConditionEvaluation evaluation = evaluator.Evaluate(expression, context);

            // Tile preconditions report MissingData whenever the player is currently outside the event location;
            // going to that location's entry tile is an on-site transient step, not an unknown condition.
            if (expression is TileCondition
                && evaluation.Knowledge is ConditionKnowledge.Known or ConditionKnowledge.MissingData)
                continue;

            if (evaluation.Knowledge != ConditionKnowledge.Known || evaluation.Truth == ConditionTruth.Unknown)
                return EncounterClue.None;

            if (evaluation.Truth == ConditionTruth.True)
                continue;

            switch (ClassifyExpressionKind(expression, context))
            {
                case GateKind.TransientOnSite:
                    break;
                case GateKind.DailyTiming:
                    hasUnmetDailyTiming = true;
                    break;
                default:
                    return EncounterClue.None;
            }
        }

        return hasUnmetDailyTiming ? EncounterClue.ReadyForTiming : EncounterClue.AvailableToday;
    }

    private static EncounterClue ClassifyQuery(NativeQueryCondition query, ConditionEvaluationContext context)
    {
        if (context.Details?.Errors?.Contains(query.RawSegment) == true
            || !SafeGameQuery.TryParse(query.Query, out IReadOnlyList<SafeQueryClause> clauses))
            return EncounterClue.None;

        return ClassifyConjunction(clauses, query.Negated, context);
    }

    private static EncounterClue ClassifyConjunction(IReadOnlyList<SafeQueryClause> clauses, bool outerNegated, ConditionEvaluationContext context)
    {
        if (!outerNegated)
        {
            bool hasUnmetDaily = false;
            foreach (SafeQueryClause clause in clauses)
            {
                bool? raw = SafeGameQuery.EvaluateClause(clause, context);
                if (raw is null)
                    return EncounterClue.None;
                bool matched = clause.Negated ? !raw.Value : raw.Value;
                if (matched)
                    continue;
                switch (ClassifyClauseKind(clause, clause.Negated, context))
                {
                    case GateKind.TransientOnSite:
                        break;
                    case GateKind.DailyTiming:
                        hasUnmetDaily = true;
                        break;
                    default:
                        return EncounterClue.None;
                }
            }
            return hasUnmetDaily ? EncounterClue.ReadyForTiming : EncounterClue.AvailableToday;
        }

        // Outer negation on a single clause is identical to negating that clause.
        if (clauses.Count == 1)
        {
            SafeQueryClause single = clauses[0];
            bool? raw = SafeGameQuery.EvaluateClause(single, context);
            if (raw is null)
                return EncounterClue.None;
            bool negated = !single.Negated;
            bool matched = negated ? !raw.Value : raw.Value;
            if (matched)
                return EncounterClue.AvailableToday;
            return ClassifyClauseKind(single, negated, context) switch
            {
                GateKind.TransientOnSite => EncounterClue.AvailableToday,
                GateKind.DailyTiming => EncounterClue.ReadyForTiming,
                _ => EncounterClue.None
            };
        }

        // Outer negation on multiple clauses: !(c1 && c2 && ...).
        bool? conjunct = SafeGameQuery.Evaluate(clauses, context);
        if (conjunct is null)
            return EncounterClue.None;
        if (!conjunct.Value)
            return EncounterClue.AvailableToday;

        // Every clause in the conjunction is currently true, making the negated query false.
        // Satisfying !(c1 && c2 && ...) requires flipping at least one clause to false.
        GateKind[] kinds = clauses.Select(clause => ClassifyClauseKind(clause, !clause.Negated, context)).ToArray();
        if (kinds.Contains(GateKind.TransientOnSite))
            return EncounterClue.AvailableToday;
        if (kinds.Contains(GateKind.DailyTiming))
            return EncounterClue.ReadyForTiming;
        return EncounterClue.None;
    }

    private static GateKind ClassifyExpressionKind(ConditionExpression expression, ConditionEvaluationContext context) => expression switch
    {
        TimeCondition time => ClassifyUnmetTimeWindow(time.Min, time.Max, time.Negated, context.Time),
        NpcVisibleCondition { CurrentLocationOnly: true } or TileCondition => GateKind.TransientOnSite,
        // 'v <Npc>' checks world-wide existence & !IsInvisible (e.g. Kent in Year 1); cannot be satisfied merely by walking to a map today.
        NpcVisibleCondition { CurrentLocationOnly: false } => GateKind.LongTerm,
        SeasonCondition or DayOfMonthCondition or DayOfWeekCondition or WeatherCondition
            or FestivalDayCondition or UpcomingFestivalCondition => GateKind.DailyTiming,
        OpaqueCondition or RandomCondition or LegacySendMailCondition or ConditionSet => GateKind.Unknown,
        _ => GateKind.LongTerm
    };

    private static GateKind ClassifyClauseKind(SafeQueryClause clause, bool negated, ConditionEvaluationContext context) => clause.Name switch
    {
        "ANY" when clause.SubQueries is { } subQueries => ClassifyAnyKind(subQueries, negated, context),
        "TRUE" => negated ? GateKind.LongTerm : GateKind.TransientOnSite,
        "FALSE" => negated ? GateKind.TransientOnSite : GateKind.LongTerm,
        "TIME" => ClassifyUnmetTimeWindow(
            SafeGameQuery.ParseInt(clause.Arguments[0]),
            SafeGameQuery.OptionalMax(clause.Arguments, 1),
            negated,
            context.Time),
        "SEASON" or "LOCATION_SEASON" or "SEASON_DAY" or "DAY_OF_MONTH" or "DAY_OF_WEEK"
            or "WEATHER" or "IS_GREEN_RAIN_DAY" or "IS_FESTIVAL_DAY" or "IS_PASSIVE_FESTIVAL_TODAY"
            or "IS_VISITING_ISLAND" => GateKind.DailyTiming,
        _ => GateKind.LongTerm
    };

    private static GateKind ClassifyAnyKind(IReadOnlyList<SafeQueryClause>[] subQueries, bool negated, ConditionEvaluationContext context)
    {
        if (!negated)
        {
            bool anyDaily = false;
            foreach (IReadOnlyList<SafeQueryClause> sub in subQueries)
            {
                EncounterClue subClue = ClassifyConjunction(sub, outerNegated: false, context);
                if (subClue == EncounterClue.AvailableToday)
                    return GateKind.TransientOnSite;
                if (subClue == EncounterClue.ReadyForTiming)
                    anyDaily = true;
            }
            return anyDaily ? GateKind.DailyTiming : GateKind.LongTerm;
        }

        // !ANY is unmet -> at least one branch is currently true, and ALL currently-true branches must flip to false.
        bool needsDaily = false;
        foreach (IReadOnlyList<SafeQueryClause> sub in subQueries)
        {
            bool? subValue = SafeGameQuery.Evaluate(sub, context);
            if (subValue is null)
                return GateKind.Unknown;
            if (subValue == false)
                continue;
            EncounterClue flipClue = ClassifyConjunction(sub, outerNegated: true, context);
            if (flipClue == EncounterClue.None)
                return GateKind.LongTerm;
            if (flipClue == EncounterClue.ReadyForTiming)
                needsDaily = true;
        }
        return needsDaily ? GateKind.DailyTiming : GateKind.TransientOnSite;
    }

    private static GateKind ClassifyUnmetTimeWindow(int min, int max, bool negated, int? currentTime)
    {
        if (currentTime is not int now)
            return GateKind.Unknown;

        if (!negated)
        {
            if (min > max || max < DayStartTime || min > DayEndTime)
                return GateKind.LongTerm;
            // Time advances monotonically 600..2600 within a day: if now > max, today's window has already passed.
            return now < min ? GateKind.TransientOnSite : GateKind.DailyTiming;
        }

        if (min <= DayStartTime && max >= DayEndTime)
            return GateKind.LongTerm;

        // Currently min <= now <= max; if max < 2600, waiting past max satisfies !(min..max) today, else must wait for another day.
        return max < DayEndTime ? GateKind.TransientOnSite : GateKind.DailyTiming;
    }
}
