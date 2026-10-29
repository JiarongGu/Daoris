using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What a graph run's test classes share over the runtime's fixture (PARTWHOLE1; FABRIC-CUTOVER1c, 1f): run <c>q7</c> and its record, the
/// branches' tips a pass reads, a look over the run's home, and the binding to a saved graph version. <see cref="NodeRuntimeRunTests"/> and
/// <see cref="RunNodeWordsTests"/> derive from it.
/// </summary>
public abstract class NodeRunFixture : NodeRuntimeFixture
{
    protected const string RunId = "q7";

    protected static readonly string RunUnit = RunUnits.IdOf(RunId);

    protected const string Tip1 = "1111111111111111111111111111111111111111";

    protected const string Tip2 = "2222222222222222222222222222222222222222";

    protected static readonly DateTimeOffset Opened = new(2026, 10, 17, 8, 0, 0, TimeSpan.Zero);

    /// <summary>A plugin's observed step: the deploy its plugin reports.</summary>
    protected static readonly NodeDeclaration Deployed = new(Plugin + "/deployed", Plugin + "/deployed", 1, NodeReach.Observed);

    /// <summary>A go-ahead between the claim and the landing, naming its act.</summary>
    protected const string GoAheadBeforeHanded = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"go","kind":"go-ahead","version":1,"reach":"pressed","options":{"act":"deploy the release"}},
                  {"id":"handed","kind":"handed","version":1,"reach":"observed"}],
         "wires":[{"id":"c>g","kind":"after","from":"claimed","to":"go"},{"id":"g>h","kind":"after","from":"go","to":"handed"}]}
        """;

    /// <summary>Each branch's tip as the pass reads it, by branch.</summary>
    protected readonly Dictionary<string, string> _branchTips = new(StringComparer.Ordinal);

    /// <summary>Why the units' attribution was not read, as the service's outage says it; none where it was read.</summary>
    protected IReadOnlyList<string> _unread = [];

    protected int _gatesRead;

    /// <summary>A look over the run's home: the units the test sets, the branches' tips, and the gates counted.</summary>
    protected NodeRuntimeLook RunLook(string config = "{}", bool whole = true) => Look(config, whole) with
    {
        Units = _ =>
        {
            Interlocked.Increment(ref _unitsRead);
            return Task.FromResult(new NodeUnitsRead(_units, new Dictionary<string, string> { ["engine"] = "checkout", ["other"] = "elsewhere" })
            {
                Unread = _unread,
            });
        },
        Steps = () => [Step, Build, Deployed],
        Tips = (_, branches, _) => Task.FromResult<IReadOnlyDictionary<string, string>>(
            branches.Where(_branchTips.ContainsKey).ToDictionary(branch => branch, branch => _branchTips[branch], StringComparer.Ordinal)),
        Gates = (_, _, _) =>
        {
            Interlocked.Increment(ref _gatesRead);
            return Task.FromResult<(OpinionGateState?, ReviewGateState?)>((null, null));
        },
    };

    /// <summary>Run <c>q7</c> bound to v1 of a workflow holding <paramref name="graph"/>.</summary>
    protected void BoundTo(string graph, string? ask = null) => RunHome.Bind(_home, RunId, RunHome.SaveGraph(_home, graph), _now, ask: ask);

    protected Task<NodeRuntimePass> RunPassAsync(NodeRuntime runtime, string config = "{}", bool whole = true) =>
        runtime.PassAsync(RunLook(config, whole), CancellationToken.None);

    protected NodeStatus RunStatus(string node, string unit = "") => Files.Read(unit.Length == 0 ? RunUnit : unit)!.Statuses[node];
}
