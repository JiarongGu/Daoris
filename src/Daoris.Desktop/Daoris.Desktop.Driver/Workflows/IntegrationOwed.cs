namespace Daoris.Driver;

/// <summary>
/// A repository's declaration that waits for the owner's acceptance: what its line commits, the blob of that
/// <c>daoris.gates.json</c>, against the one this machine accepted. <i>What needs you</i> lists it, its door the repository's set-up
/// page, where the change is read and accepted once for every purpose.
/// </summary>
/// <param name="Repository">The repository whose line declares.</param>
/// <param name="Workspace">The workspace of the checkout read.</param>
/// <param name="Line">The line whose tip is read.</param>
/// <param name="Blob">The declaration's blob at that tip.</param>
/// <param name="State"><see cref="IntegrationAcceptance.Unaccepted"/> or <see cref="IntegrationAcceptance.Changed"/>: integration goes on under the last accepted blob in the second.</param>
/// <param name="Since">When this blob is first read here as owed.</param>
public sealed record DeclarationOwed(string Repository, string Workspace, string Line, string Blob, string State, DateTimeOffset Since)
{
    /// <summary>The checkout the declaration is read in, for the look's own use; never said.</summary>
    internal string? Root { get; init; }

    /// <summary>What it changes against the blob accepted, by check and command; null where nothing is accepted or the texts were not read.</summary>
    public DeclarationChange? Change { get; init; }

    /// <summary>The accepted blob it is judged against: a keeping applies only while that acceptance stands.</summary>
    internal string? Against { get; init; }

    /// <summary>What a change that alters no command set aside against the blob accepted (<see cref="DeclarationCommands.SetAside"/>); none where the look judged none.</summary>
    public IReadOnlyList<DeclarationSetAside> SetAside { get; init; } = [];
}

/// <summary>One pair of accepted and line blobs judged: its change, whether it keeps the acceptance, what it set aside, or why it could not be judged.</summary>
internal sealed record DeclarationJudged(DeclarationChange? Change, bool Keeps, IReadOnlyList<DeclarationSetAside> SetAside, string? Problem = null);

/// <summary>
/// Which repositories' committed declaration their owner has not accepted: the look's own read, since the attention judge may start no
/// git. Each repository with a checkout here is read at its line's committed <c>daoris.gates.json</c> against the one acceptance
/// (<see cref="IntegrationAcceptance.Of"/>, and the landed route's gates where that route verifies). A line that holds no declaration
/// owes nothing, nor does one that declares no <c>integration</c> to a repository whose landed route verifies nothing. A repository in full
/// access (<see cref="DeclarationMode"/>) owns its line, so it accepts the declaration its line commits as it lands (<see cref="ByMode"/>)
/// and is never owed; nor is a change that alters no command (<see cref="Kept"/>). A changed row carries its <see cref="DeclarationOwed.Change"/>.
/// </summary>
/// <remarks>
/// The tip commit is read from git's own files (<see cref="TreeTips.Named"/>, no process), and git is asked only when it moved since the last
/// read: one <c>ls-tree</c> for the blob, and one <c>cat-file</c> only for a blob not seen before. Where the files cannot name the tip (a
/// reftable repository) git is asked for the blob at every look. What is owed is judged from the reads against the choices each time it is
/// asked (<see cref="Under"/>), so an acceptance ends its row at once with no git at all.
/// </remarks>
public sealed class IntegrationOwed
{
    private sealed record Read(string Root, string Workspace, string Repository, string Line, string? Tip, string Blob, bool Declares, DateTimeOffset Since);

    private readonly Dictionary<string, Read> _reads = new(StringComparer.OrdinalIgnoreCase);

    private volatile DriverConfig _config = DriverConfig.Empty;

    private volatile IReadOnlyList<Read> _held = [];

    /// <summary>What the last look found owed under the choices it read, oldest first; none before a look.</summary>
    public IReadOnlyList<DeclarationOwed> Owed => Under(_config);

    /// <summary>How many times the last look asked git, for a test to hold the one-read-per-move rule.</summary>
    internal int GitReads { get; private set; }

    /// <summary>What is owed under <paramref name="config"/> now, oldest first: the reads of the last look judged afresh, a full-access repository's left out.</summary>
    public IReadOnlyList<DeclarationOwed> Under(DriverConfig config) => Judge(config).Owed;

    /// <summary>What <paramref name="config"/>'s full-access repositories accept as it lands: each declaration waiting that its repository's own line owns.</summary>
    public IReadOnlyList<DeclarationOwed> ByMode(DriverConfig config) => Judge(config).ByMode;

    /// <summary>Each changed declaration that alters no command against the accepted blob (<see cref="DeclarationCommands.Keeps"/>): never owed.</summary>
    public IReadOnlyList<DeclarationOwed> Kept(DriverConfig config) => Judge(config).Kept;

    private (IReadOnlyList<DeclarationOwed> Owed, IReadOnlyList<DeclarationOwed> ByMode, IReadOnlyList<DeclarationOwed> Kept) Judge(DriverConfig config)
    {
        var owed = new List<DeclarationOwed>();
        var byMode = new List<DeclarationOwed>();
        var kept = new List<DeclarationOwed>();
        foreach (var read in _held)
        {
            var state = StandingOf(config, read.Repository, read.Workspace, read.Line, read.Blob, read.Declares);
            if (state is not (IntegrationAcceptance.Unaccepted or IntegrationAcceptance.Changed)) continue;
            var against = Against(config, read);
            var judged = against is not null && _judged.TryGetValue((against, read.Blob), out var pair) ? pair : null;
            var row = new DeclarationOwed(read.Repository, read.Workspace, read.Line, read.Blob, state, read.Since)
            {
                Root = read.Root, Change = judged?.Change, Against = against, SetAside = judged?.SetAside ?? [],
            };
            (!DeclarationMode.Waits(config, read.Repository, read.Workspace) ? byMode
                : state == IntegrationAcceptance.Changed && judged is { Keeps: true } ? kept
                : owed).Add(row);
        }

        static IReadOnlyList<DeclarationOwed> Ordered(List<DeclarationOwed> rows) =>
            [.. rows.OrderBy(each => each.Since).ThenBy(each => each.Repository, StringComparer.Ordinal)];
        return (Ordered(owed), Ordered(byMode), Ordered(kept));
    }

    /// <summary>The blob a read is judged against: the integration's acceptance, else the landed route's gates; null where none is accepted.</summary>
    private static string? Against(DriverConfig config, Read read) =>
        IntegrationAcceptance.Of(config, read.Repository) ?? WorkflowSelection.ResolveLanded(config, read.Repository, read.Workspace).Gates;

    /// <summary>Each pair of accepted and line blobs judged once, replaced whole by the look so a judge meanwhile reads one whole map.</summary>
    private volatile IReadOnlyDictionary<(string Accepted, string Blob), DeclarationJudged> _judged = new Dictionary<(string Accepted, string Blob), DeclarationJudged>();

    /// <summary>What the last look could not judge, one sentence each, said once a pair: the change waits for the person.</summary>
    internal IReadOnlyList<string> Unjudged { get; private set; } = [];

    /// <summary>
    /// Each held read not at the accepted blob judged against it once a pair, from git's objects; a pair no read holds is forgotten. A
    /// judgement that fails is kept as failed, so it waits for the person and is said once, and the look goes on to the next read.
    /// </summary>
    private async Task JudgeAsync(DriverConfig config, IReadOnlyList<Read> held, CancellationToken ct)
    {
        var blobs = held.Select(read => read.Blob).ToHashSet(StringComparer.Ordinal);
        var next = _judged.Where(each => blobs.Contains(each.Key.Blob)).ToDictionary(each => each.Key, each => each.Value);
        var unjudged = new List<string>();
        foreach (var read in held)
        {
            if (Against(config, read) is not { } against || against == read.Blob || next.ContainsKey((against, read.Blob))) continue;
            GitReads++;
            var was = await IntegrationAcceptance.TextAsync(read.Root, against, ct).ConfigureAwait(false);
            var now = await IntegrationAcceptance.TextAsync(read.Root, read.Blob, ct).ConfigureAwait(false);
            if (now is null || (was is null && against != IntegrationAcceptance.None)) continue;
            try
            {
                next[(against, read.Blob)] = DeclarationCommands.Keeps(was, now)
                    ? new DeclarationJudged(DeclarationChange.Between(was, now), true, DeclarationCommands.SetAside(was, now))
                    : new DeclarationJudged(DeclarationChange.Between(was, now), false, []);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                // A reader's fault never ends the look: the pair is held as a change for the person and said once.
                next[(against, read.Blob)] = new DeclarationJudged(null, false, [], error.Message);
                unjudged.Add($"{read.Repository}'s daoris.gates.json on {read.Line} could not be judged ({error.Message.TrimEnd('.')}), so its change waits for you.");
            }
        }

        _judged = next;
        Unjudged = unjudged;
    }

    /// <summary>The state of the one acceptance of <paramref name="blob"/>: the integration's where the line declares one, else the landed route's where it verifies.</summary>
    internal static string StandingOf(DriverConfig config, string repository, string workspace, string line, string blob, bool declares)
    {
        if (declares
            && IntegrationAcceptance.StateOf(IntegrationAcceptance.Of(config, repository), blob) is var held
            and (IntegrationAcceptance.Unaccepted or IntegrationAcceptance.Changed))
        {
            return held;
        }

        var route = WorkflowSelection.ResolveLanded(config, repository, workspace);
        return LandedGates.StateOf(route, new LandedTip(line, "", blob)) is var landed and (LandedGates.Unaccepted or LandedGates.Changed)
            ? landed
            : IntegrationAcceptance.Accepted;
    }

    /// <summary>
    /// One look: each repository of <paramref name="registry"/> with a checkout here, its declaration read where its line's tip moved, its
    /// state judged against <paramref name="config"/>'s acceptance. A repository whose tip cannot be read keeps the state last read for it.
    /// </summary>
    public async Task LookAsync(DriverConfig config, IEnumerable<RepoView> registry, DateTimeOffset now, CancellationToken ct)
    {
        GitReads = 0;
        _config = config;
        var held = new List<Read>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in registry.Where(row => !string.IsNullOrWhiteSpace(row.Root)))
        {
            ct.ThrowIfCancellationRequested();
            // One checkout speaks for a repository, as the Workflow tab's read takes the first that exists.
            if (seen.Contains(row.Repository) || !Directory.Exists(row.Root)) continue;
            seen.Add(row.Repository);

            var line = await CanonicalLine.ResolveAsync(row.Root!, row.Repository, row.Workspace, config, ct).ConfigureAwait(false);
            if (line.Branch is not { } branch) continue;
            var read = await ReadAsync(row.Root!, row.Workspace, row.Repository, branch, now, ct).ConfigureAwait(false);
            if (read is not null && read.Blob != IntegrationAcceptance.None) held.Add(read);
        }

        // A repository the registry no longer holds is forgotten with its read.
        foreach (var gone in _reads.Keys.Where(key => !seen.Contains(key)).ToList()) _reads.Remove(gone);
        await JudgeAsync(config, held, ct).ConfigureAwait(false);
        _held = held;
    }

    private async Task<Read?> ReadAsync(string root, string workspace, string repository, string line, DateTimeOffset now, CancellationToken ct)
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
        var read = new Read(root, workspace, repository, line, tip, blob, declares, since);
        _reads[repository] = read;
        return read;
    }

    private async Task<bool> DeclaresAsync(string root, string blob, CancellationToken ct)
    {
        GitReads++;
        return IntegrationDeclaration.Read(await IntegrationAcceptance.TextAsync(root, blob, ct).ConfigureAwait(false)).Facts is not null;
    }
}
