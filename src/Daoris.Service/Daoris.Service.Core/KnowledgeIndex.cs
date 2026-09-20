namespace Daoris.Knowledge;

/// <summary>
/// Reads a source into a store, applying the disclosure policy on the way in.
/// </summary>
/// <remarks>
/// The policy is applied at <b>ingest</b>, not at query time. Withholding at query time means the
/// material is already in the store and one forgotten filter discloses it; withholding at ingest
/// means it was never there to leak.
/// </remarks>
public sealed class KnowledgeIndex(IKnowledgeStore store, IDisclosurePolicy? disclosure = null)
{
    private readonly IDisclosurePolicy _disclosure = disclosure ?? DisclosurePolicy.LocalOnly;

    /// <param name="workspaceOf">
    /// Which circle a repository is wired to (D48). Stamped HERE, on the way in, because a source reads
    /// files and the wiring is a registry row — no scanner can know it, and asking one to would put
    /// machine configuration inside a filesystem walk. Absent, everything lands in the default, which
    /// is what a machine that never named a workspace should see.
    /// </param>
    public async Task<IndexReport> RefreshAsync(
        IKnowledgeSource source, Func<string, string>? workspaceOf = null, CancellationToken ct = default)
    {
        var read = await source.ReadAsync(ct).ConfigureAwait(false);
        var permitted = read
            .Where(_disclosure.MayLeaveMachine)
            .Select(entry => entry with { Workspace = workspaceOf?.Invoke(entry.Repository) ?? entry.Workspace })
            .ToList();

        var byRepository = permitted.GroupBy(e => e.Repository, StringComparer.Ordinal).ToList();
        foreach (var group in byRepository)
        {
            await store.ReplaceRepositoryAsync(group.Key, group.ToList(), ct).ConfigureAwait(false);
        }

        // Retire what the source no longer has. Replace-what-you-saw covers every present repository
        // and says nothing about the absent ones — which is exactly where the ghosts live: a
        // repository renamed on disk was still being served weeks later, indistinguishable from a
        // live project. Guarded on the scan having seen ANYTHING, because a scan that found nothing
        // is a mis-set root far more often than a family that emptied, and "refresh wiped the index"
        // is the wrong answer to a wrong path.
        if (byRepository.Count > 0)
        {
            var seen = new HashSet<string>(byRepository.Select(g => g.Key), StringComparer.Ordinal);
            var held = (await store.AllAsync(ct).ConfigureAwait(false))
                .Select(e => e.Repository)
                .Distinct(StringComparer.Ordinal);
            foreach (var ghost in held.Where(repository => !seen.Contains(repository)))
            {
                await store.ReplaceRepositoryAsync(ghost, [], ct).ConfigureAwait(false);
            }
        }

        return new IndexReport(source.Name, byRepository.Count, permitted.Count, read.Count - permitted.Count);
    }
}
