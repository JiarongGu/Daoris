namespace Daoris.Driver;

/// <summary>
/// What the runtime keeps on a run bound to a graph version around its rounds: the person's skips and stops applied, what a review sent back
/// told to the run's work, the accepts a carried-on session earns again, the landing started on its door, and the run's end and reopening.
/// </summary>
/// <param name="say">The say door a run's work is told what was sent back to it through: the session, the words and the key.</param>
internal sealed class NodeRunKeeping(
    NodeRecordFiles records, Func<DateTimeOffset> clock, Func<bool> holdsLease, GraphRuns graphRuns, WorkPerformer performer,
    Func<IIntegrationDoor?> integration, Func<Func<string, string, string, CancellationToken, Task<SayAnswer>>?> say, Func<ILandingDoor?> landing,
    Func<IPushDoor?> pushing, Func<IPullRequestRecords?> pullRequests)
{
    /// <summary>
    /// A run's <c>&lt;plugin&gt;/pull-request</c> step that reached: the pull request its evidence names is written on the landing record with its
    /// plugin, once, so the <c>work/state</c> asks have an address and the page shows the pull request.
    /// </summary>
    public void KeepPullRequests(GovernedUnit governed)
    {
        if (pullRequests() is not { } kept || !holdsLease() || records.Read(governed.Unit.Id) is not { } record) return;
        if (NodeEvaluations.Landed(governed, record.Statuses) is not { Unit: var unit } || BuiltInNodes.LandingOf(unit) is not { } landed) return;
        foreach (var node in governed.Declared.Values)
        {
            if (PullRequestSteps.PluginOf(node.Kind) is not { } plugin) continue;
            if (record.Statuses.GetValueOrDefault(node.Id) is not { State: NodeStates.Reached, Acceptance.Evidence.Ref: { } address }) continue;
            if (!PullRequestSteps.IsAddress(address)) continue;
            kept.Keep(governed.Watched.Repository, landed.Branch, plugin, address);
        }
    }

    /// <summary>
    /// A run's <c>push</c>, active at the tip the landing recorded, started once on the door: the branch the <c>land</c> made is pushed through
    /// the push act. The door is idempotent by node and tip, and the act is the person's press's own, so the two cannot push twice.
    /// </summary>
    public void StartPushes(GovernedUnit governed, RunScope scope)
    {
        if (pushing() is not { } door || !holdsLease() || records.Read(governed.Unit.Id) is not { } record) return;
        if (NodeEvaluations.Landed(governed, record.Statuses) is not { Unit: var unit } || BuiltInNodes.LandingOf(unit) is not { } landed) return;
        foreach (var node in governed.Declared.Values.Where(each => each.Kind == NodeKinds.Push))
        {
            var status = WorkPerformer.StatusOf(record, node.Id);
            if (status.State != NodeStates.Active || landed.Pushed && landed.PushedTip == landed.Tip) continue;
            door.Start(new PushStart(governed.Unit.Id, node, governed.Watched.Repository, governed.Watched.Workspace, governed.Watched.Root, landed.Branch, landed.Tip));
        }
    }

    /// <summary>
    /// A run's <c>land</c>, active at the tip this pass read, started once on the door: a node reaches active only once its wires hold, its
    /// subject is due and the gate holds nothing, and what authorizes it is the run's review word, or the standing authority judged again
    /// before the first effect. The door is idempotent by node and tip; its delivery key fences a press and a pass at once.
    /// </summary>
    public void StartLandings(GovernedUnit governed, RunScope scope)
    {
        if (landing() is not { } door || !holdsLease() || scope.Branch is not { } branch || records.Read(governed.Unit.Id) is not { } record) return;
        foreach (var node in governed.Declared.Values.Where(each => each.Kind == NodeKinds.Land))
        {
            var status = WorkPerformer.StatusOf(record, node.Id);
            if (status.State != NodeStates.Active || status.Binding is not { SubjectRevision: { } tip } binding) continue;
            door.Start(new LandStart(governed.Unit.Id, governed.Run, node.Id, branch.Id, branch.Executors, governed.Watched.Repository,
                governed.Watched.Workspace, tip, binding.GraphDigest, governed.Graph.Nodes.FirstOrDefault(each => each.Kind == NodeKinds.Review)?.Id));
        }
    }

    /// <summary>
    /// A review whose Accept waits on a carried-on session (<see cref="RunRepairs"/>), begun again at a tip that only continues the one the person
    /// accepted: the Accept is kept on it once more, for the next pass to apply, and the repair is spent.
    /// </summary>
    public async Task AcceptRepairsAsync(GovernedUnit governed, RunScope scope, CancellationToken ct)
    {
        if (!holdsLease() || records.Read(governed.Unit.Id) is not { Repairs.Count: > 0 } record) return;
        foreach (var repair in record.Repairs)
        {
            var status = WorkPerformer.StatusOf(record, repair.Node);
            if (status.State != NodeStates.Active || status.Binding is not { SubjectRevision: { } tip }) continue;
            if (!await RunRepairs.ContinuesAsync(scope.Root, repair, tip, ct).ConfigureAwait(false)) continue;
            if (RunNodeWords.Word(records, governed.Run, repair.Node, NodeWordActs.Accept, null, clock).Done)
            {
                RunRepairs.Take(records, governed.Unit.Id, repair.Node);
            }
        }
    }

    /// <summary>
    /// A run's end, judged once its pass's rounds are kept: its newest quest here names no step still to come here, and either its graph's end
    /// node is settled or no node of it can move again, with no attempt unsettled. Kept on its record, said once; the run then arms nothing
    /// until its chain comes back here.
    /// </summary>
    public void End(GovernedUnit governed, RunScope scope, List<string> said)
    {
        var unit = governed.Unit.Id;
        if (scope.Quest?.Records.Quest is not { } newest || records.Read(unit) is not { } record || record.Ended?.Quest == newest.Id) return;
        var repository = scope.Run.Binding.Repository;
        if (newest.Then.Any(step => step.SetUpIn is null && string.Equals(step.To, repository, StringComparison.OrdinalIgnoreCase))) return;
        if (record.Attempts.Any(attempt => attempt.State != AttemptStates.Settled || attempt.RetryAt is not null)) return;
        // A failure kept for the person waits on their word, which only an armed run applies, so the run is not over while one stands.
        if (record.Attempts.Any(attempt => attempt.Resolution is null && WorkPerformer.KeptFailure(record, attempt))) return;
        var settled = !NodeStates.Live.Contains(WorkPerformer.StatusOf(record, governed.Graph.End).State)
                      || !governed.Graph.Nodes.Any(node => WorkPerformer.StatusOf(record, node.Id).State
                          is NodeStates.Ready or NodeStates.Active or NodeStates.Unknown or NodeStates.Invalidated);
        // A declined request waits on the person's word, so the run is not over while one stands.
        var held = governed.Graph.Nodes.Any(node => node.Kind == NodeKinds.Request && NodeOptions.Text(node, "declined") != RequestOptions.Stop
            && WorkPerformer.StatusOf(record, node.Id) is { State: NodeStates.Failed, Code: RequestOptions.DeclinedCode });
        // A review's changes wait on the work's next commit, which runs the review again, so the run is not over while they stand.
        var changes = governed.Graph.Nodes.Any(node => node.Kind == NodeKinds.Review && WorkPerformer.StatusOf(record, node.Id).State == NodeStates.Rejected);
        if (!settled || held || changes) return;

        var ended = new RunEnd(newest.Id, clock());
        var (refused, _) = records.Edit(unit, current => (current with { Ended = ended }, true));
        if (refused is not null) return;
        graphRuns.Ended(scope.Run.Run, ended);
        said.Add($"work  {unit}: its run ended at #{newest.Id}: no step of its chain is still to come here, and nothing of its graph moves again; "
                 + "it is judged again once its chain comes back here.");
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
    public async Task<(List<WorkPerformed> Done, List<string> Said)> WordedAsync(IEnumerable<GovernedUnit> governed, CancellationToken ct)
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
            var promoted = integration() is { } door && pending.Pending.Any(word => word.Act == NodeWordActs.Stop && each.Declared.GetValueOrDefault(word.Node)?.Kind == NodeKinds.Integrate)
                ? await IntegrateNodes.PromotedAsync(door, pending, ct).ConfigureAwait(false)
                : null;
            var (refused, applied) = records.Edit(unit, record => RunNodeWords.Apply(record, each.Declared, scope, now, promoted));
            if (refused is not null) continue;
            foreach (var (word, code) in applied!)
            {
                done.Add(new WorkPerformed(unit, word.Node, code is null ? PerformedWhat.Resolved : PerformedWhat.Refused, code ?? word.Act));
                said.Add(RunNodeWords.AppliedSaid(unit, word, code));

                // The person's changes on a review with no way back on its rejection go to the work all the same, as the look's not-yet does;
                // one with a way back is told as that wire is taken, and only where it goes back to the work.
                if (code is null && word.Act == NodeWordActs.Changes && each.Scope is { } run && word.Words is { } words
                    && !each.Graph.Wires.Any(wire => wire.Kind == WireKinds.Back && wire.From?.Node == word.Node && wire.On == WireKinds.Rejected))
                {
                    var attempt = records.Read(unit)?.Statuses.GetValueOrDefault(word.Node)?.Attempt ?? 0;
                    await TellWorkAsync(each, run, word.Node, attempt, ChangesWords(word.Node, words), said, ct).ConfigureAwait(false);
                }
            }

            // The pass that applies the person's stop of a running check tells the executor.
            performer.StopChecks(unit, [.. applied.Where(each => each.Refused is null && each.Word.Act == NodeWordActs.Stop).Select(each => each.Word.Node)]);
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
                NodeStates.Failed => $"Daoris: `{source}` failed{(was.Code is { } code ? $" (`{code}`)" : "")}, so your work goes back to you: mend what it "
                    + "found and commit, and what you commit goes through the workflow again.",
                _ => $"Daoris: `{source}` came to `{was.Current?.Outcome}`, so your work goes back to you: what you commit goes through the workflow again.",
            };
            await TellWorkAsync(governed, scope, source, was.Attempt, text, said, ct).ConfigureAwait(false);
        }
    }

    /// <summary>What a run's work is told of the person's changes: Daoris's own sentence, then their words quoted, as a reopen's are.</summary>
    private static string ChangesWords(string node, string words)
    {
        var quoted = string.Join("\n", words.Trim().ReplaceLineEndings("\n").Split('\n').Select(line => line.Length == 0 ? ">" : "> " + line));
        return $"Daoris: the person asked for changes at `{node}` before your work lands, so it goes back to you. Make them and commit: what you "
            + $"commit is reviewed again. Their words:\n\n{quoted}";
    }

    /// <summary>The key the work is told under: the run, the step that sent it back and that step's attempt, so it is said once.</summary>
    private static string ChangesKey(string run, string node, int attempt) => $"changes/{run}/{node}/{attempt}";

    /// <summary>
    /// Tell the run's work, its newest session here, <paramref name="text"/> under its key: through the say door, which keeps one word per key.
    /// Where no session is known, no say door is wired, or the door refuses, the pass says so and the words stay on the record.
    /// </summary>
    private async Task TellWorkAsync(GovernedUnit governed, RunScope scope, string node, int attempt, string text, List<string> said, CancellationToken ct)
    {
        var unit = governed.Unit.Id;
        if ((scope.Quest?.Executors.LastOrDefault() ?? scope.Branch?.Executors.LastOrDefault()) is not { } session)
        {
            said.Add($"work  {unit}: no session of its work is known here, so nobody is told what `{node}` sent back; the words stay on its record.");
            return;
        }

        if (say() is not { } door)
        {
            said.Add($"work  {unit}: session {session} is not told what `{node}` sent back, since this Daoris holds no say door; the words stay on its record.");
            return;
        }

        try
        {
            var answer = await door(session, text, ChangesKey(scope.Run.Run, node, attempt), ct).ConfigureAwait(false);
            said.Add(answer.Kept
                ? $"work  {unit}: session {session} is told what `{node}` sent back, as its next turn."
                : $"work  {unit}: session {session} was not told what `{node}` sent back: {answer.Message}");
        }
        catch (Exception error) when (!ct.IsCancellationRequested
                                      && error is DriverException or HttpRequestException or System.Text.Json.JsonException or IOException or InvalidOperationException)
        {
            said.Add($"work  {unit}: session {session} was not told what `{node}` sent back: {error.Message}");
        }
    }
}
