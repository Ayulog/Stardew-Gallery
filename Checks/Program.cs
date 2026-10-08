using System.Text;
using System.Text.Json;
using StardewGallery;

AppearanceChecks.Run();
if (args.Contains("--appearance")) return;

EventSourceChecks.Run();
EventSourceDetailChecks.Run();
EventDefinitionChecks.Run();
ModDirectoryDiscoveryChecks.Run();
EventOriginPresentationChecks.Run();
AiModExclusionChecks.Run();
GallerySourceVisibilityChecks.Run();
SourceQueryChecks.Run();
if (args.Contains("--event-sources")) return;

GallerySearchChecks.Run();
GalleryCorrectionChecks.Run();
GalleryNavigationChecks.Run();
GalleryDraftStateChecks.Run();
StoryQueryChecks.Run();
QueryUpgradeChecks.Run();
StoryCatalogChecks.Run();
StoryReplayChecks.Run();
ConditionCoverageChecks.Run(FakeSplitArgs);
if (args.Contains("--benchmark-search"))
    GallerySearchChecks.Benchmark();

Check(EventKey.TryGetId("75160185/f Alissa 500", out string id) && id == "75160185");
Check(EventKey.TryGetId("mod.event/id/condition", out id) && id == "mod.event");
Check(!EventKey.TryGetId(" /condition", out _));
Check(EventKey.IsPlaceholderScript("speak Abigail \"You open up the XNB file hoping to find a secret, only to see this sentence. You are now disappointed.\""));
Check(!EventKey.IsPlaceholderScript("speak Abigail real event"));
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
        return Matched();
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
    _ => Matched()
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
        return Matched();
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
        return Matched();
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
    _ => Matched()
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
        return NotMatched();
    }),
    Candidate("data/events/town", "evt", "DuplicateLocation", "evt/first", "same", () =>
    {
        ignoredDuplicateCalls++;
        return Matched();
    }),
    Candidate("DATA/events/TOWN", "evt", "SelectedLocation", "evt/first", "different", () => Matched()),
    Candidate("Data/Events/Town", "evt", "LaterLocation", "evt/second", "same", () => Matched()),
    Candidate("Data/Events/Beach", "evt", "BeachLocation", "evt/only", "beach", () => Matched()),
    Candidate("Data/Events/Town", "EVT", "CaseLocation", "EVT/only", "case", () => Matched())
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
Check(townGroup.SelectionSource == ResolvedEventSelectionSource.NativeMatch);
Check(firstDuplicateCalls == 1 && ignoredDuplicateCalls == 0);
Check(candidateIndex.TryGetCurrent(new EventIdentity("DATA/Events/Town", "evt"), out ResolvedEvent selectedCurrent));
Check(selectedCurrent.LocationName == "SelectedLocation");
Check(!candidateIndex.TryGetGroup(new EventIdentity("Data/Events/Missing", "evt"), out _));
Check(!candidateIndex.TryGetCurrent(new EventIdentity("Data/Events/Missing", "evt"), out _));
Check(candidateIndex.GetCandidates(new EventIdentity("Data/Events/Missing", "evt")).Count == 0);

int candidateLoadCount = 0;
bool selectSecondVariant = false;
ResolvedEventCandidateCache productionCandidateCache = new(() =>
{
    candidateLoadCount++;
    return
    [
        Candidate("Data/Events/Town", "cached", "StateA", "cached/a", "a", () => selectSecondVariant ? NotMatched() : Matched()),
        Candidate("Data/Events/Town", "cached", "StateB", "cached/b", "b", () => selectSecondVariant ? Matched() : NotMatched())
    ];
});
Check(productionCandidateCache.GetCurrent().CurrentEvents.Single().LocationName == "StateA", "candidate cache selects current variant A");
selectSecondVariant = true;
Check(productionCandidateCache.GetCurrent().CurrentEvents.Single().LocationName == "StateB", "candidate cache refreshes current variant B");
Check(candidateLoadCount == 1, "candidate definitions load once while selection rebuilds");
productionCandidateCache.Invalidate();
Check(productionCandidateCache.GetCurrent().CurrentEvents.Single().LocationName == "StateB", "candidate cache selects after invalidation");
Check(candidateLoadCount == 2, "candidate definitions reload after invalidation");

int skippedApplicableCalls = 0;
ResolvedEventIndex multipleApplicable = ResolvedEventIndex.Build([
    Candidate("Data/Events/Town", "multi", "FirstApplicable", "multi/a", "a", () => Matched()),
    Candidate("Data/Events/Town", "multi", "SecondApplicable", "multi/b", "b", () =>
    {
        skippedApplicableCalls++;
        return Matched();
    })
]);
Check(multipleApplicable.CurrentEvents.Single().LocationName == "FirstApplicable");
Check(skippedApplicableCalls == 0);
Check(multipleApplicable.Groups.Single().SelectionSource == ResolvedEventSelectionSource.NativeMatch);

ResolvedEventIndex allFalse = ResolvedEventIndex.Build([
    Candidate("Data/Events/Town", "none", "Fallback", "none/a", "a", () => NotMatched()),
    Candidate("Data/Events/Town", "none", "NotSelected", "none/b", "b", () => NotMatched())
]);
Check(allFalse.CurrentEvents.Single().LocationName == "Fallback");
Check(allFalse.Groups.Single().SelectionSource == ResolvedEventSelectionSource.NoMatchFallback);

int afterExceptionCalls = 0;
ResolvedEventIndex exceptionThenMatch = ResolvedEventIndex.Build([
    Candidate("Data/Events/Town", "exception", "Throws", "exception/a", "a", () => ProbeError()),
    Candidate("Data/Events/Town", "exception", "AfterException", "exception/b", "b", () =>
    {
        afterExceptionCalls++;
        return Matched();
    })
]);
Check(exceptionThenMatch.CurrentEvents.Single().LocationName == "Throws");
Check(afterExceptionCalls == 0);
Check(exceptionThenMatch.Groups.Single().SelectionSource == ResolvedEventSelectionSource.IndeterminateFallback);

int afterUnsafeCalls = 0;
ResolvedEventIndex unsafeBeforeMatch = ResolvedEventIndex.Build([
    Candidate("Data/Events/Town", "unsafe", "SafelyFalse", "unsafe/a", "a", () => NotMatched()),
    Candidate("Data/Events/Town", "unsafe", "Indeterminate", "unsafe/b", "b", () => NotSafelyEvaluated()),
    Candidate("Data/Events/Town", "unsafe", "LaterMatch", "unsafe/c", "c", () => CountedMatch(() => afterUnsafeCalls++))
]);
Check(unsafeBeforeMatch.CurrentEvents.Single().LocationName == "Indeterminate");
Check(afterUnsafeCalls == 0);
Check(unsafeBeforeMatch.Groups.Single().SelectionSource == ResolvedEventSelectionSource.IndeterminateFallback);

int unsafeFirstNativeCalls = 0;
ResolvedEventIndex unsafeFirst = ResolvedEventIndex.Build([
    Candidate("Data/Events/Town", "unsafe-first", "UnsafeFirst", "unsafe-first/a", "a", () => NotSafelyEvaluated()),
    Candidate("Data/Events/Town", "unsafe-first", "LaterMatch", "unsafe-first/b", "b", () => CountedMatch(() => unsafeFirstNativeCalls++))
]);
Check(unsafeFirst.CurrentEvents.Single().LocationName == "UnsafeFirst");
Check(unsafeFirstNativeCalls == 0);
Check(unsafeFirst.Groups.Single().SelectionSource == ResolvedEventSelectionSource.IndeterminateFallback);

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
    _ => NotMatched()
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
    _ => Matched()
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
Check(galleryBuild.Catalog.Events.Count == 1, "Only the confirmed heart story enters the album");
Check(galleryBuild.Catalog.ExcludedEvents.Count == 3);
Check(galleryBuild.Catalog.Characters.Select(character => character.Name).SequenceEqual(["Bert"]));
GalleryEvent rootGalleryEvent = galleryBuild.Catalog.Events.Single(entry => entry.EventId == "root");
Check(rootGalleryEvent.Identity == "data/events/town\u001froot");
Check(rootGalleryEvent.Ownership.Kind == OwnershipKind.Direct);
Check(rootGalleryEvent.Ownership.Owners.Single().Name == "Bert");
GalleryEvent childGalleryEvent = galleryBuild.Catalog.AllEntries.Single(entry => entry.EventId == "child");
Check(childGalleryEvent.Ownership.Kind == OwnershipKind.Inherited);
Check(childGalleryEvent.Ownership.Owners.Single().Name == "Bert");
Check(childGalleryEvent.Kind == StoryKind.Ordinary, "A prerequisite alone does not establish narrative continuation");
GalleryEvent spouseGalleryEvent = galleryBuild.Catalog.AllEntries.Single(entry => entry.EventId == "spouse-event");
Check(spouseGalleryEvent.Ownership.Kind == OwnershipKind.Inferred);
Check(spouseGalleryEvent.Ownership.Owners.Single().Name == "Bert");
Check(spouseGalleryEvent.Kind == StoryKind.Ordinary, "Speaking actor ownership does not make an event a heart story");
Check(galleryBuild.Catalog.ExcludedEvents.Any(entry => entry.EventId == "silent"));

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
Check(ownership[TestIdentity("multi-direct")].Kind == OwnershipKind.Direct && ownership[TestIdentity("multi-direct")].Owners.Count == 2,
    "all explicit friendship subjects remain owners even when one speaks more");

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
Check(negatedFriendship.Conditions[0] is FriendshipCondition { Requirements: [{ Points: 1000 }], Negated: true, Scope: ConditionPlayerScope.LocalPlayer });
Check(parserParsed.Conditions[0] is SeasonCondition { Seasons.Count: 1 });
Check(parserParsed.Conditions[1] is SeasonCondition { Negated: true });
Check(parserParsed.Conditions[2] is DayOfMonthCondition { Days: [12] });
Check(parserParsed.Conditions[3] is YearCondition { DesiredYear: 2 });
Check(parserParsed.Conditions[4] is TimeCondition { Min: 1800, Max: 2200 });
Check(parserParsed.Conditions[5] is WeatherCondition { WeatherId: "Sun" });
Check(parserParsed.Conditions[6] is FriendshipCondition { Requirements: [{ Npc: "Haley", Points: 2500 }] });
Check(parserParsed.Conditions[7] is SawEventCondition { EventIds: ["123"] });
Check(parserParsed.Conditions[8] is MailCondition { MailId: "mail1", Scope: ConditionPlayerScope.LocalPlayer });
Check(parserParsed.Conditions[9] is MailCondition { MailId: "mail2", Scope: ConditionPlayerScope.HostPlayer });
Check(parserParsed.Conditions[10] is MailCondition { MailId: "mail3", Scope: ConditionPlayerScope.HostOrLocal });
Check(parserParsed.Conditions[11] is DatingCondition { Npc: "Emily" });
Check(parserParsed.Conditions[12] is SpouseCondition { Npc: "Alex" });
Check(parserParsed.Conditions[13] is RoommateCondition);
Check(parserParsed.Conditions[14] is DaysPlayedCondition { Threshold: 15, Scope: ConditionPlayerScope.HostPlayer });
Check(parserParsed.Conditions[15] is WorldStateCondition { Id: "flag" });
Check(parserParsed.Conditions[16] is NativeQueryCondition { Query: "WEATHER Here Sun" });
Check(parserParsed.Conditions[16].Source == ConditionSource.GameStateQuery);
Check(parserParsed.Conditions[17] is OpaqueCondition);
Check(parserParsed.Conditions[17].RawSegment == "UnknownToken token");
Check(parserParsed.Conditions[18] is FriendshipCondition { Requirements: [{ Points: 1000 }], Negated: true });

ConditionSet malformed = parser2.Parse(["Season", "Time x", "Friendship Haley", "DayOfMonth 40", "DayOfMonth x"]);
Check(malformed.Conditions.Where((_, index) => index != 3).All(condition => condition is OpaqueCondition));
Check(malformed.Conditions[3] is DayOfMonthCondition { Days: [40] }, "vanilla accepts any integer day declaration");
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
    Roommate: true, DaysPlayed: 16, WorldState: new HashSet<string> { "flag" });
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
        case FriendshipCondition { Requirements: [{ Npc: "Haley" }], Negated: false }:
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
        case FriendshipCondition { Requirements: [{ Npc: "Hayley" }], Negated: true }:
            Check(result.Truth == ConditionTruth.False && result.Knowledge == ConditionKnowledge.Known, "negated hayley");
            break;
        case OpaqueCondition { RawSegment: "Season" }:
            Check(result.Truth == ConditionTruth.Unknown && result.Knowledge == ConditionKnowledge.Invalid, "opaque season");
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
Check(friendshipMissing.Truth == ConditionTruth.False && friendshipMissing.Knowledge == ConditionKnowledge.Known);

ConditionSet seenSet = parser2.Parse(["SawEvent 123"]);
ConditionEvaluationContext contextSeen = fullContext with { EventsSeen = new HashSet<string>() };
Check(eval.Evaluate(seenSet.Conditions[0], contextSeen).Truth == ConditionTruth.False);
Check(eval.Evaluate(seenSet.Conditions[0], contextSeen).Gap.Kind == ConditionGapKind.MissingState);

ConditionSet mailSet = parser2.Parse(["LocalMail letter"]);
ConditionEvaluationContext contextMail = fullContext with { LocalMail = new HashSet<string>() };
Check(eval.Evaluate(mailSet.Conditions[0], contextMail).Truth == ConditionTruth.False);
Check(eval.Evaluate(mailSet.Conditions[0], contextMail).Gap.Kind == ConditionGapKind.MissingState);

ConditionExpression datingEmily = parser2.Parse(["Dating Emily"]).Conditions[0];
ConditionExpression notDatingEmily = parser2.Parse(["!Dating Emily"]).Conditions[0];
ConditionExpression spouseAlex = parser2.Parse(["Spouse Alex"]).Conditions[0];
ConditionExpression notSpouseAlex = parser2.Parse(["!Spouse Alex"]).Conditions[0];
ConditionEvaluationContext noRelationships = fullContext with
{
    Dating = new HashSet<string>(),
    Spouse = new HashSet<string>()
};
Check(eval.Evaluate(datingEmily, noRelationships).Truth == ConditionTruth.False, "empty dating is known false");
Check(eval.Evaluate(notDatingEmily, noRelationships).Truth == ConditionTruth.True, "empty dating satisfies negation");
Check(eval.Evaluate(spouseAlex, noRelationships).Truth == ConditionTruth.False, "empty spouse is known false");
Check(eval.Evaluate(notSpouseAlex, noRelationships).Truth == ConditionTruth.True, "empty spouse satisfies negation");
Check(eval.Evaluate(datingEmily, noRelationships with { Dating = null }).Knowledge == ConditionKnowledge.MissingData, "null dating remains unknown");
Check(eval.Evaluate(spouseAlex, noRelationships with { Spouse = null }).Knowledge == ConditionKnowledge.MissingData, "null spouse remains unknown");

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
Check(noNative.Evaluate(nativeSet.Conditions[0], missingContext).Knowledge == ConditionKnowledge.Unsupported);

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

ConditionTextSpec readable = ConditionDescriber.Describe(underrunSet.Conditions[0]);
Check(readable.LocalizationKey == "condition.friendship");
Check(readable.Arguments["requirements"] is FriendshipRequirementsTextValue { Values.Count: 1 });
ConditionTextSpec opaqueReadable = ConditionDescriber.Describe(unknownSet.Conditions[0]);
Check(opaqueReadable.LocalizationKey == "condition.custom");
Check(opaqueReadable.RawFallback == "SomethingElse value");
Check(opaqueReadable.Arguments.Count == 0, "opaque raw must remain diagnostic-only");
ConditionTextSpec seasonReadable = ConditionDescriber.Describe(parser2.Parse(["Season Winter"]).Conditions[0]);
Check(seasonReadable.LocalizationKey == "condition.season" && seasonReadable.Arguments["seasons"] is ListTextValue { Values: [TermTextValue { Value: "Winter" }] });
ConditionTextSpec conditionReadable = ConditionDescriber.Describe(parser2.Parse(["SawEvent 123"]).Conditions[0]);
Check(conditionReadable.LocalizationKey == "condition.seen" && conditionReadable.Arguments["id"] is ListTextValue { Values.Count: 1 });

ConditionParser aliasParser = new(_ => [], FakeSplitArgs);
Check(aliasParser.ParseSegment("f Haley 1000") is FriendshipCondition { Requirements: [{ Npc: "Haley", Points: 1000 }], Negated: false });
Check(aliasParser.ParseSegment("e 123") is SawEventCondition { EventIds: ["123"], Negated: false });
Check(aliasParser.ParseSegment("k 123") is SawEventCondition { EventIds: ["123"], Negated: true });
Check(aliasParser.ParseSegment("n letter") is MailCondition { MailId: "letter", Negated: false, Scope: ConditionPlayerScope.LocalPlayer });
Check(aliasParser.ParseSegment("l letter") is MailCondition { MailId: "letter", Negated: true, Scope: ConditionPlayerScope.LocalPlayer });
Check(aliasParser.ParseSegment("t 1800 2200") is TimeCondition { Min: 1800, Max: 2200 });
Check(aliasParser.ParseSegment("w Sun") is WeatherCondition { WeatherId: "Sun" });
Check(aliasParser.ParseSegment("y 2") is YearCondition { DesiredYear: 2 });
Check(aliasParser.ParseSegment("u 12") is DayOfMonthCondition { Days: [12] });
Check(aliasParser.ParseSegment("z Winter") is SeasonCondition { Seasons: ["Winter"], Negated: true });
Check(aliasParser.ParseSegment("j 15") is DaysPlayedCondition { Threshold: 15, Negated: false });
Check(aliasParser.ParseSegment("D Emily") is DatingCondition { Npc: "Emily" });
Check(aliasParser.ParseSegment("O Alex") is SpouseCondition { Npc: "Alex", Negated: false });
Check(aliasParser.ParseSegment("o Alex") is SpouseCondition { Npc: "Alex", Negated: true });
Check(aliasParser.ParseSegment("R") is RoommateCondition);
Check(aliasParser.ParseSegment("G SEASON Spring") is NativeQueryCondition { Query: "SEASON Spring" });
Check(aliasParser.ParseSegment("season spring") is SeasonCondition { Seasons: ["spring"] });
Check(aliasParser.ParseSegment("friendship haley 1000") is FriendshipCondition { Requirements: [{ Npc: "haley", Points: 1000 }] });
Check(aliasParser.ParseSegment("sawEvent 123") is SawEventCondition { EventIds: ["123"] });
Check(aliasParser.ParseSegment("Spouse Alex") is SpouseCondition { Npc: "Alex", Negated: false });
Check(aliasParser.ParseSegment("ROOMMATE") is RoommateCondition);
Check(aliasParser.ParseSegment("localmall letter") is OpaqueCondition);
Check(aliasParser.ParseSegment("Season") is OpaqueCondition);
Check(aliasParser.ParseSegment("!f Alex 1000") is FriendshipCondition { Negated: true });
Check(aliasParser.ParseSegment("!k 123") is SawEventCondition { EventIds: ["123"], Negated: false });
Check(aliasParser.ParseSegment("!!k 123") is SawEventCondition { EventIds: ["123"], Negated: true });
Check(aliasParser.ParseSegment("F 123 1000") is OpaqueCondition);
Check(aliasParser.ParseSegment("e 123 456") is SawEventCondition { EventIds.Count: 2 });
Check(aliasParser.ParseSegment("f Haley 2500 Abigail 1000") is FriendshipCondition { Requirements.Count: 2 });

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
Check(aliasParser.ParseSegment("NotSawEvent 123") is SawEventCondition { EventIds: ["123"], Negated: true }, "NotSawEvent");
Check(aliasParser.ParseSegment("!NotSawEvent 123") is SawEventCondition { EventIds: ["123"], Negated: false }, "!NotSawEvent");
Check(aliasParser.ParseSegment("NotLocalMail letter") is MailCondition { MailId: "letter", Negated: true, Scope: ConditionPlayerScope.LocalPlayer }, "NotLocalMail");
Check(aliasParser.ParseSegment("!NotLocalMail letter") is MailCondition { MailId: "letter", Negated: false, Scope: ConditionPlayerScope.LocalPlayer }, "!NotLocalMail");
Check(aliasParser.ParseSegment("NotSpouse Alex") is SpouseCondition { Npc: "Alex", Negated: true }, "NotSpouse");
Check(aliasParser.ParseSegment("!NotSpouse Alex") is SpouseCondition { Npc: "Alex", Negated: false }, "!NotSpouse");
Check(aliasParser.ParseSegment("NotHostMail mail") is MailCondition { MailId: "mail", Negated: true, Scope: ConditionPlayerScope.HostPlayer }, "NotHostMail");
Check(aliasParser.ParseSegment("NotHostOrLocalMail mail") is MailCondition { MailId: "mail", Negated: true, Scope: ConditionPlayerScope.HostOrLocal }, "NotHostOrLocalMail");
Check(aliasParser.ParseSegment("NotRoommate") is RoommateCondition { Negated: true }, "NotRoommate");
Check(aliasParser.ParseSegment("!NotRoommate") is RoommateCondition { Negated: false }, "!NotRoommate");

Check(aliasParser.ParseSegment("NotSeason") is OpaqueCondition, "NotSeason missing arg");
Check(aliasParser.ParseSegment("NotSawEvent 1 2") is SawEventCondition { EventIds.Count: 2, Negated: true }, "NotSawEvent supports any-of IDs");
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
Check(aliasSet.Conditions[0] is FriendshipCondition { Requirements: [{ Npc: "Haley", Points: 2500 }] });
Check(aliasSet.Conditions[1] is SawEventCondition { EventIds: ["123"], Negated: false });
Check(aliasSet.Conditions[2] is SawEventCondition { EventIds: ["456"], Negated: true });

ConditionSet negatedSeenSet = parser2.Parse(["!SawEvent 123"]);
ConditionEvaluationContext alreadySeen = fullContext with { EventsSeen = new HashSet<string> { "123" } };
ConditionEvaluation negatedSeenEval = eval.Evaluate(negatedSeenSet.Conditions[0], alreadySeen);
Check(negatedSeenEval.Truth == ConditionTruth.False && negatedSeenEval.Gap.Kind == ConditionGapKind.OverState);
ConditionEvaluation negatedSeenSatisfied = eval.Evaluate(negatedSeenSet.Conditions[0], alreadySeen with { EventsSeen = new HashSet<string>() });
Check(negatedSeenSatisfied.Truth == ConditionTruth.True && negatedSeenSatisfied.Gap.Kind == ConditionGapKind.None);

ConditionSet negatedMailSet = parser2.Parse(["!LocalMail letter"]);
ConditionEvaluationContext alreadyMail = fullContext with { LocalMail = new HashSet<string> { "letter" } };
Check(eval.Evaluate(negatedMailSet.Conditions[0], alreadyMail).Gap.Kind == ConditionGapKind.OverState);

ConditionTextSpec negatedReadable = ConditionDescriber.Describe(negatedSeenSet.Conditions[0]);
Check(negatedReadable.Negated == true);
ConditionTextSpec daysReadable = ConditionDescriber.Describe(parser2.Parse(["DaysPlayed 15"]).Conditions[0]);
Check(daysReadable.LocalizationKey == "condition.daysplayed" && daysReadable.Arguments["min"] is NumberTextValue { Value: 16 });

ConditionSet year1Set = parser2.Parse(["Year 1"]);
Check(eval.Evaluate(year1Set.Conditions[0], fullContext with { Year = 1 }).Truth == ConditionTruth.True, "Year 1 + current 1");
Check(eval.Evaluate(year1Set.Conditions[0], fullContext with { Year = 2 }).Truth == ConditionTruth.False, "Year 1 + current 2");
ConditionSet year2Set = parser2.Parse(["Year 2"]);
Check(eval.Evaluate(year2Set.Conditions[0], fullContext with { Year = 1 }).Truth == ConditionTruth.False, "Year 2 + current 1");
Check(eval.Evaluate(year2Set.Conditions[0], fullContext with { Year = 2 }).Truth == ConditionTruth.True, "Year 2 + current 2");
Check(eval.Evaluate(year2Set.Conditions[0], fullContext with { Year = 3 }).Truth == ConditionTruth.True, "Year 2 + current 3");

ConditionTextSpec datingReadable = ConditionDescriber.Describe(parser2.Parse(["Dating Emily"]).Conditions[0]);
Check(datingReadable.LocalizationKey == "condition.dating" && datingReadable.Arguments["npc"] is NpcTextValue { Name: "Emily" });
ConditionTextSpec spouseReadable = ConditionDescriber.Describe(parser2.Parse(["Spouse Alex"]).Conditions[0]);
Check(spouseReadable.LocalizationKey == "condition.spouse" && spouseReadable.Arguments["npc"] is NpcTextValue { Name: "Alex" });
ConditionTextSpec roommateReadable = ConditionDescriber.Describe(parser2.Parse(["Roommate"]).Conditions[0]);
Check(roommateReadable.LocalizationKey == "condition.roommate-with");
ConditionTextSpec worldReadable = ConditionDescriber.Describe(parser2.Parse(["WorldState flag"]).Conditions[0]);
Check(worldReadable.LocalizationKey == "condition.world-state" && worldReadable.Arguments["id"] is PlainTextValue { Value: "flag" });
ConditionTextSpec nativeReadable = ConditionDescriber.Describe(parser2.Parse(["GameStateQuery SEASON Spring"]).Conditions[0]);
Check(nativeReadable.LocalizationKey == "condition.native-query" && nativeReadable.Arguments.ContainsKey("query"));

// ---------- 2.0.3 complete vanilla event-precondition domain ----------
Dictionary<string, string> canonicalSamples = new(StringComparer.OrdinalIgnoreCase)
{
    ["SawEvent"] = "1 2", ["MissingPet"] = "Cat", ["IsHost"] = "", ["HostMail"] = "mail",
    ["WorldState"] = "flag", ["HostOrLocalMail"] = "mail", ["EarnedMoney"] = "1000", ["HasMoney"] = "500",
    ["FreeInventorySlots"] = "2", ["CommunityCenterOrWarehouseDone"] = "", ["Dating"] = "Leah", ["DaysPlayed"] = "10",
    ["JojaBundlesDone"] = "", ["Friendship"] = "Leah 1000 Robin 500", ["FestivalDay"] = "", ["Random"] = "0.5",
    ["Shipped"] = "24 2 188 1", ["SawSecretNote"] = "10", ["ChoseDialogueAnswers"] = "a b", ["LocalMail"] = "mail",
    ["GoldenWalnuts"] = "20", ["InUpgradedHouse"] = "", ["Time"] = "600 1200", ["Weather"] = "rainy",
    ["DayOfWeek"] = "Mon Fri", ["Spouse"] = "Leah", ["Roommate"] = "", ["NpcVisible"] = "Leah",
    ["NpcVisibleHere"] = "Leah", ["Season"] = "spring winter", ["SpouseBed"] = "", ["ReachedMineBottom"] = "",
    ["Year"] = "2", ["Gender"] = "male", ["HasItem"] = "24", ["Tile"] = "1 2 3 4",
    ["ActiveDialogueEvent"] = "conversation", ["DayOfMonth"] = "1 15", ["UpcomingFestival"] = "7",
    ["GameStateQuery"] = "SEASON Here spring", ["Skill"] = "Farming 5"
};
Check(ConditionParser.SupportedCanonicalNames.Count == 41, "all 41 canonical vanilla preconditions registered");
Check(ConditionParser.SupportedAliases.Count == 48, "all 48 aliases including legacy SendMail registered");
Check(canonicalSamples.Count == 41, "canonical parser matrix has 41 samples");
foreach ((string name, string arguments) in canonicalSamples)
{
    ConditionExpression parsed = aliasParser.ParseSegment(arguments.Length == 0 ? name : $"{name} {arguments}");
    Check(parsed is not OpaqueCondition, "canonical parse: " + name);
    Check(ConditionDescriber.Describe(parsed).LocalizationKey.Length > 0, "canonical describe: " + name);
    Check(aliasParser.ParseSegment((arguments.Length == 0 ? name.ToUpperInvariant() : $"{name.ToUpperInvariant()} {arguments}")) is not OpaqueCondition, "canonical case-insensitive: " + name);
}
foreach ((string alias, _) in ConditionParser.SupportedAliases)
{
    string canonical = ConditionParser.SupportedAliases[alias].Name;
    string arguments = canonical == "SendMail" ? "TestLetter" : canonicalSamples[canonical];
    Check(aliasParser.ParseSegment(arguments.Length == 0 ? alias : $"{alias} {arguments}") is not OpaqueCondition, "alias parse: " + alias);
}
Check(aliasParser.ParseSegment("Friendship Leah 1000 Robin 500") is FriendshipCondition { Requirements.Count: 2 });
Check(aliasParser.ParseSegment("Shipped 24 2 188 1") is ShippedCondition { Requirements.Count: 2 });
Check(aliasParser.ParseSegment("SawEvent 1 2") is SawEventCondition { EventIds.Count: 2 });
Check(aliasParser.ParseSegment("DayOfWeek Mon Fri") is DayOfWeekCondition { Days.Count: 2 });
Check(aliasParser.ParseSegment("Tile 1 2 3 4") is TileCondition { Positions.Count: 2 });
Check(aliasParser.ParseSegment("ChoseDialogueAnswers a b") is ChoseDialogueAnswersCondition { AnswerIds.Count: 2 });
Check(aliasParser.ParseSegment("Skill Farming nope") is OpaqueCondition { Kind: OpaqueConditionKind.MalformedKnown, KnownConditionName: "Skill" });
Check(aliasParser.ParseSegment("ThirdParty value") is OpaqueCondition { Kind: OpaqueConditionKind.UnknownType, KnownConditionName: null });
Check(aliasParser.ParseSegment("!Hl mail") is MailCondition { Scope: ConditionPlayerScope.HostPlayer, Negated: false }, "explicit and alias negation XOR");
ConditionDisplayResolver testResolver = new(name => $"NPC:{name}", id => $"ITEM:{id}", (group, value) => $"{group}:{value}", time => $"GAME-TIME:{time}");
string formattedFriendship = ConditionTextFormatter.Format(
    ConditionDescriber.Describe(aliasParser.ParseSegment("Friendship Leah 1000")),
    (key, arguments) => key == "condition.hearts-value" ? arguments["hearts"] + " ♥" : $"{key}|{arguments.GetValueOrDefault("requirements")}", testResolver);
Check(formattedFriendship.Contains("NPC:Leah") && formattedFriendship.Contains("4 ♥"), "typed friendship value resolves at formatter boundary");
System.Globalization.CultureInfo beforeHeartChecks = System.Globalization.CultureInfo.CurrentCulture;
try
{
    System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
    string HeartNumber(int points) => ConditionTextFormatter.FormatFriendshipCurrent(points, (_, arguments) => arguments["hearts"]);
    Check(HeartNumber(0) == "0" && HeartNumber(250) == "1" && HeartNumber(2600) == "10.4" && HeartNumber(2680) == "10.7",
        "2.1 final friendship points-to-hearts formatting");
}
finally { System.Globalization.CultureInfo.CurrentCulture = beforeHeartChecks; }
string formattedShipped = ConditionTextFormatter.Format(
    ConditionDescriber.Describe(aliasParser.ParseSegment("Shipped 24 2")),
    (key, arguments) => $"{key}|{arguments.GetValueOrDefault("requirements")}", testResolver);
Check(formattedShipped.Contains("ITEM:24 × 2"), "typed item value resolves at formatter boundary");
string formattedNegative = ConditionTextFormatter.Format(
    ConditionDescriber.Describe(aliasParser.ParseSegment("!Friendship Leah 1000")),
    (key, arguments) => $"{key}|{string.Join(',', arguments.Values)}", testResolver);
Check(formattedNegative.StartsWith("condition.friendship-not|"), "friendship uses natural aggregate negation");

ConditionEvaluationContext collectionContext = fullContext with
{
    Friendship = new Dictionary<string, int> { ["Leah"] = 1000, ["Robin"] = 499 },
    EventsSeen = new HashSet<string> { "2" }
};
Check(eval.Evaluate(aliasParser.ParseSegment("Friendship Leah 1000 Robin 500"), collectionContext).Truth == ConditionTruth.False, "friendship collection is all");
Check(eval.Evaluate(aliasParser.ParseSegment("SawEvent 1 2"), collectionContext).Truth == ConditionTruth.True, "seen-event collection is any");
Check(eval.Evaluate(aliasParser.ParseSegment("DaysPlayed 15"), fullContext with { DaysPlayed = 15 }).Truth == ConditionTruth.False, "days played is strict greater-than");
Check(eval.Evaluate(aliasParser.ParseSegment("Weather rainy"), fullContext with { Weather = "GreenRain", IsRaining = true }).Truth == ConditionTruth.True, "rainy uses rain predicate");
Check(eval.Evaluate(aliasParser.ParseSegment("Weather sunny"), fullContext with { Weather = "Sun", IsRaining = false }).Truth == ConditionTruth.True, "sunny uses inverse rain predicate");
Check(eval.Evaluate(aliasParser.ParseSegment("Weather GreenRain"), fullContext with { Weather = "GreenRain", IsRaining = true }).Truth == ConditionTruth.True, "custom weather compares ID");
CurrentStateSnapshot sharedWeatherState = new(
    Season: "spring", Weather: null, DayOfMonth: 1, Year: 1, Time: 600, DaysPlayed: 1,
    Friendship: null, EventsSeen: null, LocalMail: null, HostMail: null, HostOrLocalMail: null,
    Dating: new HashSet<string>(), Spouse: new HashSet<string>(), Roommate: false, WorldState: null, IsRaining: null);
CurrentStateSnapshot rainyLocationState = sharedWeatherState.ForLocation("Storm", true);
CurrentStateSnapshot sunnyLocationState = sharedWeatherState.ForLocation("Sun", false);
CurrentStateSnapshot unresolvedLocationState = sharedWeatherState.ForLocation(null, null);
Check(sharedWeatherState.Weather is null && sharedWeatherState.IsRaining is null, "shared weather stays unknown");
Check(rainyLocationState.Weather == "Storm" && rainyLocationState.IsRaining == true, "location overlay uses target rain");
Check(sunnyLocationState.Weather == "Sun" && sunnyLocationState.IsRaining == false, "location overlay uses target sun");
Check(unresolvedLocationState.Weather is null && unresolvedLocationState.IsRaining is null, "unresolved location stays unknown");
Check(eval.Evaluate(aliasParser.ParseSegment("Weather rainy"), rainyLocationState.ToConditionContext()).Truth == ConditionTruth.True, "rainy target evaluates true");
Check(eval.Evaluate(aliasParser.ParseSegment("Weather sunny"), sunnyLocationState.ToConditionContext()).Truth == ConditionTruth.True, "sunny target evaluates true");
Check(eval.Evaluate(aliasParser.ParseSegment("Weather sunny"), rainyLocationState.ToConditionContext()).Truth == ConditionTruth.False, "sunny target evaluates false while raining");
Check(eval.Evaluate(aliasParser.ParseSegment("Weather Storm"), rainyLocationState.ToConditionContext()).Truth == ConditionTruth.True, "custom target weather evaluates true");
ConditionEvaluation unresolvedRain = eval.Evaluate(aliasParser.ParseSegment("Weather rainy"), unresolvedLocationState.ToConditionContext());
Check(unresolvedRain.Truth == ConditionTruth.Unknown && unresolvedRain.Knowledge == ConditionKnowledge.MissingData, "unresolved target weather stays unknown");

System.Globalization.CultureInfo savedCulture = System.Globalization.CultureInfo.CurrentCulture;
try
{
    System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
    Check(aliasParser.ParseSegment("Random 0.5") is RandomCondition { Probability: 0.5f }, "numbers parse invariantly");
    Check(aliasParser.ParseSegment("Random 0,5") is OpaqueCondition, "localized decimal isn't accepted by vanilla syntax");
}
finally { System.Globalization.CultureInfo.CurrentCulture = savedCulture; }

string i18nDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../i18n"));
Check(aliasParser.ParseSegment("DayOfWeek Monday Friday") is DayOfWeekCondition { Days.Count: 2 }, "audit: full weekday names");
Check(aliasParser.ParseSegment("Gender Banana") is OpaqueCondition, "audit: invalid gender");
Dictionary<string, string> defaultLocale = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(i18nDirectory, "default.json")))!;
string TranslatePresentation(string key, IReadOnlyDictionary<string, string> arguments)
{
    Check(defaultLocale.ContainsKey(key), "presentation localization key: " + key);
    string template = defaultLocale[key];
    foreach ((string token, string value) in arguments)
        template = template.Replace("{{" + token + "}}", value);
    return template;
}
ConditionParser presentationParser = new(
    key => key.Split('/', StringSplitOptions.RemoveEmptyEntries),
    FakeSplitArgs);
ConditionPresentationBuilder presentationBuilder = new(
    presentationParser,
    new ConditionEvaluator(),
    TranslatePresentation,
    new ConditionDisplayResolver(name => "NPC:" + name, id => "ITEM:" + id, (group, value) => group + ":" + value, time => "GAME-TIME:" + time));
CurrentStateSnapshot presentationState = sharedWeatherState with
{
    Season = "spring",
    Time = 1400,
    Friendship = new Dictionary<string, int> { ["Abigail"] = 1750 }
};
IReadOnlyList<ConditionDisplayItem> presentationItems = presentationBuilder.Build(
    "1/Season spring/Friendship Abigail 2000/Friendship Abigail 2000/Time 1800 2200/Weather rainy/Random 0.5/SomeMod.Custom foo/Time bad/GameStateQuery WEATHER Here Sun/SendMail Letter/!Season winter",
    presentationState);
Check(presentationItems.Select(item => item.Expression.RawSegment).SequenceEqual(new[]
{
    "Season spring", "Friendship Abigail 2000", "Friendship Abigail 2000", "Time 1800 2200", "Weather rainy",
    "Random 0.5", "SomeMod.Custom foo", "Time bad", "GameStateQuery WEATHER Here Sun", "SendMail Letter", "!Season winter"
}), "presentation preserves declaration order and duplicates");
Check(presentationItems[0].Evaluation is { Truth: ConditionTruth.True, Knowledge: ConditionKnowledge.Known }, "presentation known true");
Check(presentationItems[0].CurrentValue is null, "presentation omits redundant met season current value");
ConditionDisplayItem friendshipPresentation = presentationItems[1];
Check(friendshipPresentation.Evaluation.Truth == ConditionTruth.False && friendshipPresentation.GapSubject == "NPC:Abigail", "presentation friendship gap subject");
Check(friendshipPresentation.CurrentValue == "7 hearts" && friendshipPresentation.RequiredValue == "8 hearts", "presentation friendship values humanized");
ConditionDisplayItem timePresentation = presentationItems[3];
Check(timePresentation.CurrentValue == "GAME-TIME:1400" && timePresentation.RequiredValue == "GAME-TIME:1800 – GAME-TIME:2200", "presentation time range humanized");
Check(presentationItems[4].Evaluation.Knowledge == ConditionKnowledge.MissingData, "presentation unresolved weather missing data");
Check(presentationItems[5].Evaluation.Knowledge == ConditionKnowledge.Unsupported, "presentation random unsupported");
Check(presentationItems[6].Evaluation.Knowledge == ConditionKnowledge.Unsupported, "presentation custom unsupported");
Check(presentationItems[7].Evaluation.Knowledge == ConditionKnowledge.Invalid, "presentation malformed invalid");
Check(presentationItems[8].Evaluation.Knowledge == ConditionKnowledge.MissingData, "supported weather GSQ without captured location remains unknown");
Check(presentationItems[9].Evaluation.Knowledge == ConditionKnowledge.Unsupported, "presentation SendMail unsupported");
Check(presentationItems[10].Expression is SeasonCondition { Negated: true, Source: ConditionSource.LegacyEventPrecondition }
    && presentationItems[10].Expression.RawSegment == "!Season winter", "presentation retains raw source and negation");
ConditionDisplayItem sunnyMet = presentationBuilder.Build("1/Weather sunny", sunnyLocationState).Single();
ConditionDisplayItem sunnyMissing = presentationBuilder.Build("1/Weather sunny", rainyLocationState).Single();
Check(sunnyMet.CurrentValue is null, "2.1 wording omits redundant true weather current value");
Check(sunnyMissing.CurrentValue == "weather:stormy", "2.1 wording humanizes false weather current value");
Check(presentationItems.All(item => !item.Description.StartsWith("Met:") && !item.Description.StartsWith("Missing:")
    && !item.Description.StartsWith("Can't determine safely:")), "2.1 wording keeps status out of requirements");
string compactPresentation = presentationBuilder.Compact(presentationItems);
Check(compactPresentation.Split("Friendship:", StringSplitOptions.None).Length == 2, "compact summary deduplicates repeated requirement text");
Check(!compactPresentation.Contains("Met:") && !compactPresentation.Contains("Missing:") && !compactPresentation.Contains("Can't determine safely:"),
    "compact requirement text must not contain evaluator status wrappers");
Check(presentationBuilder.Build("1", presentationState).Count == 0, "presentation no-condition items empty");
Check(presentationBuilder.Compact([]) == defaultLocale["condition.none"], "presentation no-condition compact text");

EventCardInteraction unlockedCard = GalleryUiRules.EventCardInteraction(unlocked: true);
EventCardInteraction lockedCard = GalleryUiRules.EventCardInteraction(unlocked: false);
Check(unlockedCard is { CanReplay: true, CanViewDetails: true }, "2.1 correction unlocked card actions");
Check(lockedCard is { CanReplay: false, CanViewDetails: true }, "2.1 correction locked card actions");
Check(Enumerable.Range(0, 7).Select(GalleryUiRules.EventCardPosition).SequenceEqual(new[]
{
    (0, 0), (0, 1), (1, 0), (1, 1), (2, 0), (2, 1), (3, 0)
}), "2.1 correction two-column visual order");
Check(GalleryUiRules.VisibleEventCount(4, 0) == 4 && GalleryUiRules.VisibleEventCount(5, 0) == 5
    && GalleryUiRules.VisibleEventCount(6, 0) == 6 && GalleryUiRules.VisibleEventCount(9, 0) == 6
    && GalleryUiRules.VisibleEventCount(9, 6) == 3, "2.1 final visible slot count");
var cardBounds = Enumerable.Range(0, 6).Select(GalleryUiRules.EventCardBounds).ToList();
Check(cardBounds.SelectMany((left, index) => cardBounds.Skip(index + 1).Select(right =>
    left.X < right.X + right.Width && left.X + left.Width > right.X
    && left.Y < right.Y + right.Height && left.Y + left.Height > right.Y)).All(overlap => !overlap),
    "2.1 correction cards do not overlap");

Check(ConditionRowPresentation.Status(presentationItems[0].Evaluation) == ConditionStatusIcon.Check, "2.1 correction known true icon");
Check(ConditionRowPresentation.Status(friendshipPresentation.Evaluation) == ConditionStatusIcon.Cross, "2.1 correction known false icon");
foreach (ConditionKnowledge knowledge in new[] { ConditionKnowledge.MissingData, ConditionKnowledge.Unsupported, ConditionKnowledge.Invalid, ConditionKnowledge.Error })
{
    ConditionEvaluation unknown = new(friendshipPresentation.Expression, ConditionTruth.Unknown, knowledge, new ConditionGap(ConditionGapKind.Unavailable));
    ConditionDisplayItem item = friendshipPresentation with { Evaluation = unknown };
    Check(ConditionRowPresentation.Status(unknown) == ConditionStatusIcon.Unknown, "2.1 correction unknown icon: " + knowledge);
    Check(ConditionRowPresentation.UnknownReasonKey(item) is not null, "2.1 correction unknown reason: " + knowledge);
}
string friendshipRow = ConditionRowPresentation.Text(friendshipPresentation, TranslatePresentation);
string timeRow = ConditionRowPresentation.Text(timePresentation, TranslatePresentation);
Check(friendshipRow.Contains("8 hearts") && friendshipRow.Contains("7 hearts"), "2.1 correction friendship requirement/current row");
Check(timeRow.Contains("GAME-TIME:1800") && timeRow.Contains("GAME-TIME:2200") && timeRow.Contains("GAME-TIME:1400"),
    "2.1 correction time requirement/current row");
ConditionDisplayItem multiFriendship = presentationBuilder.Build(
    "1/Friendship Abigail 2000 Leah 3000",
    presentationState with { Friendship = new Dictionary<string, int> { ["Abigail"] = 2000, ["Leah"] = 2680 } }).Single();
string multiFriendshipRow = ConditionRowPresentation.Text(multiFriendship, TranslatePresentation);
Check(multiFriendship.GapSubject == "NPC:Leah" && multiFriendshipRow.Contains("NPC:Leah current: 10.7 hearts")
    && !multiFriendshipRow.Contains("(current:", StringComparison.Ordinal), "2.1 review correction multi-friendship current subject");
ConditionDisplayItem fractionalSingleFriendship = presentationBuilder.Build(
    "1/Friendship Abigail 3000",
    presentationState with { Friendship = new Dictionary<string, int> { ["Abigail"] = 2600 } }).Single();
Check(ConditionRowPresentation.Text(fractionalSingleFriendship, TranslatePresentation).Contains("current: 10.4 hearts"),
    "2.1 final single-friendship fractional current hearts");
string noCurrentRow = ConditionRowPresentation.Text(presentationItems[5], TranslatePresentation);
Check(noCurrentRow == presentationItems[5].Description && !noCurrentRow.Contains("current", StringComparison.OrdinalIgnoreCase),
    "2.1 correction no empty current label");
Check(EventThumbnailAsset.For(TestIdentity("one")) == EventThumbnailAsset.For(TestIdentity("two"))
    && EventThumbnailAsset.For(TestIdentity("one")) == "assets/EventPlaceholder.png", "2.1 correction shared placeholder provider");
string AuditFormat(string input, Dictionary<string, string> language, ConditionDisplayResolver? resolver = null)
{
    string TranslateAudit(string key, IReadOnlyDictionary<string, string> arguments)
    {
        Check(language.ContainsKey(key), "audit: missing localization key " + key);
        string text = language[key];
        foreach (var pair in arguments)
            text = text.Replace("{{" + pair.Key + "}}", pair.Value);
        Check(!text.Contains("{{"), "audit: unresolved formatter token " + key);
        return text;
    }
    return ConditionTextFormatter.Format(ConditionDescriber.Describe(aliasParser.ParseSegment(input)), TranslateAudit, resolver ?? testResolver);
}
Check(AuditFormat("SawEvent A B", defaultLocale).Contains("A, B"), "audit: seen IDs reach final template");
Check(AuditFormat("Friendship Abigail 250", defaultLocale).Contains("1 hearts"), "audit: whole heart");
Check(AuditFormat("Friendship Abigail 251", defaultLocale).Contains("1.004 hearts"), "audit: exact fractional heart threshold");
Check(AuditFormat("Weather rainy", defaultLocale) == "It is raining", "audit: rain predicate wording");
Check(AuditFormat("Weather sunny", defaultLocale) == "Weather: sunny", "audit: sunny predicate wording");
Check(AuditFormat("!Weather sunny", defaultLocale) == "Weather: not sunny", "audit: negated sunny wording");
Check(AuditFormat("Weather GreenRain", defaultLocale).Contains("GreenRain"), "audit: custom weather raw ID");
Check(!AuditFormat("Weather Sun", defaultLocale).Contains("Sun", StringComparison.Ordinal), "2.1 wording humanizes raw Sun");
Check(AuditFormat("!Weather rainy", defaultLocale) == "It is not raining", "2.1 wording natural no-rain requirement");
Check(AuditFormat("DayOfWeek Saturday", defaultLocale).Contains("Saturday"), "2.1 wording weekday positive");
Check(AuditFormat("!DayOfWeek Saturday", defaultLocale) == "Not weekday:Saturday", "2.1 wording weekday negation");
Check(AuditFormat("!Spouse Penny", defaultLocale) == "Not married to NPC:Penny", "2.1 wording spouse negation");
Check(AuditFormat("!Dating Penny", defaultLocale) == "Not dating NPC:Penny", "2.1 wording dating negation");
Check(AuditFormat("!Roommate", defaultLocale) == "Not living with NPC:Krobus", "2.1 wording roommate negation");
Check(AuditFormat("!Season winter", defaultLocale) == "Season isn't season:winter", "2.1 wording season negation");
Check(AuditFormat("!SawEvent 75160352", defaultLocale) == "Hasn't seen event 75160352", "2.1 wording seen-event negation");
Check(AuditFormat("InUpgradedHouse 2", defaultLocale).Contains("upgrade level 2"), "2.1 wording farmhouse level");
string npcAtLocation = ConditionTextFormatter.Format(
    ConditionDescriber.Describe(aliasParser.ParseSegment("NpcVisibleHere Abigail"), "Pierre's General Store"),
    TranslatePresentation, testResolver);
Check(npcAtLocation == "NPC:Abigail at Pierre's General Store", "2.1 wording NPC at event target location");
Check(AuditFormat("NpcVisible Abigail", defaultLocale) == "NPC:Abigail is present and visible", "2.4 global visibility does not claim a location");
Check(ConditionRowPresentation.UnknownReasonKey(presentationItems[5]) == "event-detail.unknown.random", "random explains deferred drawing");
Check(ConditionRowPresentation.UnknownReasonKey(presentationItems[9]) == "event-detail.unknown.action", "mail action explains its background role");
Check(AuditFormat("Time 600 2600", defaultLocale).Contains("GAME-TIME:2600"), "audit: native time delegate");
Check(AuditFormat("HasItem Some.Invalid.Item", defaultLocale, testResolver with { Item = _ => null }).Contains("Some.Invalid.Item"), "audit: item raw fallback");
foreach (string input in new[] { "", "!", "Time \"600" })
    Check(eval.Evaluate(aliasParser.ParseSegment(input), fullContext).Knowledge == ConditionKnowledge.Invalid, "audit: malformed syntax invalid");
Check(eval.Evaluate(aliasParser.ParseSegment("Time bad"), fullContext).Knowledge == ConditionKnowledge.Invalid, "audit: malformed known invalid");
Check(eval.Evaluate(aliasParser.ParseSegment("SomeMod.Custom foo"), fullContext).Knowledge == ConditionKnowledge.Unsupported, "audit: custom unsupported");
foreach (string input in new[] { "SendMail TestLetter", "x TestLetter true", "!SendMail TestLetter false" })
{
    ConditionExpression legacy = aliasParser.ParseSegment(input);
    Check(legacy is LegacySendMailCondition, "audit: legacy SendMail typed");
    Check(eval.Evaluate(legacy, fullContext).Knowledge == ConditionKnowledge.Unsupported, "audit: SendMail never evaluated");
    Check(AuditFormat(input, defaultLocale) == "Special mail condition", "audit: SendMail player-safe wording");
}
Check(aliasParser.ParseSegment("SendMail TestLetter") is LegacySendMailCondition { InMailboxToday: false }, "audit: SendMail default");
Check(aliasParser.ParseSegment("x TestLetter true") is LegacySendMailCondition { InMailboxToday: true }, "audit: SendMail today");
Check(aliasParser.ParseSegment("x TestLetter banana") is OpaqueCondition { Kind: OpaqueConditionKind.MalformedKnown }, "audit: SendMail invalid bool");
Check(aliasParser.ParseSegment("Gender FEMALE") is GenderCondition, "audit: valid gender");
NativePreconditionProbe nativeProbe = new(key => key.Split('/', StringSplitOptions.RemoveEmptyEntries), FakeSplitArgs);
int readOnlyNativeCalls = 0;
string? NativeCatalogCheck(string key) { readOnlyNativeCalls++; return "matched"; }
Check(nativeProbe.Check("TEST/Season spring/!Time 600 1200", NativeCatalogCheck).Status == NativePreconditionProbeStatus.Matched
    && readOnlyNativeCalls == 1, "2.0.4 safe vanilla calls native exactly once");
Check(nativeProbe.Check("TEST", NativeCatalogCheck).Status == NativePreconditionProbeStatus.Matched
    && readOnlyNativeCalls == 2, "2.0.4 key without preconditions may call native");
foreach (string key in new[]
{
    "TEST/Random 0.5", "TEST/!r 0.5", "TEST/SendMail TestLetter", "TEST/x TestLetter true",
    "TEST/GameStateQuery WEATHER Here Sun", "TEST/SomeMod.Custom foo", "TEST/Time bad", "TEST/!"
})
{
    int before = readOnlyNativeCalls;
    Check(nativeProbe.Check(key, NativeCatalogCheck).Status == NativePreconditionProbeStatus.NotSafelyEvaluated,
        "2.0.4 unsafe probe blocked: " + key);
    Check(readOnlyNativeCalls == before, "2.0.4 unsafe callback count zero: " + key);
}
Check(nativeProbe.Check("TEST/Season spring", _ => throw new InvalidOperationException("native failure")).Status
    == NativePreconditionProbeStatus.Error, "2.0.4 native throw maps to error");
foreach (string? nativeResult in new string?[] { null, "", "-1" })
    Check(nativeProbe.Check("TEST/Season spring", _ => nativeResult).Status == NativePreconditionProbeStatus.NotMatched,
        "2.0.4 native false mapping");
string[] localeFiles = Directory.GetFiles(i18nDirectory, "*.json");
Check(localeFiles.Length == 12, "all 12 official locale files present");
foreach (string localeFile in localeFiles)
{
    using var localeJson = System.Text.Json.JsonDocument.Parse(File.ReadAllText(localeFile));
    Check(localeJson.RootElement.EnumerateObject().GroupBy(property => property.Name).All(group => group.Count() == 1),
        "locale keys are unique: " + Path.GetFileName(localeFile));
    Dictionary<string, string> locale = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(localeJson.RootElement.GetRawText())!;
    foreach (var sample in canonicalSamples)
        AuditFormat(sample.Key + " " + sample.Value, locale);
    foreach (string query in new[] { "G !IS_PASSIVE_FESTIVAL_TODAY SquidFest",
        "G !IS_PASSIVE_FESTIVAL_TODAY TroutDerby, !SEASON_DAY summer 17 summer 18 summer 19",
        "G PLAYER_STAT Any monstersKilled 1000", "G PLAYER_STAT Current monstersKilled 1 100" })
    {
        string text = AuditFormat(query, locale);
        Check(!text.Contains("IS_PASSIVE_FESTIVAL_TODAY") && !text.Contains("SEASON_DAY") && !text.Contains("PLAYER_STAT"),
            "GSQ commands replaced with readable requirements: " + Path.GetFileName(localeFile));
    }
    foreach (string input in new[] { "SendMail TestLetter", "Friendship Abigail 251", "Weather sunny", "!Weather rainy", "SomeMod.Custom foo", "Time bad" })
        AuditFormat(input, locale);
    Check(locale.Keys.OrderBy(key => key).SequenceEqual(defaultLocale.Keys.OrderBy(key => key)), "locale key parity: " + Path.GetFileName(localeFile));
    foreach (string key in defaultLocale.Keys)
    {
        string[] expectedTokens = System.Text.RegularExpressions.Regex.Matches(defaultLocale[key], "{{[^}]+}}" ).Select(match => match.Value).OrderBy(token => token).ToArray();
        string[] actualTokens = System.Text.RegularExpressions.Regex.Matches(locale[key], "{{[^}]+}}" ).Select(match => match.Value).OrderBy(token => token).ToArray();
        Check(actualTokens.SequenceEqual(expectedTokens), $"locale token parity: {Path.GetFileName(localeFile)}:{key}");
    }
}

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

foreach (string weather in new[] { "Sun", "Rain", "Storm", "Snow", "Wind", "GreenRain" })
{
    ReplaySceneEnvironment vanillaScene = ReplaySceneEnvironmentResolver.Resolve(
        sceneParser.ParseRawKey("203/WEATHER " + weather).Conditions, "summer", 900, "Sun");
    Check(vanillaScene.Weather == weather && vanillaScene.Warning is null,
        "P2 scene accepts vanilla weather " + weather);
}
ReplaySceneEnvironment greenRainScene = ReplaySceneEnvironmentResolver.Resolve(
    sceneParser.ParseRawKey("204/WEATHER rainy").Conditions, "summer", 900, "GreenRain");
Check(greenRainScene.Weather == "GreenRain" && greenRainScene.Warning is null,
    "P2 rainy retains already-satisfying green rain");
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
    Func<NativePreconditionProbeResult> probePrecondition)
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
    return new ResolvedEventCandidate(resolved, probePrecondition);
}

static NativePreconditionProbeResult Matched() => new(NativePreconditionProbeStatus.Matched);
static NativePreconditionProbeResult NotMatched() => new(NativePreconditionProbeStatus.NotMatched);
static NativePreconditionProbeResult NotSafelyEvaluated() => new(NativePreconditionProbeStatus.NotSafelyEvaluated);
static NativePreconditionProbeResult ProbeError() => new(NativePreconditionProbeStatus.Error);
static NativePreconditionProbeResult CountedMatch(Action count) { count(); return Matched(); }

static HashSet<string> Set(params string[] names) => new(names, StringComparer.Ordinal);

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
