using StardewValley;
using StardewValley.Locations;

namespace StardewGallery;

internal static class ConditionStateReader
{
    internal static ConditionReadState Capture(ConditionSet conditions, GameLocation? location)
    {
        Farmer player = Game1.player;
        ConditionReadState state = new();
        Dictionary<string, bool> items = new(StringComparer.Ordinal), visible = new(StringComparer.Ordinal);
        Dictionary<string, int?> skills = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, IReadOnlyList<uint>> stats = new(StringComparer.Ordinal);
        HashSet<string> errors = new(StringComparer.Ordinal);
        Dictionary<string, bool?> queryFacts = new(StringComparer.Ordinal);
        foreach (ConditionExpression condition in conditions.Conditions.Distinct())
        {
            try
            {
                switch (condition)
                {
                    case DayOfWeekCondition: state = state with { Weekday = Game1.Date.DayOfWeek }; break;
                    case IsHostCondition: state = state with { IsHost = Game1.IsMasterGame }; break;
                    case EarnedMoneyCondition: state = state with { EarnedMoney = player.totalMoneyEarned }; break;
                    case HasMoneyCondition: state = state with { Money = player.Money }; break;
                    case FreeInventorySlotsCondition: state = state with { FreeSlots = player.freeSpotsInInventory() }; break;
                    case GoldenWalnutsCondition: state = state with { Walnuts = Game1.netWorldState.Value.GoldenWalnutsFound }; break;
                    case ReachedMineBottomCondition: state = state with { MineBottoms = player.timesReachedMineBottom }; break;
                    case CommunityCenterOrWarehouseDoneCondition:
                        state = state with { CommunityComplete = Game1.MasterPlayer.eventsSeen.Contains("191393")
                            || Game1.MasterPlayer.eventsSeen.Contains("502261") || Game1.MasterPlayer.hasCompletedCommunityCenter() }; break;
                    case JojaBundlesDoneCondition: state = state with { JojaComplete = Utility.hasFinishedJojaRoute() }; break;
                    case MissingPetCondition: state = state with { HasPet = player.hasPet(), PetPreference = player.whichPetType }; break;
                    case GenderCondition: state = state with { Gender = player.IsMale ? "male" : "female" }; break;
                    case SawSecretNoteCondition: state = state with { SecretNotes = player.secretNotesSeen.ToHashSet() }; break;
                    case ChoseDialogueAnswersCondition: state = state with { DialogueAnswers = player.DialogueQuestionsAnswered.ToHashSet(StringComparer.Ordinal) }; break;
                    case ActiveDialogueEventCondition: state = state with { ActiveDialogues = player.activeDialogueEvents.Keys.ToHashSet(StringComparer.Ordinal) }; break;
                    case ShippedCondition: state = state with { Shipped = player.basicShipped.Pairs.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal) }; break;
                    case HasItemCondition item:
                        items[item.ItemId] = player.Items.ContainsId(item.ItemId)
                            || player.ActiveObject is { } active && ItemRegistry.HasItemId(active, item.ItemId); break;
                    case SkillCondition skill:
                        int index = Farmer.getSkillNumberFromName(skill.Skill);
                        skills[skill.Skill] = index < 0 ? null : player.GetUnmodifiedSkillLevel(index); break;
                    case NpcVisibleCondition { CurrentLocationOnly: false } npc:
                        visible[npc.Npc] = Game1.getCharacterFromName(npc.Npc) is { IsInvisible: false }; break;
                    case NpcVisibleCondition:
                        state = state with { NpcsAtLocation = location?.characters.Where(npc => !npc.IsInvisible).Select(npc => npc.Name).ToHashSet(StringComparer.Ordinal) }; break;
                    case InUpgradedHouseCondition:
                        state = state with { IsFarmHouse = location is null ? null : location is FarmHouse,
                            HouseUpgrade = location is FarmHouse house ? house.upgradeLevel : null }; break;
                    case SpouseBedCondition:
                        FarmHouse? home = Utility.getHomeOfFarmer(player);
                        state = state with { SpouseBed = home is null ? null : home.GetSpouseBed() is not null }; break;
                    case FestivalDayCondition or UpcomingFestivalCondition:
                        state = state with { FestivalDates = DataLoader.Festivals_FestivalDates(Game1.temporaryContent).Keys.ToHashSet(StringComparer.Ordinal) }; break;
                    case TileCondition:
                        state = state with { EntryTile = EntryTile(location, player) }; break;
                    case NativeQueryCondition query when SafeGameQuery.TryParse(query.Query, out var clauses):
                        foreach (SafeQueryClause clause in clauses)
                        {
                            queryFacts[SafeGameQuery.FactKey(clause)] = SafeQueryStateReader.Read(clause, location);
                            if (clause.Name == "IS_PASSIVE_FESTIVAL_TODAY")
                                state = state with { PassiveFestivals = Game1.netWorldState.Value.ActivePassiveFestivals.ToHashSet(StringComparer.Ordinal) };
                            else if (clause.Name == "PLAYER_STAT")
                            {
                                string selector = clause.Arguments[0], stat = clause.Arguments[1];
                                Farmer[]? farmers = SelectPlayers(selector);
                                if (farmers is not null) stats[SafeGameQuery.StatKey(selector, stat)] = farmers.Select(farmer => farmer.stats.Get(stat)).ToArray();
                            }
                        }
                        break;
                }
            }
            catch
            {
                errors.Add(condition.RawSegment);
            }
        }
        return state with { Items = items, Skills = skills, VisibleNpcs = visible, PlayerStats = stats, Errors = errors, QueryFacts = queryFacts };
    }

    private static TilePosition? EntryTile(GameLocation? location, Farmer player)
    {
        if (location is null) return null;
        if (Game1.isWarping || Game1.dedicatedServer?.FakeWarp == true)
            return Game1.locationRequest?.Location == location ? new(Game1.xLocationAfterWarp, Game1.yLocationAfterWarp) : null;
        return player.currentLocation == location ? new(player.TilePoint.X, player.TilePoint.Y) : null;
    }

    private static Farmer[]? SelectPlayers(string selector) => selector.ToUpperInvariant() switch
    {
        "ANY" or "ALL" => Game1.getAllFarmers().ToArray(),
        "CURRENT" or "TARGET" => [Game1.player],
        "HOST" => [Game1.MasterPlayer],
        _ => long.TryParse(selector, out long id) && Game1.GetPlayer(id) is { } farmer ? [farmer] : null
    };
}
