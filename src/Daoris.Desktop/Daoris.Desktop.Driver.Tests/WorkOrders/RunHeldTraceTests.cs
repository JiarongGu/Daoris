using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A run whose work is still going holds the branch unit its nodes are bound on: the session branch's trace is kept when its branch goes,
/// so the run's next step (its landing, after the person accepted) still finds the branch it lands rather than waiting, ready with no subject,
/// for a branch no record names. A run whose steps are all settled holds nothing, and the trace goes as any other.
/// </summary>
public sealed class RunHeldTraceTests : IDisposable
{
    private static readonly DateTimeOffset Then = new(2026, 11, 10, 5, 29, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-runheld-" + Guid.NewGuid().ToString("N")[..8]);

    public RunHeldTraceTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static NodeStatus Reached(string node, string subject) => new("run:r1", node, NodeStates.Reached)
    {
        Attempt = 1,
        Acceptance = new NodeAcceptance("run:r1", "c79ec557", "graph-1", node, 1, 1) { Subject = subject },
    };

    private static NodeRecord Run(string landing) => new("run:r1")
    {
        Statuses = new Dictionary<string, NodeStatus>(StringComparer.Ordinal)
        {
            ["holds"] = Reached("holds", "quest:r1"),
            ["review"] = Reached("review", "unit-a"),
            ["landing"] = landing == NodeStates.Reached ? Reached("landing", "unit-a") : new NodeStatus("run:r1", "landing", landing),
            ["closed"] = new NodeStatus("run:r1", "closed", NodeStates.Blocked),
        },
    };

    [Fact]
    public void A_runs_accepted_branch_keeps_its_trace_while_its_landing_waits_and_lets_it_go_once_every_step_settled()
    {
        var nodes = new NodeRecordFiles(_home, () => true);
        nodes.Write(Run(NodeStates.Ready));
        var grown = new SessionBranches(_home);
        grown.Record(new GrownBranch("engine", "default", "daoris/s-a", "main", "f0", null, Then) { Unit = "unit-a" });

        grown.Forget("engine", ["daoris/s-a"]);

        Assert.Contains("unit-a", WorkObligations.Owing(_home));
        var trace = Assert.Single(grown.Entries());
        Assert.Equal(("unit-a", true), (trace.Unit, trace.GoneAt is not null));

        // Landed and every step after it settled: nothing holds the unit, and its trace is forgotten as any other.
        nodes.Write(Run(NodeStates.Reached));
        grown.Record(new GrownBranch("engine", "default", "daoris/s-b", "main", "f0", null, Then) { Unit = "unit-b" });
        grown.Forget("engine", ["daoris/s-b"]);
        Assert.DoesNotContain("unit-a", WorkObligations.Owing(_home));
        Assert.DoesNotContain(grown.Entries(), each => each.Unit == "unit-b");
    }
}
