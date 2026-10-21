using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Buffs;
using StardewValley.Locations;
using StardewValley.Objects;
using StardewValley.Quests;

namespace StardewGallery;

internal sealed class ReplaySnapshot
{
    internal string LocationName { get; private init; } = "";
    internal GameLocation Location { get; private init; } = null!;
    internal Vector2 Tile { get; private init; }
    private Vector2 Position { get; init; }
    private Vector2 PositionBeforeEvent { get; init; }
    private int OrientationBeforeEvent { get; init; }
    private string? LocationBeforeForcedEvent { get; init; }
    private int Facing { get; init; }
    private int Time { get; init; }
    private string Season { get; init; } = "";
    private int Day { get; init; }
    private int Year { get; init; }
    private int Money { get; init; }
    private int TotalMoneyEarned { get; init; }
    private uint IndividualMoneyEarned { get; init; }
    private int[] Achievements { get; init; } = [];
    private int Health { get; init; }
    private int MaxHealth { get; init; }
    private float Stamina { get; init; }
    private int MaxStamina { get; init; }
    private int CurrentToolIndex { get; init; }
    private HashSet<string> EventsSeen { get; init; } = [];
    private HashSet<string> EventsSeenThisLocation { get; init; } = [];
    private Dictionary<string, FriendshipState> Friendships { get; init; } = [];
    private (Item? Item, int Stack)[] Items { get; init; } = [];
    private HashSet<string> MailReceived { get; init; } = [];
    private HashSet<string> MailForTomorrow { get; init; } = [];
    private string[] Mailbox { get; init; } = [];
    private HashSet<string> DialogueAnswers { get; init; } = [];
    private Dictionary<string, int> ActiveDialogueEvents { get; init; } = [];
    private Dictionary<string, int> PreviousDialogueEvents { get; init; } = [];
    private Dictionary<string, int> CookingRecipes { get; init; } = [];
    private Dictionary<string, int> CraftingRecipes { get; init; } = [];
    private int[] Experience { get; init; } = [];
    private HashSet<int> Professions { get; init; } = [];
    private string[] SpecialItems { get; init; } = [];
    private string[] SpecialBigCraftables { get; init; } = [];
    private ReplayQuestState[] Quests { get; init; } = [];
    private Vector2 DrawOffset { get; init; }
    private bool IgnoreMovementAnimation { get; init; }
    private bool IgnoreCollisions { get; init; }
    private bool CanOnlyWalk { get; init; }
    private Color AmbientLight { get; init; }
    private Color BackgroundColor { get; init; }
    private float FlashAlpha { get; init; }
    private bool DisplayFarmer { get; init; }
    private bool DisplayHud { get; init; }
    private bool ViewportFreeze { get; init; }
    private bool FreezeControls { get; init; }
    private bool MessagePause { get; init; }
    private float PauseTime { get; init; }
    private int NoMovementPause { get; init; }
    private int MovementPause { get; init; }
    private float MouseCursorTransparency { get; init; }
    private HashSet<string> TriggerActionsRun { get; init; } = [];
    private HashSet<string> SongsHeard { get; init; } = [];
    private HashSet<string> LocationsVisited { get; init; } = [];
    private Dictionary<string, uint> Stats { get; init; } = [];
    private Dictionary<string, Buff> Buffs { get; init; } = [];
    private (Furniture Furniture, bool IsOn)[] Fireplaces { get; init; } = [];

    /// <param name="scene">The replay location; native event setup can light its real fireplace.</param>
    internal static ReplaySnapshot Capture(GameLocation? scene = null) => new()
    {
        LocationName = Game1.currentLocation.NameOrUniqueName,
        Location = Game1.currentLocation,
        Tile = Game1.player.Tile,
        Position = Game1.player.Position,
        PositionBeforeEvent = Game1.player.positionBeforeEvent,
        OrientationBeforeEvent = Game1.player.orientationBeforeEvent,
        LocationBeforeForcedEvent = Game1.player.locationBeforeForcedEvent.Value,
        Facing = Game1.player.FacingDirection,
        Time = Game1.timeOfDay,
        Season = Game1.currentSeason,
        Day = Game1.dayOfMonth,
        Year = Game1.year,
        Money = Game1.player.Money,
        TotalMoneyEarned = Game1.player.team.totalMoneyEarned.Value,
        IndividualMoneyEarned = Game1.player.stats.IndividualMoneyEarned,
        Achievements = Game1.player.achievements.ToArray(),
        Health = Game1.player.health,
        MaxHealth = Game1.player.maxHealth,
        Stamina = Game1.player.Stamina,
        MaxStamina = Game1.player.maxStamina.Value,
        CurrentToolIndex = Game1.player.CurrentToolIndex,
        EventsSeen = Game1.player.eventsSeen.ToHashSet(StringComparer.Ordinal),
        EventsSeenThisLocation = Game1.eventsSeenSinceLastLocationChange.ToHashSet(StringComparer.Ordinal),
        Friendships = Game1.player.friendshipData.Keys.ToDictionary(name => name, name => FriendshipState.Capture(Game1.player.friendshipData[name]), StringComparer.Ordinal),
        Items = Game1.player.Items.Select(item => ((Item?)item, item?.Stack ?? 0)).ToArray(),
        MailReceived = Game1.player.mailReceived.ToHashSet(StringComparer.Ordinal),
        MailForTomorrow = Game1.player.mailForTomorrow.ToHashSet(StringComparer.Ordinal),
        Mailbox = Game1.player.mailbox.ToArray(),
        DialogueAnswers = Game1.player.dialogueQuestionsAnswered.ToHashSet(StringComparer.Ordinal),
        ActiveDialogueEvents = Game1.player.activeDialogueEvents.Keys.ToDictionary(key => key, key => Game1.player.activeDialogueEvents[key], StringComparer.Ordinal),
        PreviousDialogueEvents = Game1.player.previousActiveDialogueEvents.Keys.ToDictionary(key => key, key => Game1.player.previousActiveDialogueEvents[key], StringComparer.Ordinal),
        CookingRecipes = Game1.player.cookingRecipes.Keys.ToDictionary(key => key, key => Game1.player.cookingRecipes[key], StringComparer.Ordinal),
        CraftingRecipes = Game1.player.craftingRecipes.Keys.ToDictionary(key => key, key => Game1.player.craftingRecipes[key], StringComparer.Ordinal),
        Experience = Game1.player.experiencePoints.ToArray(),
        Professions = Game1.player.professions.ToHashSet(),
        SpecialItems = Game1.player.specialItems.ToArray(),
        SpecialBigCraftables = Game1.player.specialBigCraftables.ToArray(),
        Quests = Game1.player.questLog.Select(ReplayQuestState.Capture).ToArray(),
        DrawOffset = Game1.player.drawOffset,
        IgnoreMovementAnimation = Game1.player.ignoreMovementAnimation,
        IgnoreCollisions = Game1.player.ignoreCollisions,
        CanOnlyWalk = Game1.player.canOnlyWalk,
        AmbientLight = Game1.ambientLight,
        BackgroundColor = Game1.bgColor,
        FlashAlpha = Game1.flashAlpha,
        DisplayFarmer = Game1.displayFarmer,
        DisplayHud = Game1.displayHUD,
        ViewportFreeze = Game1.viewportFreeze,
        FreezeControls = Game1.freezeControls,
        MessagePause = Game1.messagePause,
        PauseTime = Game1.pauseTime,
        NoMovementPause = Game1.player.noMovementPause,
        MovementPause = Game1.player.movementPause,
        MouseCursorTransparency = Game1.mouseCursorTransparency,
        // Event music unlocks jukebox songs, entering a location records a visit, and dialogue
        // actions or food (farmerEat) change trigger, stat and buff state outside the event cast.
        TriggerActionsRun = Game1.player.triggerActionsRun.ToHashSet(StringComparer.Ordinal),
        SongsHeard = Game1.player.songsHeard.ToHashSet(StringComparer.Ordinal),
        LocationsVisited = Game1.player.locationsVisited.ToHashSet(StringComparer.Ordinal),
        Stats = Game1.player.stats.Values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
        Buffs = Game1.player.buffs.AppliedBuffs.ToDictionary(pair => pair.Key, pair => pair.Value),
        Fireplaces = scene is DecoratableLocation decoratable
            ? decoratable.furniture.Where(item => item.furniture_type.Value == Furniture.fireplace).Select(item => (item, item.IsOn)).ToArray()
            : []
    };

    internal void RestorePlayer()
    {
        Game1.timeOfDay = Time;
        Game1.currentSeason = Season;
        Game1.dayOfMonth = Day;
        Game1.year = Year;
        // Both Money and totalMoneyEarned setters grant rewards when increased.
        Game1.player._money = Money;
        Game1.player.team.totalMoneyEarned.Value = TotalMoneyEarned;
        Game1.player.stats.Set("individualMoneyEarned", IndividualMoneyEarned);
        Replace(Game1.player.achievements, Achievements);
        Game1.player.health = Health;
        Game1.player.maxHealth = MaxHealth;
        Game1.player.Stamina = Stamina;
        Game1.player.maxStamina.Value = MaxStamina;
        Game1.player.CurrentToolIndex = CurrentToolIndex;
        Replace(Game1.player.eventsSeen, EventsSeen);
        Replace(Game1.eventsSeenSinceLastLocationChange, EventsSeenThisLocation);
        Replace(Game1.player.mailReceived, MailReceived);
        Replace(Game1.player.mailForTomorrow, MailForTomorrow);
        Replace(Game1.player.mailbox, Mailbox);
        Replace(Game1.player.dialogueQuestionsAnswered, DialogueAnswers);
        Replace(Game1.player.activeDialogueEvents, ActiveDialogueEvents);
        Replace(Game1.player.previousActiveDialogueEvents, PreviousDialogueEvents);
        Replace(Game1.player.cookingRecipes, CookingRecipes);
        Replace(Game1.player.craftingRecipes, CraftingRecipes);
        Replace(Game1.player.professions, Professions);
        Replace(Game1.player.specialItems, SpecialItems);
        Replace(Game1.player.specialBigCraftables, SpecialBigCraftables);
        foreach (ReplayQuestState quest in Quests)
            quest.Restore();
        Replace(Game1.player.questLog, Quests.Select(quest => quest.Quest));
        for (int i = 0; i < Math.Min(Experience.Length, Game1.player.experiencePoints.Count); i++)
            Game1.player.experiencePoints[i] = Experience[i];

        foreach (string name in Game1.player.friendshipData.Keys.Where(name => !Friendships.ContainsKey(name)).ToArray())
            Game1.player.friendshipData.Remove(name);
        foreach ((string name, FriendshipState state) in Friendships)
        {
            if (!Game1.player.friendshipData.TryGetValue(name, out Friendship? friendship))
                Game1.player.friendshipData[name] = friendship = new Friendship();
            state.Restore(friendship);
        }

        // Put back the original instances. Item.getOne() copies drop state such as fishing rod bait/tackle
        // and slingshot ammo, so cloning would lose real inventory on every replay.
        for (int i = 0; i < Game1.player.Items.Count; i++)
        {
            (Item? item, int stack) = i < Items.Length ? Items[i] : (null, 0);
            if (!ReferenceEquals(Game1.player.Items[i], item))
                Game1.player.Items[i] = item;
            if (item is not null && item.Stack != stack)
                item.Stack = stack;
        }

        Replace(Game1.player.triggerActionsRun, TriggerActionsRun);
        Replace(Game1.player.songsHeard, SongsHeard);
        Replace(Game1.player.locationsVisited, LocationsVisited);
        Game1.player.stats.Values.Clear();
        foreach ((string key, uint value) in Stats)
            Game1.player.stats.Values[key] = value;
        foreach (string id in Game1.player.buffs.AppliedBuffs.Keys.ToArray())
            if (!Buffs.TryGetValue(id, out Buff? original) || !ReferenceEquals(original, Game1.player.buffs.AppliedBuffs[id]))
                Game1.player.buffs.Remove(id);
        foreach ((string id, Buff buff) in Buffs)
            if (!Game1.player.buffs.AppliedBuffs.ContainsKey(id))
                Game1.player.buffs.Apply(buff);
        foreach ((Furniture fireplace, bool isOn) in Fireplaces)
        {
            if (fireplace.IsOn == isOn)
                continue;
            fireplace.IsOn = isOn;
            fireplace.setFireplace(playSound: false);
        }
    }

    internal void RestorePositionAndPresentation()
    {
        Game1.player.Position = Position;
        Game1.player.positionBeforeEvent = PositionBeforeEvent;
        Game1.player.orientationBeforeEvent = OrientationBeforeEvent;
        Game1.player.locationBeforeForcedEvent.Value = LocationBeforeForcedEvent;
        Game1.player.faceDirection(Facing);
        Game1.fadeToBlack = false;
        Game1.fadeIn = false;
        Game1.globalFade = false;
        Game1.nonWarpFade = false;
        Game1.fadeToBlackAlpha = 0;
        Game1.eventUp = false;
        Game1.displayFarmer = DisplayFarmer;
        Game1.displayHUD = DisplayHud;
        Game1.viewportFreeze = ViewportFreeze;
        Game1.messagePause = MessagePause;
        Game1.pauseTime = PauseTime;
        Game1.mouseCursorTransparency = MouseCursorTransparency;
        Game1.player.forceCanMove();
        Game1.freezeControls = FreezeControls;
        Game1.player.noMovementPause = NoMovementPause;
        Game1.player.movementPause = MovementPause;
        Game1.player.drawOffset = DrawOffset;
        Game1.player.ignoreMovementAnimation = IgnoreMovementAnimation;
        Game1.player.ignoreCollisions = IgnoreCollisions;
        Game1.player.canOnlyWalk = CanOnlyWalk;
        Game1.ambientLight = AmbientLight;
        Game1.bgColor = BackgroundColor;
        Game1.flashAlpha = FlashAlpha;
    }

    private static void Replace<T>(ICollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (T value in values)
            target.Add(value);
    }

    private static void Replace(StardewValley.Network.NetStringDictionary<int, Netcode.NetInt> target, IReadOnlyDictionary<string, int> values)
    {
        target.Clear();
        foreach ((string key, int value) in values)
            target[key] = value;
    }
}

internal sealed record FriendshipState(int Points, int GiftsThisWeek, int GiftsToday, bool TalkedToToday, FriendshipStatus Status)
{
    internal static FriendshipState Capture(Friendship value) => new(value.Points, value.GiftsThisWeek, value.GiftsToday, value.TalkedToToday, value.Status);
    internal void Restore(Friendship value)
    {
        value.Points = Points;
        value.GiftsThisWeek = GiftsThisWeek;
        value.GiftsToday = GiftsToday;
        value.TalkedToToday = TalkedToToday;
        value.Status = Status;
    }
}
