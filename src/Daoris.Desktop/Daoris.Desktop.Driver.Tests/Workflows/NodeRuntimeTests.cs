using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The evaluation runtime in the lease's loop, over a stand-in port, on run
/// <c>q7</c> bound to a saved graph that holds the plugin's performed deploy. The deploy is started once across wakes, its outcome heard and
/// kept, its declared retry started after its persisted delay; a restart reconciles before any new start; two wakes at once start nothing
/// twice. The run's own record is <see cref="NodeRuntimeRunTests"/>', and the person's word on its nodes <see cref="RunNodeWordsTests"/>'.
/// </summary>
public sealed class NodeRuntimeTests : NodeRunFixture
{
    /// <summary>The session's branch the deploy observes: the subject its attempts are sent for.</summary>
    private const string Branch = "unit-b1";

    /// <summary>A run whose claim is done and whose deploy is the plugin's performed node, then the landing.</summary>
    private const string Governed = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"deploy","kind":"acme.deploy/deploy","version":1,"reach":"performed"},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>d","kind":"after","from":"claimed","to":"deploy"},{"id":"d>h","kind":"after","from":"deploy","to":"handed"}]}
        """;

    /// <summary>
    /// A run whose deploy takes the build's artifact: a <c>data</c> wire, read from the build's accepted output, its
    /// evidence the plugin's receipt.
    /// </summary>
    private const string Pipeline = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"build","kind":"acme.deploy/build","version":1,"reach":"performed","evidence":"acme.deploy/receipt@1","outputs":{"artifact":"acme.deploy/artifact@1"}},
                  {"id":"deploy","kind":"acme.deploy/deploy","version":1,"reach":"performed","inputs":{"release":"acme.deploy/artifact@1"}},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>b","kind":"after","from":"claimed","to":"build"},{"id":"w1","kind":"data","from":"build.artifact","to":"deploy.release"},
                  {"id":"b>d","kind":"after","from":"build","to":"deploy"},{"id":"d>h","kind":"after","from":"deploy","to":"handed"}]}
        """;

    private string? _bound;

    public NodeRuntimeTests()
    {
        _branchTips["daoris/s1"] = Tip1;
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done")), BranchUnit];
    }

    private static WorkUnit BranchUnit => RunHome.Branch(Branch, "daoris/s1", Opened, [RunId]);

    /// <summary>The run bound to <paramref name="graph"/> (once, however many looks pass), and a look over its home.</summary>
    private new NodeRuntimeLook Look(string graph, bool whole = true)
    {
        if (_bound != graph)
        {
            BoundTo(graph);
            _bound = graph;
        }

        return RunLook("{}", whole);
    }

    /// <summary>The build's completion for its attempt still waiting, with its receipt as evidence and its artifact as its output.</summary>
    private WorkStepHeard Built(string evidence, string artifact) =>
        new(Plugin, 1, new WorkStepFrame("build", new WorkOutcome(WorkStates.Reached, Evidence: new WorkEvidence(evidence, null, null, "built"),
            Outputs: new Dictionary<string, WorkOutput> { ["artifact"] = new(artifact, null) }), _now, "e-" + artifact,
            Invocation: Assert.Single(Record().Attempts, attempt => attempt.Node == "build" && attempt.State != AttemptStates.Settled).Call.Invocation));

    /// <summary>
    /// A completion's resources are its unit's receipts, by the roles its plugin's manifest declares; nothing is cleaned
    /// before the unit's end; at its true end the temporary one is asked of its plugin, which is kept owed while it fails, its back-off the
    /// loop's timer, and the result is kept.
    /// </summary>
    [Fact]
    public async Task A_units_temporary_resources_are_cleaned_by_their_plugin_at_its_true_end_and_its_result_kept()
    {
        IReadOnlyList<PluginResource> declared =
            [new("acme.deploy/preview@1", PluginResourceRoles.Temporary, PluginSteps.CleanupPoint), new("acme.deploy/site@1", PluginResourceRoles.Result, PluginSteps.CleanupPoint)];
        NodeRuntimeLook Declaring(bool whole = true) => Look(Governed, whole) with { Resources = plugin => plugin == Plugin ? declared : null };
        var runtime = Runtime();
        await runtime.PassAsync(Declaring(), CancellationToken.None);
        var invocation = Assert.Single(_port.Sent).Invocation;

        _port.Heard.Enqueue(new(Plugin, 1, new WorkStepFrame("deploy", new WorkOutcome(WorkStates.Reached)
        {
            Resources = [new("acme.deploy/preview@1", "p-1"), new("acme.deploy/site@1", "s-1")],
        }, _now, "e1", Invocation: invocation)));
        await runtime.PassAsync(Declaring(whole: false), CancellationToken.None);
        await runtime.PassAsync(Declaring(), CancellationToken.None);

        var receipts = new ResourceRecordFiles(_home, () => true).Read(RunUnit)!.Receipts;
        Assert.Equal([PluginResourceRoles.Temporary, PluginResourceRoles.Result], receipts.Select(receipt => receipt.Role));
        Assert.Empty(_port.Cleaned);
        Assert.Empty(_port.Owed[^1]);

        // The run lands: its record holds its end, the unit's true end. The first ask fails, so its plugin stays owed and the retry is the loop's next timer.
        _units = [_units[0], RunHome.Branch(Branch, "daoris/s1", Opened, [RunId], RunHome.Landing(Branch, "feature/q7", Tip1, _now, RunId))];
        await runtime.PassAsync(Declaring(), CancellationToken.None);
        _port.Cleanup = _ => new(CleanupDispositions.Failed, Message: "busy");
        var failed = await runtime.PassAsync(Declaring(), CancellationToken.None);

        Assert.True(failed.Performed.Any(done => (done.What, done.Code) == (CleanedWhat.Again, CleanupDispositions.Failed)), "ended=" + Record().Ended?.Quest + " states=" + string.Join(",", Record().Statuses.Select(s => s.Key + "=" + s.Value.State)) + " armed=" + failed.Armed + " performed=" + string.Join("|", failed.Performed.Select(d => d.What + ":" + d.Code)));
        Assert.Equal(["p-1"], _port.Cleaned.Select(call => call.Resource));
        Assert.Equal([Plugin], _port.Owed[^1]);
        Assert.Equal(_now + TimeSpan.FromMinutes(1), runtime.NextDue);

        _now += TimeSpan.FromMinutes(1);
        _port.Cleanup = _ => new(CleanupDispositions.Removed);
        var cleaned = await runtime.PassAsync(Declaring(whole: false), CancellationToken.None);

        Assert.Contains(cleaned.Performed, done => (done.What, done.Code) == (CleanedWhat.Cleaned, CleanupDispositions.Removed));
        Assert.Equal(["p-1", "p-1"], _port.Cleaned.Select(call => call.Resource));
        Assert.Single(_port.Cleaned.Select(call => call.IdempotencyKey).Distinct());
        Assert.Empty(_port.Owed[^1]);
        var owed = WorkObligations.Read(_home, _now)[RunUnit].ToDictionary(entry => entry.Resource, entry => entry.Disposition);
        Assert.Equal(InventoryDisposition.Removed, owed["acme.deploy/preview@1:p-1"]);
        Assert.Equal(InventoryDisposition.Kept, owed["acme.deploy/site@1:s-1"]);
    }

    [Fact]
    public async Task A_reach_on_evidence_not_authorised_starts_nothing_downstream_and_an_authorised_ones_output_is_read_by_the_next_node()
    {
        var runtime = Runtime();
        await runtime.PassAsync(Look(Pipeline), CancellationToken.None);
        Assert.Equal([Plugin + "/build"], _port.Sent.Select(call => call.Step));

        // A receipt of another schema than the build declares: not its evidence, so the build is held and the deploy never starts.
        _port.Heard.Enqueue(Built("acme.deploy/log@1:l1", "acme.deploy/artifact@1:a1"));
        var held = await runtime.PassAsync(Look(Pipeline, whole: false), CancellationToken.None);
        await runtime.PassAsync(Look(Pipeline), CancellationToken.None);

        Assert.Contains(held.Performed, done => (done.Node, done.What, done.Code) == ("build", PerformedWhat.Held, NodeRefusal.NoEvidence));
        Assert.Equal(NodeStates.Active, Record().Statuses["build"].State);
        Assert.Single(_port.Sent);

        // The build's own receipt: reached, and its accepted artifact is the deploy's release.
        _port.Heard.Enqueue(Built("acme.deploy/receipt@1:b1", "acme.deploy/artifact@1:a1"));
        await runtime.PassAsync(Look(Pipeline, whole: false), CancellationToken.None);

        Assert.Equal(NodeStates.Reached, Record().Statuses["build"].State);
        var deploy = Assert.Single(_port.Sent, call => call.Step == Plugin + "/deploy");
        Assert.Equal(["acme.deploy/artifact@1:a1"], deploy.Inputs);
    }

    [Fact]
    public async Task An_output_accepted_on_a_revision_the_work_has_moved_past_is_never_read()
    {
        // A revision the test moves, as a new tip moves a branch: the runtime reads every unit's revision alike.
        _branchTips["daoris/s1"] = "r1";
        var runtime = Runtime();
        await runtime.PassAsync(Look(Pipeline), CancellationToken.None);

        // The work moves to r2 before the build's completion on r1 is heard: its outcome is a stale attempt's, and the build starts again.
        _branchTips["daoris/s1"] = "r2";
        _port.Heard.Enqueue(Built("acme.deploy/receipt@1:b1", "acme.deploy/artifact@1:a1"));
        var moved = await runtime.PassAsync(Look(Pipeline), CancellationToken.None);

        Assert.Contains(moved.Performed, done => (done.Node, done.What, done.Code) == ("build", PerformedWhat.Stale, WorkRefusals.Stale));
        Assert.DoesNotContain(_port.Sent, call => call.Step == Plugin + "/deploy");
        Assert.Equal(["r1", "r2"], _port.Sent.Select(call => call.SubjectRevision));

        // The build on r2 reaches: the deploy reads its artifact, never r1's.
        _port.Heard.Enqueue(Built("acme.deploy/receipt@1:b2", "acme.deploy/artifact@1:a2"));
        await runtime.PassAsync(Look(Pipeline, whole: false), CancellationToken.None);

        var deploy = Assert.Single(_port.Sent, call => call.Step == Plugin + "/deploy");
        Assert.Equal(["acme.deploy/artifact@1:a2"], deploy.Inputs);
        Assert.Equal("r2", deploy.SubjectRevision);
    }

    private NodeRecord Record() => Files.Read(RunUnit)!;

    private WorkStepHeard Completion(WorkOutcome outcome) =>
        new(Plugin, 1, new WorkStepFrame("deploy", outcome, _now, "e1", Invocation: Assert.Single(Record().Attempts, attempt => attempt.State != AttemptStates.Settled).Call.Invocation));

    /// <summary>
    /// The home's own reading of a quest's binding (<see cref="NodeRuntime.BoundIn"/>), its run named by its chain's first quest in
    /// its repository: a binding to a named workflow is that version's, read as unreadable where the workflow is not saved, and a run that
    /// follows Current, or has no binding, is none.
    /// </summary>
    [Fact]
    public void The_homes_binding_of_a_quests_run_is_read_through_its_chain()
    {
        var runs = new WorkflowRunFiles(_home);
        Assert.True(runs.Bind(new WorkflowRunBinding("q7", "engine", "default", "docs", WorkflowLevels.Repository, _now) { Version = 1, Digest = "wfv-docs" }));
        Assert.True(runs.Bind(new WorkflowRunBinding("q8", "engine", "default", WorkflowSelection.Current, WorkflowLevels.Current, _now)));
        WorkUnit Of(string id, QuestView quest) => QuestOf(id) with { Records = new WorkUnitRecords(null, [], quest) };
        var q7 = new QuestView("q7", "", "engine", "", "", "Taken");
        var q8 = new QuestView("q8", "", "engine", "", "", "Taken");
        var q9 = new QuestView("q9", "", "engine", "", "", "Taken");
        var bound = NodeRuntime.BoundIn(_home);

        Assert.NotNull(bound(Of("quest:q7", q7), [q7, q8, q9]));
        Assert.Null(bound(Of("quest:q8", q8), [q7, q8, q9]));
        Assert.Null(bound(Of("quest:q9", q9), [q7, q8, q9]));
    }

    [Fact]
    public async Task A_performed_node_is_started_once_across_two_wakes_and_its_unit_awaited()
    {
        var runtime = Runtime();

        var first = await runtime.PassAsync(Look(Governed), CancellationToken.None);
        var second = await runtime.PassAsync(Look(Governed, whole: false), CancellationToken.None);
        var third = await runtime.PassAsync(Look(Governed), CancellationToken.None);

        Assert.True(first.Armed);
        Assert.Contains(first.Performed, done => done.What == PerformedWhat.Sent && done.Node == "deploy");
        Assert.DoesNotContain(second.Performed.Concat(third.Performed), done => done.What == PerformedWhat.Sent);
        Assert.Single(_port.Sent);
        var attempt = Assert.Single(Record().Attempts);
        Assert.Equal((AttemptStates.Sent, NodeStates.Active), (attempt.State, Record().Statuses["deploy"].State));
        // The look's deadline reads the units; the wake between judges the ones its answers concern, from what the last look read.
        Assert.Equal(2, _unitsRead);

        // The awaited set names the unit and its step, so the plugin's process is kept while the attempt waits.
        var told = _port.Told[^1];
        var watched = Assert.Single(told[Plugin]);
        Assert.Equal(["deploy"], watched.Awaiting);
        Assert.Equal(("engine", "checkout"), (watched.Repository, watched.Root));
    }

    [Fact]
    public async Task An_outcome_heard_is_kept_and_its_unit_no_longer_awaited()
    {
        var runtime = Runtime();
        await runtime.PassAsync(Look(Governed), CancellationToken.None);

        _port.Heard.Enqueue(Completion(new WorkOutcome(WorkStates.Reached, Evidence: new WorkEvidence("acme.deploy/receipt@1:r1", null, null, "deployed"))));
        var pass = await runtime.PassAsync(Look(Governed, whole: false), CancellationToken.None);

        Assert.Contains(pass.Performed, done => (done.What, done.Code) == (PerformedWhat.Applied, WorkStates.Reached));
        Assert.Equal(NodeStates.Reached, Record().Statuses["deploy"].State);
        Assert.Equal(AttemptStates.Settled, Assert.Single(Record().Attempts).State);
        Assert.Single(_port.Sent);
        Assert.Empty(_port.Told[^1].GetValueOrDefault(Plugin) ?? []);
    }

    [Fact]
    public async Task A_declared_retry_is_started_after_its_persisted_delay_and_not_before()
    {
        var runtime = Runtime();
        await runtime.PassAsync(Look(Governed), CancellationToken.None);
        _port.Heard.Enqueue(Completion(new WorkOutcome(WorkStates.Failed, "unavailable")));

        await runtime.PassAsync(Look(Governed, whole: false), CancellationToken.None);
        var failed = Assert.Single(Record().Attempts);
        Assert.Equal(_now.AddSeconds(30), failed.RetryAt);
        Assert.Equal(failed.RetryAt, runtime.NextDue);

        _now = _now.AddSeconds(29);
        await Runtime().PassAsync(Look(Governed), CancellationToken.None);
        Assert.Single(_port.Sent);

        // Past the delay, after a restart: a new attempt with a new key.
        _now = _now.AddSeconds(2);
        var pass = await Runtime().PassAsync(Look(Governed, whole: true), CancellationToken.None);
        Assert.Contains(pass.Performed, done => done.What == PerformedWhat.Retry);
        Assert.Equal(2, _port.Sent.Count);
        Assert.NotEqual(_port.Sent[0].IdempotencyKey, _port.Sent[1].IdempotencyKey);
        Assert.Equal(2, Record().Statuses["deploy"].Attempt);
    }

    [Fact]
    public async Task A_restart_reconciles_an_attempt_sent_by_the_loop_before_it_before_any_new_start()
    {
        await Runtime().PassAsync(Look(Governed), CancellationToken.None);
        var sent = Assert.Single(_port.Sent);

        // The next loop: the attempt is asked after with its key; running, it waits, and nothing new is sent.
        _port.Answer = _ => new(ReconcileStates.Running);
        var pass = await Runtime().PassAsync(Look(Governed), CancellationToken.None);

        var asked = Assert.Single(_port.Asked);
        Assert.Equal((sent.Invocation, sent.IdempotencyKey), (asked.Invocation, asked.IdempotencyKey));
        Assert.Contains(pass.Performed, done => done.What == PerformedWhat.Running);
        Assert.Single(_port.Sent);
        Assert.Single(Record().Attempts);
    }

    [Fact]
    public async Task Two_wakes_at_once_start_nothing_twice()
    {
        var runtime = Runtime();
        var sending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _port.OnPerform = async _ =>
        {
            sending.TrySetResult();
            await release.Task;
        };

        var look = runtime.PassAsync(Look(Governed), CancellationToken.None);
        await sending.Task.WaitAsync(Poll.Patience);
        var wake = runtime.PassAsync(Look(Governed, whole: false), CancellationToken.None);

        // A verb beside the loop, or a loop that lost the lease: it holds no lease, so it reads no unit and writes nothing.
        var verb = await new NodeRuntime(new NodeRecordFiles(_home, () => false), _port, () => _now, () => false).PassAsync(Look(Governed), CancellationToken.None);
        Assert.Empty(verb.Performed);
        release.SetResult();
        await Task.WhenAll(look, wake).WaitAsync(Poll.Patience);

        Assert.Single(_port.Sent);
        var attempt = Assert.Single(Record().Attempts);
        Assert.Equal((AttemptStates.Sent, NodeStates.Active), (attempt.State, Record().Statuses["deploy"].State));
        Assert.Equal(1, _unitsRead);
    }

    [Fact]
    public async Task A_node_whose_plugin_is_not_up_is_awaited_and_not_sent_until_it_is()
    {
        _port.Generation = null;
        var runtime = Runtime();

        await runtime.PassAsync(Look(Governed), CancellationToken.None);
        Assert.Empty(_port.Sent);
        Assert.Empty(Record().Attempts);
        Assert.Equal(["deploy"], Assert.Single(_port.Told[^1][Plugin]).Awaiting);

        _port.Generation = 1;
        await runtime.PassAsync(Look(Governed, whole: false), CancellationToken.None);
        Assert.Single(_port.Sent);
    }

    /// <summary>
    /// The awaited set is published only while the loop holds the lease, asked after the pass's awaits: a loop that
    /// loses it while its send is out tells nothing.
    /// </summary>
    [Fact]
    public async Task A_lease_lost_while_a_send_is_out_publishes_no_awaited_set()
    {
        var held = true;
        _port.OnPerform = _ =>
        {
            held = false;
            return Task.CompletedTask;
        };
        var runtime = new NodeRuntime(Files, _port, () => _now, () => held);

        await runtime.PassAsync(Look(Governed), CancellationToken.None);

        Assert.Single(_port.Sent);
        Assert.Empty(_port.Told);
    }

    /// <summary>A sent attempt held for the person: the next loop asks after it, and its plugin cannot say (<c>unknown</c>).</summary>
    private async Task<(NodeRuntime Runtime, WorkPerformCall Sent)> HeldAsync()
    {
        await Runtime().PassAsync(Look(Governed), CancellationToken.None);
        var sent = Assert.Single(_port.Sent);
        var runtime = Runtime();
        await runtime.PassAsync(Look(Governed), CancellationToken.None);
        Assert.Equal(AttemptStates.Held, Assert.Single(Record().Attempts).State);
        return (runtime, sent);
    }

    /// <summary>The person's door: a performer over the same records, which asks the lease inside the record's lock.</summary>
    private WorkPerformer Door(bool holdsLease = true) => new(new NodeRecordFiles(_home, () => holdsLease), _port, () => _now);

    /// <summary>
    /// A held attempt is listed with its node, its attempt and why, and keeps its gate across passes; the person's *it did
    /// not* settles it as theirs (failed, never the plugin's word), and the node starts again as a new attempt with a new key.
    /// </summary>
    [Fact]
    public async Task A_held_attempt_is_listed_and_the_persons_did_not_restarts_it_with_a_new_key()
    {
        var (runtime, sent) = await HeldAsync();

        var held = Assert.Single(HeldAttempts.Read(Files));
        Assert.Equal((RunUnit, "deploy", 1, Plugin, ReconcileStates.Unknown, NodeStates.Unknown, (string?)null),
            (held.Unit, held.Node, held.Attempt, held.Plugin, held.Why, held.NodeState, held.Resolution));
        await runtime.PassAsync(Look(Governed), CancellationToken.None);
        Assert.Single(_port.Sent);

        Assert.Equal(PerformedWhat.Worded, Door().Word(RunUnit, "deploy", 1, HeldResolutions.DidNot).What);
        Assert.Equal(HeldResolutions.DidNot, Assert.Single(HeldAttempts.Read(Files)).Resolution);
        var pass = await runtime.PassAsync(Look(Governed, whole: false), CancellationToken.None);

        Assert.Contains(pass.Performed, done => done.What == PerformedWhat.Resolved);
        Assert.Equal(2, _port.Sent.Count);
        Assert.NotEqual(sent.IdempotencyKey, _port.Sent[1].IdempotencyKey);
        Assert.NotEqual(sent.Invocation, _port.Sent[1].Invocation);
        var first = Record().Attempts.Single(attempt => attempt.Attempt == 1);
        Assert.Equal((AttemptStates.Settled, WorkStates.Failed, HeldResolutions.DidNot), (first.State, first.Outcome, first.Resolution));
        Assert.Contains(Record().History, entry => entry.Was is { State: NodeStates.Failed, By: NodeSource.Person });
        Assert.Equal((NodeStates.Active, 2), (Record().Statuses["deploy"].State, Record().Statuses["deploy"].Attempt));
        Assert.Empty(HeldAttempts.Read(Files));
    }

    /// <summary>
    /// *It happened* reaches the node on the person's word, its evidence the person's; *stop* stops it with the outcome
    /// settled as the person's to answer for. Either lifts the gate, and neither sends anything.
    /// </summary>
    [Theory]
    [InlineData(HeldResolutions.Happened, NodeStates.Reached, WorkStates.Reached)]
    [InlineData(HeldResolutions.Stop, NodeStates.Stopped, WorkStates.Unknown)]
    public async Task The_persons_happened_and_stop_settle_the_held_attempt_as_theirs(string word, string state, string outcome)
    {
        var (runtime, _) = await HeldAsync();

        Assert.Equal(PerformedWhat.Worded, Door().Word(RunUnit, "deploy", 1, word).What);
        await runtime.PassAsync(Look(Governed, whole: false), CancellationToken.None);
        await runtime.PassAsync(Look(Governed), CancellationToken.None);

        Assert.Single(_port.Sent);
        var status = Record().Statuses["deploy"];
        Assert.Equal((state, NodeSource.Person, (int?)null), (status.State, status.By, status.Unreconciled));
        if (word == HeldResolutions.Happened) Assert.Equal(NodeSource.Person, status.Current!.Evidence!.Schema);
        var attempt = Assert.Single(Record().Attempts);
        Assert.Equal((AttemptStates.Settled, outcome, word), (attempt.State, attempt.Outcome, attempt.Resolution));
        Assert.Empty(HeldAttempts.Read(Files));
    }

    /// <summary>
    /// A close's word found on a plugin's held attempt, which no door records, is taken back at the next pass saying why;
    /// the attempt keeps its gate and its three words, and nothing is sent or moved.
    /// </summary>
    [Fact]
    public async Task A_closes_word_on_a_plugins_held_attempt_is_taken_back_and_nothing_moves()
    {
        var (runtime, _) = await HeldAsync();
        Files.Edit<bool>(RunUnit, record => (record with { Attempts = [.. record.Attempts.Select(each => each with { Resolution = HeldResolutions.Record })] }, true));

        await runtime.PassAsync(Look(Governed, whole: false), CancellationToken.None);

        var held = Assert.Single(HeldAttempts.Read(Files));
        Assert.Equal(((string?)null, WorkPerformer.NotOffered), (held.Resolution, held.Why));
        Assert.Equal(HeldResolutions.Plugin, held.Words);
        Assert.Single(_port.Sent);
        Assert.Equal(NodeStates.Unknown, Record().Statuses["deploy"].State);
    }

    /// <summary>
    /// The door records the person's word only in the process holding the home's lease, on an attempt still held with no
    /// word, and says why otherwise; nothing is written.
    /// </summary>
    [Fact]
    public async Task The_persons_word_is_refused_without_the_lease_or_on_an_attempt_not_held()
    {
        await HeldAsync();
        var before = File.ReadAllText(NodeRecordFiles.PathOf(_home, RunUnit));

        var unleased = Door(holdsLease: false).Word(RunUnit, "deploy", 1, HeldResolutions.DidNot);
        var other = Door().Word(RunUnit, "deploy", 2, HeldResolutions.DidNot);
        var unknownWord = Door().Word(RunUnit, "deploy", 1, "maybe");

        Assert.Equal((PerformedWhat.Refused, WorkPerformer.NotHeld), (unleased.What, unleased.Code));
        Assert.Contains("does not hold the home", HeldAttempts.Said(unleased, 1, HeldResolutions.DidNot));
        Assert.Equal((PerformedWhat.Refused, WorkPerformer.NotAttempt), (other.What, other.Code));
        Assert.Contains("is not held now", HeldAttempts.Said(other, 2, HeldResolutions.DidNot));
        Assert.Equal(PerformedWhat.Refused, unknownWord.What);

        // A close's words are not a plugin's attempt's, which keeps its three unchanged.
        var closeWord = Door().Word(RunUnit, "deploy", 1, HeldResolutions.Resend);
        Assert.Equal((PerformedWhat.Refused, WorkPerformer.NotOffered), (closeWord.What, closeWord.Code));
        Assert.Contains("happened, did-not or stop", HeldAttempts.Said(closeWord, 1, HeldResolutions.Resend), StringComparison.Ordinal);
        Assert.Equal(HeldResolutions.Plugin, Assert.Single(HeldAttempts.Read(Files)).Words);
        Assert.Equal(before, File.ReadAllText(NodeRecordFiles.PathOf(_home, RunUnit)));

        Assert.Equal(PerformedWhat.Worded, Door().Word(RunUnit, "deploy", 1, HeldResolutions.Stop).What);
        var again = Door().Word(RunUnit, "deploy", 1, HeldResolutions.DidNot);
        Assert.Equal(PerformedWhat.Refused, again.What);
        Assert.Contains("already has your word", HeldAttempts.Said(again, 1, HeldResolutions.DidNot));
    }

    /// <summary>
    /// A failure Daoris will not retry (a code its step does not declare transient) is listed for the person with its code
    /// and their one word, keeping its node failed across passes; their <i>again</i> restarts it as a new attempt with a new key, the failure
    /// still its plugin's, and the word cleared as it is applied.
    /// </summary>
    [Fact]
    public async Task A_failure_kept_for_the_person_is_listed_and_their_again_restarts_it_with_a_new_key()
    {
        var runtime = Runtime();
        await runtime.PassAsync(Look(Governed), CancellationToken.None);
        var sent = Assert.Single(_port.Sent);
        _port.Heard.Enqueue(Completion(new WorkOutcome(WorkStates.Failed, "broken")));
        await runtime.PassAsync(Look(Governed, whole: false), CancellationToken.None);
        await runtime.PassAsync(Look(Governed), CancellationToken.None);

        var kept = Assert.Single(HeldAttempts.Read(Files));
        Assert.Equal((RunUnit, "deploy", 1, NodeStates.Failed, WorkPerformer.FailureKept, "broken", (string?)null),
            (kept.Unit, kept.Node, kept.Attempt, kept.NodeState, kept.Why, kept.Code, kept.Resolution));
        Assert.True(kept.Failed);
        Assert.Equal(HeldResolutions.Failure, kept.Words);
        Assert.Contains("failed (broken), and Daoris does not try it again by itself", HeldAttempts.WhySaid(kept), StringComparison.Ordinal);
        Assert.Contains($"say: daoris work held {RunUnit} deploy 1 again", HeldAttempts.Lines([kept])[1], StringComparison.Ordinal);
        Assert.Single(_port.Sent);

        Assert.Equal(PerformedWhat.Worded, Door().Word(RunUnit, "deploy", 1, HeldResolutions.Again).What);
        Assert.Equal(HeldResolutions.Again, Assert.Single(HeldAttempts.Read(Files)).Resolution);
        var pass = await runtime.PassAsync(Look(Governed, whole: false), CancellationToken.None);

        Assert.True(pass.Performed.Any(done => done is { What: PerformedWhat.Resolved, Code: HeldResolutions.Again }), "armed=" + pass.Armed + " ended=" + Record().Ended?.Quest + " states=" + string.Join(",", Record().Statuses.Select(s => s.Key + "=" + s.Value.State)) + " attempts=" + string.Join(",", Record().Attempts.Select(a => a.Node + ":" + a.Attempt + ":" + a.State + ":" + a.Resolution)) + " performed=" + string.Join("|", pass.Performed.Select(d => d.What + ":" + d.Code)) + " said=" + string.Join("|", pass.Said) + " held=" + HeldAttempts.Read(Files).Count + " evaluated=" + string.Join("|", pass.Evaluated.Select(e => e.ToString())));
        Assert.Equal(2, _port.Sent.Count);
        Assert.NotEqual(sent.IdempotencyKey, _port.Sent[1].IdempotencyKey);
        Assert.NotEqual(sent.Invocation, _port.Sent[1].Invocation);
        var first = Record().Attempts.Single(attempt => attempt.Attempt == 1);
        Assert.Equal((AttemptStates.Settled, WorkStates.Failed, "broken", (string?)null), (first.State, first.Outcome, first.Code, first.Resolution));
        Assert.Contains(Record().History, entry => entry is { Why: "person-again", Was: { State: NodeStates.Failed, By: NodeSource.Plugin } });
        Assert.Equal((NodeStates.Active, 2), (Record().Statuses["deploy"].State, Record().Statuses["deploy"].Attempt));
        Assert.Empty(HeldAttempts.Read(Files));
    }

    /// <summary>
    /// A transient failure with tries left is Daoris's to retry and is not listed; spent, it is, saying so. Each kind takes
    /// its own words alone: a held attempt's on a failure, and <i>again</i> on a held attempt, are refused and nothing is written.
    /// </summary>
    [Fact]
    public async Task A_retry_coming_is_not_listed_and_the_words_of_a_failure_and_a_held_attempt_stay_apart()
    {
        var runtime = Runtime();
        await runtime.PassAsync(Look(Governed), CancellationToken.None);
        _port.Heard.Enqueue(Completion(new WorkOutcome(WorkStates.Failed, "unavailable")));
        await runtime.PassAsync(Look(Governed, whole: false), CancellationToken.None);
        Assert.Empty(HeldAttempts.Read(Files));

        for (var tried = 2; tried <= 3; tried++)
        {
            _now += TimeSpan.FromMinutes(5);
            await runtime.PassAsync(Look(Governed), CancellationToken.None);
            Assert.Equal(tried, _port.Sent.Count);
            _port.Heard.Enqueue(Completion(new WorkOutcome(WorkStates.Failed, "unavailable")));
            await runtime.PassAsync(Look(Governed, whole: false), CancellationToken.None);
        }

        var spent = Assert.Single(HeldAttempts.Read(Files));
        Assert.Equal((3, WorkPerformer.TriesSpent, "unavailable"), (spent.Attempt, spent.Why, spent.Code));
        Assert.Contains("its tries are spent", HeldAttempts.WhySaid(spent), StringComparison.Ordinal);
        var before = File.ReadAllText(NodeRecordFiles.PathOf(_home, RunUnit));

        var heldWord = Door().Word(RunUnit, "deploy", 3, HeldResolutions.DidNot);
        Assert.Equal((PerformedWhat.Refused, WorkPerformer.NotAgain), (heldWord.What, heldWord.Code));
        Assert.Contains("say again", HeldAttempts.Said(heldWord, 3, HeldResolutions.DidNot), StringComparison.Ordinal);
        Assert.Equal(WorkPerformer.NotAttempt, Door().Word(RunUnit, "deploy", 2, HeldResolutions.Again).Code);
        Assert.Equal(WorkPerformer.NotHeld, Door(holdsLease: false).Word(RunUnit, "deploy", 3, HeldResolutions.Again).Code);
        Assert.Equal(before, File.ReadAllText(NodeRecordFiles.PathOf(_home, RunUnit)));
    }

    /// <summary><i>Again</i> on an attempt held is refused, saying what to say first; its three words stay.</summary>
    [Fact]
    public async Task Again_on_a_held_attempt_is_refused()
    {
        await HeldAsync();

        var again = Door().Word(RunUnit, "deploy", 1, HeldResolutions.Again);

        Assert.Equal((PerformedWhat.Refused, WorkPerformer.NotAgain), (again.What, again.Code));
        Assert.Contains("is held, not failed", HeldAttempts.Said(again, 1, HeldResolutions.Again), StringComparison.Ordinal);
        Assert.Equal(HeldResolutions.Plugin, Assert.Single(HeldAttempts.Read(Files)).Words);
    }
}
