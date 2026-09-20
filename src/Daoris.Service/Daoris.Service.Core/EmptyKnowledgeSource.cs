namespace Daoris.Knowledge;

/// <summary>
/// A source with nothing to say — the shared deployment's source, because a shared deployment is fed,
/// not scanned (D47 §4). The `/api/refresh` door refuses in shared mode, but the service also indexes
/// on first use when its store is empty; composing with this source is what makes "never scans" true
/// by construction rather than by which route a caller happened to hit first.
/// </summary>
public sealed class EmptyKnowledgeSource : IKnowledgeSource
{
    public string Name => "fed";

    public Task<IReadOnlyList<KnowledgeEntry>> ReadAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<KnowledgeEntry>>([]);
}
