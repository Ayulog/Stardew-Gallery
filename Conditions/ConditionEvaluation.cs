namespace StardewGallery;

internal sealed record ConditionEvaluation(
    ConditionExpression Condition,
    ConditionTruth Truth,
    ConditionKnowledge Knowledge,
    ConditionGap Gap
);
