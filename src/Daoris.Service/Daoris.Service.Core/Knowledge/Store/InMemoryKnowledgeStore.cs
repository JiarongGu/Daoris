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

    private readonly Dictionary<string, IndexReading> _readings = new(StringComparer.Ordinal);

    /// <summary>One write or held read at a time, as the file's transaction makes them.</summary>
    private readonly SemaphoreSlim _turn = new(1, 1);

    public async Task<bool> ReplaceRepositoryAsync(
        string repository, IReadOnlyList<KnowledgeEntry> entries, ReadingWrite reading,
        Func<CancellationToken, Task>? alongside = null, CancellationToken ct = default)
    {
        await _turn.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var generation = _readings.TryGetValue(repository, out var stood) ? stood.Generation : 0;
            if (reading.Expected is { } expected && expected != generation) return false;

            var held = new HashSet<string>(
                _byRepository.Where(pair => pair.Key != repository).SelectMany(pair => pair.Value).Select(entry => entry.Id),
                StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (!held.Add(entry.Id)) throw new InvalidOperationException($"UNIQUE constraint failed: entries.id ({entry.Id})");
            }

            // Before anything here changes, so a failure there leaves this store as it was.
            if (alongside is not null) await alongside(CancellationToken.None).ConfigureAwait(false);
            _byRepository[repository] = [.. entries];
            _readings[repository] = new IndexReading(repository, generation + 1, reading.Commit, reading.Signature, reading.Fed, reading.At);
            return true;
        }
        finally
        {
            _turn.Release();
        }
    }

    public async Task RestampAsync(string repository, string workspace, CancellationToken ct = default)
    {
        await _turn.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            foreach (var (name, entries) in _byRepository.Where(pair => string.Equals(pair.Key, repository, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                if (entries.All(entry => Workspaces.Same(entry.Workspace, workspace))) continue;
                _byRepository[name] = [.. entries.Select(entry => entry with { Workspace = Workspaces.Normalize(workspace) })];
                if (_readings.TryGetValue(name, out var reading)) _readings[name] = reading with { Generation = reading.Generation + 1 };
            }
        }
        finally
        {
            _turn.Release();
        }
    }

    public async Task<IReadOnlyDictionary<string, long>> GenerationsAsync(CancellationToken ct = default)
    {
        await _turn.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return _readings.ToDictionary(pair => pair.Key, pair => pair.Value.Generation, StringComparer.Ordinal);
        }
        finally
        {
            _turn.Release();
        }
    }

    public async Task<IndexReading?> ReadingAsync(string repository, CancellationToken ct = default)
    {
        await _turn.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return _readings.GetValueOrDefault(repository);
        }
        finally
        {
            _turn.Release();
        }
    }

    public async Task<HeldTogether> HeldAsync(
        IReadOnlyCollection<string> ids, Func<KnowledgeEntry, bool> withFile, CancellationToken ct = default)
    {
        await _turn.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var all = _byRepository.Values.SelectMany(held => held).ToList();
            var entries = new Dictionary<string, KnowledgeEntry>(StringComparer.Ordinal);
            var files = new Dictionary<(string Repository, string Path), IReadOnlyList<KnowledgeEntry>>();
            var readings = new Dictionary<string, IndexReading>(StringComparer.Ordinal);
            foreach (var id in ids)
            {
                if (all.FirstOrDefault(entry => entry.Id == id) is not { } entry) continue;
                entries[id] = entry;
                if (withFile(entry))
                {
                    files[(entry.Repository, entry.RelativePath)] =
                        all.Where(held => held.Repository == entry.Repository && held.RelativePath == entry.RelativePath).ToList();
                }

                if (_readings.TryGetValue(entry.Repository, out var reading)) readings[entry.Repository] = reading;
            }

            return new HeldTogether(entries, files, readings);
        }
        finally
        {
            _turn.Release();
        }
    }

    public Task<IReadOnlyList<KnowledgeEntry>> AllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<KnowledgeEntry>>(_byRepository.Values.SelectMany(e => e).ToList());

    public Task<IReadOnlyList<KnowledgeEntry>> OfFileAsync(string repository, string relativePath, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<KnowledgeEntry>>(_byRepository.TryGetValue(repository, out var held)
            ? held.Where(entry => entry.RelativePath == relativePath).ToList()
            : []);

    public Task<IReadOnlyDictionary<string, int>> CountByRepositoryAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<string, int>>(_byRepository
            .Where(pair => pair.Value.Count > 0)
            .ToDictionary(pair => pair.Key, pair => pair.Value.Count, StringComparer.Ordinal));

    public Task<KnowledgeEntry?> FindAsync(string id, CancellationToken ct = default) =>
        Task.FromResult(_byRepository.Values.SelectMany(e => e)
            .FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.Ordinal)));
}
