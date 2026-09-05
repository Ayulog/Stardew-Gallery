using StardewValley;

namespace StardewGallery;

/// <summary>Reads current-state values directly from the live game for analysis.</summary>
internal static class RuntimeStateReader
{
    internal static CurrentStateSnapshot Capture()
    {
        Farmer player = Game1.player;
        Farmer host = Game1.MasterPlayer;
        Dictionary<string, int> friendship = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string>? dating = null;
        foreach (string npc in player.friendshipData.Keys)
        {
            Friendship? value = player.friendshipData[npc];
            friendship[npc] = value?.Points ?? 0;
            if (value?.Status == FriendshipStatus.Dating)
            {
                dating ??= new HashSet<string>(StringComparer.Ordinal);
                dating.Add(npc);
            }
        }
        return new CurrentStateSnapshot(
            Season: Game1.currentSeason,
            Weather: Game1.currentLocation?.GetWeather().Weather,
            DayOfMonth: Game1.dayOfMonth,
            Year: Game1.year,
            Time: Game1.timeOfDay,
            DaysPlayed: Game1.stats.DaysPlayed is uint days ? (int)days : null,
            Friendship: friendship,
            EventsSeen: player.eventsSeen is null ? null : player.eventsSeen.ToHashSet(StringComparer.OrdinalIgnoreCase),
            LocalMail: player.mailReceived is null ? null : player.mailReceived.ToHashSet(StringComparer.OrdinalIgnoreCase),
            HostMail: host.mailReceived?.ToHashSet(StringComparer.OrdinalIgnoreCase),
            HostOrLocalMail: player.mailReceived?.Concat(host.mailReceived ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase),
            Dating: dating,
            Spouse: string.IsNullOrEmpty(player.spouse) ? null : new HashSet<string>(StringComparer.Ordinal) { player.spouse },
            Roommate: player.hasRoommate(),
            WorldState: Game1.worldStateIDs is null ? null : new HashSet<string>(Game1.worldStateIDs, StringComparer.Ordinal));
    }
}

/// <summary>
/// Reads and writes the query-visible slots a preview may temporarily touch. Only the
/// restorable kinds are exposed; weather/relationship/world-state are intentionally absent
/// so an unsafe injection can never be requested.
/// </summary>
internal sealed class RuntimePreviewStateAccessor : IPreviewStateAccessor
{
    public string? Season { get => Game1.currentSeason; set => Game1.currentSeason = value ?? ""; }
    public int? DayOfMonth { get => Game1.dayOfMonth; set => Game1.dayOfMonth = value ?? 1; }
    public int? Year { get => Game1.year; set => Game1.year = value ?? 1; }
    public int? Time { get => Game1.timeOfDay; set => Game1.timeOfDay = value ?? 600; }
    public int? GetFriendship(string npc) => Game1.player.friendshipData.GetValueOrDefault(npc)?.Points;
    public void SetFriendship(string npc, int points)
    {
        if (!Game1.player.friendshipData.TryGetValue(npc, out Friendship? friendship))
            Game1.player.friendshipData[npc] = friendship = new Friendship();
        friendship.Points = points;
    }

    public bool HasEventSeen(string id) => Game1.player.eventsSeen.Contains(id);
    public void SetEventSeen(string id, bool seen)
    {
        if (seen)
            Game1.player.eventsSeen.Add(id);
        else
            Game1.player.eventsSeen.Remove(id);
    }

    public bool HasMail(string id) => Game1.player.mailReceived.Contains(id);
    public void SetMail(string id, bool present)
    {
        if (present)
            Game1.player.mailReceived.Add(id);
        else
            Game1.player.mailReceived.Remove(id);
    }
}
