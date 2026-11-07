namespace Daoris.Driver;

/// <summary>
/// A repository's integration declaration that waits for the owner's acceptance: what its
/// line commits, the blob of that <c>daoris.gates.json</c>, against the one this machine accepted. <i>What needs you</i> lists it, its door
/// the repository's set-up page, where the declaration is read and accepted.
/// </summary>
/// <param name="Repository">The repository whose line declares an integration.</param>
/// <param name="Workspace">The workspace of the checkout read.</param>
/// <param name="Line">The line whose tip is read.</param>
/// <param name="Blob">The declaration's blob at that tip.</param>
/// <param name="State"><see cref="IntegrationAcceptance.Unaccepted"/> or <see cref="IntegrationAcceptance.Changed"/>: integration goes on under the last accepted blob in the second.</param>
/// <param name="Since">When this blob is first read here as owed.</param>
public sealed record DeclarationOwed(string Repository, string Workspace, string Line, string Blob, string State, DateTimeOffset Since);

/// <summary>
/// Which repositories declare an integration their owner has not accepted: the look's own read,
/// since the attention judge may start no git. Each repository with a checkout here is read at its line's committed
/// <c>daoris.gates.json</c> against <see cref="IntegrationAcceptance.Of"/>. A line that holds no declaration, or one that declares no
/// <c>integration</c>, owes nothing.
/// </summary>
/// <remarks>
/// The tip commit is read from git's own files (<see cref="TreeTips.Named"/>, no process), and git is asked only when it moved since the last
/// read: one <c>ls-tree</c> for the blob, and one <c>cat-file</c> only for a blob not seen before. Where the files cannot name the tip (a
/// reftable repository) git is asked for the blob at every look. The acceptance is judged afresh at every look from the choices, so an
/// acceptance ends its row at the next look with no git at all.
/// </remarks>
public sealed class IntegrationOwed
{
    private sealed record Read(string Root, string Line, string? Tip, string Blob, bool Declares, DateTimeOffset Since);

    private readonly Dictionary<string, Read> _reads = new(StringComparer.OrdinalIgnoreCase);

    private volatile IReadOnlyList<DeclarationOwed> _owed = [];

    /// <summary>What the last look found owed, oldest first; none before a look.</summary>
    public IReadOnlyList<DeclarationOwed> Owed => _owed;

    /// <summary>How many times the last look asked git, for a test to hold the one-read-per-move rule.</summary>
    internal int GitReads { get; private set; }

    /// <summary>
    /// One look: each repository of <paramref name="registry"/> with a checkout here, its declaration read where its line's tip moved, its
    /// state judged against <paramref name="config"/>'s acceptance. A repository whose tip cannot be read keeps the state last read for it.
    /// </summary>
    public async Task LookAsync(DriverConfig config, IEnumerable<RepoView> registry, DateTimeOffset now, CancellationToken ct)
    {
        GitReads = 0;
        var owed = new List<DeclarationOwed>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in registry.Where(row => !string.IsNullOrWhiteSpace(row.Root)))
        {
            ct.ThrowIfCancellationRequested();
            // One checkout speaks for a repository, as the Workflow tab's read takes the first that exists.
            if (seen.Contains(row.Repository) || !Directory.Exists(row.Root)) continue;
            seen.Add(row.Repository);

            var line = await CanonicalLine.ResolveAsync(row.Root!, row.Repository, row.Workspace, config, ct).ConfigureAwait(false);
            if (line.Branch is not { } branch) continue;
            var read = await ReadAsync(row.Root!, row.Repository, branch, now, ct).ConfigureAwait(false);
            if (read is not { Declares: true }) continue;
            var state = IntegrationAcceptance.StateOf(IntegrationAcceptance.Of(config, row.Repository), read.Blob);
            if (state is IntegrationAcceptance.Unaccepted or IntegrationAcceptance.Changed)
            {
                owed.Add(new DeclarationOwed(row.Repository, row.Workspace, branch, read.Blob, state, read.Since));
            }
        }

        // A repository the registry no longer holds is forgotten with its read.
        foreach (var gone in _reads.Keys.Where(key => !seen.Contains(key)).ToList()) _reads.Remove(gone);
        _owed = [.. owed.OrderBy(each => each.Since).ThenBy(each => each.Repository, StringComparer.Ordinal)];
    }

    private async Task<Read?> ReadAsync(string root, string repository, string line, DateTimeOffset now, CancellationToken ct)
    {
        _reads.TryGetValue(repository, out var held);
        var tip = TreeTips.CommonDirectory(root) is { } common ? TreeTips.Named(common, $"refs/heads/{line}") : null;
        if (held is not null && tip is not null && held.Root == root && held.Line == line && held.Tip == tip) return held;

        GitReads++;
        if (await IntegrationAcceptance.TipAsync(root, line, ct).ConfigureAwait(false) is not { } blob) return held;
        // A blob is its text: one seen before is not read again, whatever commit carries it.
        var declares = held is not null && held.Blob == blob && held.Root == root
            ? held.Declares
            : blob != IntegrationAcceptance.None && await DeclaresAsync(root, blob, ct).ConfigureAwait(false);
        var since = held is not null && held.Blob == blob && held.Root == root ? held.Since : now;
        var read = new Read(root, line, tip, blob, declares, since);
        _reads[repository] = read;
        return read;
    }

    private async Task<bool> DeclaresAsync(string root, string blob, CancellationToken ct)
    {
        GitReads++;
        return IntegrationDeclaration.Read(await IntegrationAcceptance.TextAsync(root, blob, ct).ConfigureAwait(false)).Facts is not null;
    }
}
