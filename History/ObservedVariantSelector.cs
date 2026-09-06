namespace StardewGallery;

internal static class ObservedVariantSelector
{
    internal static bool TrySelect(
        IReadOnlyList<string> candidateRawKeys,
        Func<string, NativePreconditionProbeResult> probePrecondition,
        out int selectedIndex)
    {
        selectedIndex = -1;
        if (candidateRawKeys.Count == 0)
            return false;
        if (candidateRawKeys.Count == 1)
        {
            selectedIndex = 0;
            return true;
        }
        for (int index = 0; index < candidateRawKeys.Count; index++)
        {
            NativePreconditionProbeStatus status = probePrecondition(candidateRawKeys[index]).Status;
            if (status == NativePreconditionProbeStatus.Matched)
            {
                selectedIndex = index;
                return true;
            }
            if (status != NativePreconditionProbeStatus.NotMatched)
                return false;
        }
        return false;
    }
}
