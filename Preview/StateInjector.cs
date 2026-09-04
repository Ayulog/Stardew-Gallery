namespace StardewGallery;

/// <summary>
/// The bounded set of mutable game-state slots a preview may temporarily touch.
/// All values are query-visible only; no gameplay actions (gifts, saving, day advance)
/// are ever performed here.
/// </summary>
internal interface IPreviewStateAccessor
{
    string? Season { get; set; }
    int? DayOfMonth { get; set; }
    int? Year { get; set; }
    int? Time { get; set; }
    int? GetFriendship(string npc);
    void SetFriendship(string npc, int points);
    bool HasEventSeen(string id);
    void SetEventSeen(string id, bool seen);
    bool HasMail(string id);
    void SetMail(string id, bool present);
}

/// <summary>
/// RAII scope: snapshots exactly the touched slots, applies an override, and restores
/// the originals on Dispose (idempotent, success and failure). Untouched slots are never
/// rewritten. Preview never saves.
/// </summary>
internal sealed class PreviewInjectionScope : IDisposable
{
    private readonly IPreviewStateAccessor accessor;
    private readonly List<Action> restores = [];
    private bool disposed;

    private PreviewInjectionScope(IPreviewStateAccessor accessor) => this.accessor = accessor;

    internal static PreviewInjectionScope Apply(IPreviewStateAccessor accessor, PreviewState state)
    {
        PreviewInjectionScope scope = new(accessor);
        try
        {
            scope.CaptureAndApply(state);
        }
        catch
        {
            // The scope is still returned so its Dispose restores whatever was applied.
            // Partial capture must never leave injected state on the live game.
        }
        return scope;
    }

    private void CaptureAndApply(PreviewState state)
    {
        if (state.Season is not null && !StringComparer.Ordinal.Equals(state.Season, accessor.Season))
        {
            string before = accessor.Season ?? "";
            restores.Add(() => accessor.Season = before);
            accessor.Season = state.Season;
        }
        if (state.DayOfMonth is int day && day != accessor.DayOfMonth)
        {
            int? before = accessor.DayOfMonth;
            restores.Add(() => accessor.DayOfMonth = before);
            accessor.DayOfMonth = state.DayOfMonth;
        }
        if (state.Year is int year && year != accessor.Year)
        {
            int? before = accessor.Year;
            restores.Add(() => accessor.Year = before);
            accessor.Year = state.Year;
        }
        if (state.Time is int time && time != accessor.Time)
        {
            int? before = accessor.Time;
            restores.Add(() => accessor.Time = before);
            accessor.Time = state.Time;
        }
        if (state.Friendship is not null)
            foreach ((string npc, int points) in state.Friendship)
            {
                int before = accessor.GetFriendship(npc) ?? 0;
                if (before == points)
                    continue;
                restores.Add(() => accessor.SetFriendship(npc, before));
                accessor.SetFriendship(npc, points);
            }
        if (state.EventsSeen is not null)
            foreach (string id in state.EventsSeen)
            {
                bool before = accessor.HasEventSeen(id);
                if (before)
                    continue;
                restores.Add(() => accessor.SetEventSeen(id, before));
                accessor.SetEventSeen(id, true);
            }
        if (state.Mail is not null)
            foreach (string id in state.Mail)
            {
                bool before = accessor.HasMail(id);
                if (before)
                    continue;
                restores.Add(() => accessor.SetMail(id, before));
                accessor.SetMail(id, true);
            }
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        for (int index = restores.Count - 1; index >= 0; index--)
        {
            try
            {
                restores[index]();
            }
            catch
            {
                // Restore continues for the remaining slots; handled by caller recovery policy.
            }
        }
        restores.Clear();
    }
}

/// <summary>
/// An optimistic guard that blocks saving while a preview/replay is in flight.
/// The underlying accessor must refuse to persist temporary state.
/// </summary>
internal interface IPreviewSaveGuard
{
    bool IsActive { get; }
    IDisposable Block();
}
