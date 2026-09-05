using System.Text;
using System.Text.Json;
using StardewGallery;

Check(EventKey.TryGetId("75160185/f Alissa 500", out string id) && id == "75160185");
Check(EventKey.TryGetId("mod.event/id/condition", out id) && id == "mod.event");
Check(!EventKey.TryGetId(" /condition", out _));
Check(EventKey.GetIdentity("FarmHouse", "4383992") != EventKey.GetIdentity("Town", "4383992"));
Check(EventKey.SelectVariantIndex(3, index => index == 1) == 1);
Check(EventKey.SelectVariantIndex(3, _ => false) == 0);
Check(EventKey.IsPlaceholderScript("speak Abigail \"You open up the XNB file hoping to find a secret, only to see this sentence. You are now disappointed.\""));
Check(!EventKey.IsPlaceholderScript("speak Abigail real event"));
Check(EventKey.GetScriptFingerprint("same") == EventKey.GetScriptFingerprint("same"));
Check(EventKey.GetScriptFingerprint("same") != EventKey.GetScriptFingerprint("different"));
Dictionary<string, IReadOnlyDictionary<string, string>> snapshotAssetsA = new()
{
    ["Data/Events/Town"] = new Dictionary<string, string> { ["b"] = "two", ["a"] = "one" }
};
Dictionary<string, IReadOnlyDictionary<string, string>> snapshotAssetsB = new()
{
    ["Data/Events/Town"] = new Dictionary<string, string> { ["a"] = "one", ["b"] = "two" }
};
Check(EventKey.GetSnapshotFingerprint("root", snapshotAssetsA, new Dictionary<string, string> { ["z:k"] = "text" })
    == EventKey.GetSnapshotFingerprint("root", snapshotAssetsB, new Dictionary<string, string> { ["z:k"] = "text" }));
Check(EventKey.GetSnapshotFingerprint("root", snapshotAssetsA, new Dictionary<string, string>())
    != EventKey.GetSnapshotFingerprint("root changed", snapshotAssetsA, new Dictionary<string, string>()));

EventIdentity normalizedIdentity = new(" Data\\Events\\Town ", " 123 ");
Check(normalizedIdentity.AssetName == "Data/Events/Town");
Check(normalizedIdentity.EventId == "123");
Check(normalizedIdentity == new EventIdentity("Data/Events/Town", "123"));
Check(normalizedIdentity == new EventIdentity("data/events/town", "123"));
Check(new EventIdentity("Data/Events/Town", "abc") != new EventIdentity("Data/Events/Town", "ABC"));
Check(new EventIdentity("Data/Events/Town", "123") != new EventIdentity("Data/Events/Beach", "123"));
Check(new HashSet<EventIdentity>
{
    new("Data/Events/Town", "123"),
    new("data\\events\\town", "123")
}.Count == 1);
Check(normalizedIdentity.StorageKey == "Data/Events/Town\u001f123");
Check(normalizedIdentity.ToString() == normalizedIdentity.StorageKey);
Check(default(EventIdentity) == new EventIdentity("", ""));
Check(default(EventIdentity).GetHashCode() == new EventIdentity("", "").GetHashCode());

string rootScriptHash = EventHashes.RootScript("same");
Check(rootScriptHash.Length == 64 && rootScriptHash.All(Uri.IsHexDigit));
Check(rootScriptHash == "0967115F2813A3541EAEF77DE9D9D5773F1C0C04314B0BBFE4FF3B3B1C55B5D5");
Check(rootScriptHash == EventHashes.RootScript("same"));
Check(rootScriptHash != EventHashes.RootScript("different"));
string summerDefinitionHash = EventHashes.RootDefinition("123/Season Summer", "same");
Check(summerDefinitionHash.Length == 64 && summerDefinitionHash.All(Uri.IsHexDigit));
Check(summerDefinitionHash == "22D5843B8E8D5A649958AAACDA055DB6B83C522F62EB8D5058759EE6845C3D04");
Check(summerDefinitionHash != EventHashes.RootDefinition("123/Season Winter", "same"));
Check(summerDefinitionHash != EventHashes.RootDefinition("123/Season Summer", "different"));

EventFragments emptyFragments = new([], []);
ResolvedEvent townResolved = new(
    new EventIdentity("Data/Events/Town", "123"),
    "SharedLocation",
    "123/Season Summer",
    "same",
    emptyFragments,
    EventHashes.RootDefinition("123/Season Summer", "same"),
    rootScriptHash);
ResolvedEvent beachResolved = new(
    new EventIdentity("Data/Events/Beach", "123"),
    "SharedLocation",
    "123/Season Summer",
    "same",
    emptyFragments,
    EventHashes.RootDefinition("123/Season Summer", "same"),
    rootScriptHash);
Check(townResolved.Identity != beachResolved.Identity);
GalleryEvent adapted = new(townResolved, new EventOwnership(OwnershipKind.Excluded, [], "test"));
Check(adapted.Resolved == townResolved);
Check(adapted.Identity == townResolved.Identity.StorageKey);
Check(adapted.LocationName == townResolved.LocationName);
Check(adapted.AssetName == townResolved.AssetName);
Check(adapted.EventId == townResolved.EventId);
Check(adapted.EventKey == townResolved.RawEventKey);
Check(adapted.Script == townResolved.ResolvedScript);
Check(adapted.Fragments == townResolved.Fragments);
using (JsonDocument adaptedJson = JsonDocument.Parse(JsonSerializer.Serialize(adapted)))
{
    string[] compatibilityProperties =
    [
        "Identity", "LocationName", "AssetName", "EventId", "EventKey", "Script", "Fragments", "Ownership"
    ];
    Check(compatibilityProperties.All(name => adaptedJson.RootElement.TryGetProperty(name, out _)));
}

WatchedEventSnapshot legacySnapshot = JsonSerializer.Deserialize<WatchedEventSnapshot>(
    """
    {
      "LocationName": "Town",
      "AssetName": "Data\\Events\\Town",
      "EventId": "123",
      "EventKey": "123/f Haley 1000",
      "RootScript": "speak Haley hello",
      "EventAssets": {},
      "Translations": {},
      "Locale": "zh",
      "Fingerprint": "abc",
      "FirstWatchedAt": "2026-09-01T01:02:03+00:00",
      "LastWatchedAt": "2026-09-02T04:05:06+00:00"
    }
    """) ?? throw new Exception("Legacy snapshot did not deserialize.");
Check(legacySnapshot.LocationName == "Town");
Check(legacySnapshot.AssetName == "Data\\Events\\Town");
Check(legacySnapshot.EventId == "123");
Check(legacySnapshot.EventKey == "123/f Haley 1000");
Check(legacySnapshot.RootScript == "speak Haley hello");
Check(legacySnapshot.EventAssets.Count == 0);
Check(legacySnapshot.Translations.Count == 0);
Check(legacySnapshot.Locale == "zh");
Check(legacySnapshot.Fingerprint == "abc");
Check(legacySnapshot.FirstWatchedAt == new DateTimeOffset(2026, 9, 1, 1, 2, 3, TimeSpan.Zero));
Check(legacySnapshot.LastWatchedAt == new DateTimeOffset(2026, 9, 2, 4, 5, 6, TimeSpan.Zero));
Check(legacySnapshot.Identity == new EventIdentity("Data/Events/Town", "123"));
Check(legacySnapshot.Playback.RootScript == legacySnapshot.RootScript);
Check(legacySnapshot.Playback.EventAssets.Count == 0);
Check(legacySnapshot.Playback.Translations.Count == 0);
Check(legacySnapshot.Playback.Locale == legacySnapshot.Locale);
Check(legacySnapshot.Playback.PlaybackHash == legacySnapshot.Fingerprint);
using (JsonDocument roundTrip = JsonDocument.Parse(JsonSerializer.Serialize(legacySnapshot)))
{
    JsonElement root = roundTrip.RootElement;
    string[] persistedProperties =
    [
        "LocationName", "AssetName", "EventId", "EventKey", "RootScript", "EventAssets",
        "Translations", "Locale", "Fingerprint", "FirstWatchedAt", "LastWatchedAt"
    ];
    Check(root.EnumerateObject().Count() == persistedProperties.Length);
    Check(persistedProperties.All(name => root.TryGetProperty(name, out _)));
    Check(!root.TryGetProperty("Identity", out _));
    Check(!root.TryGetProperty("Playback", out _));
}

LegacyHistoryProjection legacyProjection = LegacyHistoryAdapter.From(legacySnapshot);
Check(legacyProjection.Variant.Key.Identity == new EventIdentity("Data/Events/Town", "123"));
Check(legacyProjection.Variant.RawEventKey == "123/f Haley 1000");
Check(legacyProjection.Variant.RootScriptHash == EventHashes.RootScript("speak Haley hello"));
Check(legacyProjection.Variant.Key.RootDefinitionHash == EventHashes.RootDefinition("123/f Haley 1000", "speak Haley hello"));
Check(legacyProjection.Variant.Key.PlaybackHash == "abc");
Check(legacyProjection.Variant.Playback.PlaybackHash == "abc");
Check(legacyProjection.Observation.FirstObservedAt == legacySnapshot.FirstWatchedAt);
Check(legacyProjection.Observation.LastObservedAt == legacySnapshot.LastWatchedAt);
Check(legacyProjection.Observation.LastObservedLocationName == "Town");
Check(legacyProjection.Observation.LastObservedLocale == "zh");
Check(legacyProjection.Seen.EventId == "123");
Check(legacyProjection.Seen.Identity == new EventIdentity("Data/Events/Town", "123"));
Check(legacyProjection.Seen.Source == KnownSeenSource.LegacyCapturedVariant);

EventIdentity townId = new("Data/Events/Town", "123");
ObservedVariantKey keyA = new(townId, EventHashes.RootDefinition("123/A", "root"), "playback");
ObservedVariantKey keyB = new(townId, EventHashes.RootDefinition("123/A", "root"), "playback");
ObservedVariantKey keyC = new(townId, EventHashes.RootDefinition("123/B", "root"), "playback");
ObservedVariantKey keyD = new(townId, EventHashes.RootDefinition("123/A", "root"), "playback2");
Check(keyA == keyB);
Check(keyA != keyC);
Check(keyA != keyD);
Check(keyA.GetHashCode() == keyB.GetHashCode());
Check(new EventIdentity("Data\\Events\\Town", "123") == townId);

string condDiffRootDef = EventHashes.RootDefinition("123/A", "root");
string condDiffPlayback = EventKey.GetSnapshotFingerprint("root", new Dictionary<string, IReadOnlyDictionary<string, string>>(), new Dictionary<string, string>());
Check(EventHashes.RootDefinition("123/B", "root") != condDiffRootDef);
Check(EventKey.GetSnapshotFingerprint("root", new Dictionary<string, IReadOnlyDictionary<string, string>>(), new Dictionary<string, string>()) == condDiffPlayback);
Check(new ObservedVariantKey(townId, condDiffRootDef, condDiffPlayback) != new ObservedVariantKey(townId, EventHashes.RootDefinition("123/B", "root"), condDiffPlayback));

WatchedEventSnapshot condA = legacySnapshot with { EventKey = "123/A", RootScript = "same root", Fingerprint = "same-play", FirstWatchedAt = new DateTimeOffset(2026, 9, 1, 1, 1, 1, TimeSpan.Zero), LastWatchedAt = new DateTimeOffset(2026, 9, 1, 1, 1, 1, TimeSpan.Zero) };
WatchedEventSnapshot condB = legacySnapshot with { EventKey = "123/B", RootScript = "same root", Fingerprint = "same-play", FirstWatchedAt = new DateTimeOffset(2026, 9, 2, 2, 2, 2, TimeSpan.Zero), LastWatchedAt = new DateTimeOffset(2026, 9, 2, 2, 2, 2, TimeSpan.Zero) };
LegacyHistoryProjection projA = LegacyHistoryAdapter.From(condA);
LegacyHistoryProjection projB = LegacyHistoryAdapter.From(condB);
Check(projA.Variant.Key.PlaybackHash == projB.Variant.Key.PlaybackHash, "condition-only same playback");
Check(projA.Variant.Key.RootDefinitionHash != projB.Variant.Key.RootDefinitionHash, "condition-only diff rootdef");
Check(projA.Variant.Key != projB.Variant.Key, "condition-only diff ObservedVariantKey");

Dictionary<string, Dictionary<string, string>> fragA = new() { ["Data/Events/Sub"] = new Dictionary<string, string> { ["branch"] = "speak A" } };
Dictionary<string, Dictionary<string, string>> fragB = new() { ["Data/Events/Sub"] = new Dictionary<string, string> { ["branch"] = "speak B" } };
WatchedEventSnapshot playA = legacySnapshot with { EventKey = "123/f Haley 1000", RootScript = "root", EventAssets = fragA, Fingerprint = EventKey.GetSnapshotFingerprint("root", Assets(fragA), new Dictionary<string, string>()) };
WatchedEventSnapshot playB = legacySnapshot with { EventKey = "123/f Haley 1000", RootScript = "root", EventAssets = fragB, Fingerprint = EventKey.GetSnapshotFingerprint("root", Assets(fragB), new Dictionary<string, string>()) };
LegacyHistoryProjection projA2 = LegacyHistoryAdapter.From(playA);
LegacyHistoryProjection projB2 = LegacyHistoryAdapter.From(playB);
Check(projA2.Variant.Key.RootDefinitionHash == projB2.Variant.Key.RootDefinitionHash, "playback-only same rootdef");
Check(projA2.Variant.Key.PlaybackHash != projB2.Variant.Key.PlaybackHash, "playback-only diff playback");
Check(projA2.Variant.Key != projB2.Variant.Key, "playback-only diff ObservedVariantKey");

Check(projA.Observation.FirstObservedAt == condA.FirstWatchedAt);
Check(projA.Observation.LastObservedAt == condA.LastWatchedAt);
Check(projA.Observation.LastObservedLocationName == "Town");
Check(projA.Seen.Identity is not null);
Check(projA.Seen.Source == KnownSeenSource.LegacyCapturedVariant);

KnownSeenEvidence saveSeen = new("999", null, KnownSeenSource.SaveEventsSeen);
Check(saveSeen.EventId == "999");
Check(saveSeen.Identity is null);
Check(saveSeen.Source == KnownSeenSource.SaveEventsSeen);

Dictionary<string, Dictionary<string, string>> defensiveSource = new() { ["Data/Events/Sub"] = new Dictionary<string, string> { ["branch"] = "speak X" } };
Dictionary<string, string> defensiveTranslation = new() { ["z:key"] = "value" };
WatchedEventSnapshot defensiveSnap = legacySnapshot with { EventAssets = defensiveSource, Translations = defensiveTranslation };
LegacyHistoryProjection defensiveProj = LegacyHistoryAdapter.From(defensiveSnap);
defensiveSource["Data/Events/Sub"]["branch"] = "speak MUTATED";
defensiveTranslation["z:key"] = "MUTATED";
Check(defensiveProj.Variant.Playback.EventAssets["Data/Events/Sub"]["branch"] == "speak X", "defensive asset copy");
Check(defensiveProj.Variant.Playback.Translations["z:key"] == "value", "defensive translation copy");

WatchedEventSnapshot conditionOnlyA = legacySnapshot with { EventKey = "123/A", RootScript = "same root", Fingerprint = "same-play" };
WatchedEventSnapshot conditionOnlyB = legacySnapshot with { EventKey = "123/B", RootScript = "same root", Fingerprint = "same-play" };
List<WatchedEventSnapshot> persistenceList = [conditionOnlyA, conditionOnlyB];
Check(persistenceList.Count(snapshot => snapshot.Fingerprint == "same-play") == 2, "two snapshots same playback diff rootdef");
string reSer = JsonSerializer.Serialize(persistenceList);
List<WatchedEventSnapshot>? reLoad = JsonSerializer.Deserialize<List<WatchedEventSnapshot>>(reSer);
Check(reLoad is not null && reLoad.Count == 2, "load retains two diff-rootdef snapshots");
Check(reLoad![0].Fingerprint == reLoad[1].Fingerprint && reLoad[0].EventKey != reLoad[1].EventKey, "load does not merge by fingerprint");

Check(LegacyHistoryAdapter.From(conditionOnlyA).Variant.Key != LegacyHistoryAdapter.From(conditionOnlyB).Variant.Key, "adapter diff ObservedVariant even with same playback");

Check(ObservedVariantSelector.TrySelect(["single"], _ => "0", out int sel0), "single candidate");
Check(sel0 == 0, "single candidate index 0");
Check(ObservedVariantSelector.TrySelect(["A", "B"], key => key == "B" ? "0" : "-1", out int sel1), "first false second true");
Check(sel1 == 1, "second selected");
Check(ObservedVariantSelector.TrySelect(["A", "B"], _ => "0", out int sel2), "both true");
Check(sel2 == 0, "first selected");
Check(!ObservedVariantSelector.TrySelect(["A", "B"], key => throw new InvalidOperationException(key), out _), "both throw failure");
Check(ObservedVariantSelector.TrySelect(["A", "B"], key => key == "B" ? "0" : throw new InvalidOperationException("A throws"), out int sel3), "first throws handled");
Check(sel3 == 1, "second selected after first throws");
Check(!ObservedVariantSelector.TrySelect(["A", "B"], _ => "-1", out _), "all false failure");
Check(!ObservedVariantSelector.TrySelect([], _ => "0", out _), "empty candidates failure");
Check(!ObservedVariantSelector.IsCurrentState(null), "null false");
Check(!ObservedVariantSelector.IsCurrentState(""), "empty false");
Check(!ObservedVariantSelector.IsCurrentState("-1"), "-1 false");
Check(ObservedVariantSelector.IsCurrentState("0"), "0 true");
Check(ObservedVariantSelector.IsCurrentState(" "), "whitespace true");
Check(ObservedVariantSelector.IsCurrentState("matched"), "other nonempty true");

const string sameRootScript = "same";
string candidateAKey = "123/Friendship Haley 1000";
string candidateBKey = "123/Friendship Haley 2000";
Check(ObservedVariantSelector.TrySelect([candidateAKey, candidateBKey],
    key => key == candidateBKey ? "0" : "-1", out int selectedDefinitionIndex), "semantic fixture selects second");
Check(selectedDefinitionIndex == 1, "semantic fixture second index");
string selectedKey = selectedDefinitionIndex == 1 ? candidateBKey : candidateAKey;
Check(selectedKey == candidateBKey, "semantic fixture selected B");
string rootDefB = EventHashes.RootDefinition(candidateBKey, sameRootScript);
Check(rootDefB == EventHashes.RootDefinition("123/Friendship Haley 2000", "same"), "B rootdef");
Check(rootDefB != EventHashes.RootDefinition(candidateAKey, sameRootScript), "B rootdef != A rootdef");
Check(EventHashes.RootDefinition(candidateAKey, sameRootScript) != rootDefB, "A rootdef != B rootdef");

ResolvedEventReader testReader = new(
    (key, _) => !key.StartsWith("invalid", StringComparison.Ordinal),
    script => script.Split('|'),
    command => command.Split(' ', StringSplitOptions.RemoveEmptyEntries),
    _ => null
);
string? filteredPreconditionKey = null;
EventAssetSource filteredSource = new(
    "Data/Events/Filter",
    "FilterLaunch",
    "FilterRoot",
    [
        new EventAssetDefinition("invalid", "none|0 0|Actor 1 1 2"),
        new EventAssetDefinition(" /condition", "none|0 0|Actor 1 1 2"),
        new EventAssetDefinition("placeholder", "speak Abigail \"You open up the XNB file hoping to find a secret, only to see this sentence. You are now disappointed.\""),
        new EventAssetDefinition("mod.event/id/condition", "none|0 0|Actor 1 1 2|speak Actor hello")
    ],
    _ => null,
    key =>
    {
        filteredPreconditionKey = key;
        return "0";
    }
);
IReadOnlyList<ResolvedEventCandidate> filteredCandidates = testReader.Read(filteredSource);
Check(filteredCandidates.Count == 1);
Check(filteredCandidates[0].Resolved.EventId == "mod.event");
Check(filteredCandidates[0].Resolved.LocationName == "FilterLaunch");
Check(filteredCandidates[0].Resolved.RawEventKey == "mod.event/id/condition");
Check(filteredCandidates[0].Resolved.ResolvedScript == "none|0 0|Actor 1 1 2|speak Actor hello");
Check(filteredCandidates[0].Resolved.RootDefinitionHash == EventHashes.RootDefinition(
    filteredCandidates[0].Resolved.RawEventKey,
    filteredCandidates[0].Resolved.ResolvedScript));
Check(filteredCandidates[0].Resolved.RootScriptHash == EventHashes.RootScript(filteredCandidates[0].Resolved.ResolvedScript));
ResolvedEventIndex filteredIndex = ResolvedEventIndex.Build(filteredCandidates);
Check(filteredIndex.CurrentEvents.Single() == filteredCandidates[0].Resolved);
Check(filteredPreconditionKey == "mod.event/id/condition");
EventAssetSource missingFragmentSource = new(
    "Data/Events/Missing",
    "MissingLaunch",
    "MissingRoot",
    [new EventAssetDefinition("missing", "none|0 0|Actor 1 1 2|fork absent")],
    _ => new Dictionary<string, string>(),
    _ => "0"
);
IReadOnlyList<ResolvedEventCandidate> missingFragmentCandidates = testReader.Read(missingFragmentSource);
Check(missingFragmentCandidates.Count == 1);
Check(missingFragmentCandidates[0].Resolved.Fragments.MissingKeys.SequenceEqual(["absent"]));

List<string> pipelineCalls = [];
Dictionary<string, string> alphaFragments = new() { ["branch"] = "speak Alpha branch" };
Dictionary<string, string> betaFragments = new() { ["branch"] = "speak Beta branch" };
EventAssetSource alphaSource = new(
    "Data/Events/Alpha",
    "AlphaLaunch",
    "AlphaRoot",
    [new EventAssetDefinition("alpha", "none|0 0|Alpha 1 1 2|fork branch")],
    location =>
    {
        Check(location == "AlphaRoot");
        pipelineCalls.Add("load:Alpha");
        return alphaFragments;
    },
    key =>
    {
        Check(key == "alpha");
        pipelineCalls.Add("check:Alpha");
        return "0";
    }
);
EventAssetSource betaSource = new(
    "Data/Events/Beta",
    "BetaLaunch",
    "BetaRoot",
    [new EventAssetDefinition("beta", "none|0 0|Beta 1 1 2|fork branch")],
    location =>
    {
        Check(location == "BetaRoot");
        pipelineCalls.Add("load:Beta");
        return betaFragments;
    },
    key =>
    {
        Check(key == "beta");
        pipelineCalls.Add("check:Beta");
        return "0";
    }
);
ResolvedEventIndex visitedIndex = ResolvedEventIndex.ReadCurrent(
    new FakeEventAssetSourceCatalog([alphaSource, betaSource], pipelineCalls),
    testReader
);
Check(pipelineCalls.SequenceEqual([
    "visit:AlphaLaunch", "load:Alpha", "after:AlphaLaunch",
    "visit:BetaLaunch", "load:Beta", "after:BetaLaunch",
    "check:Alpha", "check:Beta"
]));
Check(visitedIndex.Groups.Count == 2);
Check(visitedIndex.CurrentEvents.Select(entry => entry.LocationName).SequenceEqual(["AlphaLaunch", "BetaLaunch"]));
Check(visitedIndex.CurrentEvents.All(entry => entry.Fragments.Scripts.Count == 2));

List<string> failureCalls = [];
EventAssetSource failingSource = new(
    "Data/Events/Failure",
    "FailureLaunch",
    "FailureRoot",
    [new EventAssetDefinition("failure", "none|0 0|Actor 1 1 2|fork branch")],
    _ => throw new InvalidOperationException("expected fragment failure"),
    _ => "0"
);
bool readerFailureEscaped = false;
try
{
    ResolvedEventIndex.ReadCurrent(
        new FakeEventAssetSourceCatalog([failingSource, betaSource], failureCalls),
        testReader
    );
}
catch (InvalidOperationException error) when (error.Message == "expected fragment failure")
{
    readerFailureEscaped = true;
}
Check(readerFailureEscaped);
Check(failureCalls.SequenceEqual(["visit:FailureLaunch"]));

int firstDuplicateCalls = 0;
int ignoredDuplicateCalls = 0;
ResolvedEventIndex candidateIndex = ResolvedEventIndex.Build([
    Candidate("Data\\Events\\Town", "evt", "FirstLocation", "evt/first", "same", () =>
    {
        firstDuplicateCalls++;
        return "-1";
    }),
    Candidate("data/events/town", "evt", "DuplicateLocation", "evt/first", "same", () =>
    {
        ignoredDuplicateCalls++;
        return "0";
    }),
    Candidate("DATA/events/TOWN", "evt", "SelectedLocation", "evt/first", "different", () => "0"),
    Candidate("Data/Events/Town", "evt", "LaterLocation", "evt/second", "same", () => "0"),
    Candidate("Data/Events/Beach", "evt", "BeachLocation", "evt/only", "beach", () => "0"),
    Candidate("Data/Events/Town", "EVT", "CaseLocation", "EVT/only", "case", () => "0")
]);
Check(candidateIndex.Groups.Count == 3);
Check(candidateIndex.Groups.Select(group => group.Current.LocationName)
    .SequenceEqual(["SelectedLocation", "BeachLocation", "CaseLocation"]));
Check(candidateIndex.CurrentEvents.Select(entry => entry.LocationName)
    .SequenceEqual(["SelectedLocation", "BeachLocation", "CaseLocation"]));
Check(candidateIndex.TryGetGroup(new EventIdentity("data/events/town", "evt"), out ResolvedEventGroup townGroup));
Check(townGroup.Candidates.Count == 3);
Check(townGroup.Candidates.Select(entry => entry.LocationName)
    .SequenceEqual(["FirstLocation", "SelectedLocation", "LaterLocation"]));
Check(townGroup.Candidates[0].RawEventKey == townGroup.Candidates[1].RawEventKey);
Check(townGroup.Candidates[0].ResolvedScript != townGroup.Candidates[1].ResolvedScript);
Check(townGroup.Candidates[0].ResolvedScript == townGroup.Candidates[2].ResolvedScript);
Check(townGroup.Candidates[0].RawEventKey != townGroup.Candidates[2].RawEventKey);
Check(townGroup.Identity.StorageKey == "DATA/events/TOWN\u001fevt");
Check(firstDuplicateCalls == 1 && ignoredDuplicateCalls == 0);
Check(candidateIndex.TryGetCurrent(new EventIdentity("DATA/Events/Town", "evt"), out ResolvedEvent selectedCurrent));
Check(selectedCurrent.LocationName == "SelectedLocation");
Check(!candidateIndex.TryGetGroup(new EventIdentity("Data/Events/Missing", "evt"), out _));
Check(!candidateIndex.TryGetCurrent(new EventIdentity("Data/Events/Missing", "evt"), out _));
Check(candidateIndex.GetCandidates(new EventIdentity("Data/Events/Missing", "evt")).Count == 0);

int skippedApplicableCalls = 0;
ResolvedEventIndex multipleApplicable = ResolvedEventIndex.Build([
    Candidate("Data/Events/Town", "multi", "FirstApplicable", "multi/a", "a", () => "0"),
    Candidate("Data/Events/Town", "multi", "SecondApplicable", "multi/b", "b", () =>
    {
        skippedApplicableCalls++;
        return "0";
    })
]);
Check(multipleApplicable.CurrentEvents.Single().LocationName == "FirstApplicable");
Check(skippedApplicableCalls == 0);

ResolvedEventIndex allFalse = ResolvedEventIndex.Build([
    Candidate("Data/Events/Town", "none", "Fallback", "none/a", "a", () => "-1"),
    Candidate("Data/Events/Town", "none", "NotSelected", "none/b", "b", () => "")
]);
Check(allFalse.CurrentEvents.Single().LocationName == "Fallback");

int afterExceptionCalls = 0;
ResolvedEventIndex exceptionThenMatch = ResolvedEventIndex.Build([
    Candidate("Data/Events/Town", "exception", "Throws", "exception/a", "a", () => throw new InvalidOperationException("expected")),
    Candidate("Data/Events/Town", "exception", "AfterException", "exception/b", "b", () =>
    {
        afterExceptionCalls++;
        return "matched";
    })
]);
Check(exceptionThenMatch.CurrentEvents.Single().LocationName == "AfterException");
Check(afterExceptionCalls == 1);
Check(!ResolvedEventIndex.MatchesCurrentState(null));
Check(!ResolvedEventIndex.MatchesCurrentState(""));
Check(!ResolvedEventIndex.MatchesCurrentState("-1"));
Check(ResolvedEventIndex.MatchesCurrentState("0"));
Check(ResolvedEventIndex.MatchesCurrentState(" "));

ResolvedEventReader galleryReader = new(
    (_, _) => true,
    script => script.Split('|'),
    command => command.Split(' ', StringSplitOptions.RemoveEmptyEntries),
    _ => null
);
EventAssetSource nonSelectedSource = new(
    "Data\\Events\\Town",
    "FirstTown",
    "Town",
    [new EventAssetDefinition("root/f Alissa 1000", "none|0 0|Alissa 1 1 2|speak Alissa hello")],
    _ => null,
    _ => "-1"
);
EventAssetSource selectedSource = new(
    "data/events/town",
    "SelectedTown",
    "Town",
    [
        new EventAssetDefinition("root/f Bert 1000", "none|0 0|Bert 1 1 2|speak Bert hello"),
        new EventAssetDefinition("child/e root", "none|0 0|Bert 1 1 2|pause 100"),
        new EventAssetDefinition("spouse-event", "none|0 0|spouse 1 1 2|fork branch"),
        new EventAssetDefinition("silent", "none|0 0|Alissa 1 1 2|pause 100")
    ],
    _ => new Dictionary<string, string> { ["branch"] = "speak Bert branch" },
    _ => "0"
);
ResolvedEventIndex galleryIndex = ResolvedEventIndex.ReadCurrent(
    new FakeEventAssetSourceCatalog([nonSelectedSource, selectedSource]),
    galleryReader
);
GalleryCatalogBuilder testBuilder = new(
    key => key.Split('/'),
    script => script.Split('|'),
    command => command.Split(' ', StringSplitOptions.RemoveEmptyEntries),
    positions => positions.Split(' ', StringSplitOptions.RemoveEmptyEntries),
    () => "Bert"
);
GalleryCatalogBuildResult galleryBuild = testBuilder.Build(
    [
        new GalleryCharacter("Alissa", "Alissa", true, 1000),
        new GalleryCharacter("Bert", "Bert", true, 1000),
        new GalleryCharacter("Unused", "Unused", true, 0)
    ],
    galleryIndex.CurrentEvents
);
Check(galleryIndex.CurrentEvents.Count == 4);
Check(galleryIndex.CurrentEvents.Single(entry => entry.EventId == "root").LocationName == "SelectedTown");
Check(galleryBuild.AnalyzedEvents.Count == 4);
Check(galleryBuild.Catalog.Events.Count == 3);
Check(galleryBuild.Catalog.ExcludedEvents.Count == 1);
Check(galleryBuild.Catalog.Characters.Select(character => character.Name).SequenceEqual(["Bert"]));
GalleryEvent rootGalleryEvent = galleryBuild.Catalog.Events.Single(entry => entry.EventId == "root");
Check(rootGalleryEvent.Identity == "data/events/town\u001froot");
Check(rootGalleryEvent.Ownership.Kind == OwnershipKind.Direct);
Check(rootGalleryEvent.Ownership.Owners.Single().Name == "Bert");
GalleryEvent childGalleryEvent = galleryBuild.Catalog.Events.Single(entry => entry.EventId == "child");
Check(childGalleryEvent.Ownership.Kind == OwnershipKind.Inherited);
Check(childGalleryEvent.Ownership.Owners.Single().Name == "Bert");
GalleryEvent spouseGalleryEvent = galleryBuild.Catalog.Events.Single(entry => entry.EventId == "spouse-event");
Check(spouseGalleryEvent.Ownership.Kind == OwnershipKind.Inferred);
Check(spouseGalleryEvent.Ownership.Owners.Single().Name == "Bert");
Check(galleryBuild.Catalog.ExcludedEvents.Single().EventId == "silent");

HashSet<string> characters = ["Torts", "Lenny", "Alissa", "Bert"];
List<EventEvidence> events =
[
    Evidence("torts7", "75160284", new Dictionary<string, int> { ["Torts"] = 1750 }, [], Set("Lenny"), new Dictionary<string, int> { ["Lenny"] = 4 }),
    Evidence("torts8", "75160285", new Dictionary<string, int>(), ["75160284"], Set("Torts"), new Dictionary<string, int>()),
    Evidence("tie", "900", new Dictionary<string, int>(), [], Set("Alissa", "Bert"), new Dictionary<string, int> { ["Alissa"] = 2, ["Bert"] = 2 }),
    Evidence("silent", "901", new Dictionary<string, int>(), [], Set("Alissa"), new Dictionary<string, int>()),
    Evidence("inferred-root", "902", new Dictionary<string, int>(), [], Set("Alissa"), new Dictionary<string, int> { ["Alissa"] = 1 }),
    Evidence("inferred-child", "903", new Dictionary<string, int>(), ["902"], Set("Bert"), new Dictionary<string, int>())
    ,Evidence("multi-direct", "904", new Dictionary<string, int> { ["Alissa"] = 3500, ["Bert"] = 3500 }, [], Set("Alissa", "Bert"), new Dictionary<string, int> { ["Alissa"] = 3 })
];
IReadOnlyDictionary<EventIdentity, EventOwnership> ownership = OwnershipResolver.Resolve(events, characters);
Check(ownership[TestIdentity("torts7")].Kind == OwnershipKind.Direct && ownership[TestIdentity("torts7")].Owners.Single().Name == "Torts");
Check(ownership[TestIdentity("torts8")].Kind == OwnershipKind.Inherited && ownership[TestIdentity("torts8")].Owners.Single().Name == "Torts");
Check(ownership[TestIdentity("tie")].Kind == OwnershipKind.Inferred && ownership[TestIdentity("tie")].Owners.Count == 2);
Check(ownership[TestIdentity("silent")].Kind == OwnershipKind.Excluded);
Check(ownership[TestIdentity("inferred-child")].Kind == OwnershipKind.Inherited && ownership[TestIdentity("inferred-child")].Owners.Single().Name == "Alissa");
Check(ownership[TestIdentity("multi-direct")].Kind == OwnershipKind.Direct && ownership[TestIdentity("multi-direct")].Owners.Single().Name == "Alissa");

IReadOnlyDictionary<EventIdentity, EventOwnership> normalizedOwnership = OwnershipResolver.Resolve(
    [Evidence("typed", "typed", new Dictionary<string, int> { ["Alissa"] = 1000 }, [], Set("Alissa"), new Dictionary<string, int>())],
    characters
);
EventOwnership normalizedOwner = normalizedOwnership[new EventIdentity("data\\events\\checks", "typed")];
Check(normalizedOwner.Kind == OwnershipKind.Direct && normalizedOwner.Owners.Single().Name == "Alissa");

List<EventEvidence> ambiguousPredecessors =
[
    new EventEvidence(new EventIdentity("Data/Events/A", "first"), "shared", new Dictionary<string, int> { ["Alissa"] = 1000 }, [], Set("Alissa"), new Dictionary<string, int>()),
    new EventEvidence(new EventIdentity("Data/Events/B", "second"), "shared", new Dictionary<string, int> { ["Bert"] = 1000 }, [], Set("Bert"), new Dictionary<string, int>()),
    new EventEvidence(new EventIdentity("Data/Events/C", "child"), "child", new Dictionary<string, int>(), ["shared"], Set("Bert"), new Dictionary<string, int> { ["Bert"] = 1 })
];
IReadOnlyDictionary<EventIdentity, EventOwnership> ambiguousOwnership = OwnershipResolver.Resolve(ambiguousPredecessors, characters);
Check(ambiguousOwnership[new EventIdentity("Data/Events/C", "child")].Kind == OwnershipKind.Inferred);
Check(ambiguousOwnership[new EventIdentity("Data/Events/C", "child")].Owners.Single().Name == "Bert");

List<EventEvidence> caseSensitivePredecessor =
[
    new EventEvidence(new EventIdentity("Data/Events/A", "root"), "RootCase", new Dictionary<string, int> { ["Alissa"] = 1000 }, [], Set("Alissa"), new Dictionary<string, int>()),
    new EventEvidence(new EventIdentity("Data/Events/B", "child"), "case-child", new Dictionary<string, int>(), ["rootcase"], Set("Bert"), new Dictionary<string, int> { ["Bert"] = 1 })
];
IReadOnlyDictionary<EventIdentity, EventOwnership> caseSensitiveOwnership = OwnershipResolver.Resolve(caseSensitivePredecessor, characters);
Check(caseSensitiveOwnership[new EventIdentity("Data/Events/B", "child")].Kind == OwnershipKind.Inferred);

Dictionary<string, string> fragments = new()
{
    ["branch"] = "speak Alissa two|switchEvent ending",
    ["ending"] = "speak Alissa three|fork branch"
};
EventFragments collected = EventFragmentCollector.Collect(
    "none|0 0|Alissa 1 1 2 farmer 2 2 0|fork branch",
    "Town",
    _ => fragments,
    script => script.Split('|'),
    command => command.Split(' ', StringSplitOptions.RemoveEmptyEntries),
    _ => null
);
Check(collected.Scripts.Count == 3 && collected.MissingKeys.Count == 0);
Check(!collected.Scripts.Any(script => script.Contains("75160284")));
Dictionary<string, IReadOnlyDictionary<string, string>> locationFragments = new()
{
    ["FarmHouse"] = new Dictionary<string, string>(),
    ["Pool"] = new Dictionary<string, string> { ["poolBranch"] = "speak Alissa hello" }
};
EventFragments crossLocation = EventFragmentCollector.Collect(
    "none|0 0|Alissa 1 1 2|changeLocation Pool|fork poolBranch",
    "FarmHouse",
    location => locationFragments.GetValueOrDefault(location),
    script => script.Split('|'),
    command => command.Split(' ', StringSplitOptions.RemoveEmptyEntries),
    _ => null
);
Check(crossLocation.Scripts.Count == 2 && crossLocation.MissingKeys.Count == 0);
Check(GalleryLayout.Center(1706, 960, 1672, 941) == (17, 9));
Check(GalleryLayout.Center(1280, 720, 1672, 941) == (-196, -110));
Check(GalleryLayout.Changed(1280, 720, 1706, 960));
Check(Math.Abs(GalleryLayout.ScaleToFit(1280, 720, 1672, 941, 24) - 672d / 941d) < .0001);
Check(GalleryLayout.ScaleToFit(2560, 1440, 1672, 941, 24) == 1d);
Check(GalleryUiRules.HeartCapacity(true) == 14);
Check(GalleryUiRules.HeartCapacity(false) == 10);
Check(GalleryUiRules.FilledHearts(2749, 10) == 10);
Check(GalleryUiRules.FilledHearts(249, 14) == 0);
Check(GalleryUiRules.DisplayName("Lenny", false, false) == "???");
Check(GalleryUiRules.DisplayName("Lenny", false, true) == "Lenny");
Check(GalleryUiRules.PreferredReplayRow(7, 4, 4) == 3);
Check(GalleryUiRules.PreferredReplayRow(8, 4, 4) == 0);
Check(!GalleryUiRules.ShouldCloseFromShortcut(shortcutPressed: true, searchSelected: true), "typing the gallery shortcut into search must not close the gallery");
Check(GalleryUiRules.ShouldCloseFromShortcut(shortcutPressed: true, searchSelected: false), "gallery shortcut still closes when search is not selected");

(int scroll, int slot) returnPos0 = GalleryUiRules.ResolveReturnPosition(0, 0, 6, 3, 21);
Check(returnPos0.scroll == 0 && returnPos0.slot == 0, "idx0 old0 -> scroll0 slot0");
(int scroll, int slot) returnPos20a = GalleryUiRules.ResolveReturnPosition(20, 0, 6, 3, 21);
Check(returnPos20a.scroll == 1 && returnPos20a.slot == 14, "idx20 old0 -> auto scroll to row showing 20");
(int scroll, int slot) returnPos20b = GalleryUiRules.ResolveReturnPosition(20, 1, 6, 3, 21);
Check(returnPos20b.scroll == 1 && returnPos20b.slot == 14, "idx20 old already visible -> keep old scroll");
(int scroll, int slot) returnPosNone = GalleryUiRules.ResolveReturnPosition(-1, 5, 6, 3, 21);
Check(returnPosNone.scroll == 1 && returnPosNone.slot == -1, "missing character -> clamp old scroll, no target slot");
(int scroll, int slot) returnPosShort = GalleryUiRules.ResolveReturnPosition(5, 9, 6, 3, 6);
Check(returnPosShort.scroll == 0 && returnPosShort.slot == 5, "filtered shrunk -> scroll clamped to 0");
(int scroll, int slot) returnPos18 = GalleryUiRules.ResolveReturnPosition(18, 0, 6, 3, 36);
Check(returnPos18.scroll == 1 && returnPos18.slot == 12, "idx18 old0 -> scroll to make row 3 visible, slot 12");
(int scroll, int slot) returnPosKeep = GalleryUiRules.ResolveReturnPosition(7, 1, 6, 3, 21);
Check(returnPosKeep.scroll == 1 && returnPosKeep.slot == 1, "idx7 old1 already visible -> keep old scroll, slot 1");

Check(!ReplayLifecycleRules.ShouldRestore(false, 999, 899, 900));
Check(ReplayLifecycleRules.ShouldRestore(false, 0, 900, 900));
Check(!ReplayLifecycleRules.ShouldRestore(true, 14, 5000, 900));
Check(ReplayLifecycleRules.ShouldRestore(true, 15, 16, 900));
Check(!ReplayLifecycleRules.CanFinishRestore(true, true, false, 10));
Check(!ReplayLifecycleRules.CanFinishRestore(true, false, true, 10));
Check(!ReplayLifecycleRules.CanFinishRestore(true, false, false, 1));
Check(ReplayLifecycleRules.CanFinishRestore(true, false, false, 2));
Check(!ReplayLifecycleRules.CanApplyRestore(true, false));
Check(!ReplayLifecycleRules.CanApplyRestore(false, true));
Check(ReplayLifecycleRules.CanApplyRestore(false, false));
Check(ReplayLifecycleRules.NextSpeed(1) == 2);
Check(ReplayLifecycleRules.NextSpeed(2) == 4);
Check(ReplayLifecycleRules.NextSpeed(4) == 1);
Check(!ReplayLifecycleRules.IsTransitionBlocking(0f, false, false, false));
Check(ReplayLifecycleRules.IsTransitionBlocking(.01f, false, false, false));
Check(ReplayLifecycleRules.IsTransitionBlocking(0f, false, false, true));
Check(ReplayLifecycleRules.BlocksReplaySpeed(true, false));
Check(!ReplayLifecycleRules.BlocksReplaySpeed(true, true));
object originalEvent = new();
Check(!ReplayLifecycleRules.IsSecondaryEvent(false, originalEvent, new object()));
Check(!ReplayLifecycleRules.IsSecondaryEvent(true, originalEvent, originalEvent));
Check(ReplayLifecycleRules.IsSecondaryEvent(true, originalEvent, new object()));

int splitArgsCalls = 0;
ConditionParser parser = new(
    _ => ["event.id", "Season Spring", "Time 1800 2200"],
    segment =>
    {
        splitArgsCalls++;
        return FakeSplitArgs(segment);
    });
ConditionSet allParsed = parser.ParseRawKey("ignored");
Check(allParsed.Conditions.Count == 2, "ParseRawKey must skip EventId");
Check(allParsed.Conditions[0] is SeasonCondition);
Check(allParsed.Conditions[1] is TimeCondition { Min: 1800, Max: 2200 });
Check(allParsed.Conditions.All(condition => condition is not OpaqueCondition), "no event.id Opaque");
Check(splitArgsCalls == 2, "injected splitArguments must be called");

ConditionParser parser2 = new(
    key => key.Split('/', StringSplitOptions.RemoveEmptyEntries),
    FakeSplitArgs);
ConditionParser parserSeg = new(_ => [], FakeSplitArgs);
ConditionSet set = parserSeg.Parse([]);
Check(set.Conditions.Count == 0);

ConditionSet parserParsed = parser2.Parse(
[
    "Season Spring", "!Season Winter", "DayOfMonth 12", "Year 2", "Time 1800 2200",
    "Weather Sun", "Friendship Haley 2500", "SawEvent 123", "LocalMail mail1", "HostMail mail2",
    "HostOrLocalMail mail3", "Dating Emily", "Spouse Alex", "Roommate", "DaysPlayed 15",
    "WorldState flag", "GameStateQuery WEATHER Here Sun", "UnknownToken token", "!Friendship Alex 1000"
]);
Check(parserParsed.Conditions.Count == 19);
ConditionSet negatedFriendship = parser2.Parse(["!Friendship Alex 1000"]);
Check(negatedFriendship.Conditions[0] is FriendshipCondition { Points: 1000, Negated: true, Scope: ConditionPlayerScope.LocalPlayer });
Check(parserParsed.Conditions[0] is SeasonCondition { Seasons.Count: 1 });
Check(parserParsed.Conditions[1] is SeasonCondition { Negated: true });
Check(parserParsed.Conditions[2] is DayOfMonthCondition { Days: [12] });
Check(parserParsed.Conditions[3] is YearCondition { Min: 2 });
Check(parserParsed.Conditions[4] is TimeCondition { Min: 1800, Max: 2200 });
Check(parserParsed.Conditions[5] is WeatherCondition { Weather: "Sun" });
Check(parserParsed.Conditions[6] is FriendshipCondition { Npc: "Haley", Points: 2500 });
Check(parserParsed.Conditions[7] is SawEventCondition { EventId: "123" });
Check(parserParsed.Conditions[8] is MailCondition { MailId: "mail1", Scope: ConditionPlayerScope.LocalPlayer });
Check(parserParsed.Conditions[9] is MailCondition { MailId: "mail2", Scope: ConditionPlayerScope.HostPlayer });
Check(parserParsed.Conditions[10] is MailCondition { MailId: "mail3", Scope: ConditionPlayerScope.HostOrLocal });
Check(parserParsed.Conditions[11] is DatingCondition { Npc: "Emily" });
Check(parserParsed.Conditions[12] is SpouseCondition { Npc: "Alex" });
Check(parserParsed.Conditions[13] is RoommateCondition);
Check(parserParsed.Conditions[14] is DaysPlayedCondition { Min: 15, Scope: ConditionPlayerScope.HostPlayer });
Check(parserParsed.Conditions[15] is WorldStateCondition { Id: "flag" });
Check(parserParsed.Conditions[16] is NativeQueryCondition { Query: "WEATHER Here Sun" });
Check(parserParsed.Conditions[16].Source == ConditionSource.GameStateQuery);
Check(parserParsed.Conditions[17] is OpaqueCondition);
Check(parserParsed.Conditions[17].RawSegment == "UnknownToken token");
Check(parserParsed.Conditions[18] is FriendshipCondition { Points: 1000, Negated: true });

ConditionSet malformed = parser2.Parse(["Season", "Time x", "Friendship Haley", "DayOfMonth 40", "DayOfMonth x"]);
Check(malformed.Conditions.All(condition => condition is OpaqueCondition));
Check(malformed.Conditions.All(condition => condition.RawSegment.Length > 0));
Check(malformed.Conditions[0].RawSegment == "Season");
Check(malformed.Conditions[1].RawSegment == "Time x");

ConditionSet conditions = parser2.Parse(["Season Spring", "!Season Winter", "Time 1800 2200", "Weather Sun", "Friendship Haley 2500", "SawEvent 123", "LocalMail letter", "HostMail hostLetter", "HostOrLocalMail either", "Dating Emily", "Spouse Alex", "Roommate", "DaysPlayed 15", "WorldState flag", "GameStateQuery SEASON Spring", "UnknownToken raw", "!Friendship Hayley 1000", "Season"]);
Check(conditions.Conditions.Count == 18);
ConditionEvaluator eval = new(query => query == "SEASON Spring");
ConditionEvaluationContext fullContext = new(
    Season: "Spring", DayOfMonth: 12, Year: 2, Time: 1900, Weather: "Sun",
    Friendship: new Dictionary<string, int> { ["Haley"] = 5000, ["Alex"] = 1000, ["Hayley"] = 1000 },
    EventsSeen: new HashSet<string> { "123" },
    LocalMail: new HashSet<string> { "letter" },
    HostMail: new HashSet<string> { "hostLetter" },
    HostOrLocalMail: new HashSet<string> { "either" },
    Dating: new HashSet<string> { "Emily" },
    Spouse: new HashSet<string> { "Alex" },
    Roommate: true, DaysPlayed: 15, WorldState: new HashSet<string> { "flag" });
foreach (ConditionExpression condition in conditions.Conditions)
{
    ConditionEvaluation result = eval.Evaluate(condition, fullContext);
    Check(result.Condition == condition);
    Check(result.Gap is not null);
    switch (condition)
    {
        case SeasonCondition { Negated: false }:
            Check(result.Truth == ConditionTruth.True && result.Knowledge == ConditionKnowledge.Known, "season");
            break;
        case SeasonCondition { Negated: true, Seasons: ["Winter"] }:
            Check(result.Truth == ConditionTruth.True && result.Knowledge == ConditionKnowledge.Known, "negated winter");
            break;
        case TimeCondition { Negated: false }:
            Check(result.Truth == ConditionTruth.True && result.Knowledge == ConditionKnowledge.Known, "time");
            break;
        case WeatherCondition { Negated: false }:
            Check(result.Truth == ConditionTruth.True && result.Knowledge == ConditionKnowledge.Known, "weather");
            break;
        case FriendshipCondition { Npc: "Haley", Negated: false }:
            Check(result.Truth == ConditionTruth.True && result.Knowledge == ConditionKnowledge.Known, "friendship haley");
            break;
        case SawEventCondition { Negated: false }:
            Check(result.Truth == ConditionTruth.True && result.Knowledge == ConditionKnowledge.Known, "sawevent");
            break;
        case MailCondition { MailId: "letter", Negated: false }:
            Check(result.Truth == ConditionTruth.True && result.Knowledge == ConditionKnowledge.Known, "local mail");
            break;
        case MailCondition { MailId: "hostLetter", Negated: false }:
            Check(result.Truth == ConditionTruth.True && result.Knowledge == ConditionKnowledge.Known, "host mail");
            break;
        case MailCondition { MailId: "either", Negated: false }:
            Check(result.Truth == ConditionTruth.True && result.Knowledge == ConditionKnowledge.Known, "hostorlocal mail");
            break;
        case DatingCondition { Negated: false }:
            Check(result.Truth == ConditionTruth.True && result.Knowledge == ConditionKnowledge.Known, "dating");
            break;
        case SpouseCondition { Negated: false }:
            Check(result.Truth == ConditionTruth.True && result.Knowledge == ConditionKnowledge.Known, "spouse");
            break;
        case RoommateCondition:
            Check(result.Truth == ConditionTruth.True && result.Knowledge == ConditionKnowledge.Known, "roommate");
            break;
        case DaysPlayedCondition { Negated: false }:
            Check(result.Truth == ConditionTruth.True && result.Knowledge == ConditionKnowledge.Known, "daysplayed");
            break;
        case WorldStateCondition { Negated: false }:
            Check(result.Truth == ConditionTruth.True && result.Knowledge == ConditionKnowledge.Known, "worldstate");
            break;
        case NativeQueryCondition { Negated: false }:
            Check(result.Truth == ConditionTruth.True && result.Knowledge == ConditionKnowledge.Known, "nativequery");
            break;
        case OpaqueCondition { RawSegment: "UnknownToken raw" }:
            Check(result.Truth == ConditionTruth.Unknown && result.Knowledge == ConditionKnowledge.Unsupported, "opaque unknown");
            break;
        case FriendshipCondition { Npc: "Hayley", Negated: true }:
            Check(result.Truth == ConditionTruth.False && result.Knowledge == ConditionKnowledge.Known, "negated hayley");
            break;
        case OpaqueCondition { RawSegment: "Season" }:
            Check(result.Truth == ConditionTruth.Unknown && result.Knowledge == ConditionKnowledge.Unsupported, "opaque season");
            break;
        default:
            throw new Exception("Unexpected condition type in loop: " + condition.GetType().Name);
    }
}

ConditionEvaluationContext missingContext = new(
    Season: null, DayOfMonth: null, Year: null, Time: null, Weather: null,
    Friendship: null, EventsSeen: null, LocalMail: null, HostMail: null, HostOrLocalMail: null,
    Dating: null, Spouse: null, Roommate: null, DaysPlayed: null, WorldState: null);
foreach (ConditionExpression condition in conditions.Conditions)
{
    if (condition is OpaqueCondition || condition is NativeQueryCondition)
        continue;
    ConditionEvaluation result = eval.Evaluate(condition, missingContext);
    Check(result.Truth == ConditionTruth.Unknown, "missing truth: " + condition.RawSegment);
    Check(result.Knowledge == ConditionKnowledge.MissingData, "missing knowledge: " + condition.RawSegment);
}

ConditionSet underrunSet = parser2.Parse(["Friendship Haley 2500"]);
ConditionEvaluationContext context1750 = fullContext with { Friendship = new Dictionary<string, int> { ["Haley"] = 1750 } };
ConditionEvaluation friendshipGap = eval.Evaluate(underrunSet.Conditions[0], context1750);
Check(friendshipGap.Truth == ConditionTruth.False && friendshipGap.Knowledge == ConditionKnowledge.Known);
Check(friendshipGap.Gap.Kind == ConditionGapKind.NumericGap && friendshipGap.Gap.Target == "2500" && friendshipGap.Gap.Current == "1750");

ConditionEvaluationContext contextTime = fullContext with { Time = 1420 };
ConditionSet timeSet = parser2.Parse(["Time 1800 2200"]);
ConditionEvaluation timeGap = eval.Evaluate(timeSet.Conditions[0], contextTime);
Check(timeGap.Truth == ConditionTruth.False);
Check(timeGap.Gap.Kind == ConditionGapKind.RequiredRange);

ConditionEvaluationContext contextMissingNpc = fullContext with { Friendship = new Dictionary<string, int>() };
ConditionEvaluation friendshipMissing = eval.Evaluate(underrunSet.Conditions[0], contextMissingNpc);
Check(friendshipMissing.Truth == ConditionTruth.Unknown && friendshipMissing.Knowledge == ConditionKnowledge.MissingData);

ConditionSet seenSet = parser2.Parse(["SawEvent 123"]);
ConditionEvaluationContext contextSeen = fullContext with { EventsSeen = new HashSet<string>() };
Check(eval.Evaluate(seenSet.Conditions[0], contextSeen).Truth == ConditionTruth.False);
Check(eval.Evaluate(seenSet.Conditions[0], contextSeen).Gap.Kind == ConditionGapKind.MissingState);

ConditionSet mailSet = parser2.Parse(["LocalMail letter"]);
ConditionEvaluationContext contextMail = fullContext with { LocalMail = new HashSet<string>() };
Check(eval.Evaluate(mailSet.Conditions[0], contextMail).Truth == ConditionTruth.False);
Check(eval.Evaluate(mailSet.Conditions[0], contextMail).Gap.Kind == ConditionGapKind.MissingState);

int nativeCalls = 0;
ConditionEvaluator nativeEval = new(query => { nativeCalls++; return query == "SEASON Spring"; });
ConditionSet nativeSet = parser2.Parse(["GameStateQuery SEASON Spring"]);
Check(nativeEval.Evaluate(nativeSet.Conditions[0], missingContext).Truth == ConditionTruth.True);
Check(nativeEval.Evaluate(nativeSet.Conditions[0], missingContext).Knowledge == ConditionKnowledge.Known);
Check(nativeCalls == 2);
ConditionEvaluator throwingNative = new(_ => throw new InvalidOperationException("expected"));
Check(throwingNative.Evaluate(nativeSet.Conditions[0], missingContext).Truth == ConditionTruth.Unknown);
Check(throwingNative.Evaluate(nativeSet.Conditions[0], missingContext).Knowledge == ConditionKnowledge.Error);
ConditionEvaluator noNative = new();
Check(noNative.Evaluate(nativeSet.Conditions[0], missingContext).Truth == ConditionTruth.Unknown);
Check(noNative.Evaluate(nativeSet.Conditions[0], missingContext).Knowledge == ConditionKnowledge.MissingData);

ConditionSet worldSet = parser2.Parse(["WorldState flag"]);
ConditionEvaluation worldMissing = eval.Evaluate(worldSet.Conditions[0], missingContext);
Check(worldMissing.Truth == ConditionTruth.Unknown && worldMissing.Knowledge == ConditionKnowledge.MissingData);

ConditionSet unknownSet = parser2.Parse(["SomethingElse value"]);
ConditionEvaluation opaqueEval = eval.Evaluate(unknownSet.Conditions[0], fullContext);
Check(opaqueEval.Truth == ConditionTruth.Unknown && opaqueEval.Knowledge == ConditionKnowledge.Unsupported);
Check(opaqueEval.Condition.RawSegment == "SomethingElse value");

ConditionSet negatedSet = parser2.Parse(["!Friendship Hayley 1000"]);
ConditionEvaluationContext context1000 = fullContext with { Friendship = new Dictionary<string, int> { ["Hayley"] = 1000 } };
ConditionEvaluation negatedEval = eval.Evaluate(negatedSet.Conditions[0], context1000);
Check(negatedEval.Truth == ConditionTruth.False);
ConditionEvaluationContext context999 = fullContext with { Friendship = new Dictionary<string, int> { ["Hayley"] = 999 } };
Check(eval.Evaluate(negatedSet.Conditions[0], context999).Truth == ConditionTruth.True);

ReadableCondition readable = ConditionDescriber.Describe(underrunSet.Conditions[0]);
Check(readable.LocalizationKey == "condition.hearts");
Check(readable.Arguments["npc"] == "Haley");
Check(readable.Arguments["points"] == "2500");
Check(readable.Arguments["hearts"] == "10");
ReadableCondition opaqueReadable = ConditionDescriber.Describe(unknownSet.Conditions[0]);
Check(opaqueReadable.LocalizationKey is null);
Check(opaqueReadable.RawFallback == "SomethingElse value");
Check(opaqueReadable.Arguments.Count == 0);
ReadableCondition seasonReadable = ConditionDescriber.Describe(parser2.Parse(["Season Winter"]).Conditions[0]);
Check(seasonReadable.LocalizationKey == "condition.season" && seasonReadable.Arguments["seasons"] == "Winter");
ReadableCondition conditionReadable = ConditionDescriber.Describe(parser2.Parse(["SawEvent 123"]).Conditions[0]);
Check(conditionReadable.LocalizationKey == "condition.seen" && conditionReadable.Arguments["id"] == "123");

ConditionParser aliasParser = new(_ => [], FakeSplitArgs);
Check(aliasParser.ParseSegment("f Haley 1000") is FriendshipCondition { Npc: "Haley", Points: 1000, Negated: false });
Check(aliasParser.ParseSegment("e 123") is SawEventCondition { EventId: "123", Negated: false });
Check(aliasParser.ParseSegment("k 123") is SawEventCondition { EventId: "123", Negated: true });
Check(aliasParser.ParseSegment("n letter") is MailCondition { MailId: "letter", Negated: false, Scope: ConditionPlayerScope.LocalPlayer });
Check(aliasParser.ParseSegment("l letter") is MailCondition { MailId: "letter", Negated: true, Scope: ConditionPlayerScope.LocalPlayer });
Check(aliasParser.ParseSegment("t 1800 2200") is TimeCondition { Min: 1800, Max: 2200 });
Check(aliasParser.ParseSegment("w Sun") is WeatherCondition { Weather: "Sun" });
Check(aliasParser.ParseSegment("y 2") is YearCondition { Min: 2 });
Check(aliasParser.ParseSegment("u 12") is DayOfMonthCondition { Days: [12] });
Check(aliasParser.ParseSegment("z Winter") is SeasonCondition { Seasons: ["Winter"], Negated: true });
Check(aliasParser.ParseSegment("j 15") is DaysPlayedCondition { Min: 15, Negated: false });
Check(aliasParser.ParseSegment("D Emily") is DatingCondition { Npc: "Emily" });
Check(aliasParser.ParseSegment("O Alex") is SpouseCondition { Npc: "Alex", Negated: false });
Check(aliasParser.ParseSegment("o Alex") is SpouseCondition { Npc: "Alex", Negated: true });
Check(aliasParser.ParseSegment("R") is RoommateCondition);
Check(aliasParser.ParseSegment("G SEASON Spring") is NativeQueryCondition { Query: "SEASON Spring" });
Check(aliasParser.ParseSegment("season spring") is SeasonCondition { Seasons: ["spring"] });
Check(aliasParser.ParseSegment("friendship haley 1000") is FriendshipCondition { Npc: "haley", Points: 1000 });
Check(aliasParser.ParseSegment("sawEvent 123") is SawEventCondition { EventId: "123" });
Check(aliasParser.ParseSegment("Spouse Alex") is SpouseCondition { Npc: "Alex", Negated: false });
Check(aliasParser.ParseSegment("ROOMMATE") is RoommateCondition);
Check(aliasParser.ParseSegment("localmall letter") is OpaqueCondition);
Check(aliasParser.ParseSegment("Season") is OpaqueCondition);
Check(aliasParser.ParseSegment("!f Alex 1000") is FriendshipCondition { Negated: true });
Check(aliasParser.ParseSegment("!k 123") is SawEventCondition { EventId: "123", Negated: false });
Check(aliasParser.ParseSegment("!!k 123") is SawEventCondition { EventId: "123", Negated: true });
Check(aliasParser.ParseSegment("F 123 1000") is OpaqueCondition);
Check(aliasParser.ParseSegment("e 123 456") is OpaqueCondition);
Check(aliasParser.ParseSegment("f Haley 2500 Abigail 1000") is OpaqueCondition);

int quoteSplitCalls = 0;
ConditionParser quoteParser = new(_ => [], segment => { quoteSplitCalls++; return FakeSplitArgs(segment); });
ConditionExpression quotedGsq = quoteParser.ParseSegment("GameStateQuery \"SEASON Spring\"");
Check(quoteSplitCalls == 1, "quote parser must invoke injected splitArguments");
Check(quotedGsq is NativeQueryCondition { Query: "SEASON Spring" }, "quoted query unquoted");
Check(quotedGsq.RawSegment == "GameStateQuery \"SEASON Spring\"", "raw segment preserved verbatim");

Check(aliasParser.ParseSegment("Time 1800") is OpaqueCondition, "Time missing max");
Check(aliasParser.ParseSegment("Time 1800 2200 2300") is OpaqueCondition, "Time extra arg");
Check(aliasParser.ParseSegment("Time 1800 x") is OpaqueCondition, "Time invalid max");
Check(aliasParser.ParseSegment("DaysPlayed 15 20") is OpaqueCondition, "DaysPlayed extra arg");
Check(aliasParser.ParseSegment("LocalMail a b") is OpaqueCondition, "Mail extra arg");
Check(aliasParser.ParseSegment("HostMail a b") is OpaqueCondition, "HostMail extra arg");
Check(aliasParser.ParseSegment("HostOrLocalMail a b") is OpaqueCondition, "HostOrLocalMail extra arg");
Check(aliasParser.ParseSegment("Weather Sun Rain") is OpaqueCondition, "Weather extra arg");
Check(aliasParser.ParseSegment("Year 2 3") is OpaqueCondition, "Year extra arg");
Check(aliasParser.ParseSegment("Dating Emily Sam") is OpaqueCondition, "Dating extra arg");
Check(aliasParser.ParseSegment("Spouse Alex Emily") is OpaqueCondition, "Spouse extra arg");
Check(aliasParser.ParseSegment("Roommate extra") is OpaqueCondition, "Roommate extra arg");
Check(aliasParser.ParseSegment("WorldState a b") is OpaqueCondition, "WorldState extra arg");

Check(aliasParser.ParseSegment("NotSeason Winter") is SeasonCondition { Seasons: ["Winter"], Negated: true }, "NotSeason");
Check(aliasParser.ParseSegment("!NotSeason Winter") is SeasonCondition { Seasons: ["Winter"], Negated: false }, "!NotSeason");
Check(aliasParser.ParseSegment("NotSawEvent 123") is SawEventCondition { EventId: "123", Negated: true }, "NotSawEvent");
Check(aliasParser.ParseSegment("!NotSawEvent 123") is SawEventCondition { EventId: "123", Negated: false }, "!NotSawEvent");
Check(aliasParser.ParseSegment("NotLocalMail letter") is MailCondition { MailId: "letter", Negated: true, Scope: ConditionPlayerScope.LocalPlayer }, "NotLocalMail");
Check(aliasParser.ParseSegment("!NotLocalMail letter") is MailCondition { MailId: "letter", Negated: false, Scope: ConditionPlayerScope.LocalPlayer }, "!NotLocalMail");
Check(aliasParser.ParseSegment("NotSpouse Alex") is SpouseCondition { Npc: "Alex", Negated: true }, "NotSpouse");
Check(aliasParser.ParseSegment("!NotSpouse Alex") is SpouseCondition { Npc: "Alex", Negated: false }, "!NotSpouse");
Check(aliasParser.ParseSegment("NotHostMail mail") is MailCondition { MailId: "mail", Negated: true, Scope: ConditionPlayerScope.HostPlayer }, "NotHostMail");
Check(aliasParser.ParseSegment("NotHostOrLocalMail mail") is MailCondition { MailId: "mail", Negated: true, Scope: ConditionPlayerScope.HostOrLocal }, "NotHostOrLocalMail");
Check(aliasParser.ParseSegment("NotRoommate") is RoommateCondition { Negated: true }, "NotRoommate");
Check(aliasParser.ParseSegment("!NotRoommate") is RoommateCondition { Negated: false }, "!NotRoommate");

Check(aliasParser.ParseSegment("NotSeason") is OpaqueCondition, "NotSeason missing arg");
Check(aliasParser.ParseSegment("NotSawEvent 1 2") is OpaqueCondition, "NotSawEvent extra arg");
Check(aliasParser.ParseSegment("NotLocalMail a b") is OpaqueCondition, "NotLocalMail extra arg");
Check(aliasParser.ParseSegment("NotSpouse A B") is OpaqueCondition, "NotSpouse extra arg");

int emptySplitCalls = 0;
ConditionParser emptySplitParser = new(_ => [], _ => { emptySplitCalls++; return []; });
ConditionExpression emptySplitResult = emptySplitParser.ParseSegment("AnythingAtAll");
Check(emptySplitCalls == 1, "empty splitArguments must be invoked");
Check(emptySplitResult is OpaqueCondition, "empty splitArguments yields Opaque");
Check(emptySplitResult.RawSegment == "AnythingAtAll", "empty splitArguments preserves raw segment");

Check(aliasParser.ParseSegment("NotSeason Winter").RawSegment == "NotSeason Winter", "NotSeason raw preserved");
Check(aliasParser.ParseSegment("z Winter") is SeasonCondition { Negated: true }, "z alias parity");
Check(aliasParser.ParseSegment("Season Winter") is SeasonCondition { Negated: false }, "Season parity");
Check(aliasParser.ParseSegment("NotSeason Winter") is SeasonCondition { Negated: true }, "NotSeason == z parity");

ConditionSet aliasSet = parser2.Parse(["f Haley 2500", "e 123", "k 456"]);
Check(aliasSet.Conditions[0] is FriendshipCondition { Npc: "Haley", Points: 2500 });
Check(aliasSet.Conditions[1] is SawEventCondition { EventId: "123", Negated: false });
Check(aliasSet.Conditions[2] is SawEventCondition { EventId: "456", Negated: true });

ConditionSet negatedSeenSet = parser2.Parse(["!SawEvent 123"]);
ConditionEvaluationContext alreadySeen = fullContext with { EventsSeen = new HashSet<string> { "123" } };
ConditionEvaluation negatedSeenEval = eval.Evaluate(negatedSeenSet.Conditions[0], alreadySeen);
Check(negatedSeenEval.Truth == ConditionTruth.False && negatedSeenEval.Gap.Kind == ConditionGapKind.OverState);
ConditionEvaluation negatedSeenSatisfied = eval.Evaluate(negatedSeenSet.Conditions[0], alreadySeen with { EventsSeen = new HashSet<string>() });
Check(negatedSeenSatisfied.Truth == ConditionTruth.True && negatedSeenSatisfied.Gap.Kind == ConditionGapKind.None);

ConditionSet negatedMailSet = parser2.Parse(["!LocalMail letter"]);
ConditionEvaluationContext alreadyMail = fullContext with { LocalMail = new HashSet<string> { "letter" } };
Check(eval.Evaluate(negatedMailSet.Conditions[0], alreadyMail).Gap.Kind == ConditionGapKind.OverState);

ReadableCondition negatedReadable = ConditionDescriber.Describe(negatedSeenSet.Conditions[0]);
Check(negatedReadable.Negated == true);
ReadableCondition daysReadable = ConditionDescriber.Describe(parser2.Parse(["DaysPlayed 15"]).Conditions[0]);
Check(daysReadable.LocalizationKey == "condition.daysplayed" && daysReadable.Arguments["min"] == "15");

ConditionSet year1Set = parser2.Parse(["Year 1"]);
Check(eval.Evaluate(year1Set.Conditions[0], fullContext with { Year = 1 }).Truth == ConditionTruth.True, "Year 1 + current 1");
Check(eval.Evaluate(year1Set.Conditions[0], fullContext with { Year = 2 }).Truth == ConditionTruth.False, "Year 1 + current 2");
ConditionSet year2Set = parser2.Parse(["Year 2"]);
Check(eval.Evaluate(year2Set.Conditions[0], fullContext with { Year = 1 }).Truth == ConditionTruth.False, "Year 2 + current 1");
Check(eval.Evaluate(year2Set.Conditions[0], fullContext with { Year = 2 }).Truth == ConditionTruth.True, "Year 2 + current 2");
Check(eval.Evaluate(year2Set.Conditions[0], fullContext with { Year = 3 }).Truth == ConditionTruth.True, "Year 2 + current 3");

ReadableCondition datingReadable = ConditionDescriber.Describe(parser2.Parse(["Dating Emily"]).Conditions[0]);
Check(datingReadable.LocalizationKey is null, "Dating must not map to condition.present");
Check(datingReadable.RawFallback == "Dating Emily");
ReadableCondition spouseReadable = ConditionDescriber.Describe(parser2.Parse(["Spouse Alex"]).Conditions[0]);
Check(spouseReadable.LocalizationKey is null, "Spouse must not map to condition.present");
Check(spouseReadable.RawFallback == "Spouse Alex");
ReadableCondition roommateReadable = ConditionDescriber.Describe(parser2.Parse(["Roommate"]).Conditions[0]);
Check(roommateReadable.LocalizationKey is null, "Roommate must be raw fallback");

Check(ReplayBackupRetention.Retain([]).Count == 0, "retention 0 stale keep 0");
Check(ReplayBackupRetention.Retain(["A"]).SequenceEqual(["A"]), "retention 1 stale keep 1");
Check(ReplayBackupRetention.Retain(["A", "B"]).Count == 2, "retention 2 stale keep 2");
Check(ReplayBackupRetention.Retain(["D", "C", "B", "A"]).SequenceEqual(["D", "C"]), "retention 4 stale keep newest 2");
Check(ReplayBackupRetention.Retain(["J", "I", "H", "G", "F", "E", "D", "C", "B", "A"]).SequenceEqual(["J", "I"]), "retention 10 stale keep newest 2");
Check(ReplayBackupRetention.Discard(["D", "C", "B", "A"]).SequenceEqual(["B", "A"]), "retention discard old 2");

// ---------- Phase 6 exact-script launch contract ----------
EventIdentity p6Id = new("Data/Events/Town", "123");
EventFragments p6Frag = new([], []);
ResolvedEvent p6Resolved = new(
    p6Id, "Town", "123/A", "ScriptA", p6Frag,
    EventHashes.RootDefinition("123/A", "ScriptA"), EventHashes.RootScript("ScriptA"));
EventPlayback p6Current = EventPlayback.ForCurrent(p6Resolved);
Check(p6Current.Identity == p6Id, "P6-A current identity");
Check(p6Current.LocationName == "Town", "P6-A current location");
Check(p6Current.RootScript == "ScriptA", "P6-A current root script = resolved.ResolvedScript");
Check(p6Current.AssetName == "Data/Events/Town", "P6-A asset name");
Check(p6Current.EventId == "123", "P6-A event id");

// P6-B: launcher uses selected ResolvedEvent script, never re-resolves by EventId.
// Here the selected resolved event is "123/A"->"ScriptA"; a distinct same-EventId candidate
// ("123/B"->"ScriptB") exists and is NOT consumed by EventPlayback.ForCurrent.
ResolvedEvent p6CandidateB = new(
    p6Id, "Town", "123/B", "ScriptB", p6Frag,
    EventHashes.RootDefinition("123/B", "ScriptB"), EventHashes.RootScript("ScriptB"));
Check(p6CandidateB.EventId == p6Current.EventId, "P6-B same EventId candidate");
Check(p6Current.RootScript == "ScriptA", "P6-B selection independence: chosen script is selected one, not EventId re-resolution");
Check(p6Current.RootScript != p6CandidateB.ResolvedScript, "P6-B candidate B script not used by launcher");

WatchedEventSnapshot p6Hist = new(
    "HistoricalTown", "Data/Events/Town", "123", "123/A", "HistoricalRoot",
    new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase),
    new Dictionary<string, string>(StringComparer.Ordinal), "zh", "fp", DateTimeOffset.Now, DateTimeOffset.Now);
EventPlayback p6Historical = EventPlayback.ForHistorical(p6Hist);
Check(p6Historical.RootScript == "HistoricalRoot", "P6-C historical root script = snapshot.RootScript");
Check(p6Historical.EventId == "123" && p6Historical.AssetName == "Data/Events/Town", "P6-C historical identity");
Check(p6Historical.LocationName == "HistoricalTown", "P6-C historical snapshot LocationName is the sole launch target (independent of current entry location)");
Check(p6Current.LocationName == "Town", "P6-C current entry location retained for its own playback");
Check(p6Current is EventPlayback && p6Historical is EventPlayback, "P6-D both current/historical produce EventPlayback");

// ---------- Phase 7B historical execution trace contract ----------
string p7PlaybackHash = FullHash('A');
string p7RootCommandsHash = HistoricalExecutionContextRules.HashCommandList(["question fork1 q#a#b", "fork branch"]);
ScriptSourceIdentity p7RootSource = new(ScriptSourceKind.RootPlayback, null, null);
ScriptSegmentIdentity p7Root = new(
    ScriptSegmentKind.Root,
    HistoricalExecutionContextRules.HashRootPath(p7PlaybackHash, p7RootCommandsHash),
    p7RootCommandsHash,
    p7RootSource,
    null);
DecisionLocator p7ChoiceLocator = new(
    p7Root,
    ExecutionDecisionKind.NativeQuestion,
    HistoricalExecutionContextRules.HashCommand("question fork1 q#a#b"),
    0,
    0);
List<ReplayResponseOption> p7Options =
[
    new(ResponseIdentityKind.GeneratedOrdinal, "0", FullHash('5')),
    new(ResponseIdentityKind.GeneratedOrdinal, "1", FullHash('6'))
];
string p7OptionSetHash = HistoricalExecutionContextRules.HashOptionSet(p7Options);
ResponseIdentity p7Response = new(
    ResponseIdentityKind.GeneratedOrdinal,
    "1",
    1,
    2,
    p7OptionSetHash,
    FullHash('6'),
    "zh");
PlayerChoiceDecision p7Choice = new(0, p7ChoiceLocator, p7Response);
SegmentEntryIdentity p7Entry = new(
    p7Root.PathHash,
    HistoricalExecutionContextRules.HashCommand("fork branch"),
    1,
    0,
    "branch");
ScriptSourceIdentity p7BranchSource = new(ScriptSourceKind.EventAssetEntry, "Data/Events/HaleyHouse", "branch");
string p7BranchCommandsHash = HistoricalExecutionContextRules.HashCommandList(["speak Sophia hi", "end"]);
ScriptSegmentIdentity p7Branch = new(
    ScriptSegmentKind.ForkReplacement,
    HistoricalExecutionContextRules.HashChildPath(p7PlaybackHash, ScriptSegmentKind.ForkReplacement, p7BranchSource, p7Entry, p7BranchCommandsHash),
    p7BranchCommandsHash,
    p7BranchSource,
    p7Entry);
DecisionLocator p7ForkLocator = new(
    p7Root,
    ExecutionDecisionKind.Fork,
    p7Entry.CommandHash,
    1,
    0);
AutomaticDecision p7Fork = new(
    1,
    p7ForkLocator,
    AutomaticDecisionCausality.PlayerChoiceDerived,
    0,
    new AutomaticDecisionResult(AutomaticDecisionOutcome.ReplaceCommands, "branch", null, p7Branch));
ExecutionTraceCoverageSummary p7FullCoverage = new(
    ExecutionTraceCoverage.Complete,
    ExecutionTraceCoverage.Complete,
    OpaqueDecisionCoverage.NoneObserved);
HistoricalExecutionContext p7Context = new(
    HistoricalExecutionContextRules.CurrentSchemaVersion,
    p7PlaybackHash,
    ExecutionTraceCompletion.Complete,
    ExecutionTraceEndReason.NaturalComplete,
    p7FullCoverage,
    new ExecutionTraceProvenance("1.6.15.24356", "1.0.0", "zh"),
    [p7Fork],
    [p7Choice],
    []);

Check(HistoricalExecutionContextRules.TryValidate(p7Context, out _), "P7B valid context");
Check(HistoricalExecutionContextCodec.TryEncode(p7Context, out string p7Json), "P7B context encodes");
HistoricalExecutionContextLoad p7Roundtrip = HistoricalExecutionContextCodec.Decode(p7Json, p7PlaybackHash);
Check(p7Roundtrip.State == HistoricalExecutionContextState.Complete && p7Roundtrip.Context is not null, "P7B roundtrip complete");
HistoricalExecutionContext p7Loaded = p7Roundtrip.Context ?? throw new Exception("P7B roundtrip context missing");
Check(p7Loaded.AutomaticDecisions[0].Result.ReplacementSegment == p7Branch, "P7B roundtrip nested segment");
Check(p7Loaded.PlayerChoices[0].Response.NativeKey == "1", "P7B roundtrip response");
Check(p7Loaded.Equals(p7Context) && p7Loaded.GetHashCode() == p7Context.GetHashCode(), "P7B deep value equality");
Check(p7Json.Contains(p7PlaybackHash, StringComparison.Ordinal) && p7Json.Contains(p7Branch.PathHash, StringComparison.Ordinal), "P7B full hashes persisted");

HistoricalExecutionContextLoad p7Missing = HistoricalExecutionContextCodec.Decode(null, p7PlaybackHash);
Check(p7Missing.State == HistoricalExecutionContextState.Missing && p7Missing.Context is null, "P7B Missing is explicit");
HistoricalExecutionContext p7Empty = CopyExecutionContext(
    p7Context,
    completion: ExecutionTraceCompletion.EmptyComplete,
    automaticDecisions: [],
    playerChoices: []);
Check(HistoricalExecutionContextCodec.TryEncode(p7Empty, out string p7EmptyJson), "P7B EmptyComplete encodes");
Check(HistoricalExecutionContextCodec.Decode(p7EmptyJson, p7PlaybackHash).State == HistoricalExecutionContextState.EmptyComplete, "P7B Missing != EmptyComplete");

HistoricalExecutionContext p7Partial = CopyExecutionContext(
    p7Context,
    completion: ExecutionTraceCompletion.Partial,
    endReason: ExecutionTraceEndReason.NaturalComplete,
    coverage: p7FullCoverage with { AutomaticDecisions = ExecutionTraceCoverage.Incomplete });
Check(HistoricalExecutionContextRules.TryValidate(p7Partial, out _), "P7B Partial is structurally valid");
Check(HistoricalExecutionContextRules.GetState(p7Partial) == HistoricalExecutionContextState.Partial, "P7B Partial != Complete");
Check(HistoricalExecutionContextRules.GetCapability(p7Partial, p7PlaybackHash) == HistoricalReplayCapability.ContentOnly, "P7B Partial is content-only");

Check(HistoricalExecutionContextCodec.Decode("{not-json", p7PlaybackHash).InvalidReason == ExecutionContextInvalidReason.MalformedPayload, "P7B malformed payload degrades");
Check(HistoricalExecutionContextCodec.Decode("[]", p7PlaybackHash).InvalidReason == ExecutionContextInvalidReason.MalformedPayload, "P7B non-object payload degrades");
Check(HistoricalExecutionContextCodec.Decode("{\"schemaVersion\":\"1\"}", p7PlaybackHash).InvalidReason == ExecutionContextInvalidReason.MalformedPayload, "P7B non-numeric schema degrades");
string p7FutureJson = p7Json.Replace("\"schemaVersion\":1", "\"schemaVersion\":99", StringComparison.Ordinal);
Check(HistoricalExecutionContextCodec.Decode(p7FutureJson, p7PlaybackHash).InvalidReason == ExecutionContextInvalidReason.FutureSchema, "P7B future schema degrades");
Check(HistoricalExecutionContextCodec.Decode(p7Json, FullHash('B')).InvalidReason == ExecutionContextInvalidReason.PlaybackMismatch, "P7B binding mismatch rejected");
string p7NumericEnumJson = p7Json.Replace("\"completion\":\"Complete\"", "\"completion\":999", StringComparison.Ordinal);
Check(HistoricalExecutionContextCodec.Decode(p7NumericEnumJson, p7PlaybackHash).InvalidReason == ExecutionContextInvalidReason.MalformedPayload, "P7B numeric enum rejected");
HistoricalExecutionContext p7UndefinedEnum = CopyExecutionContext(p7Context, completion: (ExecutionTraceCompletion)999);
Check(!HistoricalExecutionContextRules.TryValidate(p7UndefinedEnum, out _), "P7B undefined enum rejected");
HistoricalExecutionContext p7ReboundWithoutSegments = CopyExecutionContext(p7Context, playbackHash: FullHash('B'));
Check(!HistoricalExecutionContextRules.TryValidate(p7ReboundWithoutSegments, out _), "P7B segment paths bind playback hash");

Check(p7ChoiceLocator == p7ChoiceLocator with { }, "P7B DecisionLocator equality");
Check(p7ChoiceLocator != p7ChoiceLocator with { Occurrence = 1 }, "P7B repeated command occurrence differs");
Check(p7ChoiceLocator != p7ChoiceLocator with { CommandOrdinal = 1 }, "P7B duplicate command ordinal differs");
Check(p7Root == p7Root with { }, "P7B ScriptSegmentIdentity equality");
Check(p7Root != p7Branch, "P7B child segment identity differs");

HistoricalExecutionContext p7Gap = CopyExecutionContext(p7Context, automaticDecisions: [p7Fork with { Sequence = 2 }]);
Check(!HistoricalExecutionContextRules.TryValidate(p7Gap, out _), "P7B sequence gap rejected");
HistoricalExecutionContext p7BadCause = CopyExecutionContext(p7Context, automaticDecisions: [p7Fork with { CausedByPlayerChoiceSequence = 2 }]);
Check(!HistoricalExecutionContextRules.TryValidate(p7BadCause, out _), "P7B future choice cause rejected");
HistoricalExecutionContext p7BadResult = CopyExecutionContext(
    p7Context,
    automaticDecisions: [p7Fork with
    {
        Result = new AutomaticDecisionResult(AutomaticDecisionOutcome.ReplaceCommands, "branch", null, null)
    }]);
Check(!HistoricalExecutionContextRules.TryValidate(p7BadResult, out _), "P7B malformed automatic result rejected");
ScriptSegmentIdentity p7WrongEntryBranch = p7Branch with
{
    EnteredBy = p7Entry with { CommandOrdinal = 99 }
};
p7WrongEntryBranch = p7WrongEntryBranch with
{
    PathHash = HistoricalExecutionContextRules.HashChildPath(
        p7PlaybackHash, p7WrongEntryBranch.Kind, p7WrongEntryBranch.Source,
        p7WrongEntryBranch.EnteredBy!, p7WrongEntryBranch.CommandListHash)
};
HistoricalExecutionContext p7WrongEntry = CopyExecutionContext(
    p7Context,
    automaticDecisions: [p7Fork with
    {
        Result = p7Fork.Result with { ReplacementSegment = p7WrongEntryBranch }
    }]);
Check(!HistoricalExecutionContextRules.TryValidate(p7WrongEntry, out _), "P7B replacement entry must match decision locator");

List<ReplayResponseOption> p7AuthoredOptions =
[
    new(ResponseIdentityKind.AuthoredKey, "Olivia_event5", FullHash('5')),
    new(ResponseIdentityKind.AuthoredKey, "Olivia_event6", FullHash('6'))
];
ResponseIdentity p7AuthoredResponse = new(
    ResponseIdentityKind.AuthoredKey,
    "Olivia_event5",
    0,
    2,
    FullHash('F'),
    FullHash('5'),
    "zh");
ResponseMatchResult p7AuthoredMatch = HistoricalExecutionContextRules.MatchResponse(
    p7AuthoredResponse, p7AuthoredOptions, "en");
Check(p7AuthoredMatch is { Matched: true, Index: 0, Kind: ResponseMatchKind.AuthoredKey }, "P7B authored key has priority");
ResponseMatchResult p7IndexMatch = HistoricalExecutionContextRules.MatchResponse(p7Response, p7Options, "en");
Check(p7IndexMatch is { Matched: true, Index: 1, Kind: ResponseMatchKind.OptionSetAndIndex }, "P7B option-set guarded index");
ResponseIdentity p7TextFallback = p7Response with { OptionSetHash = FullHash('F'), SelectedTextHash = FullHash('5') };
ResponseMatchResult p7TextMatch = HistoricalExecutionContextRules.MatchResponse(p7TextFallback, p7Options, "zh");
Check(p7TextMatch is { Matched: true, Index: 0, Kind: ResponseMatchKind.SameLocaleText }, "P7B same-locale text fallback");
Check(!HistoricalExecutionContextRules.MatchResponse(p7TextFallback, p7Options, "en").Matched, "P7B cross-locale text fallback rejected");
List<ReplayResponseOption> p7AmbiguousText =
[
    new(ResponseIdentityKind.IndexOnly, null, FullHash('5')),
    new(ResponseIdentityKind.IndexOnly, null, FullHash('5'))
];
Check(!HistoricalExecutionContextRules.MatchResponse(p7TextFallback, p7AmbiguousText, "zh").Matched, "P7B ambiguous text rejected");

Check(HistoricalExecutionContextRules.GetCapability(null, p7PlaybackHash) == HistoricalReplayCapability.ContentOnly, "P7B legacy capability");
Check(HistoricalExecutionContextRules.GetCapability(p7Context, p7PlaybackHash) == HistoricalReplayCapability.ExactCapable, "P7B exact capability");
AutomaticDecision p7AutonomousFork = p7Fork with
{
    Sequence = 0,
    Causality = AutomaticDecisionCausality.Autonomous,
    CausedByPlayerChoiceSequence = null
};
HistoricalExecutionContext p7AutomaticOnly = CopyExecutionContext(
    p7Context,
    coverage: p7FullCoverage with { PlayerChoices = ExecutionTraceCoverage.NotCaptured },
    automaticDecisions: [p7AutonomousFork],
    playerChoices: []);
Check(HistoricalExecutionContextRules.GetCapability(p7AutomaticOnly, p7PlaybackHash) == HistoricalReplayCapability.OutcomeAware, "P7B automatic-only capability");
Check(HistoricalExecutionContextRules.GetCapability(p7Empty, p7PlaybackHash) == HistoricalReplayCapability.ExactCapable, "P7B EmptyComplete exact capability");
HistoricalExecutionContext p7AutomaticEmpty = CopyExecutionContext(
    p7Empty,
    coverage: p7FullCoverage with { PlayerChoices = ExecutionTraceCoverage.NotCaptured });
Check(HistoricalExecutionContextRules.TryValidate(p7AutomaticEmpty, out _), "P7C automatic-only EmptyComplete validates");
Check(HistoricalExecutionContextRules.GetCapability(p7AutomaticEmpty, p7PlaybackHash) == HistoricalReplayCapability.OutcomeAware, "P7C automatic-only EmptyComplete is outcome-aware");
HistoricalExecutionContext p7Unknown = CopyExecutionContext(
    p7Context,
    automaticDecisions: [p7Fork with
    {
        Causality = AutomaticDecisionCausality.Unknown,
        CausedByPlayerChoiceSequence = null
    }]);
Check(HistoricalExecutionContextRules.TryValidate(p7Unknown, out _), "P7B unknown decision remains parseable");
Check(HistoricalExecutionContextRules.GetCapability(p7Unknown, p7PlaybackHash) == HistoricalReplayCapability.ContentOnly, "P7B unknown decision prevents fidelity claim");
HistoricalExecutionContext p7Opaque = CopyExecutionContext(
    p7Context,
    coverage: p7FullCoverage with { OpaqueDecisions = OpaqueDecisionCoverage.UnsupportedObserved });
Check(HistoricalExecutionContextRules.GetCapability(p7Opaque, p7PlaybackHash) == HistoricalReplayCapability.ContentOnly, "P7B opaque behavior prevents fidelity claim");
HistoricalExecutionContext p7OpaqueLocator = CopyExecutionContext(
    p7Context,
    automaticDecisions: [p7Fork with
    {
        Locator = p7ForkLocator with { Kind = ExecutionDecisionKind.Opaque }
    }]);
Check(HistoricalExecutionContextRules.TryValidate(p7OpaqueLocator, out _), "P7B opaque locator remains parseable");
Check(HistoricalExecutionContextRules.GetCapability(p7OpaqueLocator, p7PlaybackHash) == HistoricalReplayCapability.ContentOnly, "P7B opaque locator prevents fidelity claim");

HistoricalExecutionContext p7Oversized = CopyExecutionContext(
    p7Empty,
    provenance: p7Empty.Provenance with { ModVersion = new string('x', HistoricalExecutionContextRules.MaxExecutionJsonBytes) });
Check(!HistoricalExecutionContextCodec.TryEncode(p7Oversized, out _), "P7B payload size limit");
AutomaticDecision[] p7TooMany = Enumerable.Range(0, HistoricalExecutionContextRules.MaxTraceEntries + 1)
    .Select(index => p7Fork with
    {
        Sequence = index,
        Locator = p7ForkLocator with { Occurrence = index },
        Causality = AutomaticDecisionCausality.Autonomous,
        CausedByPlayerChoiceSequence = null
    })
    .ToArray();
HistoricalExecutionContext p7OverEntryLimit = CopyExecutionContext(p7Context, automaticDecisions: p7TooMany, playerChoices: []);
Check(!HistoricalExecutionContextRules.TryValidate(p7OverEntryLimit, out _), "P7B trace entry limit");
List<AutomaticDecision> p7MutableDecisions = [p7Fork];
HistoricalExecutionContext p7DefensiveCopy = CopyExecutionContext(p7Context, automaticDecisions: p7MutableDecisions);
p7MutableDecisions.Clear();
Check(p7DefensiveCopy.AutomaticDecisions.Count == 1, "P7B context defensively copies collections");
HistoricalExecutionContext p7InvalidChoiceKind = CopyExecutionContext(
    p7Context,
    playerChoices: [p7Choice with { Locator = p7ChoiceLocator with { Kind = ExecutionDecisionKind.Fork } }]);
Check(!HistoricalExecutionContextRules.TryValidate(p7InvalidChoiceKind, out _), "P7B player choice kind validation");
HistoricalExecutionContext p7InvalidAutomaticKind = CopyExecutionContext(
    p7Context,
    automaticDecisions: [p7Fork with { Locator = p7ForkLocator with { Kind = ExecutionDecisionKind.NativeQuestion } }]);
Check(!HistoricalExecutionContextRules.TryValidate(p7InvalidAutomaticKind, out _), "P7B automatic decision kind validation");
HistoricalExecutionContext p7ContradictoryCoverage = CopyExecutionContext(
    p7Context,
    coverage: p7FullCoverage with { PlayerChoices = ExecutionTraceCoverage.NotCaptured });
Check(!HistoricalExecutionContextRules.TryValidate(p7ContradictoryCoverage, out _), "P7B NotCaptured coverage requires empty list");
ScriptSourceIdentity p7NormalizedSource = new(ScriptSourceKind.EventAssetEntry, " data\\events\\haleyhouse ", "branch");
Check(p7NormalizedSource == p7BranchSource, "P7B source asset slash/case normalization");
Check(HistoricalExecutionContextRules.HashChildPath(p7PlaybackHash, p7Branch.Kind, p7NormalizedSource, p7Entry, p7BranchCommandsHash)
    == p7Branch.PathHash, "P7B normalized source produces stable path hash");

// ---------- Phase 7C passive execution trace builder ----------
Check(NaturalExecutionTraceRules.CanObserve(replayActive: false, isCurrentEvent: true), "P7C natural current event observed");
Check(!NaturalExecutionTraceRules.CanObserve(replayActive: true, isCurrentEvent: true), "P7C replay excluded");
Check(!NaturalExecutionTraceRules.CanObserve(replayActive: false, isCurrentEvent: false), "P7C unrelated event excluded");

string[] p7cForkRoot = ["fork mailFlag branch", "end"];
NaturalExecutionTraceBuilder p7cForkFalseBuilder = TraceBuilder(p7cForkRoot);
CommandDispatchObservation p7cForkFalse = BeginTraceCommand(
    p7cForkFalseBuilder, p7cForkRoot, 0, ["fork", "mailFlag", "branch"], "Fork", nativeFork: true);
p7cForkFalseBuilder.EndCommand(p7cForkFalse, 1, p7cForkRoot, false, false, -1);
NaturalExecutionTraceResult p7cForkFalseResult = p7cForkFalseBuilder.Finish(ExecutionTraceEndReason.NaturalComplete);
Check(p7cForkFalseResult.Context.AutomaticDecisions.Single().Result.Outcome == AutomaticDecisionOutcome.ContinueCurrentSegment, "P7C fork false captured");
Check(p7cForkFalseResult.Context.AutomaticDecisions[0].Causality == AutomaticDecisionCausality.Autonomous, "P7C required-id fork autonomous");
Check(HistoricalExecutionContextRules.TryValidate(p7cForkFalseResult.Context, out _), "P7C fork false context valid");

NaturalExecutionTraceBuilder p7cForkTrueBuilder = TraceBuilder(p7cForkRoot);
CommandDispatchObservation p7cForkTrue = BeginTraceCommand(
    p7cForkTrueBuilder, p7cForkRoot, 0, ["fork", "mailFlag", "branch"], "Fork", nativeFork: true);
string[] p7cBranchCommands = ["speak Sophia hi", "end"];
p7cForkTrueBuilder.ObserveReplacement(p7cForkTrue, p7cBranchCommands);
p7cForkTrueBuilder.EndCommand(p7cForkTrue, 0, p7cBranchCommands, true, false, -1);
NaturalExecutionTraceResult p7cForkTrueResult = p7cForkTrueBuilder.Finish(ExecutionTraceEndReason.NaturalComplete);
AutomaticDecision p7cForkDecision = p7cForkTrueResult.Context.AutomaticDecisions.Single();
Check(p7cForkDecision.Result.Outcome == AutomaticDecisionOutcome.ReplaceCommands, "P7C fork true captured");
Check(p7cForkDecision.Result.ReplacementSegment is { Kind: ScriptSegmentKind.ForkReplacement }, "P7C fork child segment");
Check(p7cForkTrueBuilder.CurrentSegment == p7cForkDecision.Result.ReplacementSegment, "P7C current segment moves to fork child");
Check(HistoricalExecutionContextRules.TryValidate(p7cForkTrueResult.Context, out _), "P7C fork true context valid");

string[] p7cSwitchRoot = ["switchEvent branch"];
NaturalExecutionTraceBuilder p7cSwitchBuilder = TraceBuilder(p7cSwitchRoot);
CommandDispatchObservation p7cSwitch = BeginTraceCommand(
    p7cSwitchBuilder, p7cSwitchRoot, 0, ["switchEvent", "branch"], "SwitchEvent", nativeSwitch: true);
p7cSwitchBuilder.ObserveReplacement(p7cSwitch, p7cBranchCommands);
p7cSwitchBuilder.EndCommand(p7cSwitch, 0, p7cBranchCommands, true, false, -1);
NaturalExecutionTraceResult p7cSwitchResult = p7cSwitchBuilder.Finish(ExecutionTraceEndReason.NaturalComplete);
Check(p7cSwitchResult.Context.AutomaticDecisions.Count == 0, "P7C switch is transition not decision");
Check(p7cSwitchBuilder.CurrentSegment.Kind == ScriptSegmentKind.SwitchEventReplacement, "P7C switch child segment");
Check(p7cSwitchResult.Context.Completion == ExecutionTraceCompletion.EmptyComplete, "P7C transition-only event EmptyComplete");
Check(HistoricalExecutionContextRules.GetCapability(p7cSwitchResult.Context, FullHash('7')) == HistoricalReplayCapability.OutcomeAware, "P7C automatic-only EmptyComplete outcome-aware");

NaturalExecutionTraceBuilder p7cDuplicateBuilder = TraceBuilder(p7cForkRoot);
CommandDispatchObservation p7cDuplicate0 = BeginTraceCommand(
    p7cDuplicateBuilder, p7cForkRoot, 0, ["fork", "mailFlag", "branch"], "Fork", nativeFork: true);
p7cDuplicateBuilder.EndCommand(p7cDuplicate0, 1, p7cForkRoot, false, false, -1);
CommandDispatchObservation p7cDuplicate1 = BeginTraceCommand(
    p7cDuplicateBuilder, p7cForkRoot, 0, ["fork", "mailFlag", "branch"], "Fork", nativeFork: true);
p7cDuplicateBuilder.EndCommand(p7cDuplicate1, 1, p7cForkRoot, false, false, -1);
NaturalExecutionTraceResult p7cDuplicateResult = p7cDuplicateBuilder.Finish(ExecutionTraceEndReason.NaturalComplete);
Check(p7cDuplicateResult.Context.AutomaticDecisions.Select(value => value.Locator.Occurrence).SequenceEqual([0, 1]), "P7C repeated decision occurrence increments on commit");
Check(p7cDuplicateResult.Context.AutomaticDecisions.Select(value => value.Sequence).SequenceEqual([0L, 1L]), "P7C global sequence");

NaturalExecutionTraceBuilder p7cRetryBuilder = TraceBuilder(["pause 100"]);
CommandDispatchObservation p7cRetry = BeginTraceCommand(
    p7cRetryBuilder, ["pause 100"], 0, ["pause", "100"], "Pause");
p7cRetryBuilder.EndCommand(p7cRetry, 0, ["pause 100"], false, false, -1);
NaturalExecutionTraceResult p7cRetryResult = p7cRetryBuilder.Finish(ExecutionTraceEndReason.NaturalComplete);
Check(p7cRetryResult.Context.AutomaticDecisions.Count == 0, "P7C retried presentation command not recorded");

NaturalExecutionTraceBuilder p7cUnknownForkBuilder = TraceBuilder(["fork branch"]);
CommandDispatchObservation p7cUnknownFork = BeginTraceCommand(
    p7cUnknownForkBuilder, ["fork branch"], 0, ["fork", "branch"], "Fork", nativeFork: true);
p7cUnknownForkBuilder.EndCommand(p7cUnknownFork, 1, ["fork branch"], false, true, -1);
NaturalExecutionTraceResult p7cUnknownForkResult = p7cUnknownForkBuilder.Finish(ExecutionTraceEndReason.NaturalComplete);
Check(p7cUnknownForkResult.Context.AutomaticDecisions[0].Causality == AutomaticDecisionCausality.Unknown, "P7C one-key fork causality fails closed");
Check(HistoricalExecutionContextRules.GetCapability(p7cUnknownForkResult.Context, FullHash('7')) == HistoricalReplayCapability.ContentOnly, "P7C unknown fork content-only");

NaturalExecutionTraceBuilder p7cOpaqueBuilder = TraceBuilder(["custom branch"]);
CommandDispatchObservation p7cOpaque = BeginTraceCommand(
    p7cOpaqueBuilder, ["custom branch"], 0, ["custom", "branch"], "custom", handlerIsNative: false);
p7cOpaqueBuilder.ObserveReplacement(p7cOpaque, p7cBranchCommands);
p7cOpaqueBuilder.EndCommand(p7cOpaque, 0, p7cBranchCommands, true, false, -1);
NaturalExecutionTraceResult p7cOpaqueResult = p7cOpaqueBuilder.Finish(ExecutionTraceEndReason.NaturalComplete);
Check(p7cOpaqueResult.Context.Completion == ExecutionTraceCompletion.Partial, "P7C opaque replacement partial");
Check(p7cOpaqueResult.Context.Coverage.OpaqueDecisions == OpaqueDecisionCoverage.UnsupportedObserved, "P7C opaque coverage");
Check(HistoricalExecutionContextRules.GetCapability(p7cOpaqueResult.Context, FullHash('7')) == HistoricalReplayCapability.ContentOnly, "P7C opaque content-only");

NaturalExecutionTraceBuilder p7cMutationBuilder = TraceBuilder(["custom"]);
CommandDispatchObservation p7cMutation = BeginTraceCommand(
    p7cMutationBuilder, ["custom"], 0, ["custom"], "custom", handlerIsNative: false);
p7cMutationBuilder.EndCommand(p7cMutation, 0, ["changed"], true, false, -1);
NaturalExecutionTraceResult p7cMutationResult = p7cMutationBuilder.Finish(ExecutionTraceEndReason.NaturalComplete);
Check(p7cMutationResult.Context is { Completion: ExecutionTraceCompletion.Partial, EndReason: ExecutionTraceEndReason.CaptureFailure }, "P7C unmarked mutation fails closed");

NaturalExecutionTraceBuilder p7cAnswerBuilder = TraceBuilder(["quickQuestion q#a#b(break)x(break)y"]);
AnswerDialogueObservation p7cAnswer = p7cAnswerBuilder.BeginAnswer(
    "quickQuestion", 1, "quickQuestion q#a#b(break)x(break)y", 0,
    ["quickQuestion q#a#b(break)x(break)y"], false, -1)!;
p7cAnswerBuilder.EndAnswer(p7cAnswer, ["quickQuestion q#a#b(break)x(break)y", "y"], false, 1);
NaturalExecutionTraceResult p7cAnswerResult = p7cAnswerBuilder.Finish(ExecutionTraceEndReason.NaturalComplete);
Check(p7cAnswerBuilder.CurrentSegment.Kind == ScriptSegmentKind.ChoiceInsertion, "P7C quickQuestion insertion segment diagnostic");
Check(p7cAnswerResult.Context.PlayerChoices.Count == 0 && p7cAnswerResult.Context.Coverage.PlayerChoices == ExecutionTraceCoverage.NotCaptured, "P7C choice is diagnostic-only");

NaturalExecutionTraceBuilder p7cNoDecisionBuilder = TraceBuilder(["end"]);
NaturalExecutionTraceResult p7cNoDecision = p7cNoDecisionBuilder.Finish(ExecutionTraceEndReason.NaturalComplete);
Check(p7cNoDecision.Context.Completion == ExecutionTraceCompletion.EmptyComplete, "P7C no-decision natural complete");
Check(HistoricalExecutionContextRules.TryValidate(p7cNoDecision.Context, out _), "P7C no-decision context valid");

NaturalExecutionTraceBuilder p7cSkippedBuilder = TraceBuilder(["end"]);
Check(p7cSkippedBuilder.Finish(ExecutionTraceEndReason.Skipped).Context.Completion == ExecutionTraceCompletion.Partial, "P7C skipped partial");
NaturalExecutionTraceBuilder p7cInterruptedBuilder = TraceBuilder(["end"]);
Check(p7cInterruptedBuilder.Finish(ExecutionTraceEndReason.Interrupted).Context.EndReason == ExecutionTraceEndReason.Interrupted, "P7C interrupted lifecycle");
NaturalExecutionTraceBuilder p7cQuitBuilder = TraceBuilder(["end"]);
Check(p7cQuitBuilder.Finish(ExecutionTraceEndReason.QuitToTitle).Context.EndReason == ExecutionTraceEndReason.QuitToTitle, "P7C quit lifecycle");
NaturalExecutionTraceBuilder p7cFailureBuilder = TraceBuilder(["end"]);
p7cFailureBuilder.MarkObserverFailure("injected-observer-failure");
NaturalExecutionTraceResult p7cFailure = p7cFailureBuilder.Finish(ExecutionTraceEndReason.NaturalComplete);
Check(p7cFailure.Context is { Completion: ExecutionTraceCompletion.Partial, EndReason: ExecutionTraceEndReason.CaptureFailure }, "P7C observer failure partial");

NaturalExecutionTraceBuilder p7cLimitBuilder = TraceBuilder(p7cForkRoot);
for (int index = 0; index <= HistoricalExecutionContextRules.MaxTraceEntries; index++)
{
    CommandDispatchObservation? observation = p7cLimitBuilder.BeginCommand(
        p7cForkRoot[0], 0, ["fork", "mailFlag", "branch"], "Fork", "native", true, true, true, false,
        false, "Town", false, -1, p7cForkRoot);
    if (observation is not null)
        p7cLimitBuilder.EndCommand(observation, 1, p7cForkRoot, false, false, -1);
}
NaturalExecutionTraceResult p7cLimit = p7cLimitBuilder.Finish(ExecutionTraceEndReason.NaturalComplete);
Check(p7cLimit.Context is { Completion: ExecutionTraceCompletion.Partial, EndReason: ExecutionTraceEndReason.TraceLimitExceeded }, "P7C trace overflow partial");
Check(p7cLimit.Context.AutomaticDecisions.Count > 0
    && p7cLimit.Context.AutomaticDecisions.Count <= HistoricalExecutionContextRules.MaxTraceEntries, "P7C trace entry hard cap");
Check(p7cLimit.Diagnostic.ExecutionJsonBytes <= HistoricalExecutionContextRules.MaxExecutionJsonBytes, "P7C encoded trace byte cap");
Check(HistoricalExecutionContextRules.TryValidate(p7cLimit.Context, out _), "P7C overflow context remains valid");

// ---------- FINAL 1.0.0 preview planning + state injection ----------
PreviewPlanner previewPlanner = new(
    key => key.Split('/', StringSplitOptions.RemoveEmptyEntries),
    FakeSplitArgs,
    null);
CurrentStateSnapshot pvState = new(
    Season: "spring", Weather: "sunny", DayOfMonth: 12, Year: 1, Time: 900, DaysPlayed: 20,
    Friendship: new Dictionary<string, int>(StringComparer.Ordinal) { ["Haley"] = 1500, ["Leah"] = 500 },
    EventsSeen: new HashSet<string>(StringComparer.Ordinal), LocalMail: new HashSet<string>(StringComparer.Ordinal),
    HostMail: null, HostOrLocalMail: null, Dating: null, Spouse: null, Roommate: false, WorldState: null);

// satisfied event -> DirectReplay
EventConditionStatus pvDirect = previewPlanner.Analyze(TestEvent("123"), pvState);
Check(pvDirect.IsCurrentlyAvailable && pvDirect.MissingCount == 0 && pvDirect.UnknownCount == 0
    && pvDirect.Capability == PreviewCapability.DirectReplay, "F1-1 satisfied event direct replay");

// supported missing friendship -> PreviewSupported with override
EventConditionStatus pvFriendship = previewPlanner.Analyze(TestEvent("124/f Haley 2500"), pvState);
Check(!pvFriendship.IsCurrentlyAvailable && pvFriendship.MissingCount == 1
    && pvFriendship.Capability == PreviewCapability.PreviewSupported, "F1-2 friendship gap preview-supported");
PreviewPlan pvFriendshipPlan = previewPlanner.Plan(TestEvent("124/f Haley 2500"), pvState);
Check(pvFriendshipPlan.Suggestion.Friendship?["Haley"] == 2500, "F1-2 friendship override suggested");
Check(pvFriendshipPlan.Overrides.Any(value => value.Kind == PreviewOverrideKind.Friendship && value.Key == "Haley" && value.Value == 2500), "F1-2 friendship override collected");
Check(pvFriendshipPlan.Capability == PreviewCapability.PreviewSupported, "F1-2 plan capability preview-supported");

// missing event-seen -> supported
EventConditionStatus pvSeen = previewPlanner.Analyze(TestEvent("125/e 555"), pvState);
Check(pvSeen.MissingCount == 1 && pvSeen.Capability == PreviewCapability.PreviewSupported, "F1-3 eventsSeen gap supported");

// opaque condition -> AnalysisOnly / partially-supported
EventConditionStatus pvOpaque = previewPlanner.Analyze(TestEvent("126/G SOME QUERY"), pvState);
Check(pvOpaque.UnknownCount >= 1 && pvOpaque.Capability is PreviewCapability.AnalysisOnly or PreviewCapability.PreviewPartiallySupported, "F1-4 opaque condition degrades");

// AND grouping: multiple missing -> preview supported when all overridable
EventConditionStatus pvMulti = previewPlanner.Analyze(TestEvent("127/f Leah 1000/e 777"), pvState);
Check(pvMulti.MissingCount == 2 && pvMulti.Capability == PreviewCapability.PreviewSupported, "F1-5 AND grouping preview-supported");

// NOT/relationship semantics: negated spouse is analyze-only when data is missing
EventConditionStatus pvNegated = previewPlanner.Analyze(TestEvent("128/o Alex"), pvState);
Check(pvNegated.UnknownCount >= 1 && pvNegated.Capability == PreviewCapability.AnalysisOnly, "F1-6 NOT/relationship analyze-only degrades");

// unsupported mutation (weather is analyze-only, not restorable)
EventConditionStatus pvWeather = previewPlanner.Analyze(TestEvent("129/w rainy"), pvState);
Check(pvWeather.MissingCount == 1 && pvWeather.Capability == PreviewCapability.PreviewPartiallySupported, "F1-7 weather analyze-only not injected");

// PreviewState is sparse and never a full snapshot
PreviewState pvSparse = new(Season: "summer", Time: 1200, Friendship: new Dictionary<string, int> { ["Haley"] = 2500 });
Check(pvSparse.Season == "summer" && pvSparse.Time == 1200 && pvSparse.Friendship?["Haley"] == 2500
    && pvSparse.Year is null && pvSparse.DayOfMonth is null, "F1-8 PreviewState sparse");

// StateInjector: apply + restore exact touched state (incl. eventsSeen/mail)
PreviewState pvFullInject = new(Season: "summer", Time: 1200,
    Friendship: new Dictionary<string, int> { ["Haley"] = 2500 },
    EventsSeen: new HashSet<string>(StringComparer.Ordinal) { "777" },
    Mail: new HashSet<string>(StringComparer.Ordinal) { "m1" });
FakePreviewAccessor pvFake = new();
pvFake.Season = "spring";
pvFake.Time = 900;
pvFake.SetFriendship("Haley", 1500);
using (PreviewInjectionScope pvScope = PreviewInjectionScope.Apply(pvFake, pvFullInject))
{
    Check(pvFake.Season == "summer", "F1-9 injected season applied");
    Check(pvFake.Time == 1200, "F1-9 injected time applied");
    Check(pvFake.GetFriendship("Haley") == 2500, "F1-9 injected friendship applied");
    Check(pvFake.HasEventSeen("777"), "F1-9 injected eventsSeen applied");
    Check(pvFake.HasMail("m1"), "F1-9 injected mail applied");
}
Check(pvFake.Season == "spring", "F1-10 season restored");
Check(pvFake.Time == 900, "F1-10 time restored");
Check(pvFake.GetFriendship("Haley") == 1500, "F1-10 friendship restored");
Check(pvFake.HasEventSeen("777") == false, "F1-10 eventsSeen restored");
Check(pvFake.HasMail("m1") == false, "F1-10 mail restored");
Check(pvFake.DayOfMonth is null && pvFake.Year is null, "F1-10 untouched slots unchanged");

// idempotent restore: second dispose is a no-op
using (PreviewInjectionScope pvScope2 = PreviewInjectionScope.Apply(pvFake, pvFullInject)) { }
Check(pvFake.Season == "spring", "F1-11 idempotent restore");

// capture failure must still return a scope that restores applied state
ThrowingPreviewAccessor pvThrow = new();
pvThrow.Season = "spring";
PreviewState pvThrowState = new(Season: "summer", Friendship: new Dictionary<string, int> { ["Haley"] = 2500 });
using (PreviewInjectionScope pvThrowScope = PreviewInjectionScope.Apply(pvThrow, pvThrowState))
{
    Check(pvThrow.Season == "summer", "F1-14 applied before failure retained");
}
Check(pvThrow.Season == "spring", "F1-14 partial failure still restores applied state");

// Playback stays current-state canonical
GalleryEvent pvCurrent = TestEvent("130/A");
EventPlayback pvPlayback = EventPlayback.ForCurrent(pvCurrent.Resolved);
Check(pvPlayback.RootScript == pvCurrent.Resolved.ResolvedScript, "F1-13 replay uses current resolved script");

// strict two-state event card model
void CheckState(string label, bool seenByPlayer, bool galleryUnlocked, bool expectedUnlocked)
{
    EventCardState state = EventCardStateResolver.Resolve(seenByPlayer, galleryUnlocked);
    Check(state.Unlocked == expectedUnlocked, label + " unlocked flag");
    Check(state.Unlocked ? state.StatusKey == "event.state-unlocked" : state.StatusKey == "event.state-locked", label + " status key");
}
CheckState("state-seen", seenByPlayer: true, galleryUnlocked: false, expectedUnlocked: true);
CheckState("state-unlock-all", seenByPlayer: false, galleryUnlocked: true, expectedUnlocked: true);
CheckState("state-locked", seenByPlayer: false, galleryUnlocked: false, expectedUnlocked: false);

ConditionParser sceneParser = new(key => key.Split('/', StringSplitOptions.RemoveEmptyEntries), FakeSplitArgs);
ReplaySceneEnvironment scene = ReplaySceneEnvironmentResolver.Resolve(
    sceneParser.ParseRawKey("200/SEASON summer fall/TIME 1800 2400/WEATHER rainy").Conditions,
    "spring", 900, "Storm");
Check(scene.Season == "summer", "2.0.1 scene picks first allowed season when current is disallowed");
Check(scene.Time == 1800, "2.0.1 scene picks minimum time when current is outside range");
Check(scene.Weather == "Storm", "2.0.1 rainy keeps current storm");
ReplaySceneEnvironment keptScene = ReplaySceneEnvironmentResolver.Resolve(
    sceneParser.ParseRawKey("201/SEASON summer fall/TIME 800 1200/WEATHER sunny").Conditions,
    "fall", 900, "Rain");
Check(keptScene.Season == "fall" && keptScene.Time == 900 && keptScene.Weather == "Sun", "2.0.1 scene keeps valid season/time and normalizes sun");
ReplaySceneEnvironment safeScene = ReplaySceneEnvironmentResolver.Resolve(
    sceneParser.ParseRawKey("202/!SEASON winter/!TIME 600 900/WEATHER CustomWeather").Conditions,
    "spring", 1200, "Sun");
Check(safeScene.Season is null && safeScene.Time is null && safeScene.Weather is null && safeScene.Warning is not null,
    "2.0.1 negative requirements are ignored and custom weather warns");

Console.WriteLine("Stardew Gallery checks passed.");

static void Check(bool condition, string message = "", [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
{
    if (!condition)
        throw new Exception(line > 0 && string.IsNullOrEmpty(message)
            ? $"Check failed at line {line}."
            : string.IsNullOrEmpty(message) ? "Check failed." : $"Check failed: {message}");
}

static EventEvidence Evidence(
    string identity,
    string id,
    IReadOnlyDictionary<string, int> friendship,
    IReadOnlyList<string> prerequisites,
    IReadOnlySet<string> actors,
    IReadOnlyDictionary<string, int> dialogue)
    => new(TestIdentity(identity), id, friendship, prerequisites, actors, dialogue);

static EventIdentity TestIdentity(string identity) => new("Data/Events/Checks", identity);

static ResolvedEventCandidate Candidate(
    string assetName,
    string eventId,
    string locationName,
    string rawEventKey,
    string script,
    Func<string?> checkPrecondition)
{
    ResolvedEvent resolved = new(
        new EventIdentity(assetName, eventId),
        locationName,
        rawEventKey,
        script,
        new EventFragments([script], []),
        EventHashes.RootDefinition(rawEventKey, script),
        EventHashes.RootScript(script)
    );
    return new ResolvedEventCandidate(resolved, checkPrecondition);
}

static HashSet<string> Set(params string[] names) => new(names, StringComparer.Ordinal);

static GalleryEvent TestEvent(string rawEventKey)
{
    EventIdentity identity = TestIdentity("evt");
    string script = "speak npc hello/end";
    ResolvedEvent resolved = new(
        identity,
        "Town",
        rawEventKey,
        script,
        new EventFragments([script], []),
        EventHashes.RootDefinition(rawEventKey, script),
        EventHashes.RootScript(script));
    return new GalleryEvent(resolved, new EventOwnership(
        OwnershipKind.Direct,
        [new EventOwner("Haley", 1000)],
        null));
}

static string FullHash(char value) => new(value, 64);

static HistoricalExecutionContext CopyExecutionContext(
    HistoricalExecutionContext source,
    int? schemaVersion = null,
    string? playbackHash = null,
    ExecutionTraceCompletion? completion = null,
    ExecutionTraceEndReason? endReason = null,
    ExecutionTraceCoverageSummary? coverage = null,
    ExecutionTraceProvenance? provenance = null,
    IReadOnlyList<AutomaticDecision>? automaticDecisions = null,
    IReadOnlyList<PlayerChoiceDecision>? playerChoices = null,
    IReadOnlyList<ExecutionTraceIssue>? issues = null)
    => new(
        schemaVersion ?? source.SchemaVersion,
        playbackHash ?? source.PlaybackHash,
        completion ?? source.Completion,
        endReason ?? source.EndReason,
        coverage ?? source.Coverage,
        provenance ?? source.Provenance,
        automaticDecisions ?? source.AutomaticDecisions,
        playerChoices ?? source.PlayerChoices,
        issues ?? source.Issues);

static NaturalExecutionTraceBuilder TraceBuilder(IReadOnlyList<string> commands)
    => new(
        new EventIdentity("Data/Events/Town", "trace"),
        "trace/key",
        FullHash('R'),
        FullHash('7'),
        commands,
        "en",
        "1.6.15.24356",
        "1.0.0");

static CommandDispatchObservation BeginTraceCommand(
    NaturalExecutionTraceBuilder builder,
    IReadOnlyList<string> commands,
    int ordinal,
    string[] arguments,
    string commandName,
    bool handlerResolved = true,
    bool handlerIsNative = true,
    bool nativeFork = false,
    bool nativeSwitch = false)
    => builder.BeginCommand(
        commands[ordinal],
        ordinal,
        arguments,
        commandName,
        handlerIsNative ? "StardewValley.Event.DefaultCommands." + commandName : "Custom.Handler",
        handlerResolved,
        handlerIsNative,
        nativeFork,
        nativeSwitch,
        false,
        "Town",
        false,
        -1,
        commands) ?? throw new Exception("P7C command observation did not start");

static Dictionary<string, IReadOnlyDictionary<string, string>> Assets(Dictionary<string, Dictionary<string, string>> source)
{
    Dictionary<string, IReadOnlyDictionary<string, string>> result = new(StringComparer.OrdinalIgnoreCase);
    foreach ((string asset, Dictionary<string, string> entries) in source)
        result[asset] = entries;
    return result;
}

static string[] FakeSplitArgs(string segment)
{
    List<string> result = [];
    StringBuilder current = new();
    bool inQuotes = false;
    foreach (char c in segment)
    {
        if (c == '"')
        {
            inQuotes = !inQuotes;
            continue;
        }
        if (c == ' ' && !inQuotes)
        {
            if (current.Length > 0)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            continue;
        }
        current.Append(c);
    }
    if (current.Length > 0)
        result.Add(current.ToString());
    return result.ToArray();
}

internal sealed class FakeEventAssetSourceCatalog(
    IReadOnlyList<EventAssetSource> sources,
    List<string>? calls = null) : IEventAssetSourceCatalog
{
    public void VisitCurrent(Action<EventAssetSource> visit)
    {
        foreach (EventAssetSource source in sources)
        {
            calls?.Add("visit:" + source.LaunchLocationName);
            visit(source);
            calls?.Add("after:" + source.LaunchLocationName);
        }
    }
}


internal sealed class FakePreviewAccessor : IPreviewStateAccessor
{
    public string? Season { get; set; }
    public int? DayOfMonth { get; set; }
    public int? Year { get; set; }
    public int? Time { get; set; }
    private readonly Dictionary<string, int> friendship = new(StringComparer.Ordinal);
    private readonly HashSet<string> seen = new(StringComparer.Ordinal);
    private readonly HashSet<string> mail = new(StringComparer.Ordinal);
    public int? GetFriendship(string npc) => friendship.GetValueOrDefault(npc);
    public void SetFriendship(string npc, int points) => friendship[npc] = points;
    public bool HasEventSeen(string id) => seen.Contains(id);
    public void SetEventSeen(string id, bool present) { if (present) seen.Add(id); else seen.Remove(id); }
    public bool HasMail(string id) => mail.Contains(id);
    public void SetMail(string id, bool present) { if (present) mail.Add(id); else mail.Remove(id); }
}

internal sealed class ThrowingPreviewAccessor : IPreviewStateAccessor
{
    public string? Season { get; set; }
    public int? DayOfMonth { get; set; }
    public int? Year { get; set; }
    public int? Time { get; set; }
    public int? GetFriendship(string npc) => 0;
    public void SetFriendship(string npc, int points) { }
    public bool HasEventSeen(string id) => false;
    public void SetEventSeen(string id, bool seen) => throw new InvalidOperationException(
        "injected eventsSeen failure");
    public bool HasMail(string id) => false;
    public void SetMail(string id, bool present) => throw new InvalidOperationException(
        "injected mail failure");
}
