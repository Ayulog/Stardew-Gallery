namespace StardewGallery;

// Values are captured on demand for one event. Null means unavailable, never a negative fact.
internal sealed record ConditionReadState
{
    internal DayOfWeek? Weekday { get; init; }
    internal bool? IsHost { get; init; }
    internal long? EarnedMoney { get; init; }
    internal int? Money { get; init; }
    internal int? FreeSlots { get; init; }
    internal bool? CommunityComplete { get; init; }
    internal bool? JojaComplete { get; init; }
    internal bool? HasPet { get; init; }
    internal string? PetPreference { get; init; }
    internal string? Gender { get; init; }
    internal int? Walnuts { get; init; }
    internal int? MineBottoms { get; init; }
    internal IReadOnlySet<int>? SecretNotes { get; init; }
    internal IReadOnlySet<string>? DialogueAnswers { get; init; }
    internal IReadOnlySet<string>? ActiveDialogues { get; init; }
    internal IReadOnlyDictionary<string, int>? Shipped { get; init; }
    internal IReadOnlyDictionary<string, bool>? Items { get; init; }
    internal IReadOnlyDictionary<string, int?>? Skills { get; init; }
    internal IReadOnlyDictionary<string, bool>? VisibleNpcs { get; init; }
    internal IReadOnlySet<string>? NpcsAtLocation { get; init; }
    internal bool? IsFarmHouse { get; init; }
    internal int? HouseUpgrade { get; init; }
    internal bool? SpouseBed { get; init; }
    internal IReadOnlySet<string>? FestivalDates { get; init; }
    internal IReadOnlySet<string>? PassiveFestivals { get; init; }
    internal TilePosition? EntryTile { get; init; }
    internal IReadOnlyDictionary<string, IReadOnlyList<uint>>? PlayerStats { get; init; }
    internal IReadOnlySet<string>? Errors { get; init; }
    internal IReadOnlyDictionary<string, bool?>? QueryFacts { get; init; }
}
