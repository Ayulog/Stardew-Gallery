namespace StardewGallery;

internal sealed class ConditionEvaluator(Func<string, bool>? checkNativeQuery = null)
{
    private static readonly ConditionGap NoGap = new(ConditionGapKind.None);
    private static readonly ConditionGap FlatUnavailable = new(ConditionGapKind.Unavailable);

    internal ConditionEvaluation Evaluate(
        ConditionExpression condition,
        ConditionEvaluationContext context)
    {
        if (context.Details?.Errors?.Contains(condition.RawSegment) == true)
            return Unknown(condition, ConditionKnowledge.Error);
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
            DayOfWeekCondition leaf => EvaluateWeekday(leaf, context.Details),
            IsHostCondition leaf => Boolean(leaf, context.Details?.IsHost),
            EarnedMoneyCondition leaf => Minimum(leaf, leaf.Minimum, context.Details?.EarnedMoney),
            HasMoneyCondition leaf => Minimum(leaf, leaf.Minimum, context.Details?.Money),
            FreeInventorySlotsCondition leaf => Minimum(leaf, leaf.Minimum, context.Details?.FreeSlots),
            GoldenWalnutsCondition leaf => Minimum(leaf, leaf.Minimum, context.Details?.Walnuts),
            ReachedMineBottomCondition leaf => Minimum(leaf, leaf.Minimum, context.Details?.MineBottoms),
            CommunityCenterOrWarehouseDoneCondition leaf => Boolean(leaf, context.Details?.CommunityComplete),
            JojaBundlesDoneCondition leaf => Boolean(leaf, context.Details?.JojaComplete),
            MissingPetCondition leaf => EvaluatePet(leaf, context.Details),
            GenderCondition leaf => Boolean(leaf, context.Details?.Gender is string gender ? leaf.Gender.Equals(gender, StringComparison.OrdinalIgnoreCase) : null),
            SawSecretNoteCondition leaf => Boolean(leaf, context.Details?.SecretNotes?.Contains(leaf.NoteId)),
            ChoseDialogueAnswersCondition leaf => Boolean(leaf, context.Details?.DialogueAnswers is { } answers ? leaf.AnswerIds.All(answers.Contains) : null),
            ActiveDialogueEventCondition leaf => Boolean(leaf, context.Details?.ActiveDialogues?.Contains(leaf.Id)),
            ShippedCondition leaf => EvaluateShipped(leaf, context.Details),
            HasItemCondition leaf => Boolean(leaf, context.Details?.Items?.TryGetValue(leaf.ItemId, out bool hasItem) == true ? hasItem : null),
            SkillCondition leaf => EvaluateSkill(leaf, context.Details),
            NpcVisibleCondition leaf => Boolean(leaf, leaf.CurrentLocationOnly ? context.Details?.NpcsAtLocation?.Contains(leaf.Npc)
                : context.Details?.VisibleNpcs?.TryGetValue(leaf.Npc, out bool visible) == true ? visible : null),
            InUpgradedHouseCondition leaf => EvaluateHouse(leaf, context.Details),
            SpouseBedCondition leaf => Boolean(leaf, context.Details?.SpouseBed),
            FestivalDayCondition leaf => EvaluateFestival(leaf, 1, context),
            UpcomingFestivalCondition leaf => EvaluateFestival(leaf, leaf.Days, context),
            TileCondition leaf => Boolean(leaf, context.Details?.EntryTile is { } tile ? leaf.Positions.Contains(tile) : null),
            NativeQueryCondition leaf => EvaluateNativeQuery(leaf, context),
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

    private ConditionEvaluation EvaluateNativeQuery(NativeQueryCondition leaf, ConditionEvaluationContext context)
    {
        if (SafeGameQuery.TryParse(leaf.Query, out var query))
            return Boolean(leaf, SafeGameQuery.Evaluate(query, context));
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

    private static ConditionEvaluation Boolean(ConditionExpression leaf, bool? matches)
        => matches is null ? Unknown(leaf, ConditionKnowledge.MissingData)
            : Known(leaf, matches.Value ? ConditionTruth.True : ConditionTruth.False,
                matches.Value ? NoGap : new(ConditionGapKind.MissingState));

    private static ConditionEvaluation Minimum(ConditionExpression leaf, long target, long? current)
        => current is null ? Unknown(leaf, ConditionKnowledge.MissingData)
            : current >= target ? Known(leaf, ConditionTruth.True, NoGap)
            : Known(leaf, ConditionTruth.False, new(ConditionGapKind.NumericGap,
                Target: target.ToString(), Current: current.Value.ToString()));

    private static ConditionEvaluation EvaluateWeekday(DayOfWeekCondition leaf, ConditionReadState? state)
        => state?.Weekday is not { } day ? Unknown(leaf, ConditionKnowledge.MissingData)
            : Boolean(leaf, leaf.Days.Contains(day));

    private static ConditionEvaluation EvaluatePet(MissingPetCondition leaf, ConditionReadState? state)
        => state?.HasPet is null ? Unknown(leaf, ConditionKnowledge.MissingData)
            : state.HasPet.Value ? Boolean(leaf, false)
            : leaf.PetType is null ? Boolean(leaf, true)
            : Boolean(leaf, state.PetPreference is string type ? leaf.PetType.Equals(type, StringComparison.OrdinalIgnoreCase) : null);

    private static ConditionEvaluation EvaluateShipped(ShippedCondition leaf, ConditionReadState? state)
    {
        if (state?.Shipped is null)
            return Unknown(leaf, ConditionKnowledge.MissingData);
        // Missing keys fail even a zero requirement, matching the game's shipment lookup.
        return Boolean(leaf, leaf.Requirements.All(item =>
            state.Shipped.TryGetValue(item.ItemId, out int count) && count >= item.Count));
    }

    private static ConditionEvaluation EvaluateSkill(SkillCondition leaf, ConditionReadState? state)
    {
        if (state?.Skills?.TryGetValue(leaf.Skill, out int? level) != true)
            return Unknown(leaf, ConditionKnowledge.MissingData);
        return level is null ? Unknown(leaf, ConditionKnowledge.Unsupported) : Minimum(leaf, leaf.MinimumLevel, level);
    }

    private static ConditionEvaluation EvaluateHouse(InUpgradedHouseCondition leaf, ConditionReadState? state)
        => state?.IsFarmHouse is null ? Unknown(leaf, ConditionKnowledge.MissingData)
            : !state.IsFarmHouse.Value ? Boolean(leaf, false) : Minimum(leaf, leaf.MinimumLevel, state.HouseUpgrade);

    private static ConditionEvaluation EvaluateFestival(ConditionExpression leaf, int days, ConditionEvaluationContext context)
    {
        if (days <= 0)
            return Boolean(leaf, false);
        string[] seasons = ["spring", "summer", "fall", "winter"];
        int season = Array.FindIndex(seasons, value => value.Equals(context.Season, StringComparison.OrdinalIgnoreCase));
        if (context.Details?.FestivalDates is not { } dates || season < 0 || context.DayOfMonth is not int day)
            return Unknown(leaf, ConditionKnowledge.MissingData);
        int originalSeason = season;
        // In 1.6.15 the native window repeats the next season after its first rollover.
        // After 56 checks no unseen date remains, even for an unbounded modded window.
        for (int offset = 0; offset < Math.Min(days, 56); offset++)
        {
            if (dates.Contains(seasons[season] + day))
                return Boolean(leaf, true);
            if (++day > 28) { day = 1; season = (originalSeason + 1) % 4; }
        }
        return Boolean(leaf, false);
    }

    private static ConditionEvaluation Known(ConditionExpression leaf, ConditionTruth truth, ConditionGap gap)
        => new(leaf, truth, ConditionKnowledge.Known, gap);

    private static ConditionEvaluation Unknown(ConditionExpression leaf, ConditionKnowledge knowledge)
        => new(leaf, ConditionTruth.Unknown, knowledge, FlatUnavailable);
}
