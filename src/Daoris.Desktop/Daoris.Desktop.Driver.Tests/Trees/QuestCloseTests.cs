using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-CLOSE1b (the fabric events design §E5; D173's FABRIC-CLOSE1a and CLOSE1e notes): <c>quest.close</c> as the graph's end, run by the
/// evaluation runtime over a stand-in close door. A bound quest is closed once its submission is current and unheld, and once only; an
/// unanswered close is sent again with the same key, after a restart too; a refusal is kept with its words and said; a chain closed only in
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

        public Task<(bool Ok, string Message)> BindQuestAsync(string id, string graph, CancellationToken ct)
        {
            Bound.Add((id, graph));
            return Task.FromResult((true, "bound"));
        }

        public List<(string Quest, string Key, string Token, IReadOnlyList<QuestCloseItem> Quests)> Sent { get; } = [];

        public Func<int, QuestCloseAnswer> Answer { get; set; } = _ => new(QuestCloseOutcomes.Closed, null, "Closed.", []);

        public Task<QuestCloseAnswer> CloseQuestsAsync(string quest, string key, string token, IReadOnlyList<QuestCloseItem> quests, CancellationToken ct)
        {
            Sent.Add((quest, key, token, quests));
            return Task.FromResult(Answer(Sent.Count));
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

    private NodeRecordFiles Files => new(_home, () => true);

    /// <summary>A new runtime over the same home: a loop's, and a loop's started again after a restart.</summary>
    private NodeRuntime Runtime() => new(Files, new NoPlugins(), () => _now, () => true, _door);

    private static string Digest(string config) =>
        NodeGraphs.Digest(NodeGraphHomes.Work(DriverConfig.Parse(config), "engine", "default", WorkUnitKind.Quest).Graph!);

    private static QuestView Taken(string id, string? closure, QuestSubmissionView? submission = null) =>
        new(id, "game", "engine", "Ship " + id, "", "Taken") { Closure = closure, Submission = submission };

    private static readonly QuestSubmissionView Ready41 = new("desk", 41, null, "ready");

    private static readonly QuestSubmissionView Ready40 = new("desk", 40, null, "ready");

    private void Quest(QuestView quest) => _quests[quest.Id] = quest;

    private NodeRuntimeLook Look(string config) => new(DriverConfig.Parse(config), Whole: true)
    {
        Units = _ => Task.FromResult(new NodeUnitsRead(
            [.. _quests.Values.Select(quest => new WorkUnit("quest:" + quest.Id, WorkUnitKind.Quest,
                new WorkOwner("engine", "default", WorkRecord.Quests, WorkRecord.ServiceWriter), new WorkSubject("#" + quest.Id, null),
                new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "open"), []) { Records = new WorkUnitRecords(null, [], quest) })],
            new Dictionary<string, string> { ["engine"] = "checkout" })),
    };

    private NodeRecord Record(string unit = Unit) => Files.Read(unit)!;

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

    [Fact]
    public async Task A_close_unanswered_is_sent_again_with_the_same_key_after_a_restart_and_closes_once()
    {
        var digest = Digest(Closing);
        Quest(Taken("q7", digest, Ready41));
        _door.Answer = sent => sent == 1 ? throw new HttpRequestException("the host did not answer in time") : new(QuestCloseOutcomes.Closed, null, "Closed.", []);

        await Runtime().PassAsync(Look(Closing), CancellationToken.None);
        Assert.Equal((NodeStates.Unknown, AttemptStates.Unknown), (Record().Statuses["close"].State, Assert.Single(Record().Attempts).State));

        // A new loop after a restart: the same close, its key, token and quests, before anything new.
        var restarted = Runtime();
        await restarted.PassAsync(Look(Closing), CancellationToken.None);
        await restarted.PassAsync(Look(Closing), CancellationToken.None);

        Assert.Equal(2, _door.Sent.Count);
        Assert.Equal((_door.Sent[0].Key, _door.Sent[0].Token), (_door.Sent[1].Key, _door.Sent[1].Token));
        Assert.Equal(_door.Sent[0].Quests, _door.Sent[1].Quests);
        Assert.Equal(NodeStates.Reached, Record().Statuses["close"].State);
        Assert.Equal(AttemptStates.Settled, Assert.Single(Record().Attempts).State);
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

    [Fact]
    public async Task A_quest_taken_while_the_loop_reads_its_closure_graph_is_bound_once_and_one_in_flight_before_is_not()
    {
        var runtime = Runtime();

        // Taken before this loop read the graph: in flight before the cutover, it keeps today's done.
        Quest(Taken("q6", null) with { Updated = _now.AddMinutes(-5) });
        await runtime.PassAsync(Look(Closing), CancellationToken.None);

        // Taken since: bound at the first look that reads the take, once.
        Quest(Taken("q7", null) with { Updated = _now });
        var pass = await runtime.PassAsync(Look(Closing), CancellationToken.None);
        await runtime.PassAsync(Look(Closing), CancellationToken.None);

        Assert.Equal([("q7", Digest(Closing))], _door.Bound);
        Assert.Contains(pass.Said, line => line.Contains("quest:q7", StringComparison.Ordinal) && line.Contains("closes through its graph", StringComparison.Ordinal));
        Assert.Empty(_door.Sent);
    }

    /// <summary>The host's two doors a bind reads and writes, each counted.</summary>
    private sealed class Binds(bool knows, bool ok = true) : IClosureBindPort
    {
        public int Asked { get; private set; }

        public List<(string Id, string Graph)> Bound { get; } = [];

        public Task<bool> KnowsClosureAsync(CancellationToken ct)
        {
            Asked++;
            return Task.FromResult(knows);
        }

        public Task<(bool Ok, string Message)> BindQuestAsync(string id, string graph, CancellationToken ct)
        {
            Bound.Add((id, graph));
            return Task.FromResult(ok ? (true, $"Quest `#{id}` closes through graph `{graph}`.") : (false, $"Quest `#{id}` is taken on another machine."));
        }
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
        var bound = await ClosureBinding.AtTakeAsync(knowing, DriverConfig.Parse(Closing), quest, "default", remote: false, CancellationToken.None);
        Assert.True(bound!.Bound);
        Assert.Equal([("q7", Digest(Closing))], knowing.Bound);

        // The service refusing the bind is said in its words, and the quest closes by its done as today.
        var taken = await ClosureBinding.AtTakeAsync(new Binds(knows: true, ok: false), DriverConfig.Parse(Closing), quest, "default", remote: false, CancellationToken.None);
        Assert.False(taken!.Bound);
        Assert.Contains("taken on another machine", taken.Said, StringComparison.Ordinal);
    }
}
