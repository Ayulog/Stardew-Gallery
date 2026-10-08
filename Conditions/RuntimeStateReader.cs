using StardewValley;

namespace StardewGallery;

/// <summary>Reads current-state values directly from the live game for analysis.</summary>
internal static class RuntimeStateReader
{
    internal static CurrentStateSnapshot Capture()
    {
        Farmer player = Game1.player;
        Farmer host = Game1.MasterPlayer;
        Dictionary<string, int> friendship = new(StringComparer.Ordinal);
        HashSet<string> dating = new(StringComparer.Ordinal);
        foreach (string npc in player.friendshipData.Keys)
        {
            Friendship? value = player.friendshipData[npc];
            friendship[npc] = value?.Points ?? 0;
            if (value?.Status == FriendshipStatus.Dating)
            {
                dating.Add(npc);
            }
        }
        return new CurrentStateSnapshot(
            Season: Game1.currentSeason,
            Weather: null,
            DayOfMonth: Game1.dayOfMonth,
            Year: Game1.year,
            Time: Game1.timeOfDay,
            DaysPlayed: Game1.stats.DaysPlayed is uint days ? (int)days : null,
            Friendship: friendship,
            EventsSeen: player.eventsSeen is null ? null : player.eventsSeen.ToHashSet(StringComparer.Ordinal),
            LocalMail: player.mailReceived is null ? null : player.mailReceived.ToHashSet(StringComparer.Ordinal),
            HostMail: host.mailReceived?.ToHashSet(StringComparer.Ordinal),
            HostOrLocalMail: player.mailReceived?.Concat(host.mailReceived ?? []).ToHashSet(StringComparer.Ordinal),
            Dating: dating,
            Spouse: string.IsNullOrEmpty(player.spouse) ? new HashSet<string>(StringComparer.Ordinal) : new HashSet<string>(StringComparer.Ordinal) { player.spouse },
            Roommate: player.hasRoommate(),
            WorldState: Game1.worldStateIDs is null ? null : new HashSet<string>(Game1.worldStateIDs, StringComparer.Ordinal),
            IsRaining: null);
    }

    internal static CurrentStateSnapshot ForLocation(CurrentStateSnapshot shared, GameLocation? target)
        => target is null
            ? shared.ForLocation(null, null)
            : shared.ForLocation(target.GetWeather().Weather, target.IsRainingHere());
}
