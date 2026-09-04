using System.Security.Cryptography;
using System.Text;

namespace StardewGallery;

internal static class HistoricalExecutionContextRules
{
    internal const int CurrentSchemaVersion = 1;
    internal const int MaxTraceEntries = 512;
    internal const int MaxExecutionJsonBytes = 256 * 1024;
    internal const int MaxSegmentDepth = 64;

    internal static bool IsSha256(string? value)
        => value?.Length == 64 && value.All(Uri.IsHexDigit);

    internal static string HashCommand(string command) => HashParts(command);

    internal static string HashCommandList(IEnumerable<string> commands)
        => HashParts(commands.ToArray());

    internal static string HashOptionSet(IReadOnlyList<ReplayResponseOption> options)
    {
        List<string> parts = ["response-options-v1", options.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)];
        foreach (ReplayResponseOption option in options)
        {
            parts.Add(option.KeyKind.ToString());
            parts.Add(option.NativeKey ?? "");
            parts.Add(option.TextHash);
        }
        return HashParts(parts.ToArray());
    }

    internal static string HashRootPath(string playbackHash, string commandListHash)
        => HashParts("segment-root-v1", playbackHash, commandListHash);

    internal static string HashChildPath(string playbackHash, ScriptSegmentKind kind, ScriptSourceIdentity source,
        SegmentEntryIdentity entry, string commandListHash)
        => HashParts(
            "segment-child-v1",
            playbackHash,
            entry.ParentSegmentPathHash,
            kind.ToString(),
            source.Kind.ToString(),
            NormalizeAssetName(source.AssetName),
            source.Key ?? "",
            entry.CommandHash,
            entry.CommandOrdinal.ToString(System.Globalization.CultureInfo.InvariantCulture),
            entry.Occurrence.ToString(System.Globalization.CultureInfo.InvariantCulture),
            entry.SelectedTarget ?? "",
            commandListHash);

    internal static bool TryValidate(HistoricalExecutionContext? context, out string reason)
    {
        reason = "";
        if (context is null)
            return Fail("context is null", out reason);
        if (context.SchemaVersion != CurrentSchemaVersion)
            return Fail("unsupported schema", out reason);
        if (!IsSha256(context.PlaybackHash))
            return Fail("invalid playback hash", out reason);
        if (!IsDefined(context.Completion) || !IsDefined(context.EndReason))
            return Fail("undefined context enum", out reason);
        if (context.Coverage is null || context.Provenance is null
            || context.AutomaticDecisions is null || context.PlayerChoices is null || context.Issues is null)
            return Fail("required member is null", out reason);
        if (!IsDefined(context.Coverage.AutomaticDecisions) || !IsDefined(context.Coverage.PlayerChoices)
            || !IsDefined(context.Coverage.OpaqueDecisions))
            return Fail("undefined coverage enum", out reason);
        if (string.IsNullOrWhiteSpace(context.Provenance.GameVersion)
            || string.IsNullOrWhiteSpace(context.Provenance.ModVersion)
            || string.IsNullOrWhiteSpace(context.Provenance.Locale))
            return Fail("invalid provenance", out reason);
        if (context.Coverage.AutomaticDecisions == ExecutionTraceCoverage.NotCaptured
            && context.AutomaticDecisions.Count != 0
            || context.Coverage.PlayerChoices == ExecutionTraceCoverage.NotCaptured
            && context.PlayerChoices.Count != 0)
            return Fail("not-captured coverage has recorded entries", out reason);

        int entryCount = context.AutomaticDecisions.Count + context.PlayerChoices.Count;
        if (entryCount > MaxTraceEntries)
            return Fail("trace entry limit exceeded", out reason);
        if (context.Completion == ExecutionTraceCompletion.EmptyComplete)
        {
            if (context.EndReason != ExecutionTraceEndReason.NaturalComplete || entryCount != 0
                || context.Coverage.AutomaticDecisions != ExecutionTraceCoverage.Complete
                || context.Coverage.PlayerChoices != ExecutionTraceCoverage.Complete
                || context.Coverage.OpaqueDecisions != OpaqueDecisionCoverage.NoneObserved
                || context.Issues.Count != 0)
                return Fail("invalid empty-complete context", out reason);
        }
        else if (context.Completion == ExecutionTraceCompletion.Complete)
        {
            if (context.EndReason != ExecutionTraceEndReason.NaturalComplete || entryCount == 0)
                return Fail("invalid complete context", out reason);
        }
        else if (context.EndReason == ExecutionTraceEndReason.NaturalComplete
            && context.Coverage.AutomaticDecisions == ExecutionTraceCoverage.Complete
            && context.Coverage.PlayerChoices == ExecutionTraceCoverage.Complete
            && context.Coverage.OpaqueDecisions == OpaqueDecisionCoverage.NoneObserved
            && context.Issues.Count == 0)
        {
            return Fail("partial context has no incomplete evidence", out reason);
        }

        Dictionary<long, PlayerChoiceDecision> playerBySequence = [];
        List<long> sequences = [];
        foreach (PlayerChoiceDecision choice in context.PlayerChoices)
        {
            if (choice is null || choice.Sequence < 0 || !TryValidate(choice.Locator, context.PlaybackHash, out reason)
                || !TryValidate(choice.Response, out reason))
                return false;
            if (choice.Locator.Kind is not (ExecutionDecisionKind.NativeQuestion
                or ExecutionDecisionKind.QuickQuestion or ExecutionDecisionKind.DialogueResponse))
                return Fail("invalid player-choice decision kind", out reason);
            if (choice.Locator.Kind is ExecutionDecisionKind.NativeQuestion or ExecutionDecisionKind.QuickQuestion
                && choice.Response.Kind == ResponseIdentityKind.AuthoredKey)
                return Fail("event question cannot use authored response identity", out reason);
            if (choice.Locator.Kind == ExecutionDecisionKind.DialogueResponse
                && choice.Response.Kind != ResponseIdentityKind.AuthoredKey)
                return Fail("dialogue response requires authored identity", out reason);
            if (!playerBySequence.TryAdd(choice.Sequence, choice))
                return Fail("duplicate player-choice sequence", out reason);
            sequences.Add(choice.Sequence);
        }

        foreach (AutomaticDecision decision in context.AutomaticDecisions)
        {
            if (decision is null || decision.Sequence < 0 || !IsDefined(decision.Causality)
                || !TryValidate(decision.Locator, context.PlaybackHash, out reason)
                || !TryValidate(decision.Result, context.PlaybackHash, out reason))
                return false;
            if (decision.Locator.Kind is not (ExecutionDecisionKind.Fork or ExecutionDecisionKind.RandomRoute
                or ExecutionDecisionKind.StateConditional or ExecutionDecisionKind.Opaque))
                return Fail("invalid automatic decision kind", out reason);
            if (decision.Causality == AutomaticDecisionCausality.PlayerChoiceDerived)
            {
                if (decision.CausedByPlayerChoiceSequence is not long cause
                    || cause >= decision.Sequence || !playerBySequence.ContainsKey(cause))
                    return Fail("invalid player-choice cause", out reason);
            }
            else if (decision.CausedByPlayerChoiceSequence is not null)
            {
                return Fail("unexpected player-choice cause", out reason);
            }
            if (decision.Result.ReplacementSegment is ScriptSegmentIdentity replacement
                && !MatchesEntry(decision.Locator, decision.Result, replacement.EnteredBy))
                return Fail("replacement segment entry does not match decision", out reason);
            sequences.Add(decision.Sequence);
        }

        sequences.Sort();
        for (int index = 0; index < sequences.Count; index++)
        {
            if (sequences[index] != index)
                return Fail("decision sequence must be unique and contiguous", out reason);
        }

        foreach (ExecutionTraceIssue issue in context.Issues)
        {
            if (issue is null || !IsDefined(issue.Kind) || issue.Sequence is < 0
                || issue.Sequence is long sequence && sequence >= entryCount)
                return Fail("invalid issue", out reason);
        }
        return true;
    }

    internal static HistoricalReplayCapability GetCapability(HistoricalExecutionContext? context, string expectedPlaybackHash)
    {
        if (!TryValidate(context, out _) || context is null
            || !StringComparer.Ordinal.Equals(context.PlaybackHash, expectedPlaybackHash)
            || context.Completion == ExecutionTraceCompletion.Partial
            || context.Coverage.AutomaticDecisions != ExecutionTraceCoverage.Complete
            || context.Coverage.OpaqueDecisions != OpaqueDecisionCoverage.NoneObserved
            || context.AutomaticDecisions.Any(value => value.Causality == AutomaticDecisionCausality.Unknown)
            || context.AutomaticDecisions.Any(value => value.Locator.Kind == ExecutionDecisionKind.Opaque)
            || context.PlayerChoices.Any(value => value.Locator.Kind == ExecutionDecisionKind.Opaque)
            || context.Issues.Count != 0)
            return HistoricalReplayCapability.ContentOnly;

        return context.Coverage.PlayerChoices == ExecutionTraceCoverage.Complete
            ? HistoricalReplayCapability.ExactCapable
            : HistoricalReplayCapability.OutcomeAware;
    }

    internal static HistoricalExecutionContextState GetState(HistoricalExecutionContext context)
        => context.Completion switch
        {
            ExecutionTraceCompletion.EmptyComplete => HistoricalExecutionContextState.EmptyComplete,
            ExecutionTraceCompletion.Complete => HistoricalExecutionContextState.Complete,
            _ => HistoricalExecutionContextState.Partial
        };

    internal static ResponseMatchResult MatchResponse(ResponseIdentity identity,
        IReadOnlyList<ReplayResponseOption> options, string replayLocale)
    {
        if (!TryValidate(identity, out _) || options is null || options.Count == 0
            || options.Any(option => option is null || !IsDefined(option.KeyKind) || !IsSha256(option.TextHash)))
            return ResponseMatchResult.NoMatch;

        if (identity.Kind == ResponseIdentityKind.AuthoredKey && !string.IsNullOrWhiteSpace(identity.NativeKey))
        {
            int[] matches = options.Select((option, index) => (option, index))
                .Where(pair => pair.option.KeyKind == ResponseIdentityKind.AuthoredKey
                    && StringComparer.Ordinal.Equals(pair.option.NativeKey, identity.NativeKey))
                .Select(pair => pair.index)
                .ToArray();
            if (matches.Length == 1)
                return new ResponseMatchResult(true, matches[0], ResponseMatchKind.AuthoredKey);
        }

        if (StringComparer.Ordinal.Equals(HashOptionSet(options), identity.OptionSetHash)
            && identity.OriginalIndex < options.Count)
            return new ResponseMatchResult(true, identity.OriginalIndex, ResponseMatchKind.OptionSetAndIndex);

        if (identity.SelectedTextHash is not null
            && StringComparer.Ordinal.Equals(identity.CaptureLocale, replayLocale))
        {
            int[] matches = options.Select((option, index) => (option, index))
                .Where(pair => StringComparer.Ordinal.Equals(pair.option.TextHash, identity.SelectedTextHash))
                .Select(pair => pair.index)
                .ToArray();
            if (matches.Length == 1)
                return new ResponseMatchResult(true, matches[0], ResponseMatchKind.SameLocaleText);
        }

        return ResponseMatchResult.NoMatch;
    }

    private static bool TryValidate(DecisionLocator? locator, string playbackHash, out string reason)
    {
        reason = "";
        if (locator is null || locator.CommandOrdinal < 0 || locator.Occurrence < 0
            || !IsDefined(locator.Kind) || !IsSha256(locator.CommandHash))
            return Fail("invalid decision locator", out reason);
        return TryValidate(locator.Segment, playbackHash, out reason);
    }

    private static bool TryValidate(ScriptSegmentIdentity? segment, string playbackHash, out string reason)
    {
        reason = "";
        if (segment is null || !IsDefined(segment.Kind)
            || !IsSha256(segment.PathHash) || !IsSha256(segment.CommandListHash))
            return Fail("invalid segment", out reason);
        if (!TryValidate(segment.Source, out reason))
            return false;
        if (segment.Kind == ScriptSegmentKind.Root)
        {
            if (segment.Source.Kind != ScriptSourceKind.RootPlayback || segment.EnteredBy is not null)
                return Fail("invalid root segment", out reason);
            return StringComparer.Ordinal.Equals(segment.PathHash, HashRootPath(playbackHash, segment.CommandListHash))
                || Fail("root path hash mismatch", out reason);
        }
        if (segment.EnteredBy is null || !IsSha256(segment.EnteredBy.ParentSegmentPathHash)
            || !IsSha256(segment.EnteredBy.CommandHash) || segment.EnteredBy.CommandOrdinal < 0
            || segment.EnteredBy.Occurrence < 0)
            return Fail("invalid child segment entry", out reason);
        return StringComparer.Ordinal.Equals(segment.PathHash,
            HashChildPath(playbackHash, segment.Kind, segment.Source, segment.EnteredBy, segment.CommandListHash))
            || Fail("child path hash mismatch", out reason);
    }

    private static bool TryValidate(ScriptSourceIdentity source, out string reason)
    {
        reason = "";
        if (!IsDefined(source.Kind))
            return Fail("undefined source kind", out reason);
        bool valid = source.Kind switch
        {
            ScriptSourceKind.RootPlayback => source.AssetName is null && source.Key is null,
            ScriptSourceKind.EventAssetEntry => !string.IsNullOrWhiteSpace(source.AssetName)
                && !string.IsNullOrWhiteSpace(source.Key),
            ScriptSourceKind.TranslationKey or ScriptSourceKind.FestivalField => !string.IsNullOrWhiteSpace(source.Key),
            _ => true
        };
        return valid || Fail("invalid script source", out reason);
    }

    private static bool TryValidate(AutomaticDecisionResult? result, string playbackHash, out string reason)
    {
        reason = "";
        if (result is null)
            return Fail("missing automatic result", out reason);
        if (!IsDefined(result.Outcome))
            return Fail("undefined automatic outcome", out reason);
        bool valid = result.Outcome switch
        {
            AutomaticDecisionOutcome.ContinueCurrentSegment => result.ReplacementSegment is null
                && result.SelectedIndex is null,
            AutomaticDecisionOutcome.ReplaceCommands => result.ReplacementSegment is not null,
            AutomaticDecisionOutcome.SelectAlternative => result.SelectedIndex is >= 0
                && result.ReplacementSegment is null,
            _ => false
        };
        if (!valid)
            return Fail("invalid automatic result", out reason);
        return result.ReplacementSegment is null || TryValidate(result.ReplacementSegment, playbackHash, out reason);
    }

    private static bool TryValidate(ResponseIdentity? response, out string reason)
    {
        reason = "";
        if (response is null || response.OriginalIndex < 0 || response.OptionCount <= 0
            || !IsDefined(response.Kind)
            || response.OriginalIndex >= response.OptionCount || !IsSha256(response.OptionSetHash)
            || response.SelectedTextHash is not null && !IsSha256(response.SelectedTextHash))
            return Fail("invalid response identity", out reason);
        if (response.Kind == ResponseIdentityKind.AuthoredKey && string.IsNullOrWhiteSpace(response.NativeKey))
            return Fail("authored response key is missing", out reason);
        if (response.Kind == ResponseIdentityKind.GeneratedOrdinal
            && !StringComparer.Ordinal.Equals(response.NativeKey,
                response.OriginalIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            return Fail("generated ordinal response key is invalid", out reason);
        if (response.Kind == ResponseIdentityKind.IndexOnly && response.NativeKey is not null)
            return Fail("index-only response has a key", out reason);
        return true;
    }

    private static bool MatchesEntry(DecisionLocator locator, AutomaticDecisionResult result, SegmentEntryIdentity? entry)
        => entry is not null
            && StringComparer.Ordinal.Equals(entry.ParentSegmentPathHash, locator.Segment.PathHash)
            && StringComparer.Ordinal.Equals(entry.CommandHash, locator.CommandHash)
            && entry.CommandOrdinal == locator.CommandOrdinal
            && entry.Occurrence == locator.Occurrence
            && (result.StableResultId is null
                || StringComparer.Ordinal.Equals(entry.SelectedTarget, result.StableResultId));

    private static bool IsDefined<T>(T value) where T : struct, Enum
        => Enum.IsDefined(typeof(T), value);

    private static string NormalizeAssetName(string? assetName)
        => (assetName ?? "").Replace('\\', '/').Trim().ToUpperInvariant();

    private static string HashParts(params string[] parts)
    {
        StringBuilder text = new();
        foreach (string part in parts)
            text.Append(part.Length).Append(':').Append(part);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    private static bool Fail(string value, out string reason)
    {
        reason = value;
        return false;
    }
}
