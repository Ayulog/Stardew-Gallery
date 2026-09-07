namespace StardewGallery;

internal sealed record ConditionDisplayItem(
    ConditionExpression Expression,
    ConditionEvaluation Evaluation,
    string Description,
    string CompactText,
    string? CurrentValue,
    string? RequiredValue,
    string? GapSubject);

internal enum ConditionStatusIcon
{
    Check,
    Cross,
    Unknown
}

internal static class ConditionRowPresentation
{
    internal static ConditionStatusIcon Status(ConditionEvaluation evaluation)
        => evaluation.Knowledge != ConditionKnowledge.Known || evaluation.Truth == ConditionTruth.Unknown
            ? ConditionStatusIcon.Unknown
            : evaluation.Truth == ConditionTruth.True ? ConditionStatusIcon.Check : ConditionStatusIcon.Cross;

    internal static string Text(ConditionDisplayItem item, Func<string, IReadOnlyDictionary<string, string>, string> translate)
        => item.CurrentValue is null
            ? item.Description
            : translate("event-detail.current-inline", new Dictionary<string, string>
            {
                ["condition"] = item.Description,
                ["current"] = item.CurrentValue
            });

    internal static string? UnknownReasonKey(ConditionDisplayItem item)
        => Status(item.Evaluation) != ConditionStatusIcon.Unknown ? null : item.Evaluation.Knowledge switch
        {
            ConditionKnowledge.MissingData => "event-detail.unknown.missing-data",
            ConditionKnowledge.Invalid => "event-detail.unknown.invalid",
            ConditionKnowledge.Error => "event-detail.unknown.error",
            _ => "event-detail.unknown.unsupported"
        };
}

internal sealed class ConditionPresentationBuilder(
    ConditionParser parser,
    ConditionEvaluator evaluator,
    Func<string, IReadOnlyDictionary<string, string>, string> translate,
    ConditionDisplayResolver resolver)
{
    internal IReadOnlyList<ConditionDisplayItem> Build(string rawEventKey, CurrentStateSnapshot state)
    {
        List<ConditionDisplayItem> items = [];
        foreach (ConditionExpression expression in parser.ParseRawKey(rawEventKey).Conditions)
        {
            string description = ConditionTextFormatter.Format(ConditionDescriber.Describe(expression), translate, resolver);
            ConditionEvaluation evaluation = evaluator.Evaluate(expression, state.ToConditionContext());
            string? current = CurrentValue(expression, evaluation, state);
            string? required = evaluation.Gap.Target is null ? null : ConditionTextFormatter.FormatGap(expression, evaluation.Gap.Target, translate, resolver);
            string compactDescription = current is not null && required is not null
                ? description + translate("condition.gap", new Dictionary<string, string> { ["current"] = current, ["target"] = required })
                : description;
            string statusKey = evaluation.Knowledge != ConditionKnowledge.Known || evaluation.Truth == ConditionTruth.Unknown
                ? "condition.status-unknown"
                : evaluation.Truth == ConditionTruth.True ? "condition.status-met" : "condition.status-missing";
            items.Add(new ConditionDisplayItem(
                expression,
                evaluation,
                description,
                translate(statusKey, new Dictionary<string, string> { ["condition"] = compactDescription }),
                current,
                required,
                expression is FriendshipCondition && evaluation.Gap.Kind == ConditionGapKind.NumericGap && evaluation.Gap.Detail is string npc
                    ? resolver.Npc(npc)
                    : null));
        }
        return items;
    }

    private string? CurrentValue(ConditionExpression expression, ConditionEvaluation evaluation, CurrentStateSnapshot state)
    {
        if (evaluation.Gap.Current is string gapCurrent)
            return ConditionTextFormatter.FormatGap(expression, gapCurrent, translate, resolver);
        return expression switch
        {
            SeasonCondition when state.Season is not null => resolver.Term("season", state.Season),
            TimeCondition when state.Time is int time => resolver.Time(time),
            FriendshipCondition { Requirements.Count: 1 } friendship
                when state.Friendship?.TryGetValue(friendship.Requirements[0].Npc, out int points) == true
                => ConditionTextFormatter.FormatGap(expression, points.ToString(), translate, resolver),
            WeatherCondition { Kind: WeatherKind.Rainy or WeatherKind.Sunny } when state.IsRaining is bool raining
                => translate(raining ? "condition.raining" : "condition.not-raining", EmptyArguments()),
            WeatherCondition { Kind: WeatherKind.Custom } when state.Weather is not null => state.Weather,
            _ => null
        };
    }

    internal string Compact(IReadOnlyList<ConditionDisplayItem> items)
    {
        if (items.Count == 0)
            return translate("condition.none", EmptyArguments());
        return string.Join(
            translate("condition.separator", EmptyArguments()),
            items.Select(item => item.CompactText).Distinct(StringComparer.CurrentCulture));
    }

    private static IReadOnlyDictionary<string, string> EmptyArguments()
        => new Dictionary<string, string>();
}
