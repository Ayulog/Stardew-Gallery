using System.Numerics;
using System.Text;

namespace StardewGallery;

/// <summary>
/// Finds Chinese text by pinyin typed in Latin letters, for keyboards that can't type Chinese, such as the game's
/// on-screen keyboard. Readings come from assets/pinyin.txt (tools/Generate-PinyinTable.ps1).
/// </summary>
internal sealed class PinyinTable
{
    private const char First = '\u4E00', Last = '\u9FFF';
    private readonly string[]?[] readings = new string[]?[Last - First + 1];

    /// <summary>Read the table: comment lines start with #, other lines are a syllable, a space and its characters.</summary>
    internal static PinyinTable Parse(IEnumerable<string> lines)
    {
        PinyinTable table = new();
        foreach (string line in lines)
        {
            if (line.Length == 0 || line[0] == '#')
                continue;
            int space = line.IndexOf(' ');
            if (space <= 0)
                throw new FormatException("Expected a syllable, a space and characters: " + line);
            string syllable = line[..space];
            foreach (char character in line.AsSpan(space + 1).TrimEnd())
            {
                if (character is < First or > Last)
                    throw new FormatException($"U+{(int)character:X4} is outside the table ({syllable}).");
                ref string[]? known = ref table.readings[character - First];
                known = known is null ? [syllable] : [.. known, syllable];
            }
        }
        return table;
    }

    /// <summary>
    /// The query as pinyin letters: lower case, without the spaces and apostrophes typed between syllables. Null when
    /// it has no Latin letter, since matching the text itself already covers it.
    /// </summary>
    internal static string? Spelling(string query)
    {
        StringBuilder spelling = new(query.Length);
        bool letters = false;
        foreach (char character in query)
        {
            if (char.IsWhiteSpace(character) || character is '\'' or '’')
                continue;
            char lower = char.ToLowerInvariant(character);
            letters |= lower is >= 'a' and <= 'z';
            spelling.Append(lower);
        }
        return letters && spelling.Length < 64 ? spelling.ToString() : null;
    }

    /// <summary>
    /// Whether consecutive characters of the text spell the query. A Chinese character takes the start of any of its
    /// readings, so "abge", "abigaier" and "abig" all find 阿比盖尔; every character also matches itself.
    /// </summary>
    internal bool Matches(string text, string spelling)
    {
        // Bit i is set when a run of characters ending here spells the first i letters; bit 0 starts a run anywhere.
        ulong reached = 1, done = 1UL << spelling.Length;
        foreach (char character in text)
        {
            string[]? options = character is >= First and <= Last ? readings[character - First] : null;
            char lower = char.ToLowerInvariant(character);
            ulong next = 1;
            for (ulong open = reached; open != 0; open &= open - 1)
            {
                int start = BitOperations.TrailingZeroCount(open);
                if (spelling[start] == lower)
                    next |= 2UL << start;
                if (options is null)
                    continue;
                foreach (string reading in options)
                    for (int length = 0; length < reading.Length && start + length < spelling.Length
                        && Same(spelling[start + length], reading[length]); length++)
                        next |= 2UL << (start + length);
            }
            if ((next & done) != 0)
                return true;
            reached = next;
        }
        return false;
    }

    // Readings write ü as v, as pinyin keyboards do; u and ü are accepted for it too.
    private static bool Same(char typed, char reading) => typed == reading || (reading == 'v' && typed is 'u' or '\u00FC');
}
