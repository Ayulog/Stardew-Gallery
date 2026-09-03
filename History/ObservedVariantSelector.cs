namespace StardewGallery;

internal static class ObservedVariantSelector
{
    internal static bool TrySelect(
        IReadOnlyList<string> candidateRawKeys,
        Func<string, string?> checkPrecondition,
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
            string? result;
            try
            {
                result = checkPrecondition(candidateRawKeys[index]);
            }
            catch
            {
                continue;
            }
            if (IsCurrentState(result))
            {
                selectedIndex = index;
                return true;
            }
        }
        return false;
    }

    internal static bool IsCurrentState(string? preconditionResult)
        => !string.IsNullOrEmpty(preconditionResult) && preconditionResult != "-1";
}
