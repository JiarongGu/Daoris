using Daoris.Assistant.Ports;

namespace Daoris.Assistant.Testing;

/// <summary>The assistant's records in memory, by kind and key.</summary>
public sealed class MemoryAssistantRecords : IAssistantRecords
{
    private readonly Lock _gate = new();
    private readonly Dictionary<(AssistantRecordKind Kind, string Key), AssistantRecord> _kept = [];

    public Task KeepAsync(AssistantRecord record, CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();
        lock (_gate) _kept[(record.Kind, record.Key)] = record;
        return Task.CompletedTask;
    }

    public Task<AssistantRecord?> ReadAsync(AssistantRecordKind kind, string key, CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();
        lock (_gate) return Task.FromResult(_kept.GetValueOrDefault((kind, key)));
    }

    public Task<IReadOnlyList<AssistantRecord>> ListAsync(AssistantRecordKind kind, CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<AssistantRecord>>(
                [.. _kept.Values.Where(each => each.Kind == kind).OrderBy(each => each.Key, StringComparer.Ordinal)]);
        }
    }
}
