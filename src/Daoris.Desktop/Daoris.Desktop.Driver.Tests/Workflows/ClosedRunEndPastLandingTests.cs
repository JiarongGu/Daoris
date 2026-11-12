using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A closed quest's run whose landing happened: a step after it (a monitor, a plugin's release) reads the landing, never the branch, so a
/// branch tidied away or a line that holds the commit leaves it still to run, and the run does not end under it.
/// </summary>
public sealed class ClosedRunEndPastLandingTests
{
    private const string Unit = "run:r1";

    private static readonly NodeGraph Graph = new(
        [
            new NodeDeclaration("handed", NodeKinds.Land, 1, NodeReach.Performed),
            new NodeDeclaration("monitor", NodeKinds.Monitor, 1, NodeReach.Observed),
        ],
        [new Wire("w", WireKinds.After, new WireEnd("handed"), new WireEnd("monitor"))], ["handed"], "monitor");

    private static NodeRecord Record(string landing, string monitor) => new(Unit)
    {
        Statuses = new Dictionary<string, NodeStatus>
        {
            ["handed"] = new(Unit, "handed", landing) { Attempt = 1, Tries = 1, Binding = new NodeAcceptance(Unit, "tip", "gph", "handed", 1, 1) },
            ["monitor"] = new(Unit, "monitor", monitor) { Attempt = 1, Tries = 1, Binding = new NodeAcceptance(Unit, "tip", "gph", "monitor", 1, 1) },
        },
    };

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void A_monitor_waiting_after_a_reached_landing_keeps_the_run_whatever_became_of_the_branch_and_the_line(bool branchStands, bool onLine)
    {
        var held = onLine ? new HashSet<string> { "tip" } : new HashSet<string>();

        Assert.Null(ClosedRunEnds.Why(Graph, Record(NodeStates.Reached, NodeStates.Active), false, branchStands, held));
    }

    [Fact]
    public void A_landing_not_yet_reached_still_ends_with_its_branch_gone_or_its_commit_on_the_line()
    {
        var record = Record(NodeStates.Active, NodeStates.Blocked);

        Assert.Equal(ClosedRunEnds.BranchGone, ClosedRunEnds.Why(Graph, record, false, false, new HashSet<string>()));
    }
}
