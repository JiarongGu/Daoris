using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-CLOSE1b (the fabric events design §E5; D173's FABRIC-CLOSE1a and CLOSE1e notes): <c>quest.close</c> as the graph's end, run by the
/// evaluation runtime over a stand-in close door. A bound quest is closed once its submission is current and unheld, and once only; an
/// unanswered close is asked after by its key, never sent again blind (FABRIC-CLOSE1u); a refusal is kept with its words and said; a chain closed only in
/// part is said, naming the quest; the person's own done ends the graph as overridden and stops its nodes; an unbound quest is unchanged; and a
/// quest is bound at its take only where its host and its circle know a bound quest.
/// </summary>
public sealed class QuestCloseTests : IDisposable
{
    private const string Unit = "quest:q7";

    /// <summary>The engine's quests closed by a graph of one node, the close.</summary>
    private const string Closing = """
        { "workCycles": { "engine": { "quest": {
            "schemaVersion": 2, "entries": ["close"], "end": "close",
            "nodes": [{ "id": "close", "kind": "quest.close", "version": 1, "reach": "performed" }],
            "wires": [] } } } }
        """;

    /// <summary>The same, with a person's task before the close: what the person's own done stops.</summary>
    private const string Reviewed = """
        { "workCycles": { "engine": { "quest": {
            "schemaVersion": 2, "entries": ["qa"], "end": "close",
            "nodes": [{ "id": "qa", "kind": "human", "version": 1, "reach": "pressed" },
                      { "id": "close", "kind": "quest.close", "version": 1, "reach": "performed" }],
            "wires": [{ "id": "a", "kind": "after", "from": "qa", "to": "close" }] } } } }
        """;

    /// <summary>FABRIC-CLOSE1t: a plugin's deploy, then the close, so each quest of a chain has its own deploy before its own close.</summary>
    private const string Deployed = """
        { "workCycles": { "engine": { "quest": {
            "schemaVersion": 2, "entries": ["deploy"], "end": "close",
            "nodes": [{ "id": "deploy", "kind": "acme.deploy/deploy", "version": 1, "reach": "performed" },
                      { "id": "close", "kind": "quest.close", "version": 1, "reach": "performed" }],
            "wires": [{ "id": "a", "kind": "after", "from": "deploy", "to": "close" }] } } } }
        """;

    /// <summary>The deploy plugin's step, as its catalogue declares it.</summary>
    private static readonly NodeDeclaration DeployStep = new("acme.deploy/deploy", "acme.deploy/deploy", 1, NodeReach.Performed)
    {
        Retry = new NodeRetry(3, [30, 60]),
        Perform = new NodePerform(Idempotent: true, Reconcile: true, Transient: ["unavailable"]),
    };

    /// <summary>The plugins' steps the pass reads: none, unless a test's graph names a plugin's.</summary>
    private IReadOnlyList<NodeDeclaration> _steps = [];

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-close-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly Door _door = new();

    private readonly DateTimeOffset _now = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);

    /// <summary>The quests the pass reads, by id: each a unit of its own.</summary>
    private readonly Dictionary<string, QuestView> _quests = [];

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    /// <summary>The close door as the closer calls it: each close kept, each answer the test's (a throw is a close unanswered).</summary>
    private sealed class Door : IQuestClosePort, IClosureBindPort
    {
        public List<(string Id, string Graph)> Bound { get; } = [];

        public Task<bool> KnowsClosureAsync(CancellationToken ct) => Task.FromResult(true);

        public Task<(bool Ok, string Message)> BindQuestAsync(string id, string graph, string session, CancellationToken ct)
        {
            Bound.Add((id, graph));
            return Task.FromResult((true, "bound"));
        }

        public List<(string Quest, string Key, string Token, IReadOnlyList<QuestCloseItem> Quests)> Sent { get; } = [];

        public Func<int, QuestCloseAnswer> Answer { get; set; } = _ => new(QuestCloseOutcomes.Closed, null, "Closed.", []);

        /// <summary>FABRIC-CLOSE1u: the outcome the door keeps under each key it took, a withdrawn key's included, as its store does.</summary>
        public Dictionary<string, QuestCloseAnswer> Taken { get; } = new(StringComparer.Ordinal);

        /// <summary>FABRIC-CLOSE1u: each question of what became of a key, by the quest and the key.</summary>
        public List<(string Quest, string Key)> Asked { get; } = [];

        /// <summary>FABRIC-CLOSE1u: the loop ends after the attempt is written and before the close leaves: nothing sent, nothing taken.</summary>
        public bool Crash { get; set; }

        /// <summary>FABRIC-CLOSE1u: the door takes the close and keeps its outcome, and its answer is lost on the way back.</summary>
        public bool Lose { get; set; }

        /// <summary>FABRIC-CLOSE1u: the answer to a question of what became of a key, where the test sets one (a throw is a door not answering).</summary>
        public Func<int, QuestCloseAnswer>? Told { get; set; }

        public Task<QuestCloseAnswer> CloseQuestsAsync(string quest, string key, string token, IReadOnlyList<QuestCloseItem> quests, CancellationToken ct)
        {
            if (Crash) throw new InvalidOperationException("the loop ended before the close left");
            Sent.Add((quest, key, token, quests));
            if (Taken.TryGetValue(key, out var first)) return Task.FromResult(first);
            var answer = Answer(Sent.Count);
            Taken[key] = answer;
            return Lose ? throw new HttpRequestException("the answer was lost on its way back") : Task.FromResult(answer);
        }

        public Task<QuestCloseAnswer> CloseOutcomeAsync(string quest, string key, string token, IReadOnlyList<QuestCloseItem> quests, CancellationToken ct)
        {
            Asked.Add((quest, key));
            if (Told is { } told) return Task.FromResult(told(Asked.Count));
            if (Taken.TryGetValue(key, out var first)) return Task.FromResult(first);
            var withdrawn = new QuestCloseAnswer(QuestCloseOutcomes.Refused, QuestCloseOutcomes.Withdrawn, $"Key `{key}` was never taken, and is withdrawn.", []);
            Taken[key] = withdrawn;
            return Task.FromResult(withdrawn);
        }
    }

    /// <summary>No plugin: the close is Daoris's own, and no process is asked anything.</summary>
    private sealed class NoPlugins : IWorkRuntimePort
    {
        public Task<int> PerformAsync(string plugin, WorkPerformCall call, CancellationToken ct) => throw new InvalidOperationException("no plugin is sent anything");

        public Task<(WorkReconcileAnswer Answer, int Generation)> ReconcileStepAsync(string plugin, WorkReconcileCall call, CancellationToken ct) =>
            throw new InvalidOperationException("no plugin is asked anything");

        public int? GenerationOf(string plugin) => null;

        public void Watch(IReadOnlyDictionary<string, IReadOnlyList<WatchedUnit>> awaited)
        {
        }

        public IReadOnlyList<WorkStepHeard> TakeSteps() => [];
    }

    /// <summary>
    /// FABRIC-CLOSE1t: the deploy plugin's work process as the runtime sees it: each perform kept, a unit's left unanswered where the test says
    /// (so it is unknown, and held once its reconcile answers unknown too), each completion the test's.
    /// </summary>
    private sealed class Deploys : IWorkRuntimePort
    {
        public List<WorkPerformCall> Sent { get; } = [];

        public HashSet<string> Unanswered { get; } = new(StringComparer.Ordinal);

        public Queue<WorkStepHeard> Heard { get; } = new();

        public Task<int> PerformAsync(string plugin, WorkPerformCall call, CancellationToken ct)
        {
            Sent.Add(call);
            return Unanswered.Contains(call.Unit) ? Task.FromException<int>(new IOException("the plugin did not answer")) : Task.FromResult(1);
        }

        public Task<(WorkReconcileAnswer Answer, int Generation)> ReconcileStepAsync(string plugin, WorkReconcileCall call, CancellationToken ct) =>
            Task.FromResult((new WorkReconcileAnswer(ReconcileStates.Unknown), 1));

        public int? GenerationOf(string plugin) => 1;

        public void Watch(IReadOnlyDictionary<string, IReadOnlyList<WatchedUnit>> awaited)
        {
        }

        public IReadOnlyList<WorkStepHeard> TakeSteps()
        {
            var taken = Heard.ToList();
            Heard.Clear();
            return taken;
        }
    }

    /// <summary>The deploy's completion, reached, for the attempt its call names.</summary>
    private WorkStepHeard Reached(WorkPerformCall call) =>
        new("acme.deploy", 1, new WorkStepFrame("deploy", new WorkOutcome(WorkStates.Reached, Evidence: new WorkEvidence("acme.deploy/receipt@1:r1", null, null, "deployed")),
            _now, "e-" + call.Invocation, Invocation: call.Invocation));

    private NodeRecordFiles Files => new(_home, () => true);

    /// <summary>A new runtime over the same home: a loop's, and a loop's started again after a restart.</summary>
    private NodeRuntime Runtime(IWorkRuntimePort? plugins = null) => new(Files, plugins ?? new NoPlugins(), () => _now, () => true, _door);

    private static string Digest(string config) =>
        NodeGraphs.Digest(NodeGraphHomes.Work(DriverConfig.Parse(config), "engine", "default", WorkUnitKind.Quest).Graph!);

    private static QuestView Taken(string id, string? closure, QuestSubmissionView? submission = null) =>
        new(id, "game", "engine", "Ship " + id, "", "Taken") { Closure = closure, Submission = submission };

    private static readonly QuestSubmissionView Ready41 = new("desk", 41, null, "ready");

    private static readonly QuestSubmissionView Ready40 = new("desk", 40, null, "ready");

    private void Quest(QuestView quest) => _quests[quest.Id] = quest;

    /// <summary>Each session branch the pass reads, by name: the quest whose sessions worked it, and its tip.</summary>
    private readonly Dictionary<string, (string Quest, string Tip)> _branches = [];

    /// <summary>
    /// How many times the units were read, and what happens at each read: the fresh read right before a close is one. Counted only while
    /// <c>_onRead</c> is set, so a test counting reads sets one, a no-op where it needs nothing done.
    /// </summary>
    private int _reads;

    private Action<int>? _onRead;

    private NodeRuntimeLook Look(string config) => new(DriverConfig.Parse(config), Whole: true)
    {
        Units = _ =>
        {
            _onRead?.Invoke(++_reads);
            return Task.FromResult(new NodeUnitsRead(
                [
                    .. _quests.Values.Select(quest => new WorkUnit("quest:" + quest.Id, WorkUnitKind.Quest,
                        new WorkOwner(quest.To, "default", WorkRecord.Quests, WorkRecord.ServiceWriter),
                        new WorkSubject("#" + quest.Id, quest.Updated is { } moved ? Moments.Write(moved) : null),
                        new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "open"), []) { Records = new WorkUnitRecords(null, [], quest) }),
                    .. _branches.Select(branch => new WorkUnit("branch:engine/" + branch.Key, WorkUnitKind.Branch,
                        new WorkOwner("engine", "default", "trees", WorkRecord.DriverWriter), new WorkSubject(branch.Key, branch.Value.Tip),
                        new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "open"), [])
                    {
                        Links = [new WorkLink(WorkRelation.ProducedBy, "quest:" + branch.Value.Quest)],
                    }),
                ],
                new Dictionary<string, string> { ["engine"] = "checkout" }) { Tips = Tips() });
        },
        Steps = () => _steps,
    };

    /// <summary>What git answered of each branch's tip at a read (FABRIC-CLOSE1p), by its unit's id; null, the tips of <c>_branches</c> as they stand.</summary>
    private Dictionary<string, string?>? _tips;

    /// <summary>A read that asked no git (FABRIC-CLOSE1r): its tips are null, and a close made on it waits.</summary>
    private bool _noTips;

    /// <summary>The tips a read carries: those the test set, else each branch's own tip, as git would answer them (FABRIC-CLOSE1r).</summary>
    private Dictionary<string, string?>? Tips() =>
        _noTips ? null : _tips is not null ? new(_tips) : _branches.ToDictionary(branch => "branch:engine/" + branch.Key, branch => (string?)branch.Value.Tip);

    private NodeRecord Record(string unit = Unit) => Files.Read(unit)!;

    /// <summary>The close node's status with its binding set to <paramref name="binding"/>, written as another writer would.</summary>
    private void Bind(NodeAcceptance? binding) => Files.Edit<bool>(Unit, record => (record with
    {
        Statuses = new Dictionary<string, NodeStatus>(record.Statuses, StringComparer.Ordinal) { ["close"] = record.Statuses["close"] with { Binding = binding } },
    }, true));

    /// <summary>
    /// FABRIC-CLOSE1q: a door that closes, while its close is out, what the close's node needs to take the answer (its binding): the answer
    /// does not bind, so the attempt is held. Returns the binding taken, to put back.
    /// </summary>
    private Func<NodeAcceptance?> ClosedUnbound()
    {
        NodeAcceptance? taken = null;
        _door.Answer = sent =>
        {
            if (sent == 1)
            {
                taken = Record().Statuses["close"].Binding;
                Bind(null);
            }

            return new(QuestCloseOutcomes.Closed, null, "Closed.", []);
        };
        return () => taken;
    }

    [Fact]
    public async Task A_bound_quest_is_closed_once_its_submission_is_current_and_unheld_and_once_only()
    {
        var digest = Digest(Closing);
        var runtime = Runtime();
        Assert.True(NodeRuntime.Armed(DriverConfig.Parse(Closing)));

        // Taken and bound, with nothing submitted yet: nothing is sent.
        Quest(Taken("q7", digest));
        await runtime.PassAsync(Look(Closing), CancellationToken.None);
        Assert.Empty(_door.Sent);

        // Submitted, its evidence not read yet: held, so nothing is sent.
        Quest(Taken("q7", digest, Ready41) with { Held = true, Hold = "evidence-unread" });
        await runtime.PassAsync(Look(Closing), CancellationToken.None);
        Assert.Empty(_door.Sent);

        // Unheld: closed, on the submission judged, with a key and a token persisted first.
        Quest(Taken("q7", digest, Ready41));
        await runtime.PassAsync(Look(Closing), CancellationToken.None);
        var (quest, key, token, named) = Assert.Single(_door.Sent);
        Assert.Equal("q7", quest);
        Assert.Equal([new QuestCloseItem("q7", "desk", 41)], named);
        Assert.Equal(NodeStates.Reached, Record().Statuses["close"].State);
        Assert.Equal(NodeSource.CloseDoor, Record().Statuses["close"].By);
        var attempt = Assert.Single(Record().Attempts);
        Assert.Equal((AttemptStates.Settled, key, token), (attempt.State, attempt.Call.IdempotencyKey, attempt.Close!.Token));
        Assert.StartsWith("ctk-", token, StringComparison.Ordinal);

        // Done by the close: never sent again, and never read as the person's.
        Quest(Taken("q7", digest, Ready41) with { Status = "Done" });
        var later = await runtime.PassAsync(Look(Closing), CancellationToken.None);
        await runtime.PassAsync(Look(Closing), CancellationToken.None);
        Assert.Single(_door.Sent);
        Assert.Equal(NodeStates.Reached, Record().Statuses["close"].State);
        Assert.DoesNotContain(later.Said, line => line.Contains("closed by you", StringComparison.Ordinal));
    }

    /// <summary>
    /// FABRIC-CLOSE1u (§E5 step 4): a close whose answer was lost is settled from what its door kept under its key, asked after a restart before
    /// anything new and never sent again: closed is the reach on its key, a refusal the failure with the door's code.
    /// </summary>
    [Theory]
    [InlineData(QuestCloseOutcomes.Closed, null, NodeStates.Reached)]
    [InlineData(QuestCloseOutcomes.Refused, "held", NodeStates.Failed)]
    public async Task A_close_whose_answer_was_lost_is_settled_from_its_doors_answer_after_a_restart_and_never_sent_again(string outcome, string? code, string state)
    {
        Quest(Taken("q7", Digest(Closing), Ready41));
        _door.Answer = _ => new(outcome, code, "The door's words.", []);
        _door.Lose = true;

        await Runtime().PassAsync(Look(Closing), CancellationToken.None);
        Assert.Equal((NodeStates.Unknown, AttemptStates.Unknown), (Record().Statuses["close"].State, Assert.Single(Record().Attempts).State));

        // A new loop after a restart: the door is asked what became of the key, before anything new, and nothing is sent.
        _door.Lose = false;
        var restarted = Runtime();
        await restarted.PassAsync(Look(Closing), CancellationToken.None);
        await restarted.PassAsync(Look(Closing), CancellationToken.None);

        var sent = Assert.Single(_door.Sent);
        Assert.Equal(("q7", sent.Key), Assert.Single(_door.Asked));
        var status = Record().Statuses["close"];
        Assert.Equal(state, status.State);
        if (state == NodeStates.Reached) Assert.Equal((NodeSource.CloseDoor, "close:" + sent.Key), (status.By, status.Current!.Evidence!.Ref));
        else Assert.Equal((code, false), (status.Code, status.Transient));
        var attempt = Assert.Single(Record().Attempts);
        Assert.Equal((AttemptStates.Settled, sent.Key), (attempt.State, attempt.Call.IdempotencyKey));
        Assert.Empty(HeldAttempts.Read(Files));
    }

    /// <summary>
    /// FABRIC-CLOSE1u (§E5 steps 1 and 4; the 2026-10-18 review of the close path): a close recorded and never delivered is never sent on the
    /// evidence it was recorded on. The loop ends after the attempt is written and before the close leaves; the session's branch moves; a loop
    /// started again asks the door what became of the key, which it never took, so the key is withdrawn and nothing is sent until the work is
    /// judged again on the new tip: its deploy there first, then one close under a new key, bound to that revision.
    /// </summary>
    [Fact]
    public async Task A_close_recorded_and_never_delivered_sends_nothing_after_its_branch_moved_until_judged_again_on_the_new_tip()
    {
        _steps = [DeployStep];
        var plugins = new Deploys();
        Quest(Taken("q7", Digest(Deployed), Ready41));
        _branches["daoris/q7"] = ("q7", "c1");
        var runtime = Runtime(plugins);

        // The deploy sent and reached at c1; the close recorded at c1, and the loop ends before it leaves.
        await runtime.PassAsync(Look(Deployed), CancellationToken.None);
        plugins.Heard.Enqueue(Reached(Assert.Single(plugins.Sent)));
        _door.Crash = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.PassAsync(Look(Deployed), CancellationToken.None));
        var recorded = Record().Attempts.Single(attempt => attempt.Plugin == QuestClosing.Sender);
        Assert.Equal(AttemptStates.Recorded, recorded.State);
        Assert.Empty(_door.Sent);

        // The session commits again, and the loop starts again.
        _door.Crash = false;
        _branches["daoris/q7"] = ("q7", "c2");
        var restarted = Runtime(plugins);
        await restarted.PassAsync(Look(Deployed), CancellationToken.None);
        await restarted.PassAsync(Look(Deployed), CancellationToken.None);

        // Asked, never sent: the key withdrawn, its attempt settled as never started, and the deploy sent again for the new tip.
        Assert.Empty(_door.Sent);
        Assert.Equal(("q7", recorded.Call.IdempotencyKey), Assert.Single(_door.Asked));
        var superseded = Record().Attempts.Single(attempt => attempt.Call.Invocation == recorded.Call.Invocation);
        Assert.Equal((AttemptStates.Settled, ReconcileStates.NotStarted, QuestCloseOutcomes.Withdrawn), (superseded.State, superseded.Outcome, superseded.Code));
        Assert.Equal(2, plugins.Sent.Count);
        Assert.NotEqual(plugins.Sent[0].SubjectRevision, plugins.Sent[1].SubjectRevision);
        Assert.DoesNotContain(Record().Statuses["close"].State, new[] { NodeStates.Active, NodeStates.Unknown, NodeStates.Reached });
        Assert.Empty(HeldAttempts.Read(Files));

        // The deploy reached at c2: one close, under a new key, bound to the new revision.
        plugins.Heard.Enqueue(Reached(plugins.Sent[^1]));
        await restarted.PassAsync(Look(Deployed), CancellationToken.None);
        var (_, key, _, _) = Assert.Single(_door.Sent);
        Assert.NotEqual(recorded.Call.IdempotencyKey, key);
        var made = Record().Attempts.Single(attempt => attempt.Call.IdempotencyKey == key);
        Assert.Equal(plugins.Sent[1].SubjectRevision, made.Call.SubjectRevision);
        Assert.Equal(NodeStates.Reached, Record().Statuses["close"].State);
    }

    /// <summary>
    /// FABRIC-CLOSE1u: a close whose door answers neither the close nor the question of what became of it stays unknown, is never sent again by
    /// itself, and is offered to the person with a held close's words. Their stop stops its work, and the close is still asked after, settled
    /// as never started once its door says it never took the key.
    /// </summary>
    [Fact]
    public async Task A_close_its_door_does_not_answer_stays_unknown_is_offered_to_the_person_and_stopped_by_them()
    {
        Quest(Taken("q7", Digest(Closing), Ready41));
        _door.Answer = _ => throw new HttpRequestException("the host did not answer in time");
        _door.Told = _ => throw new HttpRequestException("the host did not answer in time");
        var runtime = Runtime();
        await runtime.PassAsync(Look(Closing), CancellationToken.None);
        await runtime.PassAsync(Look(Closing), CancellationToken.None);
        await Runtime().PassAsync(Look(Closing), CancellationToken.None);

        var sent = Assert.Single(_door.Sent);
        Assert.Equal(2, _door.Asked.Count);
        Assert.All(_door.Asked, asked => Assert.Equal(sent.Key, asked.Key));
        Assert.Equal((NodeStates.Unknown, AttemptStates.Unknown), (Record().Statuses["close"].State, Assert.Single(Record().Attempts).State));

        // Offered to the person, with a close's words: nothing to record again, so asked for again, or its work stopped.
        var listed = Assert.Single(HeldAttempts.Read(Files));
        Assert.Equal((QuestClosing.Sender, NodeStates.Unknown), (listed.Plugin, listed.NodeState));
        Assert.Equal(HeldResolutions.CloseUnkept, listed.Words);
        Assert.Contains(HeldAttempts.Lines([listed]), line => line.EndsWith("resend|stop", StringComparison.Ordinal));
        var record = HeldAttempts.Word(Files, Unit, "close", listed.Attempt, HeldResolutions.Record, () => _now);
        Assert.Equal((PerformedWhat.Refused, WorkPerformer.NotKept), (record.What, record.Code));

        // The person's stop: its work stopped as theirs, nothing sent, the close still asked after and no longer offered.
        Assert.Equal(PerformedWhat.Worded, HeldAttempts.Word(Files, Unit, "close", listed.Attempt, HeldResolutions.Stop, () => _now).What);
        await Runtime().PassAsync(Look(Closing), CancellationToken.None);
        var status = Record().Statuses["close"];
        Assert.Equal((NodeStates.Stopped, NodeSource.Person), (status.State, status.By));
        Assert.Equal((AttemptStates.Unknown, (string?)null), (Assert.Single(Record().Attempts).State, Record().Attempts[0].Resolution));
        Assert.Empty(HeldAttempts.Read(Files));

        // Its door answers at last: the key never taken, so withdrawn, and the attempt settled as never started, its node left stopped.
        _door.Told = null;
        await Runtime().PassAsync(Look(Closing), CancellationToken.None);
        Assert.Single(_door.Sent);
        var attempt = Assert.Single(Record().Attempts);
        Assert.Equal((AttemptStates.Settled, ReconcileStates.NotStarted), (attempt.State, attempt.Outcome));
        Assert.Equal(NodeStates.Stopped, Record().Statuses["close"].State);
    }

    [Fact]
    public async Task A_refused_close_is_kept_with_its_words_and_said_and_never_sent_again_by_itself()
    {
        const string Words = "This close was refused when it was first sent with key `k-1`: quest `#q7`'s submission was held for the person.";
        Quest(Taken("q7", Digest(Closing), Ready41));
        _door.Answer = _ => new(QuestCloseOutcomes.Refused, "held", Words, []);
        var runtime = Runtime();

        var first = await runtime.PassAsync(Look(Closing), CancellationToken.None);
        await runtime.PassAsync(Look(Closing), CancellationToken.None);
        await Runtime().PassAsync(Look(Closing), CancellationToken.None);

        Assert.Single(_door.Sent);
        var status = Record().Statuses["close"];
        Assert.Equal((NodeStates.Failed, "held", false), (status.State, status.Code, status.Transient));
        var attempt = Assert.Single(Record().Attempts);
        Assert.Equal((AttemptStates.Settled, Words), (attempt.State, attempt.Close!.Words));
        Assert.Contains(first.Said, line => line.Contains(Words, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_chain_closes_at_its_last_quest_in_one_close_and_a_part_lost_since_is_said_once_naming_it()
    {
        var digest = Digest(Closing);
        Quest(Taken("q6", digest, Ready40) with { Then = [new QuestStepView("engine", "Ship q7", "")] });
        Quest(Taken("q7", digest, Ready41) with { Parent = "q6" });
        var runtime = Runtime();

        await runtime.PassAsync(Look(Closing), CancellationToken.None);

        // One close, from the chain's last quest here, naming the chain oldest first; the first quest's own close waits for it.
        var (quest, _, _, named) = Assert.Single(_door.Sent);
        Assert.Equal("q7", quest);
        Assert.Equal([new QuestCloseItem("q6", "desk", 40), new QuestCloseItem("q7", "desk", 41)], named);

        // Both Done by it: the first quest's close is reached by the chain's, never read as the person's.
        Quest(_quests["q6"] with { Status = "Done" });
        Quest(_quests["q7"] with { Status = "Done" });
        var closed = await runtime.PassAsync(Look(Closing), CancellationToken.None);
        Assert.Equal(NodeStates.Reached, Record("quest:q6").Statuses["close"].State);
        Assert.DoesNotContain(closed.Said, line => line.Contains("closed by you", StringComparison.Ordinal));

        // A remote refused q6's Done since, its take lost to another machine and its submission forgotten with it: the graph reads partially
        // closed, said once, and nothing retried.
        Quest(_quests["q6"] with { Status = "Taken", Submission = null });
        var partial = await runtime.PassAsync(Look(Closing), CancellationToken.None);
        var again = await runtime.PassAsync(Look(Closing), CancellationToken.None);
        Assert.Contains(partial.Said, line => line.Contains("partially closed", StringComparison.Ordinal) && line.Contains("#q6", StringComparison.Ordinal));
        Assert.DoesNotContain(again.Said, line => line.Contains("partially closed", StringComparison.Ordinal));
        Assert.Single(_door.Sent);
    }

    /// <summary>FABRIC-CLOSE1l, the review's finding 1: a chain that leaves this repository and comes back is walked whole.</summary>
    [Fact]
    public async Task A_chain_that_leaves_this_repository_and_comes_back_closes_at_its_last_quest_here_only_with_its_first()
    {
        var digest = Digest(Closing);
        // A → B → A: q5 here, q6 in another repository, q7 here again; the first A held for the person since.
        Quest(Taken("q5", digest, Ready40) with
        {
            Then = [new QuestStepView("ui", "Ship q6", ""), new QuestStepView("engine", "Ship q7", "")], Held = true, Hold = "unreviewed",
        });
        Quest(new QuestView("q6", "game", "ui", "Ship q6", "", "Done") { Parent = "q5", Then = [new QuestStepView("engine", "Ship q7", "")] });
        Quest(Taken("q7", digest, Ready41) with { Parent = "q6" });
        var runtime = Runtime();

        // The first A is held: the last A's close waits for it, and nothing is sent.
        await runtime.PassAsync(Look(Closing), CancellationToken.None);
        Assert.Empty(_door.Sent);

        // Released: one close from the last A names both A's, oldest first, and not B, which is its own repository's.
        Quest(_quests["q5"] with { Held = false, Hold = null });
        await runtime.PassAsync(Look(Closing), CancellationToken.None);
        var (quest, _, _, named) = Assert.Single(_door.Sent);
        Assert.Equal("q7", quest);
        Assert.Equal([new QuestCloseItem("q5", "desk", 40), new QuestCloseItem("q7", "desk", 41)], named);
    }

    /// <summary>
    /// FABRIC-CLOSE1l, the review's finding 5 (§E5 step 1): git, the binding and the chain are read again right before the close, so a commit
    /// landing after the evaluation judged the close never closes on that stale judgement; the next pass judges the new tip and closes.
    /// </summary>
    [Fact]
    public async Task A_commit_after_the_evaluations_read_refuses_the_close_and_the_next_pass_closes_at_the_new_tip()
    {
        Quest(Taken("q7", Digest(Closing), Ready41));
        _branches["daoris/q7"] = ("q7", "c1");
        // The session commits once more between the pass's read and the close: the second read is the fresh one before it.
        _onRead = read =>
        {
            if (read == 2) _branches["daoris/q7"] = ("q7", "c2");
        };
        var runtime = Runtime();

        await runtime.PassAsync(Look(Closing), CancellationToken.None);
        Assert.Empty(_door.Sent);
        Assert.Empty(Record().Attempts);

        await runtime.PassAsync(Look(Closing), CancellationToken.None);
        Assert.Single(_door.Sent);
        Assert.Equal(NodeStates.Reached, Record().Statuses["close"].State);
    }

    /// <summary>
    /// FABRIC-CLOSE1p: a branch whose tip git did not answer at the read right before the close leaves the close waiting, said naming the branch,
    /// and read once per pass; once git answers it, the close is sent.
    /// </summary>
    [Fact]
    public async Task A_branch_tip_git_did_not_answer_leaves_the_close_waiting_until_it_does()
    {
        Quest(Taken("q7", Digest(Closing), Ready41));
        _branches["daoris/q7"] = ("q7", "c1");
        _tips = new() { ["branch:engine/daoris/q7"] = "c1" };
        // The fresh read right before the close finds git could not say where the branch stands.
        _onRead = read =>
        {
            if (read == 2) _tips["branch:engine/daoris/q7"] = null;
        };
        var runtime = Runtime();

        var waiting = await runtime.PassAsync(Look(Closing), CancellationToken.None);
        Assert.Empty(_door.Sent);
        Assert.Equal(2, _reads);
        Assert.Single(waiting.Said, line => line.Contains("git did not say where `daoris/q7`", StringComparison.Ordinal));

        _onRead = null;
        _tips["branch:engine/daoris/q7"] = "c1";
        await runtime.PassAsync(Look(Closing), CancellationToken.None);
        Assert.Single(_door.Sent);
    }

    /// <summary>
    /// FABRIC-CLOSE1r: a read that carried no tips (a future read that forgot them) never closes: the close waits and says why, so the defect
    /// CLOSE1p mended cannot return silently; a read that carries them closes.
    /// </summary>
    [Fact]
    public async Task A_read_without_git_tips_leaves_the_close_waiting_and_says_why()
    {
        Quest(Taken("q7", Digest(Closing), Ready41));
        _branches["daoris/q7"] = ("q7", "c1");
        _noTips = true;
        var runtime = Runtime();

        var waiting = await runtime.PassAsync(Look(Closing), CancellationToken.None);
        Assert.Empty(_door.Sent);
        Assert.Empty(Record().Attempts);
        Assert.Single(waiting.Said, line => line.Contains("git's tips were not read for this look", StringComparison.Ordinal));

        _noTips = false;
        await runtime.PassAsync(Look(Closing), CancellationToken.None);
        Assert.Single(_door.Sent);
    }

    /// <summary>FABRIC-CLOSE1l, the review's finding 6: a graph edited since a quest was bound leaves it closed by the graph it was bound to.</summary>
    [Fact]
    public async Task A_graph_edit_leaves_a_bound_quest_closed_by_the_graph_it_was_bound_to()
    {
        var digest = Digest(Closing);
        Quest(Taken("q7", digest));
        var runtime = Runtime();
        await runtime.PassAsync(Look(Closing), CancellationToken.None);

        // The person puts a task before the close since: the quest bound to the graph before it closes by that graph, without the task.
        Quest(Taken("q7", digest, Ready41));
        await runtime.PassAsync(Look(Reviewed), CancellationToken.None);

        var (_, _, token, named) = Assert.Single(_door.Sent);
        Assert.Equal([new QuestCloseItem("q7", "desk", 41)], named);
        Assert.False(Record().Statuses.ContainsKey("qa"));
        Assert.Equal(NodeStates.Reached, Record().Statuses["close"].State);
        Assert.NotNull(token);
    }

    /// <summary>
    /// FABRIC-CLOSE1l, the review's finding 6: an unanswered close is reconciled whether or not a closure graph still arms the runtime;
    /// FABRIC-CLOSE1u: by asking its door what became of its key, never by sending it again.
    /// </summary>
    [Fact]
    public async Task A_close_unanswered_is_reconciled_after_the_last_closure_graph_is_removed()
    {
        Quest(Taken("q7", Digest(Closing), Ready41));
        _door.Lose = true;
        await Runtime().PassAsync(Look(Closing), CancellationToken.None);

        // The graph removed, and the loop started again: the door is still asked what became of the key before anything else.
        _door.Lose = false;
        await Runtime().PassAsync(Look("{}"), CancellationToken.None);

        var sent = Assert.Single(_door.Sent);
        Assert.Equal(("q7", sent.Key), Assert.Single(_door.Asked));
        Assert.Equal((AttemptStates.Settled, WorkStates.Reached), (Assert.Single(Record().Attempts).State, Record().Attempts[0].Outcome));
    }

    /// <summary>
    /// FABRIC-CLOSE1l, the review's finding 8: a hold an ancestor gained between the read and the send refuses the close; once it is
    /// released, the refusal's cause is gone and the close is made again under a fresh key, as the terminal quest's own release does.
    /// </summary>
    [Fact]
    public async Task An_ancestors_hold_released_after_a_held_refusal_is_retried_with_a_fresh_key()
    {
        var digest = Digest(Closing);
        Quest(Taken("q6", digest, Ready40) with { Then = [new QuestStepView("engine", "Ship q7", "")], Updated = _now });
        Quest(Taken("q7", digest, Ready41) with { Parent = "q6", Updated = _now });
        _door.Answer = sent => sent == 1
            ? new(QuestCloseOutcomes.Refused, "held", "Quest `#q6`'s submission is held for the person.", [])
            : new(QuestCloseOutcomes.Closed, null, "Closed.", []);
        var runtime = Runtime();
        await runtime.PassAsync(Look(Closing), CancellationToken.None);
        Assert.Equal(NodeStates.Failed, Record().Statuses["close"].State);

        // q6's hold gained and released since: it moved, and nothing holds it now.
        Quest(_quests["q6"] with { Updated = _now.AddMinutes(2) });
        await runtime.PassAsync(Look(Closing), CancellationToken.None);
        await runtime.PassAsync(Look(Closing), CancellationToken.None);

        Assert.Equal(2, _door.Sent.Count);
        Assert.NotEqual(_door.Sent[0].Key, _door.Sent[1].Key);
        Assert.Equal(NodeStates.Reached, Record().Statuses["close"].State);
    }

    /// <summary>
    /// FABRIC-CLOSE1t: a two-quest chain under <see cref="Deployed"/>, each quest's deploy sent, the first's left unanswered: the second's deploy is
    /// then reached, and the first's held, since its reconcile answers unknown too. Returns the runtime, its plugin and the pass that found it held.
    /// </summary>
    private async Task<(NodeRuntime Runtime, Deploys Plugins, NodeRuntimePass Held)> ChainHeldAsync()
    {
        var digest = Digest(Deployed);
        _steps = [DeployStep];
        var plugins = new Deploys();
        plugins.Unanswered.Add("quest:q6");
        Quest(Taken("q6", digest, Ready40) with { Then = [new QuestStepView("engine", "Ship q7", "")], Updated = _now });
        Quest(Taken("q7", digest, Ready41) with { Parent = "q6", Updated = _now });
        var runtime = Runtime(plugins);

        await runtime.PassAsync(Look(Deployed), CancellationToken.None);
        Assert.Equal(["quest:q6", "quest:q7"], plugins.Sent.Select(call => call.Unit).Order(StringComparer.Ordinal));

        plugins.Heard.Enqueue(Reached(plugins.Sent.Single(call => call.Unit == "quest:q7")));
        var held = await runtime.PassAsync(Look(Deployed), CancellationToken.None);
        Assert.Equal(AttemptStates.Held, Assert.Single(Record("quest:q6").Attempts).State);
        Assert.Equal(NodeStates.Reached, Record().Statuses["deploy"].State);
        return (runtime, plugins, held);
    }

    /// <summary>The process starts made within the test, by program: a close's judgement of its chain starts none.</summary>
    private static IDisposable Counting(List<string> starts) => ProcessStarts.Within(info =>
    {
        lock (starts) starts.Add(Path.GetFileNameWithoutExtension(info.FileName));
        return System.Diagnostics.Process.Start(info);
    });

    /// <summary>
    /// FABRIC-CLOSE1t (§E5 step 1; the 2026-10-18 review of the close path): a chain's close is sent only once every quest it names has its own
    /// graph's prerequisites to its close reached. The first quest published the second while its deploy stays held: the second's own deploy
    /// reached, no close is sent, and each pass says once which quest and which node it waits for. No process is started for it.
    /// </summary>
    [Fact]
    public async Task A_chains_close_waits_while_its_first_quests_deploy_is_held_and_says_which_quest_and_node()
    {
        var starts = new List<string>();
        using var counting = Counting(starts);
        var (runtime, _, held) = await ChainHeldAsync();
        var again = await runtime.PassAsync(Look(Deployed), CancellationToken.None);

        Assert.Empty(_door.Sent);
        foreach (var pass in new[] { held, again })
        {
            var said = Assert.Single(pass.Said, line => line.Contains("its close waits for #q6", StringComparison.Ordinal));
            Assert.Contains("`deploy`", said, StringComparison.Ordinal);
            Assert.Contains("held", said, StringComparison.Ordinal);
        }

        Assert.Empty(starts);
    }

    /// <summary>FABRIC-CLOSE1t: once the first quest's deploy is reached (here by the person's word on its held attempt), one close names both.</summary>
    [Fact]
    public async Task A_chains_close_names_both_quests_once_its_first_quests_deploy_is_reached()
    {
        var starts = new List<string>();
        using var counting = Counting(starts);
        var (runtime, plugins, _) = await ChainHeldAsync();
        Assert.Empty(_door.Sent);

        Assert.Equal(PerformedWhat.Worded, new WorkPerformer(Files, plugins, () => _now).Word("quest:q6", "deploy", 1, HeldResolutions.Happened).What);
        var reached = await runtime.PassAsync(Look(Deployed), CancellationToken.None);

        Assert.Equal(NodeStates.Reached, Record("quest:q6").Statuses["deploy"].State);
        var (quest, _, _, named) = Assert.Single(_door.Sent);
        Assert.Equal("q7", quest);
        Assert.Equal([new QuestCloseItem("q6", "desk", 40), new QuestCloseItem("q7", "desk", 41)], named);
        Assert.DoesNotContain(reached.Said, line => line.Contains("its close waits for", StringComparison.Ordinal));
        Assert.Empty(starts);
    }

    /// <summary>
    /// FABRIC-CLOSE1t: a quest a chain's close would name whose own node record does not read is never closed on a guess: the close waits and
    /// says so, and is sent once the record reads.
    /// </summary>
    [Fact]
    public async Task A_chains_close_waits_while_its_first_quests_node_record_does_not_read()
    {
        var digest = Digest(Closing);
        Quest(Taken("q6", digest, Ready40) with { Then = [new QuestStepView("engine", "Ship q7", "")] });
        Quest(Taken("q7", digest, Ready41) with { Parent = "q6" });
        var unreadable = NodeRecordFiles.PathOf(_home, "quest:q6");
        Directory.CreateDirectory(Path.GetDirectoryName(unreadable)!);
        File.WriteAllText(unreadable, "{ not a record");
        var runtime = Runtime();

        var waiting = await runtime.PassAsync(Look(Closing), CancellationToken.None);
        Assert.Empty(_door.Sent);
        var said = Assert.Single(waiting.Said, line => line.Contains("its close waits for #q6", StringComparison.Ordinal));
        Assert.Contains("node record does not read", said, StringComparison.Ordinal);

        File.Delete(unreadable);
        await runtime.PassAsync(Look(Closing), CancellationToken.None);
        var (_, _, _, named) = Assert.Single(_door.Sent);
        Assert.Equal([new QuestCloseItem("q6", "desk", 40), new QuestCloseItem("q7", "desk", 41)], named);
    }

    /// <summary>
    /// FABRIC-CLOSE1t: an unarmed look is as it was. A chain bound to a graph the choices no longer hold, and no graph kept, reads no units,
    /// keeps no record, sends and says nothing, and starts no process.
    /// </summary>
    [Fact]
    public async Task An_unarmed_look_reads_no_units_judges_no_chain_and_starts_no_process()
    {
        var digest = Digest(Deployed);
        _steps = [DeployStep];
        Quest(Taken("q6", digest, Ready40) with { Then = [new QuestStepView("engine", "Ship q7", "")] });
        Quest(Taken("q7", digest, Ready41) with { Parent = "q6" });
        var plugins = new Deploys();
        var starts = new List<string>();
        NodeRuntimePass pass;
        using (Counting(starts)) pass = await Runtime(plugins).PassAsync(Look("{}"), CancellationToken.None);

        Assert.False(pass.Armed);
        Assert.Equal(0, _reads);
        Assert.Empty(plugins.Sent);
        Assert.Empty(_door.Sent);
        Assert.Empty(_door.Asked);
        Assert.Empty(pass.Said);
        Assert.False(Directory.Exists(NodeRecordFiles.FolderOf(_home)));
        Assert.Empty(starts);
    }

    [Fact]
    public async Task The_persons_own_done_ends_the_graph_as_overridden_and_stops_its_nodes()
    {
        var digest = Digest(Reviewed);
        Quest(Taken("q7", digest, Ready41));
        var runtime = Runtime();
        await runtime.PassAsync(Look(Reviewed), CancellationToken.None);
        Assert.Equal(NodeStates.Ready, Record().Statuses["qa"].State);

        // The person's done, before the work's end.
        Quest(_quests["q7"] with { Status = "Done" });
        var overridden = await runtime.PassAsync(Look(Reviewed), CancellationToken.None);
        var again = await runtime.PassAsync(Look(Reviewed), CancellationToken.None);

        Assert.Empty(_door.Sent);
        Assert.Equal((NodeStates.Stopped, NodeStates.Stopped), (Record().Statuses["qa"].State, Record().Statuses["close"].State));
        var said = Assert.Single(overridden.Said, line => line.Contains("closed by you before its end", StringComparison.Ordinal));
        Assert.Contains("qa", said, StringComparison.Ordinal);
        Assert.DoesNotContain(again.Said, line => line.Contains("closed by you", StringComparison.Ordinal));
    }

    /// <summary>
    /// FABRIC-CLOSE1q, part 1: a close's authority is the submission it judged, not the quest's id. The same commit made ready again is
    /// another submission (another operation, its machine and sequence), so the person's done on it is theirs, never reached through the
    /// close kept for the one before.
    /// </summary>
    [Fact]
    public async Task A_close_kept_for_one_submission_is_never_the_authority_for_another_of_the_same_commit()
    {
        var digest = Digest(Closing);
        var runtime = Runtime();

        // S1 closed by the close, and its Done read.
        Quest(Taken("q7", digest, Ready41));
        await runtime.PassAsync(Look(Closing), CancellationToken.None);
        Quest(_quests["q7"] with { Status = "Done" });
        await runtime.PassAsync(Look(Closing), CancellationToken.None);
        var key = Assert.Single(_door.Sent).Key;
        Assert.Equal(NodeStates.Reached, Record().Statuses["close"].State);
        var kept = Record().Attempts.ToList();

        // Its take lost since, then taken again here: S2, the same commit made ready again by a new operation, its evidence not read yet.
        Quest(_quests["q7"] with { Status = "Taken", Submission = null });
        await runtime.PassAsync(Look(Closing), CancellationToken.None);
        var s2 = new QuestSubmissionView("desk", 43, null, "ready");
        Quest(_quests["q7"] with { Submission = s2, Held = true, Hold = "evidence-unread" });
        await runtime.PassAsync(Look(Closing), CancellationToken.None);

        // The person closes S2 before the close does: overridden, its pending node stopped, and S1's close never borrowed.
        Quest(_quests["q7"] with { Status = "Done", Submission = s2, Held = false, Hold = null });
        var closed = await runtime.PassAsync(Look(Closing), CancellationToken.None);
        await runtime.PassAsync(Look(Closing), CancellationToken.None);

        Assert.Contains(closed.Said, line => line.Contains("closed by you before its end", StringComparison.Ordinal));
        Assert.Equal(NodeStates.Stopped, Record().Statuses["close"].State);
        Assert.NotEqual(NodeSource.CloseDoor, Record().Statuses["close"].By);
        Assert.Single(_door.Sent);
        Assert.DoesNotContain(Record().Attempts.Skip(kept.Count), attempt => attempt.Call.IdempotencyKey == key);
        Assert.Equal(kept.Select(Kept), Record().Attempts.Take(kept.Count).Select(Kept));

        static (string, string, string, string?, string) Kept(NodeAttempt attempt) =>
            (attempt.Call.Invocation, attempt.Plugin, attempt.State, attempt.Outcome, attempt.Call.IdempotencyKey);
    }

    /// <summary>
    /// FABRIC-CLOSE1q, part 2: a close its door made, whose answer its node could not take, is held as the door's: no override is read and
    /// no plugin's word offered. Each of its recoveries, said and then applied by a loop started again, keeps the one key: the answer
    /// recorded again, asked for again with the same close, or its work's further nodes stopped with the close's answer kept. The quest's
    /// Done stands, and no second close is made. FABRIC-CLOSE1u: asked for again is the door's answer under the key, the close never sent again.
    /// </summary>
    [Theory]
    [InlineData(HeldResolutions.Record, NodeStates.Reached, 0)]
    [InlineData(HeldResolutions.Resend, NodeStates.Reached, 1)]
    [InlineData(HeldResolutions.Stop, NodeStates.Stopped, 0)]
    public async Task A_close_made_whose_answer_did_not_bind_is_held_as_the_doors_and_each_recovery_keeps_its_one_key(string word, string state, int asks)
    {
        Quest(Taken("q7", Digest(Closing), Ready41));
        var binding = ClosedUnbound();
        var held = await Runtime().PassAsync(Look(Closing), CancellationToken.None);

        // The door closed it: the quest reads Done, and the close stays held as the door's, never the person's.
        Quest(_quests["q7"] with { Status = "Done" });
        var after = await Runtime().PassAsync(Look(Closing), CancellationToken.None);
        var listed = Assert.Single(HeldAttempts.Read(Files));
        Assert.Equal((QuestClosing.Sender, WorkStates.Reached), (listed.Plugin, listed.Outcome));
        Assert.Equal(HeldResolutions.Close, listed.Words);
        Assert.Contains("was closed", HeldAttempts.WhySaid(listed), StringComparison.Ordinal);
        Assert.Contains(HeldAttempts.Lines([listed]), line => line.EndsWith("record|resend|stop", StringComparison.Ordinal));
        Assert.DoesNotContain(held.Said.Concat(after.Said), line => line.Contains("closed by you", StringComparison.Ordinal));

        // The person's word, applied by a loop started again since.
        if (word != HeldResolutions.Stop) Bind(binding());
        Assert.Equal(PerformedWhat.Worded, HeldAttempts.Word(Files, Unit, "close", listed.Attempt, word, () => _now).What);
        var restarted = Runtime();
        var applied = await restarted.PassAsync(Look(Closing), CancellationToken.None);
        await restarted.PassAsync(Look(Closing), CancellationToken.None);

        Assert.Single(_door.Sent);
        Assert.Equal(asks, _door.Asked.Count);
        Assert.All(_door.Asked, each => Assert.Equal(("q7", _door.Sent[0].Key), each));
        var status = Record().Statuses["close"];
        Assert.Equal(state, status.State);
        if (state == NodeStates.Reached) Assert.Equal((NodeSource.CloseDoor, "close:" + _door.Sent[0].Key), (status.By, status.Current!.Evidence!.Ref));
        var attempt = Record().Attempts.Single(each => each.Plugin == QuestClosing.Sender);
        Assert.Equal((AttemptStates.Settled, WorkStates.Reached, (string?)null), (attempt.State, attempt.Outcome, attempt.Resolution));
        Assert.Empty(HeldAttempts.Read(Files));
        Assert.DoesNotContain(applied.Said, line => line.Contains("closed by you", StringComparison.Ordinal));
    }

    /// <summary>
    /// FABRIC-CLOSE1q: neither of a plugin's words is a held close's, whose outcome is its door's answer: each is refused at the door, and one
    /// recorded before this build is taken back, saying why, never failing the node or making a second close. <c>record</c> is offered only
    /// where the door's answer is kept.
    /// </summary>
    [Fact]
    public async Task A_held_close_takes_no_plugin_word_and_one_recorded_before_is_taken_back_never_a_second_close()
    {
        Quest(Taken("q7", Digest(Closing), Ready41));
        ClosedUnbound();
        var runtime = Runtime();
        await runtime.PassAsync(Look(Closing), CancellationToken.None);
        var held = Assert.Single(HeldAttempts.Read(Files));

        foreach (var word in new[] { HeldResolutions.Happened, HeldResolutions.DidNot })
        {
            var refused = HeldAttempts.Word(Files, Unit, "close", held.Attempt, word, () => _now);
            Assert.Equal((PerformedWhat.Refused, WorkPerformer.NotCloseWord), (refused.What, refused.Code));
            Assert.Contains("record, resend or stop", HeldAttempts.Said(refused, held.Attempt, word), StringComparison.Ordinal);
        }

        // A word FABRIC-PERFORM1g's build recorded on it: taken back, saying why; nothing failed, nothing sent.
        Files.Edit<bool>(Unit, record => (record with { Attempts = [record.Attempts[0] with { Resolution = HeldResolutions.DidNot, ResolvedAt = _now }] }, true));
        await runtime.PassAsync(Look(Closing), CancellationToken.None);
        await Runtime().PassAsync(Look(Closing), CancellationToken.None);

        var taken = Assert.Single(HeldAttempts.Read(Files));
        Assert.Equal(((string?)null, WorkPerformer.NotCloseWord), (taken.Resolution, taken.Why));
        Assert.Contains("door", HeldAttempts.WhySaid(taken), StringComparison.Ordinal);
        Assert.Single(_door.Sent);
        Assert.Equal(1, Record().Statuses["close"].Attempt);
        Assert.Contains(Record().Statuses["close"].State, new[] { NodeStates.Active, NodeStates.Unknown });

        // A held close whose door's answer is not kept is asked for again or stopped; there is nothing to record again.
        Files.Edit<bool>(Unit, record => (record with { Attempts = [record.Attempts[0] with { Outcome = null }] }, true));
        Assert.Equal([HeldResolutions.Resend, HeldResolutions.Stop], Assert.Single(HeldAttempts.Read(Files)).Words);
        var unkept = HeldAttempts.Word(Files, Unit, "close", held.Attempt, HeldResolutions.Record, () => _now);
        Assert.Equal((PerformedWhat.Refused, WorkPerformer.NotKept), (unkept.What, unkept.Code));
        Assert.Contains("say resend or stop", HeldAttempts.Said(unkept, held.Attempt, HeldResolutions.Record), StringComparison.Ordinal);
    }

    /// <summary>
    /// FABRIC-CLOSE1q2 (D173's CLOSE1q, CLOSE1u and CLOSE1c notes): a close for S1 is left unanswered when the person reopens S1, then S2 is
    /// made ready at S1's commit, and the person's word on S1's close is applied by a loop started again. Its door took the close only after
    /// the reopen, so its question answers <c>stale</c>. Whether the word was given before the loop read S2, or while the loop asked the door
    /// with S2's deploy already sent, it never reaches S2: <c>record</c> is refused at the door, <c>resend</c> asks after S1's key alone, and
    /// <c>stop</c> stops nothing of S2's work and is taken back, saying why. S2's deploy stays live, and S2 closes under a key of its own.
    /// </summary>
    [Theory]
    [InlineData(HeldResolutions.Record, false)]
    [InlineData(HeldResolutions.Resend, false)]
    [InlineData(HeldResolutions.Stop, false)]
    [InlineData(HeldResolutions.Record, true)]
    [InlineData(HeldResolutions.Resend, true)]
    [InlineData(HeldResolutions.Stop, true)]
    public async Task A_word_on_a_close_left_unanswered_across_a_reopen_and_a_restart_never_reaches_the_next_submission_at_its_commit(string word, bool started)
    {
        _steps = [DeployStep];
        var plugins = new Deploys();
        Quest(Taken("q7", Digest(Deployed), Ready41) with { Updated = _now });
        _branches["daoris/q7"] = ("q7", "c1");
        var runtime = Runtime(plugins);

        // S1's deploy reached; its close sent, taken by its door only after the person reopened S1, so refused stale, and its answer lost.
        await runtime.PassAsync(Look(Deployed), CancellationToken.None);
        plugins.Heard.Enqueue(Reached(Assert.Single(plugins.Sent)));
        _door.Answer = _ => new(QuestCloseOutcomes.Refused, "stale", "Quest #q7's submission desk/41 is not its current one.", []);
        _door.Lose = true;
        _door.Told = _ => throw new HttpRequestException("the host did not answer in time");
        await runtime.PassAsync(Look(Deployed), CancellationToken.None);
        var s1 = Assert.Single(_door.Sent);
        Assert.Equal([new QuestCloseItem("q7", "desk", 41)], s1.Quests);
        var listed = Assert.Single(HeldAttempts.Read(Files));
        Assert.Equal((QuestClosing.Sender, NodeStates.Unknown), (listed.Plugin, listed.NodeState));

        // S2: the same commit made ready again after the reopen, by a new operation.
        Quest(_quests["q7"] with { Submission = new QuestSubmissionView("desk", 43, null, "ready"), Updated = _now.AddMinutes(5) });

        // The person's word: given before the loop read S2, or while it asked the door, its pass then sending S2's deploy.
        WorkPerformed? worded = null;
        void Say() => worded ??= HeldAttempts.Word(Files, Unit, "close", listed.Attempt, word, () => _now);
        if (started)
        {
            _door.Told = _ =>
            {
                Say();
                throw new HttpRequestException("the host did not answer in time");
            };
            await runtime.PassAsync(Look(Deployed), CancellationToken.None);
            Assert.Equal(2, plugins.Sent.Count);
            Assert.Equal(NodeStates.Active, Record().Statuses["deploy"].State);
        }
        else Say();

        if (word == HeldResolutions.Record) Assert.Equal((PerformedWhat.Refused, WorkPerformer.NotKept), (worded!.What, worded.Code));
        else Assert.Equal(PerformedWhat.Worded, worded!.What);

        // A loop started again applies it, its door now answering what it kept under S1's key.
        _door.Told = null;
        var restarted = Runtime(plugins);
        var applied = await restarted.PassAsync(Look(Deployed), CancellationToken.None);
        await restarted.PassAsync(Look(Deployed), CancellationToken.None);

        // S1's key alone asked after and never sent again; its attempt settled on its door's stale, the word cleared.
        Assert.Single(_door.Sent);
        Assert.NotEmpty(_door.Asked);
        Assert.All(_door.Asked, each => Assert.Equal(("q7", s1.Key), each));
        var attempt = Record().Attempts.Single(each => each.Call.IdempotencyKey == s1.Key);
        Assert.Equal((AttemptStates.Settled, WorkStates.Failed, "stale", (string?)null), (attempt.State, attempt.Outcome, attempt.Code, attempt.Resolution));

        // S2's work untouched: its deploy live on S2's revision (asked after where an earlier loop sent it), its close neither stopped nor
        // settled on S1's answer.
        var deploy = Record().Statuses["deploy"];
        Assert.Equal((started ? NodeStates.Unknown : NodeStates.Active, 2), (deploy.State, plugins.Sent.Count));
        Assert.NotEqual(NodeSource.Person, deploy.By);
        Assert.NotEqual(plugins.Sent[0].SubjectRevision, plugins.Sent[1].SubjectRevision);
        var close = Record().Statuses["close"];
        Assert.DoesNotContain(close.State, new[] { NodeStates.Stopped, NodeStates.Reached, NodeStates.Failed });
        Assert.DoesNotContain(HeldAttempts.Read(Files), held => held.Plugin == QuestClosing.Sender);

        // The stop taken back, saying why: nothing of S2's work is S1's close's to stop.
        if (word == HeldResolutions.Stop)
        {
            var refused = Assert.Single(applied.Performed, done => done.Code == WorkPerformer.SubmissionMoved);
            Assert.Equal((Unit, "close", PerformedWhat.Refused), (refused.Unit, refused.Node, refused.What));
            Assert.Contains(applied.Said, line => line.Contains("reopened or replaced", StringComparison.Ordinal));
        }

        // S2's deploy reached: S2 closes under a key of its own, naming S2 alone.
        plugins.Heard.Enqueue(Reached(plugins.Sent[^1]));
        _door.Lose = false;
        _door.Answer = _ => new(QuestCloseOutcomes.Closed, null, "Closed.", []);
        await restarted.PassAsync(Look(Deployed), CancellationToken.None);
        Assert.Equal(2, _door.Sent.Count);
        Assert.NotEqual(s1.Key, _door.Sent[1].Key);
        Assert.Equal([new QuestCloseItem("q7", "desk", 43)], _door.Sent[1].Quests);
        Assert.Equal(NodeStates.Reached, Record().Statuses["close"].State);
    }

    /// <summary>
    /// FABRIC-CLOSE1q2: a held close for S1, its door's close kept, whose quest stands on S2 at the same commit since: <c>record</c> on it,
    /// applied by a pass that is not a whole look (a wake), is judged on the quests read afresh, never on the read before S2. It is taken back,
    /// said, and listed with why; S1's key never reaches the close node, and S2 closes under its own.
    /// </summary>
    [Fact]
    public async Task Record_on_a_held_close_for_a_replaced_submission_is_taken_back_on_a_wake_judged_on_the_quests_read_afresh()
    {
        Quest(Taken("q7", Digest(Closing), Ready41));
        var binding = ClosedUnbound();
        var runtime = Runtime();
        await runtime.PassAsync(Look(Closing), CancellationToken.None);
        var held = Assert.Single(HeldAttempts.Read(Files));
        Assert.Equal(HeldResolutions.Close, held.Words);
        var s1 = Assert.Single(_door.Sent).Key;

        // S1's Done lost since (D69), and the same commit made ready again here: S2. The person's record, applied at the loop's next wake.
        Quest(_quests["q7"] with { Submission = new QuestSubmissionView("desk", 43, null, "ready") });
        Bind(binding());
        Assert.Equal(PerformedWhat.Worded, HeldAttempts.Word(Files, Unit, "close", held.Attempt, HeldResolutions.Record, () => _now).What);
        _onRead = _ => { };
        var reads = _reads;
        var woken = await runtime.PassAsync(Look(Closing) with { Whole = false }, CancellationToken.None);

        var status = Record().Statuses["close"];
        Assert.NotEqual("close:" + s1, status.Current?.Evidence?.Ref);
        Assert.Contains(woken.Performed, done => (done.What, done.Code) == (PerformedWhat.Refused, WorkPerformer.SubmissionMoved));
        Assert.Contains(woken.Said, line => line.Contains("desk/41", StringComparison.Ordinal) && line.Contains("reopened or replaced", StringComparison.Ordinal));
        var taken = Assert.Single(HeldAttempts.Read(Files));
        Assert.Equal(((string?)null, WorkPerformer.SubmissionMoved), (taken.Resolution, taken.Why));
        Assert.Contains("reopened or replaced", HeldAttempts.WhySaid(taken), StringComparison.Ordinal);
        Assert.True(_reads > reads);

        // S1's held attempt still gates a new one (§D3): the person's resend asks its door after S1's key, which settles it as a stale one's,
        // and S2 closes under its own key.
        Assert.Single(_door.Sent);
        Assert.Equal(PerformedWhat.Worded, HeldAttempts.Word(Files, Unit, "close", held.Attempt, HeldResolutions.Resend, () => _now).What);
        await runtime.PassAsync(Look(Closing), CancellationToken.None);
        Assert.Equal(("q7", s1), Assert.Single(_door.Asked));
        Assert.Equal(2, _door.Sent.Count);
        Assert.Equal([new QuestCloseItem("q7", "desk", 43)], _door.Sent[1].Quests);
        status = Record().Statuses["close"];
        Assert.Equal((NodeStates.Reached, "close:" + _door.Sent[1].Key), (status.State, status.Current!.Evidence!.Ref));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("gph-another")]
    public async Task A_quest_no_closure_graph_binds_is_unchanged(string? closure)
    {
        Quest(Taken("q7", closure, Ready41));
        var runtime = Runtime();

        var pass = await runtime.PassAsync(Look(Closing), CancellationToken.None);
        Quest(_quests["q7"] with { Status = "Done" });
        var done = await runtime.PassAsync(Look(Closing), CancellationToken.None);

        Assert.Empty(_door.Sent);
        Assert.False(Directory.Exists(NodeRecordFiles.FolderOf(_home)));
        Assert.Empty(pass.Said.Concat(done.Said));
    }

    /// <summary>
    /// FABRIC-CLOSE1l, the review's findings 2 and 3: the bind is the take's own (CLOSE1f), so the runtime binds no quest it reads taken, at
    /// whatever moment; a quest taken unbound keeps today's done.
    /// </summary>
    [Fact]
    public async Task The_runtime_binds_no_quest_it_reads_taken_since_the_bind_is_the_takes_own()
    {
        var runtime = Runtime();
        Quest(Taken("q7", null) with { Updated = _now });

        var pass = await runtime.PassAsync(Look(Closing), CancellationToken.None);
        await runtime.PassAsync(Look(Closing), CancellationToken.None);

        Assert.Empty(_door.Bound);
        Assert.Empty(_door.Sent);
        Assert.DoesNotContain(pass.Said, line => line.Contains("closes through its graph", StringComparison.Ordinal));
    }

    /// <summary>
    /// FABRIC-CLOSE1l (CLOSE1f, CLOSE1g): what a session's start owes its quest's closure graph. The owner's install, with no closure graph,
    /// asks, binds and keeps nothing and says no `ready`; an open quest's first start names its graph for the take and keeps it; a quest
    /// bound already says `ready` asking nothing; a quest taken unbound keeps today's done.
    /// </summary>
    [Fact]
    public async Task A_start_names_its_graph_for_the_take_only_under_a_closure_graph_and_says_ready_only_where_bound()
    {
        var digest = Digest(Closing);
        var open = new QuestView("q7", "game", "engine", "Ship q7", "", "Open");

        var none = new Binds(knows: true);
        Assert.Null(await ClosureBinding.AtStartAsync(none, DriverConfig.Parse("{}"), open, "default", remote: false, _home, CancellationToken.None));
        Assert.Null(await ClosureBinding.AtStartAsync(none, DriverConfig.Parse("{}"), Taken("q7", null), "default", remote: false, _home, CancellationToken.None));
        Assert.Equal((0, 0), (none.Asked, none.Bound.Count));
        Assert.False(Directory.Exists(ClosureGraphs.FolderOf(_home)));

        var first = new Binds(knows: true);
        var named = await ClosureBinding.AtStartAsync(
            first, DriverConfig.Parse(Closing), open, "default", remote: false, _home, CancellationToken.None, session: "ses-q7");
        Assert.True(named!.Bound);
        Assert.Equal([("q7", digest)], first.Bound);
        Assert.Equal(digest, NodeGraphs.Digest(ClosureGraphs.Read(_home, digest)!));

        var unbound = new Binds(knows: true);
        Assert.Null(await ClosureBinding.AtStartAsync(unbound, DriverConfig.Parse(Closing), Taken("q7", null), "default", remote: false, _home, CancellationToken.None));
        var resumed = await ClosureBinding.AtStartAsync(unbound, DriverConfig.Parse("{}"), Taken("q7", digest), "default", remote: false, _home, CancellationToken.None);
        Assert.True(resumed!.Bound);
        Assert.Equal((0, 0), (unbound.Asked, unbound.Bound.Count));

        var remote = new Binds(knows: true);
        Assert.False((await ClosureBinding.AtStartAsync(remote, DriverConfig.Parse(Closing), open, "default", remote: true, _home, CancellationToken.None))!.Bound);
        Assert.Empty(remote.Bound);
    }

    /// <summary>The host's two doors a bind reads and writes, each counted.</summary>
    private sealed class Binds(bool knows, bool ok = true) : IClosureBindPort
    {
        public int Asked { get; private set; }

        public List<(string Id, string Graph)> Bound { get; } = [];

        /// <summary>The session each bind was named for (FABRIC-CLOSE1o).</summary>
        public List<string> Sessions { get; } = [];

        public Task<bool> KnowsClosureAsync(CancellationToken ct)
        {
            Asked++;
            return Task.FromResult(knows);
        }

        public Task<(bool Ok, string Message)> BindQuestAsync(string id, string graph, string session, CancellationToken ct)
        {
            Bound.Add((id, graph));
            Sessions.Add(session);
            return Task.FromResult(ok ? (true, $"Quest `#{id}` closes through graph `{graph}`.") : (false, $"Quest `#{id}` is taken on another machine."));
        }
    }

    /// <summary>
    /// FABRIC-CLOSE1o: the graph is named for the session opening, whose take alone binds it, and a start that names no session names no
    /// graph, so its quest closes by its done. The loop's line says once, at that session's end, that its take left the quest unbound.
    /// </summary>
    [Fact]
    public async Task A_start_names_its_graph_for_its_own_session_and_its_end_says_once_a_take_left_unbound()
    {
        var digest = Digest(Closing);
        var open = new QuestView("q7", "game", "engine", "Ship q7", "", "Open");

        var named = new Binds(knows: true);
        var bound = await ClosureBinding.AtStartAsync(
            named, DriverConfig.Parse(Closing), open, "default", remote: false, _home, CancellationToken.None, session: "ses-q7");
        Assert.True(bound!.Bound);
        Assert.Equal(["ses-q7"], named.Sessions);

        var nameless = new Binds(knows: true);
        var unnamed = await ClosureBinding.AtStartAsync(nameless, DriverConfig.Parse(Closing), open, "default", remote: false, _home, CancellationToken.None);
        Assert.False(unnamed!.Bound);
        Assert.Contains("session", unnamed.Said, StringComparison.Ordinal);
        Assert.Empty(nameless.Bound);

        // The take went unbound (its bind expired, say): the end's line says so, naming the graph's done; a bound take says nothing.
        var said = ClosureBinding.UnboundAtEnd(bound, Taken("q7", null));
        Assert.NotNull(said);
        Assert.Contains("#q7", said, StringComparison.Ordinal);
        Assert.Contains("done", said, StringComparison.Ordinal);
        Assert.Null(ClosureBinding.UnboundAtEnd(bound, Taken("q7", digest)));
        // Never taken, nothing named, or named and refused at the start: nothing to say at the end.
        Assert.Null(ClosureBinding.UnboundAtEnd(bound, open));
        Assert.Null(ClosureBinding.UnboundAtEnd(null, Taken("q7", null)));
        Assert.Null(ClosureBinding.UnboundAtEnd(unnamed, Taken("q7", null)));
        Assert.Null(ClosureBinding.UnboundAtEnd(bound, null));
    }

    [Fact]
    public async Task A_quest_is_bound_at_its_take_only_under_a_closure_graph_its_host_and_circle_know()
    {
        var quest = Taken("q7", null);

        // No closure graph: nothing asked, nothing bound, nothing said (the owner's install).
        var none = new Binds(knows: true);
        Assert.Null(await ClosureBinding.AtTakeAsync(none, DriverConfig.Parse("{}"), quest, "default", remote: false, CancellationToken.None));
        Assert.Equal((0, 0), (none.Asked, none.Bound.Count));

        // A host that does not know a bound quest: refused, saying why, and never bound.
        var older = new Binds(knows: false);
        var refused = await ClosureBinding.AtTakeAsync(older, DriverConfig.Parse(Closing), quest, "default", remote: false, CancellationToken.None);
        Assert.False(refused!.Bound);
        Assert.Contains("does not know", refused.Said, StringComparison.Ordinal);
        Assert.Empty(older.Bound);

        // A circle synced through a remote: its machines cannot be known to run this build, so it is not bound.
        var circle = new Binds(knows: true);
        var remote = await ClosureBinding.AtTakeAsync(circle, DriverConfig.Parse(Closing), quest, "default", remote: true, CancellationToken.None);
        Assert.False(remote!.Bound);
        Assert.Contains("remote", remote.Said, StringComparison.Ordinal);
        Assert.Empty(circle.Bound);

        // A host that knows it, alone: bound once, to the graph's digest.
        var knowing = new Binds(knows: true);
        var bound = await ClosureBinding.AtTakeAsync(
            knowing, DriverConfig.Parse(Closing), quest, "default", remote: false, CancellationToken.None, session: "ses-q7");
        Assert.True(bound!.Bound);
        Assert.Equal([("q7", Digest(Closing))], knowing.Bound);

        // The service refusing the bind is said in its words, and the quest closes by its done as today.
        var taken = await ClosureBinding.AtTakeAsync(
            new Binds(knows: true, ok: false), DriverConfig.Parse(Closing), quest, "default", remote: false, CancellationToken.None, session: "ses-q7");
        Assert.False(taken!.Bound);
        Assert.Contains("taken on another machine", taken.Said, StringComparison.Ordinal);
    }
}
