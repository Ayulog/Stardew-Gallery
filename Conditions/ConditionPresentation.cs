namespace StardewGallery;

internal sealed record ConditionDisplayItem(
    ConditionExpression Expression,
    ConditionEvaluation Evaluation,
    string Description,
    string CompactText,
    string? CurrentValue,
    string? RequiredValue,
    string? GapSubject);

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
            string? current = evaluation.Gap.Current is null ? null : ConditionTextFormatter.FormatGap(expression, evaluation.Gap.Current, translate, resolver);
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
