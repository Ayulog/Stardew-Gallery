using StardewGallery;

internal static class OrdinaryReplayChecks
{
    internal static void Run()
    {
        OrdinaryReplayPolicy policy = new(script => script.Split('/'), command => command.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        ResolvedEvent entry = Entry("ordinary", "none/0 0/Abigail 1 1 2 farmer 2 1 2/skippable/speak Abigail hello/pause 20/end");
        ReplayCompatibility CheckEntry(ResolvedEvent value) => policy.Check(value, _ => true, name => name == "Town", _ => true);
        Require(CheckEntry(entry).Supported, "complete ordinary scene can replay");
        Require(CheckEntry(Entry("ordinary", "none/0 0/farmer 1 1 2/addConversationTopic test 4/eventSeen other/questionAnswered topic/end")).Supported,
            "player-state commands covered by replay snapshot are supported");
        Require(!CheckEntry(Entry("ordinary", "none/0 0/farmer 1 1 2/pause 50")).Supported, "unending scene is not a complete replay");
        Require(!CheckEntry(entry with { HasLocationContext = false }).Supported, "supplement without real location stays read-only");
        Require(!CheckEntry(entry with { Fragments = new([entry.ResolvedScript], ["fork"]) }).Supported, "missing fragment blocks before launch");
        Require(!policy.Check(entry, _ => false, _ => true, _ => true).Supported, "replaced/custom handlers cannot masquerade as native");
        Require(!policy.Check(entry, _ => true, _ => false, _ => true).Supported, "missing live location");
        Require(!policy.Check(entry, _ => true, _ => true, _ => false).Supported, "missing required actor");
        foreach (string command in new[] { "end newDay", "end Leo", "end wedding", "action AddMail test", "setSkipActions AddItem test", "quickQuestion x(break)action AddMail test", "locationSpecificCommand reward", "modCommand test", "addSpecialOrder test", "changeLocation Missing", "money -3000" })
            Require(!CheckEntry(Entry("ordinary", "none/0 0/Abigail 1 1 2/" + command + "/end")).Supported, "reject unverified execution: " + command);
        Require(!CheckEntry(Entry("ordinary", "none/0 0/Abigail -1 -1 2/speak Abigail hello/end")).Supported, "do not reuse live NPC for scene actors");
        Require(!CheckEntry(Entry("ordinary", "none/0 0/Abigail -01 -1 2/speak Abigail hello/end")).Supported, "numeric live-actor sentinel cannot bypass isolation");
        Require(!CheckEntry(Entry("ordinary", "none/0 0/farmer 1 1 2/speak Abigail hello/end")).Supported, "speaker cannot mutate live NPC dialogue");
        Require(!CheckEntry(Entry("ordinary", "none/0 0/farmer 1 1 2/changePortrait Abigail/end")).Supported, "portrait command cannot mutate live NPC appearance");
        Require(!CheckEntry(Entry("1039573", entry.ResolvedScript)).Supported, "special completion side effect requires dedicated support");
        Require(!CheckEntry(Entry("2123343", entry.ResolvedScript)).Supported, "native skip new-day side effect blocked");
        Require(CheckEntry(Entry("ordinary", "none/0 0/farmer 1 1 2/message hello/end position 1 1")).Supported, "ownerless ordinary scene supported");
        var fork = entry with { Fragments = new([entry.ResolvedScript, "action WorldState test"], []) };
        Require(!CheckEntry(fork).Supported, "every referenced branch is checked");
        GalleryEvent ordinary = new(entry, new(OwnershipKind.Excluded, [])) { OrdinaryReplaySupported = true };
        GalleryCatalog catalog = new([], [], [ordinary]);
        Require(GalleryEventNavigation.IsReplayListed(catalog, ordinary), "ordinary playback needs no social owner");
        Require(!GalleryEventNavigation.IsReplayListed(catalog, ordinary with { ReplayUnavailableReason = "blocked" }), "explicit restriction beats unlocked status");
        Require(!GalleryEventNavigation.IsReplayListed(new([], [], []), ordinary), "entry must be in the current catalog");
        Console.WriteLine("Ordinary replay policy checks passed.");
    }
    private static ResolvedEvent Entry(string id, string script) => new(new("Data/Events/Town", id), "Town", id, script, new([script], []), "definition", "script");
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("Ordinary replay: " + message); }
}
