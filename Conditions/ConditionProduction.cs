namespace StardewGallery;

internal static class ConditionProduction
{
    internal static ConditionParser CreateParser(
        Func<string, string[]> splitPreconditions,
        Func<string, string[]> splitArguments)
        => new(splitPreconditions, splitArguments);

    internal static ConditionEvaluator CreateEvaluator(Func<string, bool>? checkNativeQuery)
        => new(checkNativeQuery);
}
