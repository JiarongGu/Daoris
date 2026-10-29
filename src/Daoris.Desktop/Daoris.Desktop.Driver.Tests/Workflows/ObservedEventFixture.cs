using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What the observed events' test classes share (PARTWHOLE1): a scratch home, a stand-in port, a stand-in read of the tips counting each
/// read, the plugin's observed deploy and the performed notify after it, the branch units a pass reads and the event a plugin sends. One
/// class over two hand-split parts until PARTWHOLE1 gave each a class of its own: <see cref="ObservedEventTests"/> and
/// <see cref="ObservedRunEventTests"/>.
/// </summary>
public abstract class ObservedEventFixture : IDisposable
{
    protected const string Plugin = "acme.deploy";
    protected const string A = "engine~feature-a";
    protected const string B = "engine~feature-b";
    protected const string C0 = "0000000000000000000000000000000000000000";
    protected const string C1 = "1111111111111111111111111111111111111111";
    protected const string C2 = "2222222222222222222222222222222222222222";

    /// <summary>The plugin's observed step, and a performed one a wire starts after it.</summary>
    protected static readonly NodeDeclaration Deployed = new(Plugin + "/deployed", Plugin + "/deployed", 1, NodeReach.Observed);

    protected static readonly NodeDeclaration Notify = new(Plugin + "/notify", Plugin + "/notify", 1, NodeReach.Performed)
    {
        Perform = new NodePerform(Idempotent: true, Reconcile: true, Transient: []),
    };

    /// <summary>A repository's branches under a landing rule whose cycle observes the deploy and then notifies.</summary>
    protected const string Observing = """
        { "landings": { "engine": { "form": "merge", "cycle": {
            "schemaVersion": 2, "entries": ["deployed"], "end": "notify",
            "nodes": [
              { "id": "deployed", "kind": "acme.deploy/deployed", "version": 1, "reach": "observed" },
              { "id": "notify", "kind": "acme.deploy/notify", "version": 1, "reach": "performed" } ],
            "wires": [{ "id": "w1", "kind": "after", "from": "deployed", "to": "notify" }] } } } }
        """;

    protected readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-observed-" + Guid.NewGuid().ToString("N")[..8]);

    private protected readonly ObservedPort _port = new();

    protected readonly Dictionary<string, string> _tips = new(StringComparer.Ordinal) { ["feature/a"] = C1, ["feature/b"] = C1 };

    protected readonly List<(string Root, int Branches)> _tipReads = [];

    protected DateTimeOffset _now = new(2026, 10, 18, 9, 0, 0, TimeSpan.Zero);

    protected IReadOnlyList<WorkUnit> _units = [BranchUnit(A, "feature/a")];

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    protected NodeRecordFiles Files => new(_home, () => true);

    protected NodeRuntime Runtime() => new(Files, _port, () => _now, () => true);

    /// <summary>A branch unit as the inventory gives it: its revision the end fact's (<paramref name="revision"/>), which may lag the branch.</summary>
    protected static WorkUnit BranchUnit(string id, string branch, string revision = C1) => new(id, WorkUnitKind.Branch,
        new WorkOwner("engine", "default", "session-branches.json", WorkRecord.DriverWriter), new WorkSubject(branch, revision),
        new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "open"), []);

    protected NodeRuntimeLook Look(string config, bool whole = true) => new(DriverConfig.Parse(config), whole)
    {
        Units = _ => Task.FromResult(new NodeUnitsRead(_units, new Dictionary<string, string> { ["engine"] = "checkout" })),
        Steps = () => [Deployed, Notify],
        Tips = (root, branches, _) =>
        {
            _tipReads.Add((root, branches.Count));
            return Task.FromResult<IReadOnlyDictionary<string, string>>(branches.Where(_tips.ContainsKey).ToDictionary(branch => branch, branch => _tips[branch]));
        },
    };

    protected Task<NodeRuntimePass> PassAsync(NodeRuntime runtime, string config = Observing, bool whole = true) =>
        runtime.PassAsync(Look(config, whole), CancellationToken.None);

    protected NodeRecord Record(string unit = A) => Files.Read(unit)!;

    protected WorkStepHeard Event(string state, string id, DateTimeOffset at, string commit = C1, string branch = "feature/a", string step = "deployed") =>
        new(Plugin, _port.Generation!.Value, new WorkStepFrame(step, new WorkOutcome(state, Code: state == WorkStates.Failed ? "broken" : null,
            Evidence: state == WorkStates.Reached ? new WorkEvidence(null, "https://deploy.example/" + id, "staging", "deployed") : null), at, id, "engine", branch, commit));
}
