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
