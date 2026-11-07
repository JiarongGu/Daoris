namespace Daoris.Knowledge;

/// <summary>
/// Cites an entry at the reading the index stores for it, and rechecks a citation.
/// </summary>
internal sealed class KnowledgeCitations(
    IKnowledgeStore store,
    Func<string, string, bool?>? fileUncommitted,
    Func<string, RootSignature>? rootSignature,
    Action refuseNewerIndex,
    Func<CancellationToken, Task> ensureIndexed,
    Func<string, CancellationToken, Task<string?>> readAgainWhereMoved)
{
    /// <summary>
    /// The entry's commit, read or fed, and its body's digest, the root read again first where it moved. An entry its own
    /// record already supersedes is refused, naming what superseded it.
    /// </summary>
    /// <param name="lines">The span a hit named, which must be lines of the entry; null cites it whole.</param>
    public async Task<CitationAnswer> CiteAsync(string id, LineSpan? lines, CancellationToken ct)
    {
        refuseNewerIndex();
        await ensureIndexed(ct).ConfigureAwait(false);
        await readAgainWhereMoved(RepositoryOfId(id), ct).ConfigureAwait(false);
        var held = await store.HeldAsync([id], entry => entry.Kind == EntryKind.Decision, ct).ConfigureAwait(false);
        if (!held.Entries.TryGetValue(id, out var entry)) return new(null, $"No entry with id `{id}` is indexed. Ids come from `knowledge_search`.");
        if (lines is { } range && entry.Cut(range) is null) return new(null, $"Lines {range} are not lines of `{id}`, as a hit names them.");
        if (!held.Readings.TryGetValue(entry.Repository, out var reading) || reading.Commit is null)
        {
            return new(null, $"No reading of `{entry.Repository}` names a commit, so nothing of it can be cited yet.");
        }

        if (DecisionMarks.Marked(entry, held, "superseded") is { } superseded)
        {
            var successor = superseded.By is { } by ? $"D{by}" : null;
            return new(null, $"`{id}` is already superseded by {successor ?? "a later note of its own record"}: cite that instead.", successor);
        }

        return new(new Citation(id, lines, reading.Commit, reading.Fed, FeedDigest.Of(entry.Body)));
    }

    /// <summary>
    /// Whether a citation still holds. The root's signature is compared with the stored one and the root read again where it
    /// moved; the entry, its file's marks and the reading are read in one transaction; then the cited file's own state is asked
    /// against that commit, and the signature again. No in-process gate is taken but a moved root's read.
    /// </summary>
    /// <param name="attempt">How many rechecks in a row this one is: a cited id found gone asks to be cited again at <see cref="KnowledgeService.GoneTries"/>.</param>
    public async Task<CitationCheck> RecheckAsync(Citation citation, int attempt, CancellationToken ct)
    {
        refuseNewerIndex();
        await ensureIndexed(ct).ConfigureAwait(false);
        var root = await readAgainWhereMoved(RepositoryOfId(citation.Id), ct).ConfigureAwait(false);
        var held = await store.HeldAsync([citation.Id], entry => entry.Kind == EntryKind.Decision, ct).ConfigureAwait(false);
        if (!held.Entries.TryGetValue(citation.Id, out var entry))
        {
            var again = attempt >= KnowledgeService.GoneTries;
            return Inconclusive($"`{citation.Id}` is no longer indexed{(again ? "; cite it again" : "")}", null, again);
        }

        if (!held.Readings.TryGetValue(entry.Repository, out var reading) || reading.Commit is null)
        {
            return Inconclusive($"no reading of `{entry.Repository}` names a commit", null);
        }

        if (FeedDigest.Of(entry.Body) != citation.Digest) return Inconclusive("its body changed since it was cited; cite it again", reading, citeAgain: true);

        // A fed reading answers for its fed commit; a read one, for its file as that commit has it, here.
        if (!reading.Fed)
        {
            if (root is null || fileUncommitted is null) return Inconclusive("its file's state cannot be asked here", reading);
            bool? modified;
            try
            {
                modified = fileUncommitted(root, entry.RelativePath);
            }
            catch (Exception)
            {
                modified = null;
            }

            if (modified is null) return Inconclusive("its file's state is unknown", reading);
            if (modified == true) return Inconclusive("its file has changes not committed", reading);
            var moved = rootSignature is null ? RootSignature.Head(root) != reading.Commit : rootSignature(root) != reading.Signature;
            if (moved) return Inconclusive("its root moved during the check", reading);
        }

        if (DecisionMarks.Marked(entry, held, "superseded") is { } superseded)
        {
            var by = superseded.By is { } failed ? $"D{failed}" : null;
            return new(CitationStanding.Failed, $"superseded by {by ?? "a later note of its own record"}", by, reading.Commit, reading.Fed);
        }

        if (DecisionMarks.Marked(entry, held, "amended") is { } amended)
        {
            var by = amended.By is { } later ? $"D{later}" : null;
            return new(CitationStanding.Amended, $"cited record amended by {by ?? "a later note of its own record"}", by, reading.Commit, reading.Fed);
        }

        return new(CitationStanding.Holds, $"holds, {(reading.Fed ? "fed" : "read")} at {Clipped.ShortCommit(reading.Commit, 8)}", null, reading.Commit, reading.Fed);

        static CitationCheck Inconclusive(string why, IndexReading? reading, bool citeAgain = false) =>
            new(CitationStanding.Inconclusive, $"inconclusive: {why}", null, reading?.Commit, reading?.Fed ?? false, citeAgain);
    }

    /// <summary>The repository an entry id names: what stands before its first colon.</summary>
    private static string RepositoryOfId(string id) => id.IndexOf(':') is var colon and > 0 ? id[..colon] : id;
}
