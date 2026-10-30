namespace Daoris.Knowledge;

/// <summary>
/// Reads a source into a store, applying the disclosure policy on the way in.
/// </summary>
/// <remarks>
/// The policy is applied at <b>ingest</b>, not at query time. Withholding at query time means the
/// material is already in the store and one forgotten filter discloses it; withholding at ingest
/// means it was never there to leak.
/// </remarks>
public sealed class KnowledgeIndex(IKnowledgeStore store, IDisclosurePolicy? disclosure = null, TimeProvider? time = null)
{
    private readonly IDisclosurePolicy _disclosure = disclosure ?? DisclosurePolicy.LocalOnly;

    private readonly TimeProvider _time = time ?? TimeProvider.System;

    /// <param name="workspaceOf">
    /// Which circle a repository is wired to (D48). Stamped HERE, on the way in, because a source reads
    /// files and the wiring is a registry row — no scanner can know it, and asking one to would put
    /// machine configuration inside a filesystem walk. Absent, everything lands in the default, which
    /// is what a machine that never named a workspace should see.
    /// </param>
    /// <param name="stillRegistered">
    /// Whether a repository is still registered, when the source reads the registered roots and
    /// nothing else fills this store (a local host, POLISH5). Then a held repository the registry no
    /// longer names is a ghost whatever the scan saw — retiring the LAST repository leaves a scan that
    /// sees nothing, and the guard below would keep every retired repository indexed forever. Absent
    /// for a fed store, whose empty source says nothing about what it was fed.
    /// </param>
    public async Task<IndexReport> RefreshAsync(
        IKnowledgeSource source, Func<string, string>? workspaceOf = null,
        Func<string, bool>? stillRegistered = null, CancellationToken ct = default,
        IReadOnlyList<string>? onlyRoots = null) =>
        (await RefreshReadingAsync(source, workspaceOf, stillRegistered, onlyRoots, generations: null, readOf: null, ct)
            .ConfigureAwait(false)).Report;

    /// <summary>A refresh that says which repositories it did not write (KNOWCENTRAL2a).</summary>
    /// <param name="generations">
    /// Each repository's generation, read before anything the refresh judged by (the registry, the source); null reads them here,
    /// before the source. A repository written since is not written again, and is named in <c>Refused</c>.
    /// </param>
    /// <param name="readOf">The commit and root signature a repository was read at; null names none.</param>
    internal async Task<(IndexReport Report, IReadOnlySet<string> Refused)> RefreshReadingAsync(
        IKnowledgeSource source, Func<string, string>? workspaceOf, Func<string, bool>? stillRegistered,
        IReadOnlyList<string>? onlyRoots, IReadOnlyDictionary<string, long>? generations,
        Func<string, (string? Commit, RootSignature? Signature)>? readOf, CancellationToken ct)
    {
        generations ??= await store.GenerationsAsync(ct).ConfigureAwait(false);
        var at = _time.GetUtcNow();
        ReadingWrite Reading(string repository, string? commit = null, RootSignature? signature = null) =>
            new(generations.TryGetValue(repository, out var generation) ? generation : 0, commit, signature, Fed: false, at);
        var refused = new HashSet<string>(StringComparer.Ordinal);

        // Some roots alone (KNOWCURRENT1): a root that moved is replaced and the others stand. No ghost is retired from a
        // part, since what it did not read says nothing about what is gone.
        var partial = onlyRoots is not null && source is IRootReadingSource;
        var read = partial
            ? await ((IRootReadingSource)source).ReadRootsAsync(onlyRoots!, ct).ConfigureAwait(false)
            : await source.ReadAsync(ct).ConfigureAwait(false);
        var permitted = read
            .Where(_disclosure.MayLeaveMachine)
            .Select(entry => entry with { Workspace = workspaceOf?.Invoke(entry.Repository) ?? entry.Workspace })
            .ToList();

        var byRepository = permitted.GroupBy(e => e.Repository, StringComparer.Ordinal).ToList();
        foreach (var group in byRepository)
        {
            var (commit, signature) = readOf?.Invoke(group.Key) ?? (null, null);
            if (!await store.ReplaceRepositoryAsync(group.Key, group.ToList(), Reading(group.Key, commit, signature), ct: ct).ConfigureAwait(false))
            {
                refused.Add(group.Key);
            }
        }

        // Retire what the source no longer has. Replace-what-you-saw covers every present repository
        // and says nothing about the absent ones — which is exactly where the ghosts live: a
        // repository renamed on disk was still being served weeks later, indistinguishable from a
        // live project. Guarded on the scan having seen ANYTHING, because a scan that found nothing
        // is a mis-set root far more often than a family that emptied, and "refresh wiped the index"
        // is the wrong answer to a wrong path.
        // The guard is about a PATH, so it keeps a registered repository whose checkout could not be
        // read; it never keeps one the registry stopped naming.
        var seen = new HashSet<string>(byRepository.Select(g => g.Key), StringComparer.Ordinal);
        if (!partial && (byRepository.Count > 0 || stillRegistered is not null))
        {
            var held = (await store.AllAsync(ct).ConfigureAwait(false))
                .Select(e => e.Repository)
                .Distinct(StringComparer.Ordinal);
            foreach (var ghost in held.Where(repository => !seen.Contains(repository)
                && (byRepository.Count > 0 || !stillRegistered!(repository))))
            {
                await store.ReplaceRepositoryAsync(ghost, [], Reading(ghost), ct: ct).ConfigureAwait(false);
            }
        }

        return (new IndexReport(source.Name, byRepository.Count, permitted.Count, read.Count - permitted.Count), refused);
    }
}
