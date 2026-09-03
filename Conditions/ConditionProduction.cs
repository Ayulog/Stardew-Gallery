namespace StardewGallery;

internal static class ConditionProduction
{
    internal static ConditionParser CreateParser(Func<string, string[]> splitPreconditions)
        => new(splitPreconditions);

    internal static ConditionEvaluator CreateEvaluator(Func<string, bool>? checkNativeQuery)
        => new(checkNativeQuery);
}
