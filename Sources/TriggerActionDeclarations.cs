using System.Text.Json;

namespace StardewGallery;

/// <summary>A Data/TriggerActions entry key found in a static patch, before Content Patcher tokens are expanded.</summary>
/// <param name="RawId">The entry ID as written (may contain tokens).</param>
/// <param name="Definition">The raw JSON of a full entry, or of the edit for a modification; used only for hashing.</param>
/// <param name="IsModification">True when the patch edits or moves an existing entry instead of supplying a complete one.</param>
internal sealed record TriggerActionDeclaration(string RawId, string Definition, bool IsModification);

/// <summary>
/// Reads Data/TriggerActions entry IDs from Content Patcher's static list-asset structures without executing
/// anything. Load files and EditData Entries (or EditData FromFile entry maps/lists) supply complete entries
/// (declarations); Fields, MoveEntries, TargetField and TextOperations only change or reorder an entry that
/// must already exist (modifications), and null Entries delete, so none of those can name an original provider.
/// </summary>
internal static class TriggerActionDeclarations
{
    private static readonly HashSet<string> PatchProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "Entries", "Fields", "MoveEntries", "TargetField", "TextOperations", "LogName", "When", "Update"
    };

    /// <summary>True when an EditData patch scopes its inline fields and FromFile to one existing entry via TargetField.</summary>
    internal static bool HasNonEmptyTargetField(JsonElement patch)
        => Property(patch, "TargetField", out JsonElement targetField) && targetField.ValueKind != JsonValueKind.Null
            && !(targetField.ValueKind == JsonValueKind.Array && targetField.GetArrayLength() == 0);

    /// <summary>Read a Load file: a JSON list of entries with their Id field (or an EditData-shaped object).</summary>
    internal static IReadOnlyList<TriggerActionDeclaration> ReadLoad(JsonElement root, List<string>? problems = null)
    {
        List<TriggerActionDeclaration> result = [];
        if (root.ValueKind == JsonValueKind.Array) ReadList(root, result, problems);
        else if (root.ValueKind == JsonValueKind.Object) return ReadEditFile(root, problems);
        else problems?.Add("Trigger action file is neither a JSON list nor an entry object");
        return result;
    }

    /// <summary>
    /// Read an external JSON file referenced by EditData FromFile for Data/TriggerActions. Content Patcher's
    /// EditData FromFile supplies the entry dictionary {"RuleId": {...}} or entry list [{...}] directly at the
    /// root; patch-shaped wrapper files ({"Entries": ...}) are also tolerated, as event FromFile scanning does.
    /// </summary>
    internal static IReadOnlyList<TriggerActionDeclaration> ReadEditFile(JsonElement root, List<string>? problems = null)
    {
        List<TriggerActionDeclaration> result = [];
        if (root.ValueKind == JsonValueKind.Array)
        {
            ReadList(root, result, problems);
            return result;
        }
        if (root.ValueKind != JsonValueKind.Object)
        {
            problems?.Add("Trigger action file is neither a JSON list nor an entry object");
            return result;
        }
        if (IsPatchWrapper(root))
            return ReadEdit(root, problems);
        ReadEntryMap(root, result, problems);
        return result;
    }

    /// <summary>Read an inline EditData patch targeting Data/TriggerActions.</summary>
    internal static IReadOnlyList<TriggerActionDeclaration> ReadEdit(JsonElement patch, List<string>? problems = null)
    {
        List<TriggerActionDeclaration> result = [];
        if (patch.ValueKind != JsonValueKind.Object) return result;
        if (HasNonEmptyTargetField(patch))
        {
            Property(patch, "TargetField", out JsonElement targetField);
            // A non-empty TargetField scopes Entries/Fields/TextOperations/FromFile to a field inside one existing entry.
            if (targetField.ValueKind == JsonValueKind.Array && targetField[0].ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(targetField[0].GetString()))
                result.Add(new(targetField[0].GetString()!, patch.GetRawText(), true));
            else problems?.Add("TargetField does not name a trigger action entry");
            return result;
        }
        if (Property(patch, "Entries", out JsonElement entries))
        {
            if (entries.ValueKind == JsonValueKind.Object) ReadEntryMap(entries, result, problems);
            // Tolerate a list of entries keyed by their Id field, as in a Load file.
            else if (entries.ValueKind == JsonValueKind.Array) ReadList(entries, result, problems);
        }
        if (Property(patch, "Fields", out JsonElement fields) && fields.ValueKind == JsonValueKind.Object)
            foreach (JsonProperty entry in fields.EnumerateObject())
                if (entry.Name.Trim().Length > 0) result.Add(new(entry.Name, entry.Value.GetRawText(), true));
        if (Property(patch, "MoveEntries", out JsonElement moves) && moves.ValueKind == JsonValueKind.Array)
            foreach (JsonElement move in moves.EnumerateArray())
                if (Property(move, "ID", out JsonElement movedId) && movedId.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(movedId.GetString()))
                    result.Add(new(movedId.GetString()!, move.GetRawText(), true));
        if (Property(patch, "TextOperations", out JsonElement operations) && operations.ValueKind == JsonValueKind.Array)
            foreach (JsonElement operation in operations.EnumerateArray())
            {
                if (!Property(operation, "Target", out JsonElement target) || target.ValueKind != JsonValueKind.Array
                    || target.GetArrayLength() < 2 || target[0].ValueKind != JsonValueKind.String || target[1].ValueKind != JsonValueKind.String)
                    continue;
                string root = target[0].GetString()!;
                if (root.Equals("Entries", StringComparison.OrdinalIgnoreCase) || root.Equals("Fields", StringComparison.OrdinalIgnoreCase))
                    result.Add(new(target[1].GetString()!, operation.GetRawText(), true));
            }
        return result;
    }

    private static bool IsPatchWrapper(JsonElement obj)
    {
        bool hasPatchSection = false;
        foreach (JsonProperty property in obj.EnumerateObject())
        {
            if (!PatchProperties.Contains(property.Name)) return false;
            if (property.Name.Equals("Entries", StringComparison.OrdinalIgnoreCase)
                || property.Name.Equals("Fields", StringComparison.OrdinalIgnoreCase)
                || property.Name.Equals("MoveEntries", StringComparison.OrdinalIgnoreCase)
                || property.Name.Equals("TargetField", StringComparison.OrdinalIgnoreCase)
                || property.Name.Equals("TextOperations", StringComparison.OrdinalIgnoreCase))
                hasPatchSection = true;
        }
        return hasPatchSection;
    }

    private static void ReadEntryMap(JsonElement entries, List<TriggerActionDeclaration> result, List<string>? problems)
    {
        foreach (JsonProperty entry in entries.EnumerateObject())
        {
            // Null deletes the entry; scalar values aren't valid trigger action data.
            if (entry.Value.ValueKind != JsonValueKind.Object) continue;
            if (Property(entry.Value, "Id", out JsonElement innerId) && innerId.ValueKind != JsonValueKind.Null)
            {
                if (innerId.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(innerId.GetString()))
                    result.Add(new(innerId.GetString()!, entry.Value.GetRawText(), false));
                else problems?.Add("Trigger action entry without a static Id");
            }
            else if (!string.IsNullOrWhiteSpace(entry.Name) && !entry.Name.StartsWith('#'))
                result.Add(new(entry.Name, entry.Value.GetRawText(), false));
            else problems?.Add("Trigger action entry without a static Id");
        }
    }

    private static void ReadList(JsonElement list, List<TriggerActionDeclaration> result, List<string>? problems)
    {
        foreach (JsonElement entry in list.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object) continue;
            if (Property(entry, "Id", out JsonElement id) && id.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(id.GetString()))
                result.Add(new(id.GetString()!, entry.GetRawText(), false));
            else problems?.Add("Trigger action entry without a static Id");
        }
    }

    private static bool Property(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
            foreach (JsonProperty property in element.EnumerateObject())
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) { value = property.Value; return true; }
        value = default;
        return false;
    }
}
