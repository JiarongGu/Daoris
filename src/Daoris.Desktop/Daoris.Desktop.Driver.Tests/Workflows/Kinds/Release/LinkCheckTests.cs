using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A holder's <i>Done outside Daoris</i> link reaches the plugin's check (W3c row 2): asked with the link and the step's options before the
/// word is applied, a check that refuses it takes the word back with its code, one that settles it credits the identity it read, and an
/// answer that names no link (an older kit's) leaves the word its holder's. Seen failing first.
/// </summary>
public sealed class LinkCheckTests : IDisposable
{
    private const string Unit = "unit-r";
    private const string Plugin = "daoris-azure-devops";
    private const string Tip = "0123456789abcdef0123456789abcdef01234567";
    private const string Link = "https://dev.example/_releaseProgress?releaseId=92";

    private static readonly NodePerform Performs = new(Idempotent: true, Reconcile: true, Transient: []);

    private static readonly NodeGraph Read = NodeGraphs.Read("""
        { "schemaVersion": 2, "entries": ["release-create"], "end": "after",
          "nodes": [
            { "id": "release-create", "kind": "daoris-azure-devops/release-create", "version": 1, "reach": "performed", "options": { "releaseDefinition": 43 } },
            { "id": "after", "kind": "daoris-azure-devops/after", "version": 1, "reach": "performed" } ],
          "wires": [{ "id": "w1", "kind": "after", "from": "release-create", "to": "after" }] }
        """).Graph!;

    private static readonly NodeGraph Graph = Read with { Nodes = [Read.Nodes[0] with { Perform = Performs }, Read.Nodes[1] with { Perform = Performs }] };

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-link-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly Port _port = new();

    private DateTimeOffset _now = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

    /// <summary>The outputs the first step declares, none unless a test says so.</summary>
    private IReadOnlyDictionary<string, string> _outputs = new Dictionary<string, string>();

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private sealed class Port : IWorkPerformPort
    {
        public List<WorkPerformCall> Sent { get; } = [];

        public List<WorkReconcileCall> Asked { get; } = [];

        public WorkReconcileAnswer Answer { get; set; } = new(ReconcileStates.Running);

        public Task<int> PerformAsync(string plugin, WorkPerformCall call, CancellationToken ct)
        {
            Sent.Add(call);
            return Task.FromResult(1);
        }

        public Task<(WorkReconcileAnswer Answer, int Generation)> ReconcileStepAsync(string plugin, WorkReconcileCall call, CancellationToken ct)
        {
            Asked.Add(call);
            return Task.FromResult((Answer, 1));
        }

        public bool Up { get; set; } = true;

        public int? GenerationOf(string plugin) => Up ? 1 : null;
    }

    private NodeRecordFiles Files => new(_home, () => true);

    private WorkPerformer Performer() => new(Files, _port, () => _now);

    private NodeDeclaration? Declared(string unit, string node) =>
        Graph.Nodes.FirstOrDefault(each => each.Id == node) is { } found && found.Id == "release-create" ? found with { Outputs = _outputs } : Graph.Nodes.FirstOrDefault(each => each.Id == node);

    private NodeRecord Record() => Files.Read(Unit)!;

    private NodeStatus Status(string node) => WorkPerformer.StatusOf(Record(), node);

    private async Task<WorkPerformer> WordedAsync()
    {
        var performer = Performer();
        var record = Files.Read(Unit) ?? new NodeRecord(Unit);
        var before = Graph.Nodes.ToDictionary(node => node.Id, node => record.Statuses.GetValueOrDefault(node.Id) ?? NodeLifecycle.Start(Unit, node.Id), StringComparer.Ordinal);
        var evaluated = WireEvaluation.Evaluate(Graph.Nodes, Graph.Wires, before, new WireRun("graph-1", "r1") { Revisions = new Dictionary<string, string?> { [Unit] = Tip } });
        performer.Keep(before, evaluated);
        await performer.StartAsync(Graph.Nodes[0], Assert.Single(evaluated.Starts), CancellationToken.None);
        Assert.Equal(PerformedWhat.Worded, performer.Word(Unit, "release-create", 1, HeldResolutions.DoneOutside, link: Link).What);
        return performer;
    }

    private static WorkReconcileAnswer Judged(string state, WorkOutcome outcome, string? link = Link) => new(state, outcome) { Link = link };

    [Fact]
    public async Task A_link_its_plugins_check_refuses_is_taken_back_with_the_checks_code_and_nothing_settles()
    {
        var performer = await WordedAsync();
        _port.Answer = Judged(ReconcileStates.Failed, new WorkOutcome(WorkStates.Failed, "target-changed",
            new WorkEvidence(null, null, null, "release 92 carries eeeeeeee, which is neither the landed cccccccc nor after it")));

        var checkedLink = Assert.Single(await performer.CheckLinksAsync(Declared, WorkSends.Unbounded, CancellationToken.None));
        performer.Resolve(Declared);

        var asked = Assert.Single(_port.Asked);
        Assert.Equal((Link, "43"), (asked.Link, asked.Options["releaseDefinition"]));
        Assert.Equal((PerformedWhat.Refused, "target-changed"), (checkedLink.What, checkedLink.Code));
        Assert.Contains("release 92 carries eeeeeeee", checkedLink.Detail, StringComparison.Ordinal);
        var attempt = Assert.Single(Record().Attempts);
        Assert.Equal(((string?)null, (string?)null), (attempt.Resolution, attempt.Link));
        Assert.Equal(NodeStates.Active, Status("release-create").State);
    }

    [Fact]
    public async Task A_link_its_plugins_check_settles_reaches_the_step_credited_to_who_made_it()
    {
        var performer = await WordedAsync();
        _port.Answer = Judged(ReconcileStates.Done, new WorkOutcome(WorkStates.Reached, Evidence: new WorkEvidence(null, Link, null, "Release-92 carries cccccccc.") { By = "outside: A Person" }));

        await performer.CheckLinksAsync(Declared, WorkSends.Unbounded, CancellationToken.None);
        performer.Resolve(Declared);

        Assert.Equal((NodeStates.Reached, NodeSource.Person), (Status("release-create").State, Status("release-create").By));
        Assert.Equal("outside: A Person", Assert.Single(Record().Attempts).By);
    }

    [Fact]
    public async Task A_link_applied_is_not_asked_again_so_it_spends_no_send_on_a_later_pass()
    {
        var performer = await WordedAsync();
        _port.Answer = Judged(ReconcileStates.Done, new WorkOutcome(WorkStates.Reached, Evidence: new WorkEvidence(null, Link, null, "Release-92 carries cccccccc.")));

        foreach (var _ in new[] { 1, 2 })
        {
            await performer.CheckLinksAsync(Declared, WorkSends.Unbounded, CancellationToken.None);
            performer.Resolve(Declared);
        }

        Assert.Single(_port.Asked);
        Assert.Equal(NodeStates.Reached, Status("release-create").State);
    }

    [Fact]
    public async Task A_link_its_plugin_cannot_check_is_held_never_applied_on_the_word_alone_and_past_its_deadline_climbs_the_line()
    {
        var performer = await WordedAsync();
        _port.Up = false;

        Assert.Empty(await performer.CheckLinksAsync(Declared, WorkSends.Unbounded, CancellationToken.None));
        performer.Resolve(Declared);

        Assert.Equal(NodeStates.Active, Status("release-create").State);
        var held = Assert.Single(Record().Attempts);
        Assert.Equal((HeldResolutions.Happened, Link), (held.Resolution, held.Link));

        _now += WorkPerformer.DefaultTimeout + TimeSpan.FromMinutes(1);
        var climbed = Assert.Single(performer.Resolve(Declared));
        Assert.Equal(LinkHolds.Climbed, climbed.Code);
        Assert.StartsWith("waiting on", Assert.Single(Record().Attempts).Progress!.Words, StringComparison.Ordinal);
        Assert.Empty(performer.Resolve(Declared));
        Assert.Equal(NodeStates.Active, Status("release-create").State);

        _port.Up = true;
        _port.Answer = Judged(ReconcileStates.Done, new WorkOutcome(WorkStates.Reached, Evidence: new WorkEvidence(null, Link, null, "Release-92 carries cccccccc.")));
        await performer.CheckLinksAsync(Declared, WorkSends.Unbounded, CancellationToken.None);
        performer.Resolve(Declared);
        Assert.Equal(NodeStates.Reached, Status("release-create").State);
    }

    [Fact]
    public async Task Done_outside_is_not_offered_on_a_step_declaring_outputs_and_a_word_the_table_refuses_keeps_its_refusal()
    {
        var performer = await WordedAsync();
        _outputs = new Dictionary<string, string> { ["pull-request"] = "daoris-azure-devops/pull-request@1" };
        _port.Answer = Judged(ReconcileStates.Done, new WorkOutcome(WorkStates.Reached, Evidence: new WorkEvidence(null, Link, null, "Release-92 carries cccccccc.")));

        await performer.CheckLinksAsync(Declared, WorkSends.Unbounded, CancellationToken.None);
        var refused = Assert.Single(performer.Resolve(Declared));

        Assert.Equal(NodeRefusal.NotOnWord, refused.Code);
        var attempt = Assert.Single(Record().Attempts);
        Assert.Null(attempt.Link);
        Assert.Contains(NodeRefusal.NotOnWord, attempt.Progress?.Words, StringComparison.Ordinal);
        var offered = PluginFailureHeld.Of(Record(), Declared(Unit, "release-create")!, Status("release-create"));
        Assert.DoesNotContain(HeldResolutions.DoneOutside, offered?.Words ?? []);
    }

    [Fact]
    public async Task An_answer_naming_no_link_an_older_kits_leaves_the_word_its_holders()
    {
        var performer = await WordedAsync();
        _port.Answer = Judged(ReconcileStates.Failed, new WorkOutcome(WorkStates.Failed, "rejected"), link: null);

        Assert.Empty(await performer.CheckLinksAsync(Declared, WorkSends.Unbounded, CancellationToken.None));
        performer.Resolve(Declared);

        Assert.Equal(NodeStates.Reached, Status("release-create").State);
    }
}
