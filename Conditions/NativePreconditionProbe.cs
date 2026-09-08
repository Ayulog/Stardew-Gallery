namespace StardewGallery;

internal enum NativePreconditionProbeStatus
{
    Matched,
    NotMatched,
    NotSafelyEvaluated,
    Error
}

internal sealed record NativePreconditionProbeResult(NativePreconditionProbeStatus Status, string? Detail = null);

internal sealed class NativePreconditionProbe(Func<string, string[]> splitPreconditions, Func<string, string[]> splitArguments)
{
    private readonly ConditionParser parser = new(splitPreconditions, splitArguments);

    internal NativePreconditionProbeResult Check(string rawKey, Func<string, string?> nativeCheck, bool dedicatedServer = false)
    {
        try
        {
            ConditionExpression? unsafeCondition = parser.ParseRawKey(rawKey).Conditions.FirstOrDefault(condition =>
                condition.Source != ConditionSource.LegacyEventPrecondition
                || condition is RandomCondition or LegacySendMailCondition or OpaqueCondition or NativeQueryCondition
                || dedicatedServer && condition is IsHostCondition);
            if (unsafeCondition is not null)
                return new(NativePreconditionProbeStatus.NotSafelyEvaluated, unsafeCondition.RawSegment);

            string? result = nativeCheck(rawKey);
            return new(string.IsNullOrEmpty(result) || result == "-1"
                ? NativePreconditionProbeStatus.NotMatched
                : NativePreconditionProbeStatus.Matched);
        }
        catch (Exception error)
        {
            return new(NativePreconditionProbeStatus.Error, error.Message);
        }
    }
}
