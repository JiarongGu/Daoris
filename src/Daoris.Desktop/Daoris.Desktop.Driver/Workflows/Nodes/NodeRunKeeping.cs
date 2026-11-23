namespace Daoris.Driver;

/// <summary>
/// What the runtime keeps on a run bound to a graph version around its rounds: the person's skips and stops applied, what a review sent back
/// told to the run's work, the accepts a carried-on session earns again, the landing started on its door, and the run's end and its resumption.
/// </summary>
/// <param name="say">The say door a run's work is told what its steps sent back through: the session, the words and the key.</param>
/// <param name="sessions">This machine's session records, among which a run's end finds the sessions waiting on it.</param>
internal sealed class NodeRunKeeping(
    NodeRecordFiles records, Func<DateTimeOffset> clock, Func<bool> holdsLease, GraphRuns graphRuns, WorkPerformer performer,
    Func<NodeDoors> doors, Func<Func<string, string, string, CancellationToken, Task<SayAnswer>>?> say, Func<IPullRequestRecords?> pullRequests,
    Func<ILandingWords?> landings, Func<Func<RunFinish, CancellationToken, Task<Daoris.Contracts.Quests.RunEndResponse?>>?>? finish = null,
    Func<Func<CancellationToken, Task<IReadOnlyList<SessionRecord>?>>?>? sessions = null)
{
    private readonly HashSet<string> _handed = new(StringComparer.Ordinal);

    private RunEndPorts Ends => new(records.Home)
    {
        Finish = finish?.Invoke(), Sessions = sessions?.Invoke(), Log = doors().Say, Owed = new OwedRunEnds(records.Home),
    };

    /// <summary>The run ends the service could not take, asked again (<see cref="RunSessionEnds.AgainAsync"/>).</summary>
    public async Task OwedEndsAsync(List<string> said, CancellationToken ct)
    {
        if (holdsLease()) await RunSessionEnds.AgainAsync(Ends, said, ct).ConfigureAwait(false);
    }

    /// <summary>The runs read, each at its end marked where a session of this machine waits on it (<see cref="RunWaiters"/>); unread, said once.</summary>
    public async Task<List<GraphRun>> WaitedAsync(List<GraphRun> runs, CancellationToken ct)
    {
        var ports = Ends;
        if (ports.Sessions is null || !runs.Any(run => run.Ended is not null && run.Line is null)) return runs;
        if (await ports.Sessions(ct).ConfigureAwait(false) is not { } read)
        {
            RunSessionEnds.Quiet(ports, "", EmptyRunEnds.SessionsUnread);
            return runs;
        }

        EmptyRunEnds.Changed(ports.Home, "", "read");
        var waited = RunWaiters.Runs(read);
        return [.. runs.Select(run => run.Ended is not null && waited.Contains(run.Run) ? run with { Waited = true } : run)];
    }

    /// <summary>A run a session waits on that the pass did not govern, and why: said once per reason (<see cref="EmptyRunEnds"/>).</summary>
    public void Skipped(string run, string reason) => RunSessionEnds.Quiet(Ends, run, reason);

    /// <summary>What a run's rounds leave to keep: its reviews, repairs, accepts through, doors and pull requests; none for one at its end.</summary>
    public async Task AfterRoundsAsync(GovernedUnit governed, RunScope scope, DriverConfig? config, List<string> said, CancellationToken ct)
    {
        KeepReviews(governed, scope);
        await AcceptRepairsAsync(governed, scope, ct).ConfigureAwait(false);
        if (holdsLease() && ThroughAccepts.Keep(records, governed, scope, config, clock) is { } through) said.Add(through);
        StartDoors(governed, scope);
        KeepPullRequests(governed);
    }

    /// <summary>A run's steps its agent holds: its acts journaled and settled (<see cref="StepActs"/>), a joint check's failure sent back (<see cref="JointReturns"/>), then each handed on.</summary>
    public async Task HandStepsAsync(GovernedUnit governed, RunScope scope, DriverConfig? config, List<string> said, CancellationToken ct)
    {
        if (!holdsLease()) return;
        var casts = new PlaceCastFiles(records.Home).Book();
        await StepActs.JournalAsync(records, governed, scope, sessions?.Invoke(), casts, config?.PlaceRisk, clock, said, ct).ConfigureAwait(false);
        await JointReturns.KeepAsync(records, governed, scope, sessions?.Invoke(), say(), clock, said, ct).ConfigureAwait(false);
        await StepHandOffs.HandAsync(records, governed, scope, sessions?.Invoke(), casts, config?.PlaceRisk, say(), said, ct, _handed).ConfigureAwait(false);
    }

    /// <summary>
    /// A run's active nodes after its rounds, each handed to its kind to start on its door off the pass (<see cref="IStarts.Keep"/>): the
    /// <c>land</c>, and the steps past it Daoris performs. Each door is idempotent by node and tip, so a node kept again starts nothing twice.
    /// </summary>
    private void StartDoors(GovernedUnit governed, RunScope scope)
    {
        if (!holdsLease() || records.Read(governed.Unit.Id) is not { } record) return;
        var wired = doors();
        foreach (var node in governed.Declared.Values)
        {
            if (NodeKit.Of(node.Kind) is not IStarts starts || WorkPerformer.StatusOf(record, node.Id) is not { State: NodeStates.Active } status) continue;
            starts.Keep(new NodeKept(governed, scope, node, status, record, wired) { Home = records.Home, At = clock() });
        }
    }

    /// <summary>A run's reviews begun on an attempt with no record yet, each given its review record (<see cref="ReviewRecords.KeepActive"/>).</summary>
    private void KeepReviews(GovernedUnit governed, RunScope scope)
    {
        if (holdsLease() && records.Read(governed.Unit.Id) is { } record) ReviewRecords.KeepActive(records.Home, governed, scope, record, clock());
    }

    /// <summary>A run's pull-request steps that reached, kept on the landing record by the step's own unit (<see cref="PullRequestStep.Keep"/>).</summary>
    public void KeepPullRequests(GovernedUnit governed)
    {
        if (pullRequests() is { } kept && holdsLease() && records.Read(governed.Unit.Id) is { } record) PullRequestStep.Keep(governed, record, kept);
    }

    /// <summary>
    /// A review whose Accept waits on a carried-on session (<see cref="RunRepairs"/>), begun again at a tip that only continues the one the person
    /// accepted: the Accept is kept on it once more, for the next pass to apply, and the repair is spent.
    /// </summary>
    private async Task AcceptRepairsAsync(GovernedUnit governed, RunScope scope, CancellationToken ct)
    {
        if (!holdsLease() || records.Read(governed.Unit.Id) is not { Repairs.Count: > 0 } record) return;
        foreach (var repair in record.Repairs)
        {
            var status = WorkPerformer.StatusOf(record, repair.Node);
            if (status.State != NodeStates.Active || status.Binding is not { SubjectRevision: { } tip }) continue;
            if (!CarriedRepairs.Stands(records.Home, scope, repair, tip))
            {
                RunRepairs.Take(records, governed.Unit.Id, repair.Node);
                continue;
            }

            if (!await RunRepairs.ContinuesAsync(scope.Root, repair, tip, ct).ConfigureAwait(false)) continue;
            if (RunNodeWords.Word(records, governed.Run, repair.Node, NodeWordActs.Accept, CarriedRepairs.Receipt(records.Home, governed.Run, repair, tip), clock, settle: SettleBy.Observation).Done)
            {
                RunRepairs.Take(records, governed.Unit.Id, repair.Node);
            }
        }
    }

    /// <summary>Whether a run's record still ends at its newest quest: only its end reaches it again; an ask's run ends its own way.</summary>
    public bool AtEnd(GovernedUnit governed, RunScope scope) =>
        !AskRunKey.Is(scope.Run.Binding) && scope.Quest?.Records.Quest is { } newest && records.Read(governed.Unit.Id) is { } record && HoldsEnd(record, newest);

    private static bool HoldsEnd(NodeRecord record, QuestView newest) => record.Ended?.Quest == newest.Id;

    /// <summary>
    /// Every standing step of a run another closed Done stopped before its rounds, and whoever runs each attempt told to stop
    /// (<see cref="QuestDoneRuns"/>); true where the done applies, so the pass runs no round of it, its reached steps never bound again.
    /// </summary>
    public async Task<bool> StopDoneAsync(GovernedUnit governed, RunScope scope, List<WorkPerformed> done, List<string> said, CancellationToken ct)
    {
        var unit = governed.Unit.Id;
        if (records.Read(unit) is not { } record || !QuestDoneRuns.Applies(governed, scope, record)) return false;
        if (!holdsLease()) return true;
        var promoted = doors().Door<IIntegrationDoor>() is { } door && governed.Graph.Nodes.Any(node => node.Kind == NodeKinds.Integrate)
            ? await IntegrateNodes.PromotedAsync(door, record, ct).ConfigureAwait(false)
            : null;
        var now = clock();
        var (refused, stopped) = records.Edit<IReadOnlyList<string>>(unit, current =>
        {
            if (!QuestDoneRuns.Applies(governed, scope, current)) return (null, []);
            var (next, names) = QuestDoneRuns.Stopped(governed, current, now, promoted);
            return (names.Count == 0 ? null : next, names);
        });
        if (refused is not null || stopped is not { Count: > 0 }) return true;
        performer.StandDown(unit, stopped);
        done.AddRange(stopped.Select(node => new WorkPerformed(unit, node, PerformedWhat.Applied, NodeStates.Stopped)));
        said.Add($"work  {unit}: #{scope.Quest?.Records.Quest?.Id} is Done though its run had not ended, so Daoris stopped its steps: {string.Join(", ", stopped)}.");
        return true;
    }

    /// <summary>
    /// A run's end, judged once its pass's rounds are kept: its newest quest here names no step still to come here, and no node of its graph
    /// can move again, with no attempt unsettled; or its quest another closed Done, its steps stopped (<see cref="QuestDoneRuns"/>); or its quest
    /// closed and none of its standing steps can move again (<see cref="ClosedRunEnds"/>), its steps kept as they stand. Kept on its record with
    /// how it came out (<see cref="RunOutcomes"/>), said once; the run then arms nothing until its chain comes back here.
    /// </summary>
    /// <param name="lacks">The commits of a tip the line lacks (<see cref="NodeRuntimeLook.Lacks"/>), asked of a closed quest's run alone.</param>
    /// <param name="tip">The run's branch's tip the pass read; null where it read none.</param>
    public async Task EndAsync(
        GovernedUnit governed, RunScope scope, List<string> said, Func<string, string, string, string, CancellationToken, Task<IReadOnlyList<LandingCommit>?>> lacks,
        string? tip, CancellationToken ct)
    {
        Task<IReadOnlyList<LandingCommit>?> Lacks(string commit, CancellationToken token) =>
            lacks(scope.Root, scope.Run.Binding.Repository, scope.Run.Binding.Workspace, commit, token);
        async Task<bool?> OnLine(string commit, CancellationToken token) => await Lacks(commit, token).ConfigureAwait(false) is { } lacked ? lacked.Count == 0 : null;

        var unit = governed.Unit.Id;
        if (AskRunKey.Is(scope.Run.Binding))
        {
            AskRunEnds.End(records, clock, graphRuns, governed, scope, said);
            return;
        }

        if (scope.Quest?.Records.Quest is not { } newest || records.Read(unit) is not { } record) return;
        if (HoldsEnd(record, newest))
        {
            if (holdsLease())
            {
                await RunReEnds.AgainAsync(Ends, scope.Run.Run, record.Ended, scope.Quests, RunEndQuests.Passed(governed.Graph, record), said, ct).ConfigureAwait(false);
            }

            return;
        }

        var repository = scope.Run.Binding.Repository;
        if (newest.Then.Any(step => step.SetUpIn is null && string.Equals(step.To, repository, StringComparison.OrdinalIgnoreCase))) return;
        if (QuestDoneRuns.Applies(governed, scope, record))
        {
            if (QuestDoneRuns.Ends(governed, record)) await EndDoneAsync(governed, scope, newest, record, said, Lacks, tip, ct).ConfigureAwait(false);
            return;
        }

        string? why = null;
        IReadOnlySet<string> held = new HashSet<string>();
        var stands = ClosedRunEnds.BranchStands(scope);
        bool InPlace(NodeRecord at) =>
            InPlaceRunEnds.Ends(governed.Graph, at, scope.Branch is not null || scope.Branches.Count > 0, scope.Attributed, Waits(governed.Graph, at));
        if (!Over(governed, record))
        {
            if (ClosedRunEnds.Closed(newest.Status) && !Waits(governed.Graph, record))
            {
                held = await ClosedRunEnds.OnLineAsync(ClosedRunEnds.Bound(governed.Graph, record), OnLine, ct).ConfigureAwait(false);
                why = ClosedRunEnds.Why(governed.Graph, record, false, stands, held);
            }

            // Work done in place leaves its quest open, which only the run's own end closes, so it is judged without waiting on the quest.
            if (why is null && InPlace(record)) why = InPlaceRunEnds.Why;
            if (why is null) return;
        }

        var at = clock();
        RunEnd? ended = null;
        var (refused, kept) = records.Edit<bool>(unit, current =>
            (why is null ? Over(governed, current) : why == InPlaceRunEnds.Why ? InPlace(current) : ClosedRunEnds.Why(governed.Graph, current, Waits(governed.Graph, current), stands, held) == why)
                ? (current with { Ended = ended = why == ClosedRunEnds.QuestClosed ? RunOutcomes.OfClosedQuest(governed.Graph, current, newest.Id, at)
                    : RunOutcomes.Of(governed.Graph, current, newest.Id, at) with { Why = why } }, true) : (null, false));
        if (refused is not null || kept != true || ended is null) return;
        graphRuns.Ended(scope.Run.Run, ended);
        await RunSessionEnds.FinishAsync(Ends, scope.Run.Run, ended, scope.Quests, RunEndQuests.Passed(governed.Graph, record), said, ct)
            .ConfigureAwait(false);
        var outcome = $"{ended.Outcome}{(ended.Node is { } step ? $" at `{step}`" : "")}";
        said.Add(why is null
            ? $"work  {unit}: its run ended at #{newest.Id}, {outcome}: no step of its chain is still to come here, and nothing of its graph moves "
              + "again; it is judged again once its chain comes back here."
            : why == InPlaceRunEnds.Why
                ? $"work  {unit}: its run ended at #{newest.Id}, {outcome}: its work was done in place, on the line, so no branch of it stands for "
                  + "its standing steps to read or land, and they stay as they are."
            : $"work  {unit}: its run ended at #{newest.Id}, {outcome}, its quest closed: "
              + (why switch
              {
                  ClosedRunEnds.OnLine => "the line holds the work its standing steps wait on",
                  ClosedRunEnds.QuestClosed => "its standing steps wait on a quest that is closed, so Daoris stopped the run and its sessions",
                  _ => "no branch of it stands for its standing steps to read",
              })
              + ", so they stay as they are; it is judged again once its chain comes back here.");
    }

    /// <summary>The end of a run another closed Done, once no step stands: its sessions ended with it, its line naming what its branch leaves the line.</summary>
    private async Task EndDoneAsync(
        GovernedUnit governed, RunScope scope, QuestView newest, NodeRecord record, List<string> said,
        Func<string, CancellationToken, Task<IReadOnlyList<LandingCommit>?>> lacks, string? tip, CancellationToken ct)
    {
        var unit = governed.Unit.Id;
        var branch = scope.Branch?.Records.Branch?.Branch;
        var left = branch is not null && tip is not null ? await lacks(tip, ct).ConfigureAwait(false) : null;
        var at = clock();
        RunEnd? ended = null;
        var (refused, kept) = records.Edit<bool>(unit, current => QuestDoneRuns.Applies(governed, scope, current) && QuestDoneRuns.Ends(governed, current)
            ? (current with { Ended = ended = QuestDoneRuns.End(current, newest.Id, at) }, true)
            : (null, false));
        if (refused is not null || kept != true || ended is null) return;
        graphRuns.Ended(scope.Run.Run, ended);
        await RunSessionEnds.FinishAsync(Ends, scope.Run.Run, ended, scope.Quests, RunEndQuests.Passed(governed.Graph, record), said, ct)
            .ConfigureAwait(false);
        said.Add($"work  {unit}: its run ended at #{newest.Id}, {ended.Outcome}{(ended.Node is { } step ? $" at `{step}`" : "")}: its quest was closed Done "
                 + $"outside its run, so Daoris stopped the run and the sessions waiting on it; {QuestDoneRuns.Left(branch, left)}. Its tree and branch "
                 + "are kept, and nothing of it lands by this run.");
    }

    /// <summary>The run named on the record of each branch its quests produced, so the tree has an owner that ends it (<see cref="TreeReleases"/>).</summary>
    public void OwnTrees(RunScope scope)
    {
        if (!holdsLease()) return;
        var grown = new SessionBranches(records.Home);
        foreach (var branch in scope.Branches)
        {
            if (branch.Records.Branch is { } entry) grown.Own(entry.Repository, entry.Branch, scope.Run.Run);
        }
    }

    /// <summary>A run's step refs let go of at its settled end (<see cref="StepRefs.Releases"/>); one git refuses is let go of at the next pass.</summary>
    public async Task ReleaseStepsAsync(GovernedUnit governed, RunScope scope, CancellationToken ct)
    {
        if (!holdsLease() || records.Read(governed.Unit.Id) is not { } record || !StepRefs.Releases(record) || scope.Root.Length == 0) return;
        if ((await StepRefs.ReleaseAsync(scope.Root, scope.Run.Run, ct).ConfigureAwait(false)).Done) NodeStepReads.Release(records.Home, scope.Run.Run);
    }

    /// <summary>Whether nothing on the record is still to come: no attempt or word pending, and no node of its graph able to move, its end or not.</summary>
    internal static bool Over(GovernedUnit governed, NodeRecord record) =>
        !Waits(governed.Graph, record)
        && !governed.Graph.Nodes.Any(node => WorkPerformer.StatusOf(record, node.Id).State
            is NodeStates.Ready or NodeStates.Active or NodeStates.Unknown or NodeStates.Invalidated);

    /// <summary>Whether an attempt, a word, a kept failure, a declined request or a review's changes still waits on the record.</summary>
    internal static bool Waits(NodeGraph graph, NodeRecord record)
    {
        if (record.Attempts.Any(attempt => attempt.State != AttemptStates.Settled || record.RetryPending(attempt))) return true;
        // A failure kept for the person waits on their word, which only an armed run applies, so the run is not over while one stands.
        if (record.Attempts.Any(attempt => attempt.Resolution is null && WorkPerformer.KeptFailure(record, attempt))) return true;
        if (record.Pending.Count > 0 || record.Attempts.Any(attempt => attempt is { State: AttemptStates.Settled, Resolution: HeldResolutions.Again } && attempt.Plugin != QuestClosing.Sender)) return true;
        // A declined request waits on the person's word, so the run is not over while one stands.
        var held = graph.Nodes.Any(node => node.Kind == NodeKinds.Request && NodeOptions.Text(node, "declined") != RequestOptions.Stop
            && WorkPerformer.StatusOf(record, node.Id) is { State: NodeStates.Failed, Code: RequestOptions.DeclinedCode });
        // A review's changes wait on the work's next commit, which runs the review again, so the run is not over while they stand.
        var changes = graph.Nodes.Any(node => node.Kind == NodeKinds.Review && WorkPerformer.StatusOf(record, node.Id).State == NodeStates.Rejected);
        return held || changes;
    }

    /// <summary>
    /// A run governed again after its end: its chain came back here, so the end its record holds lapses and the run goes on; the new quest
    /// moves <c>claimed</c>'s subject, and what rests on it is judged again.
    /// </summary>
    public void Reopen(GovernedUnit governed, RunScope scope)
    {
        if (records.Read(governed.Unit.Id) is not { Ended: { } ended } || scope.Quest?.Records.Quest?.Id == ended.Quest) return;
        var (refused, _) = records.Edit(governed.Unit.Id, current => (current.Ended is null ? null : current with { Ended = null }, true));
        if (refused is null) graphRuns.Ended(scope.Run.Run, null);
    }

    /// <summary>
    /// The person's skips and stops of the governed runs' nodes, kept on each run's record by their door, applied through the one table under
    /// the run's declarations and the scope its starts take, and taken off whether applied or not. Each is said, and names its run, so the run
    /// is judged this pass. A record that does not read keeps them: the evaluation says it.
    /// </summary>
    public async Task<(List<WorkPerformed> Done, List<string> Said)> WordedAsync(
        IEnumerable<GovernedUnit> governed, CancellationToken ct, Func<GovernedUnit, RunScope, Task<string?>>? tipOf = null)
    {
        var done = new List<WorkPerformed>();
        var said = new List<string>();
        foreach (var each in governed)
        {
            var unit = each.Unit.Id;
            if (each.Scope is null || records.Read(unit) is not { Pending.Count: > 0 } pending) continue;
            var scope = new NodeScope(each.GraphDigest, each.Run);
            var now = clock();
            // A stop of an `integrate` whose member promoted is refused, read from the integration record before it is applied.
            var promoted = doors().Door<IIntegrationDoor>() is { } door &&pending.Pending.Any(word => word.Act == NodeWordActs.Stop && each.Declared.GetValueOrDefault(word.Node)?.Kind == NodeKinds.Integrate)
                ? await IntegrateNodes.PromotedAsync(door, pending, ct).ConfigureAwait(false)
                : null;
            var landed = landings();
            var present = await ReviewAccepts.PresentAsync(records.Home, each, pending,
                run => tipOf is null ? Task.FromResult<string?>(null) : tipOf(each, run)).ConfigureAwait(false);
            var (refused, applied) = new StepTenureFiles(records.Home, records.HoldsLease).Settling(unit, tenures =>
                records.Edit(unit, record => RunNodeWords.Apply(record, each.Declared, scope, now, promoted, landed, present, tenures)));
            if (refused is not null) continue;
            SupervisedWords.Settle(landed, applied!);
            foreach (var (word, code) in applied!)
            {
                done.Add(new WorkPerformed(unit, word.Node, code is null ? PerformedWhat.Resolved : PerformedWhat.Refused, code ?? word.Act));
                said.Add(code == ReviewMoved.Word ? ReviewAccepts.DroppedSaid(unit, word, present) : RunNodeWords.AppliedSaid(unit, word, code));

                // The person's changes on a review with no way back on its rejection go to the work all the same, as the look's not-yet does;
                // one with a way back is told as that wire is taken, and only where it goes back to the work.
                if (code is null && word.Act == NodeWordActs.Changes && each.Scope is { } run && word.Words is { } words
                    && !each.Graph.Wires.Any(wire => wire.Kind == WireKinds.Back && wire.From?.Node == word.Node && wire.On == WireKinds.Rejected))
                {
                    var attempt = records.Read(unit)?.Statuses.GetValueOrDefault(word.Node)?.Attempt ?? 0;
                    await TellWorkAsync(each, run, word.Node, attempt, ChangesWords(word.Node, words), said, ct).ConfigureAwait(false);
                }
            }

            // The pass that applies the person's skip or stop tells what runs the step's attempt: a check's executor, a step's agents, a plugin.
            performer.StandDown(unit, [.. applied.Where(each => each.Refused is null && each.Word.Act is NodeWordActs.Stop or NodeWordActs.Skip).Select(each => each.Word.Node)]);
        }

        return (done, said);
    }

    /// <summary>
    /// The <c>back</c> wires an evaluation of a run took into its work: each one kept on the record, whose target is <c>claimed</c>, tells the
    /// work's session what its source sent back, as its next turn: the person's words with their rejection, a failure's code, or the outcome
    /// reached. A way back to any other step tells nobody: that step runs again on its own.
    /// </summary>
    public async Task BackToWorkAsync(
        GovernedUnit governed, IReadOnlyDictionary<string, NodeStatus> before, WireEvaluated evaluated, List<string> said, CancellationToken ct)
    {
        if (governed.Scope is not { } scope || evaluated.Backs.Count == 0) return;
        var kept = records.Read(governed.Unit.Id)?.Fired.ToHashSet(StringComparer.Ordinal) ?? [];
        foreach (var back in evaluated.Backs)
        {
            if (!kept.Contains(back.Key) || governed.Declared.GetValueOrDefault(back.Node)?.Kind != NodeKinds.Claimed) continue;
            if (governed.Graph.Wires.FirstOrDefault(wire => wire.Id == back.Wire)?.From?.Node is not { } source || before.GetValueOrDefault(source) is not { } was) continue;
            var text = was.State switch
            {
                NodeStates.Rejected when was.Words is { } words => ChangesWords(source, words),
                NodeStates.Failed when was.Words is { Length: > 0 } own => own,
                NodeStates.Failed => FailedWords(source, was.Code, CheckResults.Newest(records.Home, scope.Run.Run, records.Read(governed.Unit.Id), source)?.Why),
                _ => $"Daoris: `{source}` came to `{was.Current?.Outcome}`, so your work goes back to you: what you commit goes through the workflow again.",
            };
            await TellWorkAsync(governed, scope, source, was.Attempt, text, said, ct).ConfigureAwait(false);
        }
    }

    internal static string FailedWords(string node, string? code, string? found) => code == UnrunChecks.GaveUp
        ? $"Daoris: `{node}` could not say whether your work passes, and Daoris gave it up as said after {UnrunChecks.Bound} asks, so nothing was judged"
            + (string.IsNullOrWhiteSpace(found) ? "" : $" ({found.Trim().ReplaceLineEndings(" ")})") + ": commit again and what you commit goes through the workflow again."
        : $"Daoris: `{node}` failed{(code is { } named ? $" (`{named}`)" : "")}, so your work goes back to you: "
        + (string.IsNullOrWhiteSpace(found) ? "mend what it found" : "\n\n" + found.Trim() + "\n\nMend what it found")
        + " and commit, and what you commit goes through the workflow again.";

    /// <summary>A check's red past its rounds' bound decided by the risk policy, then told to the work's session all the same, once (<see cref="SpentChecks"/>).</summary>
    public async Task SpentRedsAsync(GovernedUnit governed, DriverConfig? config, List<string> said, List<LookLine> noticed, CancellationToken ct)
    {
        if (governed.Scope is not { } scope || records.Read(governed.Unit.Id) is not { } record) return;
        var (workspace, repository) = (governed.Unit.Owner.Workspace, governed.Unit.Owner.Repository);
        foreach (var (node, attempt, reds) in CheckRounds.SpentOwed(record, governed.Graph.Wires, governed.Declared))
        {
            var code = record.Statuses.GetValueOrDefault(node)?.Code;
            var found = CheckResults.Newest(records.Home, scope.Run.Run, record, node)?.Why;
            await SpentChecks.SettleAsync(records, governed.Unit.Id, scope.Run.Run, node, attempt, reds, code, found, config, workspace, repository, clock(),
                text => SayWorkAsync(governed, scope, node, attempt, text, said, ct), said, noticed, governed.Declared.GetValueOrDefault(node)).ConfigureAwait(false);
        }

        await UntoldAsync(governed, scope, config, said, ct).ConfigureAwait(false);
    }

    private async Task UntoldAsync(GovernedUnit governed, RunScope scope, DriverConfig? config, List<string> said, CancellationToken ct)
    {
        if (records.Read(governed.Unit.Id) is not { } record) return;
        var (workspace, repository) = (governed.Unit.Owner.Workspace, governed.Unit.Owner.Repository);
        foreach (var (node, attempt, _) in CheckRounds.Untold(record, governed.Declared))
        {
            if (SessionFor(governed, scope, node, attempt) is null) continue;
            var told = record.ToldOf(node, attempt)!;
            var code = record.Newest(node)?.Code;
            var found = CheckResults.Newest(records.Home, scope.Run.Run, record, node)?.Why;
            var text = told.Spent
                ? SpentChecks.Words(node, code, found, CheckRounds.Reds(record).GetValueOrDefault(node), told.Decided ?? NodeTold.OwnerHeld,
                    told.Decided == NodeTold.RoleHeld ? CastReading.Now(records.Home, workspace, repository)(SpentChecks.HeldOn) : null)
                : FailedWords(node, code, found);
            var (session, why) = await SayWorkAsync(governed, scope, node, attempt, text, said, ct).ConfigureAwait(false);
            if (session is null && why == told.Why) continue;
            var again = told with { Session = session, Why = why, At = clock() };
            records.Edit(governed.Unit.Id, kept => (kept with { Told = [.. kept.Told.Where(each => each != told).TakeLast(NodeRecord.ToldKept - 1), again] }, true));
        }
    }

    /// <summary>What a run's work is told of the person's changes: Daoris's own sentence, then their words quoted, as a reopen's are.</summary>
    private static string ChangesWords(string node, string words)
    {
        var quoted = string.Join("\n", words.Trim().ReplaceLineEndings("\n").Split('\n').Select(line => line.Length == 0 ? ">" : "> " + line));
        return $"Daoris: the person asked for changes at `{node}` before your work goes on, so it goes back to you. Make them, commit what changes "
            + $"files, and hand back again: your next handback is reviewed again, even where no file changed. Their words:\n\n{quoted}";
    }

    /// <summary>The key the work is told under: the run, the step that sent it back and that step's attempt, so it is said once.</summary>
    private static string ChangesKey(string run, string node, int attempt) => $"changes/{run}/{node}/{attempt}";

    /// <summary>
    /// Tell the run's work, its newest session here, <paramref name="text"/> under its key: through the say door, which keeps one word per key.
    /// The session told, or why none is, goes on the run's record (<see cref="NodeTold"/>) and in the pass's words.
    /// </summary>
    private async Task TellWorkAsync(GovernedUnit governed, RunScope scope, string node, int attempt, string text, List<string> said, CancellationToken ct)
    {
        var (session, why) = await SayWorkAsync(governed, scope, node, attempt, text, said, ct).ConfigureAwait(false);
        var kept = new NodeTold(node, attempt, session, why, clock());
        records.Edit(governed.Unit.Id, record => (record with { Told = [.. record.Told.TakeLast(NodeRecord.ToldKept - 1), kept] }, true));
    }

    /// <summary>The run's work told <paramref name="text"/> under its key, said in the pass's words: the session told, or why none is.</summary>
    private async Task<(string? Session, string? Why)> SayWorkAsync(
        GovernedUnit governed, RunScope scope, string node, int attempt, string text, List<string> said, CancellationToken ct)
    {
        var unit = governed.Unit.Id;
        var session = SessionFor(governed, scope, node, attempt);
        var (told, why) = await SayAsync(session, scope, node, attempt, text, ct).ConfigureAwait(false);
        said.Add(told
            ? $"work  {unit}: session {session} is told what `{node}` sent back, as its next turn."
            : session is null ? $"work  {unit}: nobody is told what `{node}` sent back: {why}; the words stay on its record."
            : $"work  {unit}: session {session} was not told what `{node}` sent back: {why}");
        return told ? (session, null) : (null, why);
    }

    /// <summary>The part the person's changes name is told, by the session that made it; a check's red, the session that made the work it checks; else the newest part's.</summary>
    private string? SessionFor(GovernedUnit governed, RunScope scope, string node, int attempt)
    {
        var record = records.Read(governed.Unit.Id);
        var maker = governed.Declared.GetValueOrDefault(node) is { } declared && NodeKit.Of(declared.Kind) is ITellsMaker teller
            ? teller.MakerOf(scope.Quests, record?.Attempts.LastOrDefault(each => each.Node == node && each.Attempt == attempt)?.Call.SubjectRevision)
            : null;
        return ChangesParts.SessionOf(scope, ChangesParts.Of(record, node, attempt)) ?? maker
               ?? scope.Quest?.Executors.LastOrDefault() ?? scope.Branch?.Executors.LastOrDefault();
    }

    private async Task<(bool Told, string? Why)> SayAsync(string? session, RunScope scope, string node, int attempt, string text, CancellationToken ct)
    {
        if (session is null) return (false, NodeTold.NoSession);
        if (say() is not { } door) return (false, "this Daoris holds no say door");
        try
        {
            var answer = await door(session, text, ChangesKey(scope.Run.Run, node, attempt), ct).ConfigureAwait(false);
            return answer.Kept ? (true, null) : (false, answer.Message);
        }
        catch (Exception error) when (!ct.IsCancellationRequested
                                      && error is DriverException or HttpRequestException or System.Text.Json.JsonException or IOException or InvalidOperationException)
        {
            return (false, error.Message);
        }
    }
}
