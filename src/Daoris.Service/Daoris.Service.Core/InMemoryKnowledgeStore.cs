using System.Collections.Concurrent;

namespace Daoris.Knowledge;

/// <summary>
/// Keeps entries in memory. The store the tests use, and enough to run before a file exists — a
/// knowledge index that must be persisted before it can be tried is one that gets tried late.
/// </summary>
public sealed class InMemoryKnowledgeStore : IKnowledgeStore
{
    private readonly ConcurrentDictionary<string, List<KnowledgeEntry>> _byRepository = new(StringComparer.Ordinal);

    public Task ReplaceRepositoryAsync(string repository, IReadOnlyList<KnowledgeEntry> entries, CancellationToken ct = default)
    {
        _byRepository[repository] = [.. entries];
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<KnowledgeEntry>> AllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<KnowledgeEntry>>(_byRepository.Values.SelectMany(e => e).ToList());

    public Task<KnowledgeEntry?> FindAsync(string id, CancellationToken ct = default) =>
        Task.FromResult(_byRepository.Values.SelectMany(e => e)
            .FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.Ordinal)));
}
