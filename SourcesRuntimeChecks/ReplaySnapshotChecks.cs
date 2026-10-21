using System.Collections;
using System.Reflection;
using System.Runtime.Loader;
using HarmonyLib;

/// <summary>Exercises production capture/restore with actual game collections in a fresh, headless process.</summary>
internal static class ReplaySnapshotChecks
{
    private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static readonly string[] SetFields = ["eventsSeen", "mailReceived", "mailForTomorrow", "dialogueQuestionsAnswered"];
    private static readonly string[] DictionaryFields = ["activeDialogueEvents", "previousActiveDialogueEvents", "cookingRecipes", "craftingRecipes"];
    private static int assertions;
    private static int failures;

    internal static void Run(Assembly mod, string gameDirectory)
    {
        Assembly game = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(gameDirectory, "Stardew Valley.dll"));
        Type game1 = game.GetType("StardewValley.Game1", true)!;
        Type farmerType = game.GetType("StardewValley.Farmer", true)!;
        var harmony = new Harmony("Gallery.SnapshotChecks");
        try
        {
            // Only graphics-dependent initialization is skipped; game collections and the tested methods are real.
            foreach (MethodInfo method in new[]
            {
                game.GetType("StardewValley.BellsAndWhistles.PlayerStatusList", true)!.GetMethod("AddSpriteDefinition")!,
                farmerType.GetMethod("farmerInit", Members)!,
                game.GetType("StardewValley.AnimatedSprite", true)!.GetMethod("LoadTexture")!
            })
                harmony.Patch(method, prefix: new HarmonyMethod(typeof(ReplaySnapshotChecks), nameof(SkipGraphics)));

            object farmer = Activator.CreateInstance(farmerType)!;
            Set(game1, "player", farmer);
            object buffs = Get(farmer, "buffs");
            Set(buffs, "Player", farmer);
            Set(buffs, "Dirty", false); // Empty, already-computed buffs; avoid renderer callbacks in this headless fixture.
            Set(buffs, "AppliedBuffIds", Activator.CreateInstance(buffs.GetType().GetField("AppliedBuffIds", Members)!.FieldType)!);
            Set(game1, "game1", System.Runtime.Serialization.FormatterServices.GetUninitializedObject(game1));
            Set(game1, "currentLocation", Activator.CreateInstance(game.GetType("StardewValley.GameLocation", true)!)!);
            object world = Activator.CreateInstance(game.GetType("StardewValley.Network.NetWorldState", true)!)!;
            Set(game1, "netWorldState", Activator.CreateInstance(game1.GetField("netWorldState")!.FieldType, world)!);
            Type snapshotType = mod.GetType("StardewGallery.ReplaySnapshot", true)!;
            MethodInfo capture = snapshotType.GetMethod("Capture", Members)!;
            MethodInfo restore = snapshotType.GetMethod("RestorePlayer", Members)!;
            var sets = SetFields.Select(name => (Name: name, Values: (ICollection<string>)Get(farmer, name)))
                .Append((Name: "eventsSeenSinceLastLocationChange", Values: (ICollection<string>)Get(game1, "eventsSeenSinceLastLocationChange"))).ToArray();
            foreach (var entry in sets) Seed(entry.Values);
            object snapshot = capture.Invoke(null, capture.GetParameters().Length == 0 ? null : [null])!;
            foreach (var entry in sets) { entry.Values.Clear(); entry.Values.Add("ReplayOnly"); }
            restore.Invoke(snapshot, null);
            foreach (var entry in sets) CheckPair(entry.Values, entry.Name + " survives real capture/restore");
            restore.Invoke(snapshot, null);
            foreach (var entry in sets) CheckPair(entry.Values, entry.Name + " survives repeated restore");

            // Reset these after the first phase so the current-state reader is tested independently.
            foreach (var entry in sets) Seed(entry.Values);
            foreach (string field in DictionaryFields)
            {
                object dictionary = Get(farmer, field);
                dictionary.GetType().GetMethod("Clear", Type.EmptyTypes)!.Invoke(dictionary, null);
                Put(dictionary, "Case.Key", 12);
                Put(dictionary, "CASE.KEY", 34);
            }
            object friendships = Get(farmer, "friendshipData");
            Type friendshipType = game.GetType("StardewValley.Friendship", true)!;
            object first = Activator.CreateInstance(friendshipType)!;
            object second = Activator.CreateInstance(friendshipType)!;
            Set(first, "Points", 123);
            Set(second, "Points", 456);
            Put(friendships, "Case.Key", first);
            Put(friendships, "CASE.KEY", second);
            try
            {
                snapshot = capture.Invoke(null, capture.GetParameters().Length == 0 ? null : [null])!;
                foreach (string field in DictionaryFields)
                {
                    object dictionary = Get(farmer, field);
                    dictionary.GetType().GetMethod("Clear", Type.EmptyTypes)!.Invoke(dictionary, null);
                    Put(dictionary, "ReplayOnly", 99);
                }
                friendships.GetType().GetMethod("Clear", Type.EmptyTypes)!.Invoke(friendships, null);
                Put(friendships, "ReplayOnly", Activator.CreateInstance(friendshipType)!);
                restore.Invoke(snapshot, null);
                foreach (string field in DictionaryFields)
                {
                    object dictionary = Get(farmer, field);
                    CheckPair(((IEnumerable)Get(dictionary, "Keys")).Cast<string>(), field + " preserves both keys and removes replay additions");
                    Check((int)At(dictionary, "Case.Key") == 12 && (int)At(dictionary, "CASE.KEY") == 34, field + " preserves distinct values");
                }
                CheckPair(((IEnumerable)Get(friendships, "Keys")).Cast<string>(), "friendshipData preserves both keys and removes replay additions");
                Check((int)Get(At(friendships, "Case.Key"), "Points") == 123 && (int)Get(At(friendships, "CASE.KEY"), "Points") == 456, "Friendship values restored independently");
            }
            catch (TargetInvocationException ex) when (ex.InnerException is ArgumentException)
            {
                Check(false, "Real capture accepts case-distinct dictionary keys: " + ex.InnerException.Message);
            }

            object current = mod.GetType("StardewGallery.RuntimeStateReader", true)!.GetMethod("Capture", Members)!.Invoke(null, null)!;
            foreach (string name in new[] { "EventsSeen", "LocalMail", "HostMail", "HostOrLocalMail" })
                CheckPair((IEnumerable<string>)Get(current, name), "RuntimeStateReader " + name + " retains case-sensitive flags");
            var currentFriendships = (IReadOnlyDictionary<string, int>)Get(current, "Friendship");
            Check(currentFriendships.Count == 2 && currentFriendships["Case.Key"] == 123 && currentFriendships["CASE.KEY"] == 456
                && !currentFriendships.ContainsKey("case.key"), "RuntimeStateReader preserves distinct friendships and lookup semantics");
            CheckInventory((StardewValley.Farmer)farmer, capture, restore);
            CheckProgressFlags((StardewValley.Farmer)farmer, capture, restore);
            CheckRelationshipState(mod, (StardewValley.Farmer)farmer);
        }
        finally { harmony.UnpatchAll(harmony.Id); }
        if (failures > 0) throw new InvalidOperationException($"{failures} of {assertions} replay snapshot checks failed.");
        Console.WriteLine($"All {assertions} replay snapshot checks passed.");
    }

    /// <summary>Preconditions.Dating uses Friendship.IsDating(); Preconditions.Roommate uses hasCurrentOrPendingRoommate() (1.6.15).</summary>
    private static void CheckRelationshipState(Assembly mod, StardewValley.Farmer farmer)
    {
        MethodInfo read = mod.GetType("StardewGallery.RuntimeStateReader", true)!.GetMethod("Capture", Members)!;
        farmer.friendshipData.Clear();
        foreach ((string npc, StardewValley.FriendshipStatus status) in new[]
        {
            ("Dated", StardewValley.FriendshipStatus.Dating), ("Engaged", StardewValley.FriendshipStatus.Engaged),
            ("Married", StardewValley.FriendshipStatus.Married), ("Friend", StardewValley.FriendshipStatus.Friendly),
            ("Divorced", StardewValley.FriendshipStatus.Divorced)
        })
            farmer.friendshipData[npc] = new StardewValley.Friendship { Status = status };
        farmer.spouse = "Engaged";
        object state = read.Invoke(null, null)!;
        Check(((IReadOnlySet<string>)Get(state, "Dating")).SetEquals(["Dated", "Engaged", "Married"]), "D counts dating, engaged and married NPCs like the game");
        Check(Equals(Get(state, "Roommate"), false), "an engaged spouse is not a roommate");
        farmer.friendshipData["Krobus"] = new StardewValley.Friendship { Status = StardewValley.FriendshipStatus.Engaged, RoommateMarriage = true };
        farmer.spouse = "Krobus";
        Check(Equals(Get(read.Invoke(null, null)!, "Roommate"), true), "R counts a roommate before the move-in day");
        farmer.friendshipData.Clear();
        farmer.spouse = null;
    }

    /// <summary>Event music, warps, dialogue actions and food change these outside the event cast.</summary>
    private static void CheckProgressFlags(StardewValley.Farmer farmer, MethodInfo capture, MethodInfo restore)
    {
        farmer.songsHeard.Clear(); farmer.songsHeard.Add("Before");
        farmer.locationsVisited.Clear(); farmer.locationsVisited.Add("Town");
        farmer.triggerActionsRun.Clear(); farmer.triggerActionsRun.Add("Fixture.Rule");
        farmer.stats.Values.Clear(); farmer.stats.Values["stepsTaken"] = 10;
        object snapshot = capture.Invoke(null, capture.GetParameters().Length == 0 ? null : [null])!;
        farmer.songsHeard.Add("ReplayOnly");
        farmer.locationsVisited.Add("Mountain");
        farmer.triggerActionsRun.Add("Fixture.ReplayRule");
        farmer.stats.Values["stepsTaken"] = 99; farmer.stats.Values["replayOnly"] = 1;
        restore.Invoke(snapshot, null);
        Check(farmer.songsHeard.SetEquals(["Before"]), "songs heard during replay music are removed");
        Check(farmer.locationsVisited.Count == 1 && farmer.locationsVisited.Contains("Town"), "locations visited only by the replay are removed");
        Check(farmer.triggerActionsRun.Count == 1 && farmer.triggerActionsRun.Contains("Fixture.Rule"), "trigger actions applied during replay can run again later");
        Check(farmer.stats.Values.Count == 1 && farmer.stats.Values["stepsTaken"] == 10, "stats return to their pre-replay values");
    }

    /// <summary>Real tools with attachments; Item.getOne() copies would drop bait, tackle and ammo.</summary>
    private static void CheckInventory(StardewValley.Farmer farmer, MethodInfo capture, MethodInfo restore)
    {
        // Minimal item data so constructors resolve without the content pipeline.
        StardewValley.Game1.objectData = new Dictionary<string, StardewValley.GameData.Objects.ObjectData>
        {
            ["685"] = new() { Name = "Bait", DisplayName = "Bait", Type = "Basic", Category = -21, Price = 1 },
            ["687"] = new() { Name = "Dressed Spinner", DisplayName = "Dressed Spinner", Type = "Basic", Category = -22, Price = 500 },
            ["441"] = new() { Name = "Explosive Ammo", DisplayName = "Explosive Ammo", Type = "Basic", Category = -1, Price = 20 },
            ["388"] = new() { Name = "Wood", DisplayName = "Wood", Type = "Basic", Category = -16, Price = 2 }
        };
        StardewValley.Game1.weaponData = new Dictionary<string, StardewValley.GameData.Weapons.WeaponData>
            { ["32"] = new() { Name = "Slingshot", DisplayName = "Slingshot", Type = 4 } };
        StardewValley.ItemRegistry.ItemTypes.Add(new StardewValley.ItemTypeDefinitions.ObjectDataDefinition());
        StardewValley.ItemRegistry.ItemTypes.Add(new StardewValley.ItemTypeDefinitions.WeaponDataDefinition());
        var rod = new StardewValley.Tools.FishingRod { AttachmentSlotsCount = 2 };
        rod.attachments[0] = new StardewValley.Object("685", 25);
        rod.attachments[1] = new StardewValley.Object("687", 1);
        var sling = new StardewValley.Tools.Slingshot("32") { AttachmentSlotsCount = 1 };
        sling.attachments[0] = new StardewValley.Object("441", 40);
        var wood = new StardewValley.Object("388", 50);
        farmer.Items.Clear();
        farmer.Items.Add(rod); farmer.Items.Add(sling); farmer.Items.Add(wood); farmer.Items.Add(null);
        object snapshot = capture.Invoke(null, capture.GetParameters().Length == 0 ? null : [null])!;
        farmer.Items[0] = new StardewValley.Tools.FishingRod();
        farmer.Items[1] = null;
        wood.Stack = 3;
        farmer.Items[3] = new StardewValley.Object("388", 7);
        restore.Invoke(snapshot, null);
        Check(ReferenceEquals(farmer.Items[0], rod) && rod.attachments[0]?.Stack == 25 && rod.attachments[1]?.Name == "Dressed Spinner",
            "restored fishing rod is the original instance with its bait and tackle");
        Check(ReferenceEquals(farmer.Items[1], sling) && sling.attachments[0]?.Stack == 40, "restored slingshot keeps its ammo");
        Check(ReferenceEquals(farmer.Items[2], wood) && wood.Stack == 50, "changed stack size is restored on the original instance");
        Check(farmer.Items[3] is null, "items added during replay are removed");
        restore.Invoke(snapshot, null);
        Check(ReferenceEquals(farmer.Items[0], rod) && rod.attachments[0]?.Stack == 25 && wood.Stack == 50 && farmer.Items[3] is null,
            "repeated restore keeps the original inventory");
    }

    private static bool SkipGraphics() => false;
    private static void Seed(ICollection<string> values) { values.Clear(); values.Add("Case.Key"); values.Add("CASE.KEY"); }
    private static void CheckPair(IEnumerable<string> values, string message)
    {
        string[] keys = values.ToArray();
        bool exact = keys.Length == 2 && keys.Contains("Case.Key", StringComparer.Ordinal) && keys.Contains("CASE.KEY", StringComparer.Ordinal);
        bool caseSensitive = values is not IReadOnlySet<string> set || !set.Contains("case.key");
        Check(exact && caseSensitive, message);
    }
    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) failures++;
        Console.WriteLine((condition ? "PASS " : "FAIL ") + message);
    }
    private static object Get(object target, string name)
    {
        Type type = target as Type ?? target.GetType();
        object? instance = target is Type ? null : target;
        return type.GetProperty(name, Members)?.GetValue(instance) ?? type.GetField(name, Members)!.GetValue(instance)!;
    }
    private static void Set(object target, string name, object value)
    {
        Type type = target as Type ?? target.GetType();
        object? instance = target is Type ? null : target;
        if (type.GetProperty(name, Members) is { } property) property.SetValue(instance, value);
        else type.GetField(name, Members)!.SetValue(instance, value);
    }
    private static object At(object dictionary, string key) => dictionary.GetType().GetProperty("Item")!.GetValue(dictionary, [key])!;
    private static void Put(object dictionary, string key, object value) => dictionary.GetType().GetProperty("Item")!.SetValue(dictionary, value, [key]);
}
