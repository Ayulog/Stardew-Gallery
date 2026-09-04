using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StardewGallery;

internal static class HistoricalExecutionContextCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    internal static bool TryEncode(HistoricalExecutionContext context, out string payload)
    {
        payload = "";
        try
        {
            if (!HistoricalExecutionContextRules.TryValidate(context, out _))
                return false;
            string serialized = JsonSerializer.Serialize(context, Options);
            if (Encoding.UTF8.GetByteCount(serialized) > HistoricalExecutionContextRules.MaxExecutionJsonBytes)
                return false;
            payload = serialized;
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal static HistoricalExecutionContextLoad Decode(string? payload, string expectedPlaybackHash)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return new HistoricalExecutionContextLoad(HistoricalExecutionContextState.Missing, null, null);
        if (Encoding.UTF8.GetByteCount(payload) > HistoricalExecutionContextRules.MaxExecutionJsonBytes)
            return Invalid(ExecutionContextInvalidReason.PayloadTooLarge);

        try
        {
            using JsonDocument document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("schemaVersion", out JsonElement schemaElement)
                || schemaElement.ValueKind != JsonValueKind.Number
                || !schemaElement.TryGetInt32(out int schemaVersion))
                return Invalid(ExecutionContextInvalidReason.MalformedPayload);
            if (schemaVersion > HistoricalExecutionContextRules.CurrentSchemaVersion)
                return Invalid(ExecutionContextInvalidReason.FutureSchema);
            if (schemaVersion != HistoricalExecutionContextRules.CurrentSchemaVersion)
                return Invalid(ExecutionContextInvalidReason.UnsupportedSchema);

            HistoricalExecutionContext? context = JsonSerializer.Deserialize<HistoricalExecutionContext>(payload, Options);
            if (context is null)
                return Invalid(ExecutionContextInvalidReason.MalformedPayload);
            if (!HistoricalExecutionContextRules.TryValidate(context, out _))
                return Invalid(ExecutionContextInvalidReason.InvalidModel);
            if (!StringComparer.Ordinal.Equals(context.PlaybackHash, expectedPlaybackHash))
                return Invalid(ExecutionContextInvalidReason.PlaybackMismatch);
            return new HistoricalExecutionContextLoad(HistoricalExecutionContextRules.GetState(context), context, null);
        }
        catch (JsonException)
        {
            return Invalid(ExecutionContextInvalidReason.MalformedPayload);
        }
        catch (NotSupportedException)
        {
            return Invalid(ExecutionContextInvalidReason.MalformedPayload);
        }
        catch (ArgumentException)
        {
            return Invalid(ExecutionContextInvalidReason.MalformedPayload);
        }
        catch (InvalidOperationException)
        {
            return Invalid(ExecutionContextInvalidReason.MalformedPayload);
        }
    }

    private static HistoricalExecutionContextLoad Invalid(ExecutionContextInvalidReason reason)
        => new(HistoricalExecutionContextState.Invalid, null, reason);
}
