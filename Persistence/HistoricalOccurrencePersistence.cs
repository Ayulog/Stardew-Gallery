namespace StardewGallery;

internal enum ExecutionContextWriteStatus
{
    Missing,
    Stored,
    Rejected,
    Failed
}

internal sealed record NaturalOccurrenceWriteResult(
    long RecordId,
    ExecutionContextWriteStatus ContextStatus
);

internal sealed record PersistedHistoricalOccurrence(
    long RecordId,
    HistoricalEventRecord Record,
    HistoricalExecutionContextLoad ExecutionContext
);
