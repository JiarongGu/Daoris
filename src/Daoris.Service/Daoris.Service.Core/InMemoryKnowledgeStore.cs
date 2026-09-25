using System.Collections.Concurrent;

namespace Daoris.Knowledge;

/// <summary>
/// Keeps entries in memory. The store the tests use, and enough to run before a file exists — a
/// knowledge index that must be persisted before it can be tried is one that gets tried late.
/// </summary>
/// <remarks>
/// 🔴 It keeps the SQLite store's contract, not a looser one: an id is unique across the index, and a
/// replace that would repeat one is refused and changes nothing. Accepting it let a refresh that wrote
/// one id twice pass every test and fail on the real index (REV3). <c>KnowledgeStoreContractTests</c>
/// asserts both stores against the same case.
/// </remarks>
public sealed class InMemoryKnowledgeStore : IKnowledgeStore
{
    private readonly ConcurrentDictionary<string, List<KnowledgeEntry>> _byRepository = new(StringComparer.Ordinal);

    public Task ReplaceRepositoryAsync(string repository, IReadOnlyList<KnowledgeEntry> entries, CancellationToken ct = default)
    {
        var held = new HashSet<string>(
            _byRepository.Where(pair => pair.Key != repository).SelectMany(pair => pair.Value).Select(entry => entry.Id),
            StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (!held.Add(entry.Id)) throw new InvalidOperationException($"UNIQUE constraint failed: entries.id ({entry.Id})");
        }

        _byRepository[repository] = [.. entries];
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<KnowledgeEntry>> AllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<KnowledgeEntry>>(_byRepository.Values.SelectMany(e => e).ToList());

    public Task<IReadOnlyDictionary<string, int>> CountByRepositoryAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<string, int>>(_byRepository
            .Where(pair => pair.Value.Count > 0)
            .ToDictionary(pair => pair.Key, pair => pair.Value.Count, StringComparer.Ordinal));

    public Task<KnowledgeEntry?> FindAsync(string id, CancellationToken ct = default) =>
        Task.FromResult(_byRepository.Values.SelectMany(e => e)
            .FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.Ordinal)));
}
