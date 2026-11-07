using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GateFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>The <c>opinion</c> observed from the second opinion's gate.</summary>
public sealed class OpinionNodeTests
{
    /// <summary>
    /// A gate whose rule asks no second opinion here judges no commit, and a landing door's read of
    /// it carries none (<c>TreeGates.OpinionAsync</c>), so the opinion is passed over whatever commit its node is bound to, a quest's none
    /// included. Every other state still waits on a judgement of the bound commit.
    /// </summary>
    [Fact]
    public void An_opinion_whose_rule_asks_none_is_passed_over_whatever_commit_its_gate_read()
    {
        var opinion = Node(NodeKinds.Opinion);
        foreach (var (unit, binding) in new[] { (Branch(), Binding(NodeKinds.Opinion)), (Branch(), Binding(NodeKinds.Opinion) with { SubjectRevision = null }) })
        {
            var none = Applied(opinion, Reads.Opinion(unit, opinion, binding, new OpinionGateState(OpinionGateStates.None, "engine")));
            Assert.Equal((NodeStates.Skipped, NodeSource.Opinion), (none.Status.State, none.Status.By));
        }

        Assert.Null(Reads.Opinion(Branch(), opinion, Binding(NodeKinds.Opinion), new OpinionGateState(OpinionGateStates.Settled, "engine")).Move);
        Assert.Null(Reads.Opinion(Branch(), opinion, Binding(NodeKinds.Opinion), new OpinionGateState(OpinionGateStates.None, "other")).Move);
        // A none read at a commit is that commit's: at another, it says nothing of this one.
        Assert.Null(Reads.Opinion(Branch(), opinion, Binding(NodeKinds.Opinion), new OpinionGateState(OpinionGateStates.None, "engine") { Tip = "tip-0" }).Move);
    }
}
