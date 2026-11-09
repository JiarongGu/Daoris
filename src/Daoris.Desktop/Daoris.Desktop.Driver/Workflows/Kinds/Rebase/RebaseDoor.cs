namespace Daoris.Driver;

/// <summary>A run's <c>rebase</c> to perform: its run, node and attempt, the work at its bound tip and the target at the tip the step read.</summary>
public sealed record RebaseStart(string Run, string Node, int Attempt, RebaseWork Work, RebaseTarget Target) : NodeDoorStart
{
    /// <summary>Once per run, node and attempt.</summary>
    public override string Key => $"{Run}\n{Node}\n{Attempt}";
}

/// <summary>The door a run's <c>rebase</c> is performed through.</summary>
public interface IRebaseDoor : INodeDoor
{
    /// <summary>Starts the replay off the pass, once per run, node and attempt; false where one runs already or none may begin now.</summary>
    bool Start(RebaseStart start);

    /// <summary>Why the work may not be replayed now, said (<see cref="RebaseWaits"/>); null where it is still.</summary>
    Task<string?> WaitsAsync(RebaseWork work, CancellationToken ct);
}

/// <summary>
/// Performs a run's <c>rebase</c>: its work replayed onto its target in a tree of Daoris's own (<see cref="RebaseReplay"/>), leaving out what the
/// target holds by ancestry or patch, by the squash the target's landing record names, and by an operation that brought the work in as its member
/// (the inclusion record, <see cref="TogetherBinding.BroughtIn"/>). The commit made is kept on the attempt's step ref (<see cref="StepRefs"/>),
/// and what the attempt came to is written on <see cref="RebaseRecords"/>, which the step's reading reaches it from.
/// </summary>
/// <param name="home">The Daoris home whose trees, landing records and operations the replay reads, and whose record it writes.</param>
/// <param name="log">Told what an attempt that could not be tried met; the machine log's line.</param>
/// <param name="runs">Whether a replay may begin now: not while an update drains; null always may.</param>
public sealed class RebaseDoor(string home, Action<string>? log = null, Func<bool>? runs = null) : IRebaseDoor, IDoorRuns
{
    private readonly NodeDoorRuns _runs = new(runs);

    NodeDoorRuns IDoorRuns.Runs => _runs;

    /// <summary>The trees a session runs a turn or waits in; null reads none, so no turn holds a replay.</summary>
    public Func<CancellationToken, Task<IReadOnlySet<string>>>? Turning { get; init; }

    /// <inheritdoc/>
    public async Task<string?> WaitsAsync(RebaseWork work, CancellationToken ct)
    {
        var tree = TreeOf(work);
        var turning = tree is not null && Turning is not null
            && (await Turning(ct).ConfigureAwait(false)).Any(each => TogetherOperation.SameTree(each, tree));
        return RebaseWaits.Of(turning, TogetherBinding.Of(home, work.Repository, work.Branch, tree ?? "", work.Tip));
    }

    /// <inheritdoc/>
    public bool Start(RebaseStart start) => _runs.Start(start, ct => PerformAsync(start, ct), error =>
    {
        if (error is not (DriverException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException)) return false;
        log?.Invoke($"rebase  run {start.Run}: its `{start.Node}` could not be tried: {error.Message.TrimEnd().TrimEnd('.')}. It is tried again at a later pass.");
        return true;
    });

    /// <summary>Completes once every replay started has ended; a test's wait, and a closing loop's.</summary>
    public Task IdleAsync() => _runs.IdleAsync();

    /// <summary>The attempt performed once: replayed, its commit kept on its step ref, and what it came to written; nothing where it is written already.</summary>
    internal async Task<RebaseAttempt> PerformAsync(RebaseStart start, CancellationToken ct)
    {
        var records = new RebaseRecords(home);
        if (records.Of(start.Run, start.Node, start.Attempt) is { } done) return done;
        var work = start.Work;
        var tree = TreeOf(work);
        var made = await new RebaseReplay(new SessionTrees(home).Settles).ReplayAsync(work, tree, start.Target, Held(work, tree, start.Target), ct).ConfigureAwait(false);
        var commit = made.Commit;
        if (made.Outcome == RebaseOutcomes.Ready && commit is not null
            && await StepRefs.KeepAsync(work.Root, start.Run, start.Node, start.Attempt, commit, ct).ConfigureAwait(false) is { Done: false } unkept
            && !await KeptAsync(work.Root, start, commit, ct).ConfigureAwait(false))
        {
            made = new RebaseMade(NodeStates.Failed, $"The replay's commit could not be kept on its step ref: {unkept.Said}");
        }

        var attempt = new RebaseAttempt(start.Run, start.Node, start.Attempt, work.Tip, start.Target.Tip, made.Outcome, made.Said)
        {
            Commit = made.Outcome == RebaseOutcomes.Ready ? commit : null,
            StopSort = made.Stop?.Sort, PatchId = made.Stop?.PatchId, Paths = made.Stop?.Paths ?? [], StopCommit = made.StopCommit, LeftOut = made.LeftOut,
        };
        records.Write(attempt);
        return attempt;
    }

    /// <summary>Whether the attempt's step ref already holds <paramref name="commit"/>: a replay written again finds its ref made.</summary>
    private static async Task<bool> KeptAsync(string root, RebaseStart start, string commit, CancellationToken ct)
    {
        var (code, at, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", StepRefs.Of(start.Run, start.Node, start.Attempt)], ct).ConfigureAwait(false);
        return code == 0 && string.Equals(at.Trim(), commit, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The work's session tree where one stands here; null where none does.</summary>
    private string? TreeOf(RebaseWork work) =>
        DaorisHome.Places(home, HomeEntry.Trees, work.Workspace, work.Repository, Path.GetFileName(work.Branch)).FirstOrDefault(Directory.Exists);

    /// <summary>The tips the target holds the work up to by more than ancestry: its landing record's squash, an operation's inclusion.</summary>
    private IReadOnlyList<string> Held(RebaseWork work, string? tree, RebaseTarget target)
    {
        var held = new List<string>();
        if (new LandedBranches(home).Of(work.Repository, target.Name)?.Squashed is { } squashed) held.Add(squashed);
        if (TogetherBinding.BroughtIn(new TogetherOperations(home).All(), work.Repository, work.Branch, tree ?? "") is { } bound) held.Add(bound);
        return held;
    }
}
