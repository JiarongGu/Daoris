using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-RUN1b (the fabric events design §E2, D173's FABRIC-WIRE1b note's git reads): what the evaluation runtime reads for a branch unit's
/// observed built-ins, over a stand-in port and stand-in reads. <c>pushed</c> is reached once git's remote-tracking ref holds the commit the
/// plugin answered it pushed, and an ahead <c>pull-request</c> on git's ancestry; each read once a pass, and only where such a node waits on it.
/// </summary>
public sealed class NodeRuntimeReadsTests : IDisposable
{
    private const string Plugin = "acme.deploy";
    private const string Unit = "unit-a";
    private const string Branch = "feature/q1-ship";
    private const string T1 = "1111111111111111111111111111111111111111";
    private const string T2 = "2222222222222222222222222222222222222222";
    private const string T9 = "9999999999999999999999999999999999999999";

    private static readonly DateTimeOffset Then = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    /// <summary>The plugin's performed step a branch's cycle starts after the landing's node it is wired from.</summary>
    private static readonly NodeDeclaration Notify = new(Plugin + "/notify", Plugin + "/notify", 1, NodeReach.Performed)
    {
        Perform = new NodePerform(Idempotent: true, Reconcile: true, Transient: []),
    };

    /// <summary>A repository's branches landed on a branch the plugin pushes, its cycle notifying once <paramref name="after"/> is reached.</summary>
    private static string Cycle(string after) => $$"""
        { "landings": { "engine": { "form": "branch", "pattern": "feature/{quest}-{slug}", "plugin": "acme.deploy", "cycle": {
            "schemaVersion": 2, "entries": [], "end": "notify",
            "nodes": [{ "id": "notify", "kind": "acme.deploy/notify", "version": 1, "reach": "performed" }],
            "wires": [{ "id": "w1", "kind": "after", "from": "{{after}}", "to": "notify" }] } } } }
        """;

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-runtime-reads-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly ObservedPort _port = new();

    /// <summary>What git's remote-tracking refs hold, by branch: a branch absent was not read.</summary>
    private readonly Dictionary<string, string> _remotes = new(StringComparer.Ordinal) { [Branch] = T1 };

    /// <summary>Git's ancestry answers, by (holder, held): absent, git could not say.</summary>
    private readonly Dictionary<(string, string), bool> _holds = [];

    private readonly List<(string Root, IReadOnlyCollection<string> Branches)> _remoteReads = [];

    private readonly List<(string Root, string Holder, string Held)> _holdReads = [];

    private IReadOnlyList<WorkUnit> _units = [BranchUnit(Landed())];

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private NodeRecordFiles Files => new(_home, () => true);

    private NodeRuntime Runtime() => new(Files, _port, () => Then, () => true);

    private NodeRecord Record() => Files.Read(Unit)!;

    private static LandedBranch Landed(bool pushed = true, PullRequestState? pr = null) =>
        new("engine", "default", Branch, "main", T1, "s1", "q1", null, Then)
        {
            Units = [Unit], Pushed = pushed, PushedTip = pushed ? T1 : null, Plugin = Plugin, PullRequestState = pr,
        };

    /// <summary>A branch unit as the inventory gives it, its landing among its records, its revision the landed tip.</summary>
    private static WorkUnit BranchUnit(params LandedBranch[] landings) => new(Unit, WorkUnitKind.Branch,
        new WorkOwner("engine", "default", "session-branches.json", WorkRecord.DriverWriter), new WorkSubject("daoris/s1", T1),
        new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "open"), [])
    {
        Records = new WorkUnitRecords(null, landings, null),
    };

    private NodeRuntimeLook Look(string config, bool whole = true) => new(DriverConfig.Parse(config), whole)
    {
        Units = _ => Task.FromResult(new NodeUnitsRead(_units, new Dictionary<string, string> { ["engine"] = "checkout" })),
        Steps = () => [Notify],
        Remotes = (root, branches, _) =>
        {
            _remoteReads.Add((root, branches));
            return Task.FromResult<IReadOnlyDictionary<string, string>>(branches.Where(_remotes.ContainsKey).ToDictionary(branch => branch, branch => _remotes[branch]));
        },
        Holds = (root, holder, held, _) =>
        {
            _holdReads.Add((root, holder, held));
            return Task.FromResult<bool?>(_holds.TryGetValue((holder, held), out var holds) ? holds : null);
        },
        Gates = (unit, _) =>
        {
            _gateReads.Add(unit.Id);
            return Task.FromResult(_gates.GetValueOrDefault(unit.Id));
        },
    };

    private Task<NodeRuntimePass> PassAsync(NodeRuntime runtime, string config, bool whole = true) => runtime.PassAsync(Look(config, whole), CancellationToken.None);

    /// <summary>
    /// The proof's branch cycle: handed, then pushed, then the plugin's step. The landing record reaches <c>handed</c>; <c>pushed</c> is reached
    /// once git's remote-tracking ref, read in the pass, holds the commit the plugin answered it pushed; and the performed node is sent on it.
    /// </summary>
    [Fact]
    public async Task A_branch_cycle_reaches_its_performed_node_once_gits_remote_tracking_ref_holds_the_pushed_commit()
    {
        var pass = await PassAsync(Runtime(), Cycle("pushed"));

        var pushed = Record().Statuses["pushed"];
        Assert.Equal((NodeStates.Reached, NodeSource.PluginRef), (pushed.State, pushed.By));
        Assert.Equal(NodeStates.Reached, Record().Statuses["handed"].State);
        var sent = Assert.Single(_port.Sent);
        Assert.Equal((Plugin + "/notify", T1), (sent.Step, sent.SubjectRevision));
        Assert.Contains(pass.Performed, done => (done.Node, done.What) == ("notify", PerformedWhat.Sent));

        // One read of the checkout's remote-tracking refs in the pass, naming the landed branch; no ancestry asked.
        Assert.Equal(("checkout", Branch), (Assert.Single(_remoteReads).Root, Assert.Single(Assert.Single(_remoteReads).Branches)));
        Assert.Empty(_holdReads);
    }

    /// <summary>
    /// Unread, <c>pushed</c> is unknown and never guessed; holding another commit, it is not reached; and nothing after it is sent in either.
    /// Once the ref holds the pushed commit, at a later pass, it is reached and the step is sent once.
    /// </summary>
    [Fact]
    public async Task Pushed_is_unknown_while_its_ref_is_unread_and_not_reached_while_it_holds_another_commit()
    {
        _remotes.Clear();
        var runtime = Runtime();
        await PassAsync(runtime, Cycle("pushed"));
        Assert.Equal(NodeStates.Unknown, Record().Statuses["pushed"].State);

        _remotes[Branch] = T9;
        await PassAsync(runtime, Cycle("pushed"));
        Assert.NotEqual(NodeStates.Reached, Record().Statuses["pushed"].State);
        Assert.Empty(_port.Sent);

        _remotes[Branch] = T1;
        await PassAsync(runtime, Cycle("pushed"));
        await PassAsync(runtime, Cycle("pushed"), whole: false);
        Assert.Equal(NodeStates.Reached, Record().Statuses["pushed"].State);
        Assert.Single(_port.Sent);
        Assert.Equal(3, _remoteReads.Count);
    }

    /// <summary>
    /// A pull request the platform completed from a newer commit than the landed tip is reached only on git's word that the source holds the
    /// tip (D148 point 4's third check); git silent, it is unknown; git's no, it waits.
    /// </summary>
    [Theory]
    [InlineData(true, NodeStates.Reached)]
    [InlineData(false, NodeStates.Active)]
    [InlineData(null, NodeStates.Unknown)]
    public async Task An_ahead_pull_request_is_judged_on_gits_ancestry(bool? holds, string state)
    {
        var completed = new PullRequestState(PullRequestStates.Completed) { MergeCommit = T9, SourceCommit = T2, Target = "main", Plugin = Plugin };
        _units = [BranchUnit(Landed(pr: completed))];
        if (holds is { } answer) _holds[(T2, T1)] = answer;

        await PassAsync(Runtime(), Cycle("pull-request"));

        Assert.Equal(state, Record().Statuses["pull-request"].State);
        Assert.Equal(("checkout", T2, T1), Assert.Single(_holdReads));
        Assert.Equal(holds == true ? 1 : 0, _port.Sent.Count);
    }

    /// <summary>A branch's cycle wired from <c>handed</c> alone waits on no git read and no gate: neither is asked.</summary>
    [Fact]
    public async Task A_cycle_whose_waiting_nodes_need_no_git_reads_none()
    {
        _units = [BranchUnit(Landed(pushed: false))];
        var runtime = Runtime();
        await PassAsync(runtime, Cycle("handed"));
        await PassAsync(runtime, Cycle("handed"), whole: false);

        Assert.Single(_port.Sent);
        Assert.Empty(_remoteReads);
        Assert.Empty(_holdReads);
        Assert.Empty(_gateReads);
    }

    // ---- FABRIC-RUN1c: D157's gates started in the runtime, and their gates handed in

    /// <summary>The gates each unit's work stands at, as the look's gate reads them for it: by unit, absent where none was read.</summary>
    private readonly Dictionary<string, (OpinionGateState? Opinion, ReviewGateState? Review)> _gates = new(StringComparer.Ordinal);

    private readonly List<string> _gateReads = [];

    /// <summary>
    /// A repository's quests, their work cycle asking the second opinion and the person's look before the plugin notifies: D157 §2.2's gates
    /// come before a landing, so a branch's cycle, which comes after it, never holds them.
    /// </summary>
    private const string Gated = """
        { "workCycles": { "engine": { "quest": {
            "schemaVersion": 2, "entries": ["opinion"], "end": "notify",
            "nodes": [
              { "id": "opinion", "kind": "opinion", "version": 1, "reach": "observed" },
              { "id": "look", "kind": "look", "version": 1, "reach": "observed" },
              { "id": "notify", "kind": "acme.deploy/notify", "version": 1, "reach": "performed" } ],
            "wires": [
              { "id": "w1", "kind": "after", "from": "opinion", "to": "look" },
              { "id": "w2", "kind": "after", "from": "look", "to": "notify" } ] } } } }
        """;

    private const string Quest = "quest:q1";

    /// <summary>The quest a branch's work served, as the inventory gives it: no revision, by design.</summary>
    private static WorkUnit QuestUnit() => new(Quest, WorkUnitKind.Quest, new WorkOwner("engine", "default", WorkRecord.Quests, WorkRecord.ServiceWriter),
        new WorkSubject("#q1", null), new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "open"), []);

    /// <summary>The plugin's notify, its failure path into a stop: a failure Daoris will not retry reaches the stop, and the end is never reached.</summary>
    private const string Stopping = """
        { "landings": { "engine": { "form": "merge", "cycle": {
            "schemaVersion": 2, "entries": ["notify"], "end": "notify",
            "nodes": [
              { "id": "notify", "kind": "acme.deploy/notify", "version": 1, "reach": "performed" },
              { "id": "halt", "kind": "stop", "version": 1, "reach": "observed" } ],
            "wires": [{ "id": "w1", "kind": "failed", "from": "notify", "to": "halt" }] } } } }
        """;

    private NodeRuntimeLook Gating(string config, bool whole = true) => Look(config, whole);

    private static OpinionGateState Opinion(string state, string? tip = T1) => new(state, "engine") { Tip = tip };

    private static ReviewGateState Review(string state, string? tip = T1) =>
        new(state, new ReviewDecision(ReviewLevels.Chain, null) { Repository = "engine", Choice = "off" }) { Tip = tip };

    /// <summary>
    /// The proof's runtime test: an <c>opinion</c> and a <c>look</c> are begun as observed nodes are, and the second opinion's gate, whose rule
    /// asks none here, and the look the chain's choice turns off, read for the unit once a pass, pass them over by their records; the plugin's
    /// step after them is sent on what they let go.
    /// </summary>
    [Fact]
    public async Task A_second_opinions_gate_and_a_look_turned_off_move_their_nodes_and_the_step_after_them_is_sent()
    {
        _units = [QuestUnit()];
        // As a landing door reads a gate whose rule asks no opinion: no commit is read for it (TreeGates.OpinionAsync), nothing being judged.
        _gates[Quest] = (Opinion(OpinionGateStates.None, tip: null), Review(ReviewStates.None, tip: null));
        var pass = await Runtime().PassAsync(Gating(Gated), CancellationToken.None);

        var record = Files.Read(Quest)!;
        Assert.Equal((NodeStates.Skipped, NodeSource.Opinion), (record.Statuses["opinion"].State, record.Statuses["opinion"].By));
        Assert.Equal((NodeStates.Skipped, NodeSource.Review), (record.Statuses["look"].State, record.Statuses["look"].By));
        Assert.Equal(Plugin + "/notify", Assert.Single(_port.Sent).Step);
        Assert.Contains(pass.Performed, done => (done.Node, done.What) == ("opinion", PerformedWhat.Begun));

        // The gates were read for the unit once in the pass, though both nodes and several rounds asked.
        Assert.Equal([Quest], _gateReads);
    }

    /// <summary>
    /// A gate that says nothing yet keeps its node active and what follows it waiting, at every pass; and a quest's work, which has no commit,
    /// is never reached on a settled opinion of a branch's commit (FABRIC-WIRE1g). Its rule asking none moves it then.
    /// </summary>
    [Fact]
    public async Task An_opinion_whose_gate_says_nothing_yet_waits_and_moves_once_it_is_passed_over()
    {
        _units = [QuestUnit()];
        var runtime = Runtime();
        await runtime.PassAsync(Gating(Gated), CancellationToken.None);
        Assert.Equal(NodeStates.Active, Files.Read(Quest)!.Statuses["opinion"].State);

        foreach (var state in new[] { OpinionGateStates.Reading, OpinionGateStates.Settled })
        {
            _gates[Quest] = (Opinion(state), null);
            await runtime.PassAsync(Gating(Gated), CancellationToken.None);
            Assert.Equal((state, NodeStates.Active), (state, Files.Read(Quest)!.Statuses["opinion"].State));
            Assert.Empty(_port.Sent);
        }

        _gates[Quest] = (Opinion(OpinionGateStates.None, tip: null), Review(ReviewStates.None, tip: null));
        await runtime.PassAsync(Gating(Gated), CancellationToken.None);
        Assert.Equal(NodeStates.Skipped, Files.Read(Quest)!.Statuses["opinion"].State);
        Assert.Single(_port.Sent);
    }

    /// <summary>A <c>stop</c> is begun once the failure path into it holds, and reached on that failure's receipt; the end is not reached.</summary>
    [Fact]
    public async Task A_stop_is_begun_on_its_failure_path_and_reached_on_it()
    {
        _units = [BranchUnit()];
        var runtime = Runtime();
        await PassAsync(runtime, Stopping);
        var invocation = Assert.Single(Record().Attempts).Call.Invocation;

        _port.Heard.Enqueue(new WorkStepHeard(Plugin, 1, new WorkStepFrame("notify", new WorkOutcome(WorkStates.Failed, "rejected-by-platform"), Then, "e1",
            Invocation: invocation)));
        await PassAsync(runtime, Stopping, whole: false);

        var record = Record();
        Assert.Equal(NodeStates.Failed, record.Statuses["notify"].State);
        Assert.Equal((NodeStates.Reached, NodeSource.Path), (record.Statuses["halt"].State, record.Statuses["halt"].By));
    }
}
