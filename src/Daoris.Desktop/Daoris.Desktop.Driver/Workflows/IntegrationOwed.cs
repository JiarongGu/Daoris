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

    /// <summary>The units that differ from the accepted ones.</summary>
    public IReadOnlyList<string> ChangedUnits { get; init; } = [];

    /// <summary>What the change drops from a reach: said, owing nothing.</summary>
    public IReadOnlyList<DeclaredNarrowing> Narrowed { get; init; } = [];

    /// <summary>The judge's verdict on each changed unit's digest (<see cref="DeclarationVerdicts"/>).</summary>
    public IReadOnlyList<DeclarationUnitVerdict> Verdicts { get; init; } = [];
}

/// <summary>An acceptance by a whole file's blob that holds no accepted units yet.</summary>
public sealed record DeclarationUnconverted(string Repository, string Root, string Blob);

/// <summary>One pair of accepted and line blobs judged: its change, whether it keeps the acceptance, what it set aside, or why it could not be judged.</summary>
internal sealed record DeclarationJudged(
    DeclarationChange? Change, bool Keeps, IReadOnlyList<DeclarationSetAside> SetAside, string? Problem = null,
    IReadOnlyList<string>? ChangedUnits = null, IReadOnlyList<DeclaredNarrowing>? Narrowed = null, IReadOnlyDictionary<string, string>? Digests = null);

/// <summary>
/// Which repositories' committed declaration their owner has not accepted: the look's own read, since the attention judge may start no
/// git. Each repository with a checkout here is read at its line's committed <c>daoris.gates.json</c> against the one acceptance
/// (<see cref="IntegrationAcceptance.Of"/>, and the landed route's gates where that route verifies). A line that holds no declaration
/// owes nothing, nor does one that declares no <c>integration</c> to a repository whose landed route verifies nothing. A change the risk policy
/// passes on every unit (<see cref="DeclarationVerdicts"/>) is accepted as it lands (<see cref="Passed"/>), as is one that alters no command (<see cref="Kept"/>).
/// </summary>
/// <remarks>
/// The tip commit is read from git's own files (<see cref="TreeTips.Named"/>, no process), and git is asked only when it moved since the last
/// read: one <c>ls-tree</c> for the blob, and one <c>cat-file</c> only for a blob not seen before. Where the files cannot name the tip (a
/// reftable repository) git is asked for the blob at every look. What is owed is judged from the reads against the choices each time it is
/// asked (<see cref="Under"/>), so an acceptance ends its row at once with no git at all. A line that git's files name no ref for stands as
/// a miss while those files stand, and a pair of blobs git gave no text for stands by time alone, each for <see cref="MissStands"/> at most.
/// </remarks>
public sealed class IntegrationOwed
{
    private sealed record Read(string Root, string Workspace, string Repository, string Line, string? Tip, string Blob, bool Declares, DateTimeOffset Since);

    private readonly Dictionary<string, Read> _reads = new(StringComparer.OrdinalIgnoreCase);

    private volatile DriverConfig _config = DriverConfig.Empty;

    private volatile IReadOnlyList<Read> _held = [];

    /// <summary>How long an empty answer from git stands before git is asked again, while the files that name it stand.</summary>
    internal static readonly TimeSpan MissStands = TimeSpan.FromMinutes(10);

    /// <summary>A repository whose line git named no blob for: the checkout, the line, the stamp of its ref files and the time.</summary>
    private sealed record Miss(string Root, string Line, string Stamp, DateTimeOffset At);

    private readonly Dictionary<string, Miss> _misses = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Each pair of blobs git gave no text for (the accepted one is not held here) and the time it asked.</summary>
    private Dictionary<(string Accepted, string Blob), DateTimeOffset> _unread = [];

    /// <summary>What the last look found owed under the choices it read, oldest first; none before a look.</summary>
    public IReadOnlyList<DeclarationOwed> Owed => Under(_config);

    /// <summary>How many times the last look asked git, for a test to hold the one-read-per-move rule.</summary>
    internal int GitReads { get; private set; }

    /// <summary>What is owed under <paramref name="config"/> now, oldest first: the reads of the last look judged afresh, a full-access repository's left out.</summary>
    public IReadOnlyList<DeclarationOwed> Under(DriverConfig config) => Judge(config).Owed;

    /// <summary>What the risk policy of <paramref name="config"/> takes as it lands: each changed declaration whose every unit's verdict passes.</summary>
    public IReadOnlyList<DeclarationOwed> Passed(DriverConfig config) => Judge(config).Passed;

    /// <summary>Each changed declaration that alters no command against the accepted blob (<see cref="DeclarationCommands.Keeps"/>): never owed.</summary>
    public IReadOnlyList<DeclarationOwed> Kept(DriverConfig config) => Judge(config).Kept;

    private (IReadOnlyList<DeclarationOwed> Owed, IReadOnlyList<DeclarationOwed> Passed, IReadOnlyList<DeclarationOwed> Kept) Judge(DriverConfig config)
    {
        var owed = new List<DeclarationOwed>();
        var passed = new List<DeclarationOwed>();
        var kept = new List<DeclarationOwed>();
        foreach (var read in _held)
        {
            var state = StandingOf(config, read.Repository, read.Workspace, read.Line, read.Blob, read.Declares);
            if (state is not (IntegrationAcceptance.Unaccepted or IntegrationAcceptance.Changed)) continue;
            var against = Against(config, read);
            var judged = against is not null && _judged.TryGetValue((against, read.Blob), out var pair) ? pair : null;
            var keeps = state == IntegrationAcceptance.Changed && judged is { Keeps: true };
            var row = new DeclarationOwed(read.Repository, read.Workspace, read.Line, read.Blob, state, read.Since)
            {
                Root = read.Root, Change = judged?.Change, Against = against, SetAside = judged?.SetAside ?? [],
                ChangedUnits = judged?.ChangedUnits ?? [], Narrowed = judged?.Narrowed ?? [],
                Verdicts = keeps ? [] : DeclarationVerdicts.Of(config, read.Repository, read.Workspace, read.Line, read.Blob, judged?.ChangedUnits ?? [], judged?.Digests),
            };
            (keeps ? kept : DeclarationVerdicts.Passes(row.Verdicts) ? passed : owed).Add(row);
        }

        static IReadOnlyList<DeclarationOwed> Ordered(List<DeclarationOwed> rows) =>
            [.. rows.OrderBy(each => each.Since).ThenBy(each => each.Repository, StringComparer.Ordinal)];
        return (Ordered(owed), Ordered(passed), Ordered(kept));
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
    private async Task JudgeAsync(DriverConfig config, IReadOnlyList<Read> held, DateTimeOffset now, CancellationToken ct)
    {
        var blobs = held.Select(read => read.Blob).ToHashSet(StringComparer.Ordinal);
        var next = _judged.Where(each => blobs.Contains(each.Key.Blob)).ToDictionary(each => each.Key, each => each.Value);
        var unjudged = new List<string>();
        var unread = new Dictionary<(string Accepted, string Blob), DateTimeOffset>();
        foreach (var read in held)
        {
            if (Against(config, read) is not { } against || against == read.Blob || next.ContainsKey((against, read.Blob))) continue;
            // A pair git gave no text for is asked again after a while, not at every look.
            if (_unread.TryGetValue((against, read.Blob), out var asked) && now - asked < MissStands)
            {
                unread[(against, read.Blob)] = asked;
                continue;
            }

            GitReads++;
            var was = await IntegrationAcceptance.TextAsync(read.Root, against, ct).ConfigureAwait(false);
            var text = await IntegrationAcceptance.TextAsync(read.Root, read.Blob, ct).ConfigureAwait(false);
            if (text is null || (was is null && against != IntegrationAcceptance.None))
            {
                unread[(against, read.Blob)] = now;
                continue;
            }

            try
            {
                var recorded = config.IntegrationUnits.TryGetValue(read.Repository.Trim(), out var accepted) && accepted.Blob == against ? accepted.Units : null;
                next[(against, read.Blob)] = Judged(recorded, was, text);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                // A reader's fault never ends the look: the pair is held as a change for the person and said once.
                next[(against, read.Blob)] = new DeclarationJudged(null, false, [], error.Message);
                unjudged.Add($"{read.Repository}'s daoris.gates.json on {read.Line} could not be judged ({error.Message.TrimEnd('.')}), so its change waits for you.");
            }
        }

        _judged = next;
        _unread = unread;
        Unjudged = unjudged;
    }

    /// <summary>One pair judged by units, the accepted ones the home's record where it holds the accepted blob's, else those of its text.</summary>
    private static DeclarationJudged Judged(IReadOnlyDictionary<string, string>? recorded, string? accepted, string? now)
    {
        var before = recorded ?? DeclarationUnits.Of(accepted);
        var after = DeclarationUnits.Of(now);
        var changed = before is not null && after is not null ? DeclarationUnits.Differing(before, after) : null;
        var keeps = changed is not null ? changed.Count == 0 : DeclarationCommands.Keeps(accepted, now);
        return new DeclarationJudged(
            DeclarationChange.Between(accepted, now), keeps, keeps ? DeclarationCommands.SetAside(accepted, now) : [], null, changed ?? [], DeclarationUnits.Narrowed(accepted, now), after);
    }

    /// <summary>The acceptances of <paramref name="config"/> by blob that hold no units of that blob yet; converted by reading their text, so no press is owed.</summary>
    public IReadOnlyList<DeclarationUnconverted> Unconverted(DriverConfig config) =>
        [.. _reads.Values
            .Select(read => (Read: read, Blob: IntegrationAcceptance.Of(config, read.Repository)))
            .Where(each => each.Blob is { } blob && blob != IntegrationAcceptance.None
                && !(config.IntegrationUnits.TryGetValue(each.Read.Repository, out var held) && held.Blob == blob))
            .Select(each => new DeclarationUnconverted(each.Read.Repository, each.Read.Root, each.Blob!))
            .OrderBy(each => each.Repository, StringComparer.Ordinal)];

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
        foreach (var gone in _misses.Keys.Where(key => !seen.Contains(key)).ToList()) _misses.Remove(gone);
        await JudgeAsync(config, held, now, ct).ConfigureAwait(false);
        _held = held;
    }

    private async Task<Read?> ReadAsync(string root, string workspace, string repository, string line, DateTimeOffset now, CancellationToken ct)
    {
        _reads.TryGetValue(repository, out var held);
        var tip = TreeTips.CommonDirectory(root) is { } common ? TreeTips.Named(common, $"refs/heads/{line}") : null;
        if (held is not null && tip is not null && held.Root == root && held.Line == line && held.Tip == tip) return held;

        // A line git named no blob for stays so while the ref files that name it stand, and for a few minutes at most.
        var stamp = RefStamp(root, line);
        if (stamp is not null && _misses.TryGetValue(repository, out var missed) && missed.Root == root && missed.Line == line
            && missed.Stamp == stamp && now - missed.At < MissStands)
        {
            return held;
        }

        GitReads++;
        if (await IntegrationAcceptance.TipAsync(root, line, ct).ConfigureAwait(false) is not { } blob)
        {
            // Only where git's own files name no such ref: a git that failed or timed out is no answer, and is asked again at the next look.
            if (stamp is not null && tip is null) _misses[repository] = new Miss(root, line, stamp, now);
            return held;
        }

        _misses.Remove(repository);
        // A blob is its text: one seen before is not read again, whatever commit carries it.
        var declares = held is not null && held.Blob == blob && held.Root == root
            ? held.Declares
            : blob != IntegrationAcceptance.None && await DeclaresAsync(root, blob, ct).ConfigureAwait(false);
        var since = held is not null && held.Blob == blob && held.Root == root ? held.Since : now;
        var read = new Read(root, workspace, repository, line, tip, blob, declares, since);
        _reads[repository] = read;
        return read;
    }

    /// <summary>What names the line in the files git reads (its loose ref and <c>packed-refs</c>) by write time and size; null where the files cannot say (reftable, no git directory).</summary>
    private static string? RefStamp(string root, string line)
    {
        try
        {
            if (TreeTips.CommonDirectory(root) is not { } common || Directory.Exists(Path.Combine(common, "reftable"))) return null;
            static string Of(string path) => new FileInfo(path) is { Exists: true } file ? $"{file.LastWriteTimeUtc.Ticks}:{file.Length}" : "-";
            return Of(Path.Combine(common, "refs", "heads", line.Replace('/', Path.DirectorySeparatorChar))) + "|" + Of(Path.Combine(common, "packed-refs"));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private async Task<bool> DeclaresAsync(string root, string blob, CancellationToken ct)
    {
        GitReads++;
        return IntegrationDeclaration.Read(await IntegrationAcceptance.TextAsync(root, blob, ct).ConfigureAwait(false)).Facts is not null;
    }
}
