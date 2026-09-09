using StardewValley.Quests;

namespace StardewGallery;

internal sealed record ReplayQuestState(Quest Quest, byte[] Fields)
{
    internal static ReplayQuestState Capture(Quest quest)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        // Serialize the native network fields without reparenting the live quest.
        quest.NetFields.WriteFull(writer);
        writer.Flush();
        return new(quest, stream.ToArray());
    }

    internal void Restore()
    {
        using MemoryStream stream = new(Fields, writable: false);
        using BinaryReader reader = new(stream);
        Quest.NetFields.ReadFull(reader, default);
    }
}
