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
IReadOnlyDictionary<string, EventOwnership> ownership = OwnershipResolver.Resolve(events, characters);
Check(ownership["torts7"].Kind == OwnershipKind.Direct && ownership["torts7"].Owners.Single().Name == "Torts");
Check(ownership["torts8"].Kind == OwnershipKind.Inherited && ownership["torts8"].Owners.Single().Name == "Torts");
Check(ownership["tie"].Kind == OwnershipKind.Inferred && ownership["tie"].Owners.Count == 2);
Check(ownership["silent"].Kind == OwnershipKind.Excluded);
Check(ownership["inferred-child"].Kind == OwnershipKind.Inherited && ownership["inferred-child"].Owners.Single().Name == "Alissa");
Check(ownership["multi-direct"].Kind == OwnershipKind.Direct && ownership["multi-direct"].Owners.Single().Name == "Alissa");

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
    => new(identity, id, friendship, prerequisites, actors, dialogue);

static HashSet<string> Set(params string[] names) => new(names, StringComparer.Ordinal);
