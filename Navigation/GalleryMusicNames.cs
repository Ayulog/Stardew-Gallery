using StardewModdingAPI;
using StardewValley;

namespace StardewGallery;

internal static class GalleryMusicNames
{
    internal static string? ResolveCueTitle(string? cueId)
    {
        if (string.IsNullOrWhiteSpace(cueId))
            return null;
        try
        {
            if (Game1.jukeboxTrackData is null && Game1.content is not null)
                Game1.jukeboxTrackData = DataLoader.JukeboxTracks(Game1.content);
            if (Game1.jukeboxTrackData is not null)
            {
                string title = Utility.getSongTitleFromCueName(cueId);
                if (!string.IsNullOrWhiteSpace(title))
                    return title.Trim();
            }
        }
        catch
        {
            // Headless tests or malformed custom jukebox data fall back to the raw cue ID.
        }
        return cueId.Trim();
    }

    internal static string FormatForDetail(ResolvedEvent entry, ITranslationHelper i18n)
    {
        IReadOnlySet<string>? answers = Game1.player?.dialogueQuestionsAnswered?.ToHashSet(StringComparer.Ordinal);
        string? cue = EventMusicExtractor.ExtractCueId(
            entry,
            raw => Event.ParseCommands(raw),
            cmd => ArgUtility.SplitBySpaceQuoteAware(cmd),
            answers);
        return EventMusicExtractor.FormatTrackTitle(cue, ResolveCueTitle, i18n.Get("event-detail.bgm-none"));
    }

    internal static string? ResolveActiveReplayTitle(string? staticCue)
    {
        string? liveCue = null;
        try
        {
            if (Game1.currentSong is { IsPlaying: true, Name: { Length: > 0 } currentName })
            {
                IReadOnlySet<string>? answers = Game1.player?.dialogueQuestionsAnswered?.ToHashSet(StringComparer.Ordinal);
                liveCue = EventMusicExtractor.NormalizeCue(currentName, answers);
            }
        }
        catch
        {
        }
        string? cue = liveCue ?? staticCue;
        return cue is null ? null : ResolveCueTitle(cue);
    }
}
