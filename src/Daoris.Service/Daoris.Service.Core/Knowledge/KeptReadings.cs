namespace Daoris.Knowledge;

/// <summary>
/// The readings an earlier process over this store left, taken at a host's first use as its own: a start reads what it kept instead
/// of reading every registered checkout again. A reading is taken only where this build stamped it under the same telling of where
/// records live and its root's signature still stands; a root otherwise is read again, as one that moved is.
/// </summary>
/// <remarks>
/// The store holding a repository no registered root here reads (a ghost, a feed, a repository renamed) takes a whole reading, which
/// retires what it no longer reads, as every start did before the readings were kept.
/// </remarks>
internal static class KeptReadings
{
    /// <summary>
    /// What reads the roots: this build's scanner (its module's identity, which a change to any of the core's code moves) and the
    /// documents and index folders it is told, so a reading another build or another telling made is read again.
    /// </summary>
    public static string Stamp(string? documents, string? index) =>
        $"{typeof(RepositoryScanner).Assembly.ManifestModule.ModuleVersionId:N}|{documents}|{index}";

    /// <summary>
    /// Each registered root's stored reading this host may take, or null where the store holds a repository none of them answers for.
    /// A root with no reading to take is absent, so its first judgement reads it.
    /// </summary>
    public static async Task<IReadOnlyList<(string Root, IndexReading Reading)>?> TakeAsync(
        IKnowledgeStore store, IReadOnlyList<(string Repository, string Root)> roots, string stamp, CancellationToken ct)
    {
        var stored = (await store.ReadingsAsync(ct).ConfigureAwait(false)).ToDictionary(reading => reading.Repository, StringComparer.Ordinal);
        var taken = roots
            .Where(root => stored.TryGetValue(root.Repository, out var reading)
                && reading is { Fed: false, Signature: not null } && reading.Scanner == stamp)
            .Select(root => (root.Root, stored[root.Repository]))
            .ToList();

        var registered = roots.Select(root => root.Repository).ToHashSet(StringComparer.Ordinal);
        var held = await store.CountByRepositoryAsync(ct).ConfigureAwait(false);
        return held.Keys.All(registered.Contains) ? taken : null;
    }
}
