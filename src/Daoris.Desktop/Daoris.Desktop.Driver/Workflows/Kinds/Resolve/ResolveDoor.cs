namespace Daoris.Driver;

/// <summary>
/// A run's <c>resolve</c> to perform: its run, node and attempt, the work at its bound tip, the <c>rebase</c> attempt whose problem it is fed and
/// the target that rebase replays onto by name; its bound of asks per stop and each ask's time.
/// </summary>
public sealed record ResolveStart(string Run, string Node, int Attempt, RebaseWork Work, string Rebase, int RebaseAttempt, string Target) : NodeDoorStart
{
    public int Bound { get; init; } = ResolveNode.AsksAtMost;

    public TimeSpan Each { get; init; } = ResolveNode.EachAsk;

    public override string Key => $"{Run}\n{Node}\n{Attempt}";
}

/// <summary>The door a run's <c>resolve</c> is performed through.</summary>
public interface IResolveDoor : INodeDoor
{
    /// <summary>Starts the hand-off off the pass, once per run, node and attempt; false where one runs already or none may begin now.</summary>
    bool Start(ResolveStart start);
}

/// <summary>
/// Performs a run's <c>resolve</c> of a conflict (§2.4 of the process steps' design): the work's tree reserved (<see cref="TreeReserving"/>), the
/// replay prepared there at the stop (<see cref="ResolveTree"/>), its resolver asked with the files and hunks named, counted per stop on
/// <see cref="ResolveRecords"/>, the tree read at the resolver's end and gone on from, each later stop its own ask. Every end stops the resolver
/// first, then puts the tree back on its branch at its tip, keeping what an unresolved ask made on the edits ref, and closes the hand-off.
/// Resolved, the commit is kept on the rebase's attempt as its finish (<see cref="RebaseAttempt.Resolution"/>), which its <c>back</c> way reads.
/// </summary>
/// <param name="reserve">The tree's reservation, the holders read; the loop's reads the service's sessions.</param>
public sealed class ResolveDoor(
    string home, IResolvers resolvers, Func<TreeHandOff, CancellationToken, Task<TreeReserved>> reserve, Action<string>? log = null, Func<bool>? runs = null)
    : IResolveDoor, IDoorRuns
{
    private readonly NodeDoorRuns _runs = new(runs);

    NodeDoorRuns IDoorRuns.Runs => _runs;

    /// <summary>Whether the step lapsed since its start; null reads the run's record (<see cref="LapsedOf"/>).</summary>
    public Func<ResolveStart, bool>? Lapsed { get; init; }

    public TimeProvider Clock { get; init; } = TimeProvider.System;

    /// <summary>The hand-offs' table; the home's own where none is handed.</summary>
    public TreeHandOffs? HandOffs { get; init; }

    /// <inheritdoc/>
    public bool Start(ResolveStart start) => _runs.Start(start, ct => PerformAsync(start, ct), error =>
    {
        if (error is not (DriverException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException)) return false;
        log?.Invoke($"resolve  run {start.Run}: its `{start.Node}` could not be tried: {error.Message.TrimEnd().TrimEnd('.')}. It is tried again at a later pass.");
        return true;
    });

    /// <summary>Completes once every hand-off started has ended; a test's wait, and a closing loop's.</summary>
    public Task IdleAsync() => _runs.IdleAsync();

    /// <summary>
    /// Every hand-off a restart finds open put back as §2.4 point 4 says, before any pass: its resolver stopped and awaited, its edits kept, the
    /// tree on its branch at its tip, the row closed.
    /// </summary>
    public static async Task RestoreOpenAsync(string home, TreeHandOffs offs, IResolvers resolvers, CancellationToken ct)
    {
        foreach (var row in offs.Open())
        {
            await StepAuthority.RunAsync(TreeHandOffs.AuthorityFor(row), () => RestoreAsync(home, offs, resolvers, row, Hand(row), ct)).ConfigureAwait(false);
        }
    }

    /// <summary>One hand-off of the attempt, from its reservation to its restoration; nothing where the attempt is written already.</summary>
    internal async Task PerformAsync(ResolveStart start, CancellationToken ct)
    {
        var records = new ResolveRecords(home);
        if (records.Of(start.Run, start.Node, start.Attempt) is not null) return;
        if (new RebaseRecords(home).Of(start.Run, start.Rebase, start.RebaseAttempt) is not { Stop: { } stop } rebase)
        {
            records.Write(start.Run, new ResolveAttempt(start.Node, start.Attempt, NodeStates.Failed, $"Its rebase `{start.Rebase}` has no problem written."));
            return;
        }

        var tries = records.Tries(start.Run, start.Node, stop);
        if (tries >= start.Bound)
        {
            records.Write(start.Run, new ResolveAttempt(start.Node, start.Attempt, NodeStates.Failed, ResolveWords.Spent(stop, start.Bound)));
            return;
        }

        var work = start.Work;
        if (TreeOf(work) is not { } tree)
        {
            records.Write(start.Run, new ResolveAttempt(start.Node, start.Attempt, NodeStates.Failed, "Its session has no tree here to settle it in."));
            return;
        }

        var now = Clock.GetUtcNow();
        var asked = new TreeHandOff(tree, work.Repository, work.Branch, work.Tip, start.Run, start.Node, start.Attempt, ResolveRecords.KeyOf(stop),
            ResolveNode.Resolvers[0], TreeHandOff.AskKey(start.Run, start.Node, start.Attempt, tries + 1), TreeHandOffPhases.Reserved, now, now + start.Each);
        var reserved = await reserve(asked, ct).ConfigureAwait(false);
        if (reserved.Row is not { } row)
        {
            log?.Invoke($"resolve  run {start.Run}: its `{start.Node}` waits: {reserved.Waits ?? $"`{reserved.Holder?.Session}` holds its tree"}.");
            return;
        }

        var hand = new ResolveHand(work.Root, tree, work.Repository, work.Branch, work.Tip, rebase.Onto, rebase.LeftOut, StepRefs.EditsOf(start.Run, start.Node));
        await StepAuthority.RunAsync(TreeHandOffs.AuthorityFor(row), () => HandOffAsync(start, row, hand, ct)).ConfigureAwait(false);
    }

    /// <summary>The hand-off in the reserved tree, ending in its restoration whatever happens in it.</summary>
    private async Task HandOffAsync(ResolveStart start, TreeHandOff row, ResolveHand hand, CancellationToken ct)
    {
        var records = new ResolveRecords(home);
        var offs = HandOffs ?? new TreeHandOffs(home, clock: Clock);
        var trees = new ResolveTree(new SessionTrees(home).Settles);
        ResolveAttempt? made = null;
        var resolved = false;
        string? session = null;
        try
        {
            var step = await trees.PrepareAsync(hand, ct).ConfigureAwait(false);
            offs.Advance(row, TreeHandOffPhases.Prepared);
            while (step is { Kind: ResolveSteps.Stopped, At: { } at })
            {
                var ask = records.Tries(start.Run, start.Node, at.Stop) + 1;
                if (ask > start.Bound)
                {
                    made = new ResolveAttempt(start.Node, start.Attempt, NodeStates.Failed, ResolveWords.Spent(at.Stop, start.Bound));
                    break;
                }

                await ResolveTree.ApplyEditsAsync(hand, at, ct).ConfigureAwait(false);
                if (session is null)
                {
                    var (chosen, why) = await resolvers.SessionAsync(row, ct).ConfigureAwait(false);
                    if (chosen is null)
                    {
                        made = new ResolveAttempt(start.Node, start.Attempt, NodeStates.Failed, why ?? "No session may be asked to settle it.");
                        break;
                    }

                    session = chosen;
                }

                var until = Clock.GetUtcNow() + start.Each;
                var asking = row with { Until = until, Session = session };
                records.Ask(start.Run, start.Node, at.Stop, ResolveWords.Waits(at, ask, start.Bound, asking.UntilSaid));
                offs.Advance(row, TreeHandOffPhases.Asked, session);
                var ended = await resolvers.AskAsync(asking, session, ResolveWords.Ask(start.Target, at, ask, start.Bound), () => LapsedOf(start), ct).ConfigureAwait(false);
                if (ended == ResolverEnds.Declined)
                {
                    made = new ResolveAttempt(start.Node, start.Attempt, NodeStates.Failed, $"`{session}` may not be asked: the person stopped or declined it, or it stood down.");
                    break;
                }

                if (ended != ResolverEnds.Ended) break;
                step = await trees.ReadAsync(hand, at, ct).ConfigureAwait(false);
                if (step.Kind is ResolveSteps.Stopped or ResolveSteps.Done) await ResolveTree.DropEditsAsync(hand, ct).ConfigureAwait(false);
            }

            made ??= step switch
            {
                { Kind: ResolveSteps.Done, Commit: { } commit } => await FinishedAsync(start, hand, commit, ct).ConfigureAwait(false),
                { Kind: ResolveSteps.Refused or ResolveSteps.Failed } => new ResolveAttempt(start.Node, start.Attempt, NodeStates.Failed, step.Said),
                _ => null,
            };
            resolved = made is { Outcome: ResolveNode.Resolved };
        }
        finally
        {
            offs.Advance(row, TreeHandOffPhases.Restoring);
            // Nothing is put back while the resolver may still write in the tree.
            if (session is not null) await resolvers.StopAsync(row with { Session = session }, CancellationToken.None).ConfigureAwait(false);
            var put = await ResolveTree.RestoreAsync(hand, keepEdits: !resolved, CancellationToken.None).ConfigureAwait(false);
            if (put is not null) log?.Invoke($"resolve  run {start.Run}: its `{start.Node}` could not put its tree back: {put}.");
            offs.Close(row);
            records.Ended(start.Run, start.Node);
            if (made is not null) records.Write(start.Run, made);
        }
    }

    /// <summary>The replay's commit kept on the rebase's attempt as its finish, with its <c>onto</c>, and the attempt <c>resolved</c>.</summary>
    private async Task<ResolveAttempt> FinishedAsync(ResolveStart start, ResolveHand hand, string commit, CancellationToken ct)
    {
        var rebases = new RebaseRecords(home);
        var kept = await StepRefs.KeepAsync(hand.Root, start.Run, start.Rebase, start.RebaseAttempt, commit, ct).ConfigureAwait(false);
        if (!kept.Done) return new ResolveAttempt(start.Node, start.Attempt, NodeStates.Failed, $"Its commit could not be kept on the rebase's step ref: {kept.Said}");
        if (rebases.Of(start.Run, start.Rebase, start.RebaseAttempt) is { } rebase) rebases.Write(rebase with { Resolution = commit });
        return new ResolveAttempt(start.Node, start.Attempt, ResolveNode.Resolved, $"Settled; the replay onto `{start.Target}` goes on from `{TextBounds.ShortCommit(commit, 7)}`.")
        {
            Commit = commit,
        };
    }

    /// <summary>A restart's or a lapse's restoration of one row: the resolver stopped first, then the tree put back and the row closed.</summary>
    private static async Task RestoreAsync(string home, TreeHandOffs offs, IResolvers resolvers, TreeHandOff row, ResolveHand hand, CancellationToken ct)
    {
        offs.Advance(row, TreeHandOffPhases.Restoring);
        if (row.Session is not null) await resolvers.StopAsync(row, ct).ConfigureAwait(false);
        await ResolveTree.RestoreAsync(hand, keepEdits: true, ct).ConfigureAwait(false);
        offs.Close(row);
        new ResolveRecords(home).Ended(row.Run, row.Node);
    }

    private static ResolveHand Hand(TreeHandOff row) =>
        new(row.Tree, row.Tree, row.Repository, row.Branch, row.Tip, "", [], StepRefs.EditsOf(row.Run, row.Node));

    /// <summary>Whether the step lapsed: its node no longer active on this attempt in its run's record.</summary>
    private bool LapsedOf(ResolveStart start) =>
        Lapsed?.Invoke(start)
        ?? new NodeRecordFiles(home).Read(RunUnits.Prefix + start.Run)?.Statuses.GetValueOrDefault(start.Node) is not { State: NodeStates.Active } status
        || status.Attempt != start.Attempt;

    /// <summary>The work's session tree where one stands here; null where none does.</summary>
    private string? TreeOf(RebaseWork work) =>
        DaorisHome.Places(home, HomeEntry.Trees, work.Workspace, work.Repository, Path.GetFileName(work.Branch)).FirstOrDefault(Directory.Exists);
}
