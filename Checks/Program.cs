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
Console.WriteLine("Stardew Gallery checks passed.");

static void Check(bool condition)
{
    if (!condition)
        throw new Exception("Check failed.");
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
