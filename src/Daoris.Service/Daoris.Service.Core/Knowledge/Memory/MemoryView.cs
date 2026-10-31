namespace Daoris.Knowledge;

/// <summary>The reads a memory answers: what every memory tool reads, and nothing that writes.</summary>
public interface IMemoryReads
{
    /// <summary>The current entries <paramref name="query"/> matches, best first.</summary>
    Task<IReadOnlyList<MemoryEntry>> SearchAsync(string query, int limit, CancellationToken ct = default);

    /// <summary>The entry, ended or not; null where the memory keeps none by that id.</summary>
    Task<MemoryEntry?> GetAsync(string id, CancellationToken ct = default);

    /// <summary>The entries <paramref name="id"/> relates to.</summary>
    Task<IReadOnlyList<MemoryRelated>> ExpandAsync(string id, CancellationToken ct = default);
}

/// <summary>
/// Another repository's memory as a reader meets it (Lyntai's MC7; the memory coordinator design §6 B, KNOWCENTRAL2g): its type has no write,
/// a read through it moves nothing of the owner's, and every entry is the owner's by reference. Its check is asked about an entry before
/// <see cref="IMemoryReads.GetAsync"/> or <see cref="IMemoryReads.ExpandAsync"/> reads it, and one it refuses reads as missing; a search is
/// not checked per entry, being of a repository the caller's workspace already shares.
/// </summary>
public interface IMemoryView : IMemoryReads
{
    /// <summary>The entry's revision, read by its id alone; null where it is missing.</summary>
    Task<long?> RevisionOfAsync(string id, CancellationToken ct = default);
}

/// <summary>What a view asks before it reads an entry (Lyntai's <c>IMemoryViewCheck</c>): a call, not a grant; the scope stays the caller's.</summary>
public interface IMemoryViewCheck
{
    Task<bool> PermitsAsync(MemoryReference reference, CancellationToken ct = default);
}

/// <summary>
/// A view's check from a workspace (the memory coordinator design §2): an entry is read only where the registry places its repository in
/// <paramref name="workspace"/>, read again at each ask, so a repository moved out since the call began is refused before anything of it is
/// read. None resolved refuses every entry.
/// </summary>
public sealed class MemoryWorkspaceCheck(KnowledgeService registry, string? workspace) : IMemoryViewCheck
{
    private readonly List<MemoryReference> _refused = [];

    /// <summary>The entries refused, in the order asked.</summary>
    public IReadOnlyList<MemoryReference> Refused
    {
        get
        {
            lock (_refused) return [.. _refused];
        }
    }

    public async Task<bool> PermitsAsync(MemoryReference reference, CancellationToken ct = default)
    {
        var members = workspace is null ? [] : await registry.RegistryAsync(workspace, ct).ConfigureAwait(false);
        if (members.Any(member => string.Equals(member.Repository, reference.Repository, StringComparison.OrdinalIgnoreCase))) return true;
        lock (_refused) _refused.Add(reference);
        return false;
    }
}

/// <summary>Reading another repository's memory (KNOWCENTRAL2g): the words where it cannot be read, and the recheck of an entry cited.</summary>
public static class MemoryViews
{
    /// <summary>Why another repository's memory is not read where its engine gives no view, which a read through the engine could move.</summary>
    public const string CannotView = "cannot be read from here without moving it";

    /// <summary>That <paramref name="repository"/>'s memory was not read, in a sentence.</summary>
    public static string Unviewable(string repository) =>
        $"`{repository}`'s memory {CannotView}: its engine gives no read-only view, so nothing of it was read.";

    /// <summary>
    /// The view of <paramref name="repository"/>'s memory, asking <paramref name="check"/>; null where it keeps none, and
    /// <c>Unviewable</c> where its engine gives no view.
    /// </summary>
    public static (IMemoryView? View, bool Unviewable) Of(IMemoryEngines? engines, string repository, IMemoryViewCheck? check) =>
        engines?.Open(repository, MemoryReinforcement.None) is not { } engine ? (null, false)
        : engine.HoldsView ? (engine.ViewOf(check), false)
        : (null, true);

    /// <summary>
    /// A citation of another repository's entry rechecked through its view (the design §5, §6 B): <c>Holds</c> where it is at the revision
    /// cited, or current at a later one (a confirmation or a same-content write moves it, never its text); <c>Failed</c> where its owner
    /// ended it; inconclusive where it is gone, refused by the scope, or its memory cannot be read.
    /// </summary>
    public static async Task<CitationCheck> RecheckAsync(IMemoryEngines? engines, Citation citation, IMemoryViewCheck? check, CancellationToken ct = default)
    {
        if (MemoryReference.Parse(citation.Id) is not { } cited || citation.Revision is not { } read) return Inconclusive("it names no memory entry");
        var (view, unviewable) = Of(engines, cited.Repository, check);
        if (unviewable) return Inconclusive($"`{cited.Repository}`'s memory {CannotView}");
        if (view is null) return Inconclusive($"`{cited.Repository}`'s memory is not open here");
        try
        {
            if (await view.RevisionOfAsync(cited.Entry, ct).ConfigureAwait(false) == read) return new(CitationStanding.Holds, $"holds at revision {read}");
            return await view.GetAsync(cited.Entry, ct).ConfigureAwait(false) switch
            {
                null => Inconclusive("it is gone, or outside the workspace", citeAgain: true),
                { Ended: { } why } => new(CitationStanding.Failed, $"has ended: {MemoryEndToken.Strip(why).TrimEnd('.')}", cited.ToString()),
                var entry => new(CitationStanding.Holds, $"holds at revision {entry.Revision}"),
            };
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return Inconclusive("its memory could not be read");
        }

        static CitationCheck Inconclusive(string why, bool citeAgain = false) => new(CitationStanding.Inconclusive, $"inconclusive: {why}", CiteAgain: citeAgain);
    }
}
