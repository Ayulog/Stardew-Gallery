namespace StardewGallery;

internal sealed class ConditionEvaluator(Func<string, bool>? checkNativeQuery = null)
{
    private static readonly ConditionGap NoGap = new(ConditionGapKind.None);
    private static readonly ConditionGap FlatUnavailable = new(ConditionGapKind.Unavailable);

    internal ConditionEvaluation Evaluate(
        ConditionExpression condition,
        ConditionEvaluationContext context)
    {
        ConditionEvaluation baseResult = condition switch
        {
            SeasonCondition leaf => EvaluateSeason(leaf, context),
            DayOfMonthCondition leaf => EvaluateDayOfMonth(leaf, context),
            YearCondition leaf => EvaluateYear(leaf, context),
            TimeCondition leaf => EvaluateTime(leaf, context),
            WeatherCondition leaf => EvaluateWeather(leaf, context),
            FriendshipCondition leaf => EvaluateFriendship(leaf, context),
            SawEventCondition leaf => EvaluateSawEvent(leaf, context),
            MailCondition leaf => EvaluateMail(leaf, context),
            DatingCondition leaf => EvaluateDating(leaf, context),
            SpouseCondition leaf => EvaluateSpouse(leaf, context),
            RoommateCondition leaf => EvaluateRoommate(leaf, context),
            DaysPlayedCondition leaf => EvaluateDaysPlayed(leaf, context),
            WorldStateCondition leaf => EvaluateWorldState(leaf, context),
            NativeQueryCondition leaf => EvaluateNativeQuery(leaf),
            OpaqueCondition leaf => new ConditionEvaluation(condition, ConditionTruth.Unknown,
                leaf.Kind == OpaqueConditionKind.UnknownType ? ConditionKnowledge.Unsupported : ConditionKnowledge.Invalid, FlatUnavailable),
            LegacySendMailCondition => new ConditionEvaluation(condition, ConditionTruth.Unknown, ConditionKnowledge.Unsupported, FlatUnavailable),
            ConditionSet => new ConditionEvaluation(condition, ConditionTruth.Unknown, ConditionKnowledge.Invalid, FlatUnavailable),
            _ => new ConditionEvaluation(condition, ConditionTruth.Unknown, ConditionKnowledge.Unsupported, FlatUnavailable)
        };
        if (condition.Negated && baseResult.Knowledge == ConditionKnowledge.Known)
        {
            ConditionTruth flipped = baseResult.Truth == ConditionTruth.True ? ConditionTruth.False : ConditionTruth.True;
            ConditionGap gap;
            if (flipped == ConditionTruth.False)
                gap = new ConditionGap(ConditionGapKind.OverState,
                    Target: baseResult.Gap.Target,
                    Current: baseResult.Gap.Current,
                    Detail: leafOverReason(baseResult.Condition));
            else
                gap = NoGap;
            return baseResult with { Truth = flipped, Gap = gap };
        }
        return baseResult;
    }

    private static string? leafOverReason(ConditionExpression leaf)
        => leaf switch
        {
            SawEventCondition c => "already-seen:" + string.Join(',', c.EventIds),
            MailCondition c => "already-mail:" + c.MailId,
            FriendshipCondition c => "friendship-at-or-above:" + string.Join(',', c.Requirements.Select(value => value.Npc)),
            TimeCondition c => "time-inside-range",
            _ => null
        };

    private ConditionEvaluation EvaluateSeason(SeasonCondition leaf, ConditionEvaluationContext context)
    {
        if (context.Season is null)
            return Unknown(leaf, ConditionKnowledge.MissingData);
        bool matches = leaf.Seasons.Contains(context.Season, StringComparer.OrdinalIgnoreCase);
        return matches
            ? Known(leaf, ConditionTruth.True, NoGap)
            : Known(leaf, ConditionTruth.False,
                new ConditionGap(ConditionGapKind.MissingState, Target: string.Join(' ', leaf.Seasons), Current: context.Season));
    }

    private ConditionEvaluation EvaluateDayOfMonth(DayOfMonthCondition leaf, ConditionEvaluationContext context)
    {
        if (context.DayOfMonth is null)
            return Unknown(leaf, ConditionKnowledge.MissingData);
        bool matches = leaf.Days.Contains(context.DayOfMonth.Value);
        return matches
            ? Known(leaf, ConditionTruth.True, NoGap)
            : Known(leaf, ConditionTruth.False,
                new ConditionGap(ConditionGapKind.MissingState, Target: string.Join(',', leaf.Days), Current: context.DayOfMonth.Value.ToString()));
    }

    private ConditionEvaluation EvaluateYear(YearCondition leaf, ConditionEvaluationContext context)
    {
        if (context.Year is null)
            return Unknown(leaf, ConditionKnowledge.MissingData);
        bool matches = leaf.DesiredYear == 1
            ? context.Year.Value == 1
            : context.Year.Value >= leaf.DesiredYear;
        return matches
            ? Known(leaf, ConditionTruth.True, NoGap)
            : Known(leaf, ConditionTruth.False,
                new ConditionGap(ConditionGapKind.NumericGap, Target: leaf.DesiredYear.ToString(), Current: context.Year.Value.ToString()));
    }

    private ConditionEvaluation EvaluateTime(TimeCondition leaf, ConditionEvaluationContext context)
    {
        if (context.Time is null)
            return Unknown(leaf, ConditionKnowledge.MissingData);
        bool matches = context.Time.Value >= leaf.Min && context.Time.Value <= leaf.Max;
        return matches
            ? Known(leaf, ConditionTruth.True, NoGap)
            : Known(leaf, ConditionTruth.False,
                new ConditionGap(ConditionGapKind.RequiredRange,
                    Target: $"{leaf.Min}..{leaf.Max}", Current: context.Time.Value.ToString()));
    }

    private ConditionEvaluation EvaluateWeather(WeatherCondition leaf, ConditionEvaluationContext context)
    {
        if (leaf.Kind is WeatherKind.Rainy or WeatherKind.Sunny && context.IsRaining is null)
            return Unknown(leaf, ConditionKnowledge.MissingData);
        if (leaf.Kind == WeatherKind.Custom && context.Weather is null)
            return Unknown(leaf, ConditionKnowledge.MissingData);
        bool matches = leaf.Kind switch
        {
            WeatherKind.Rainy => context.IsRaining!.Value,
            WeatherKind.Sunny => !context.IsRaining!.Value,
            _ => context.Weather!.Equals(leaf.WeatherId, StringComparison.Ordinal)
        };
        return matches
            ? Known(leaf, ConditionTruth.True, NoGap)
            : Known(leaf, ConditionTruth.False,
                new ConditionGap(ConditionGapKind.MissingState, Target: leaf.WeatherId, Current: context.Weather));
    }

    private ConditionEvaluation EvaluateFriendship(FriendshipCondition leaf, ConditionEvaluationContext context)
    {
        if (context.Friendship is null)
            return Unknown(leaf, ConditionKnowledge.MissingData);
        FriendshipRequirement? missing = leaf.Requirements.FirstOrDefault(requirement => !context.Friendship.TryGetValue(requirement.Npc, out int points) || points < requirement.Points);
        bool matches = missing is null;
        return matches
            ? Known(leaf, ConditionTruth.True, NoGap)
            : Known(leaf, ConditionTruth.False,
                new ConditionGap(ConditionGapKind.NumericGap, Target: missing!.Points.ToString(), Current: context.Friendship.GetValueOrDefault(missing.Npc).ToString(), Detail: missing.Npc));
    }

    private ConditionEvaluation EvaluateSawEvent(SawEventCondition leaf, ConditionEvaluationContext context)
    {
        if (context.EventsSeen is null)
            return Unknown(leaf, ConditionKnowledge.MissingData);
        bool matches = leaf.EventIds.Any(context.EventsSeen.Contains);
        return matches
            ? Known(leaf, ConditionTruth.True, NoGap)
            : Known(leaf, ConditionTruth.False,
                new ConditionGap(ConditionGapKind.MissingState, Target: string.Join(' ', leaf.EventIds)));
    }

    private ConditionEvaluation EvaluateMail(MailCondition leaf, ConditionEvaluationContext context)
    {
        IReadOnlySet<string>? mail = leaf.Scope switch
        {
            ConditionPlayerScope.HostPlayer => context.HostMail,
            ConditionPlayerScope.HostOrLocal => context.HostOrLocalMail,
            _ => context.LocalMail
        };
        if (mail is null)
            return Unknown(leaf, ConditionKnowledge.MissingData);
        bool matches = mail.Contains(leaf.MailId);
        return matches
            ? Known(leaf, ConditionTruth.True, NoGap)
            : Known(leaf, ConditionTruth.False,
                new ConditionGap(ConditionGapKind.MissingState, Target: leaf.MailId));
    }

    private ConditionEvaluation EvaluateDating(DatingCondition leaf, ConditionEvaluationContext context)
    {
        if (context.Dating is null)
            return Unknown(leaf, ConditionKnowledge.MissingData);
        bool matches = context.Dating.Contains(leaf.Npc);
        return matches
            ? Known(leaf, ConditionTruth.True, NoGap)
            : Known(leaf, ConditionTruth.False,
                new ConditionGap(ConditionGapKind.MissingState, Target: leaf.Npc));
    }

    private ConditionEvaluation EvaluateSpouse(SpouseCondition leaf, ConditionEvaluationContext context)
    {
        if (context.Spouse is null)
            return Unknown(leaf, ConditionKnowledge.MissingData);
        bool matches = context.Spouse.Contains(leaf.Npc);
        return matches
            ? Known(leaf, ConditionTruth.True, NoGap)
            : Known(leaf, ConditionTruth.False,
                new ConditionGap(ConditionGapKind.MissingState, Target: leaf.Npc));
    }

    private ConditionEvaluation EvaluateRoommate(RoommateCondition leaf, ConditionEvaluationContext context)
    {
        if (context.Roommate is null)
            return Unknown(leaf, ConditionKnowledge.MissingData);
        bool matches = context.Roommate.Value;
        return matches
            ? Known(leaf, ConditionTruth.True, NoGap)
            : Known(leaf, ConditionTruth.False,
                new ConditionGap(ConditionGapKind.MissingState));
    }

    private ConditionEvaluation EvaluateDaysPlayed(DaysPlayedCondition leaf, ConditionEvaluationContext context)
    {
        if (context.DaysPlayed is null)
            return Unknown(leaf, ConditionKnowledge.MissingData);
        bool matches = context.DaysPlayed.Value > leaf.Threshold;
        return matches
            ? Known(leaf, ConditionTruth.True, NoGap)
            : Known(leaf, ConditionTruth.False,
                new ConditionGap(ConditionGapKind.NumericGap,
                    Target: $">{leaf.Threshold}", Current: context.DaysPlayed.Value.ToString()));
    }

    private ConditionEvaluation EvaluateWorldState(WorldStateCondition leaf, ConditionEvaluationContext context)
    {
        if (context.WorldState is null)
            return Unknown(leaf, ConditionKnowledge.MissingData);
        bool matches = context.WorldState.Contains(leaf.Id);
        return matches
            ? Known(leaf, ConditionTruth.True, NoGap)
            : Known(leaf, ConditionTruth.False,
                new ConditionGap(ConditionGapKind.MissingState, Target: leaf.Id));
    }

    private ConditionEvaluation EvaluateNativeQuery(NativeQueryCondition leaf)
    {
        if (checkNativeQuery is null)
            return Unknown(leaf, ConditionKnowledge.Unsupported);
        try
        {
            bool matches = checkNativeQuery(leaf.Query);
            return matches
                ? Known(leaf, ConditionTruth.True, NoGap)
                : Known(leaf, ConditionTruth.False, FlatUnavailable);
        }
        catch
        {
            return Unknown(leaf, ConditionKnowledge.Error);
        }
    }

    private static ConditionEvaluation Known(ConditionExpression leaf, ConditionTruth truth, ConditionGap gap)
        => new(leaf, truth, ConditionKnowledge.Known, gap);

    private static ConditionEvaluation Unknown(ConditionExpression leaf, ConditionKnowledge knowledge)
        => new(leaf, ConditionTruth.Unknown, knowledge, FlatUnavailable);
}
