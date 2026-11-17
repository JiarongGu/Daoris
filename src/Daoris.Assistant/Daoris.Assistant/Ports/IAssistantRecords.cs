namespace Daoris.Assistant.Ports;

/// <summary>
/// The assistant's own records, by kind and key: each kept whole as the core wrote its body, so the files and rows a record lands in
/// keep their formats, and read back, one or a kind's every record.
/// </summary>
public interface IAssistantRecords
{
    /// <summary>Keeps <paramref name="record"/>, replacing the one its kind keeps under its key.</summary>
    Task KeepAsync(AssistantRecord record, CancellationToken cancel);

    /// <summary>The record its kind keeps under <paramref name="key"/>, or null.</summary>
    Task<AssistantRecord?> ReadAsync(AssistantRecordKind kind, string key, CancellationToken cancel);

    /// <summary>The records <paramref name="kind"/> keeps, by key in ordinal order.</summary>
    Task<IReadOnlyList<AssistantRecord>> ListAsync(AssistantRecordKind kind, CancellationToken cancel);
}

/// <summary>One record: its kind, its key within the kind, and its body as the core wrote it.</summary>
public sealed record AssistantRecord(AssistantRecordKind Kind, string Key, string Body);

/// <summary>What the assistant keeps.</summary>
public enum AssistantRecordKind
{
    /// <summary>A second check's report and its findings.</summary>
    Report,

    /// <summary>What a start is handed.</summary>
    Receipt,

    /// <summary>A conversation's kept focus.</summary>
    Focus,

    /// <summary>A place's kept mark.</summary>
    Mark,

    /// <summary>A conversation's facts row.</summary>
    ConversationFacts,

    /// <summary>A kept press.</summary>
    Press,
}
