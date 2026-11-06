using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.HoldFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LANDNODE1k (D145's and D173's notes of 2026-11-01): what a landing's start judged outside the graph is two steps of it, <c>quest.holds</c> and
/// <c>content.holds</c>, observed by Daoris at the bound tip, each with its options; an older build reads either as no kind and holds the graph
/// whole. What the two share is here; each kind's reading is in its own folder.
/// </summary>
public sealed class HoldKindsTests
{
    [Fact]
    public void Both_are_built_in_observed_kinds_reached_by_what_Daoris_reads_and_failed_by_nothing()
    {
        foreach (var kind in new[] { NodeKinds.QuestHolds, NodeKinds.ContentHolds })
        {
            var authority = NodeKit.Row(kind)!;
            Assert.Equal((NodeReach.Observed, NodeSource.Holds, (string?)null, true, false), (authority.Reach, authority.ReachBy, authority.FailureBy, authority.PersonSkips, authority.MayEnd));
            Assert.Contains(kind, NodeKit.Kinds);
            Assert.Null(NodeDeclarations.Problem(Hold(kind)));
        }
    }

    [Theory]
    [InlineData(NodeKinds.QuestHolds, """{"yes":false,"evidence":true}""", null)]
    [InlineData(NodeKinds.QuestHolds, """{"yes":"no"}""", "yes")]
    [InlineData(NodeKinds.QuestHolds, """{"evidence":1}""", "evidence")]
    [InlineData(NodeKinds.ContentHolds, """{"line":false,"proof":false}""", null)]
    [InlineData(NodeKinds.ContentHolds, """{"proof":"off"}""", "proof")]
    public void Each_option_is_true_or_false(string kind, string options, string? refused) =>
        Assert.Equal(refused, NodeDeclarations.Problem(Hold(kind, options)));

    /// <summary>An older build's table has neither row, so it reads each as no kind and holds the graph whole (NodeContract's unknown kind).</summary>
    [Fact]
    public void An_older_build_whose_table_omits_the_holds_reads_them_as_no_kind()
    {
        var older = NodeKit.Built.Without(NodeKinds.QuestHolds).Without(NodeKinds.ContentHolds);

        Assert.Equal(NodeRefusal.UnknownKind, NodeDeclarations.Problem(Hold(NodeKinds.QuestHolds), older));
        Assert.Equal(NodeRefusal.UnknownKind, NodeDeclarations.Problem(Hold(NodeKinds.ContentHolds), older));
        Assert.Null(NodeAuthorities.Of(Hold(NodeKinds.ContentHolds), older));
    }

    /// <summary>Under Current a door judges both where it lands, so a landing its record keeps reaches them, as an opinion's landed gate does.</summary>
    [Fact]
    public void A_landing_recorded_reaches_both_where_no_run_binds_them()
    {
        var landed = Branch with
        {
            Records = new WorkUnitRecords(null, [new LandedBranch("engine", "default", "feature/q1", "main", "tip-1", "s1", "q1", "Fix the gap", DateTimeOffset.UnixEpoch)], null),
        };

        foreach (var kind in new[] { NodeKinds.QuestHolds, NodeKinds.ContentHolds })
        {
            var node = Hold(kind);
            var seen = NodeObserving.Observe(landed, node, new NodeAcceptance(landed.Id, "tip-1", Scope.GraphDigest, node.Id, 1, 1));
            Assert.Equal(NodeStates.Reached, seen.Outcome);
        }
    }

    /// <summary>The words the person reads of each kind, its options' rows among the step palette's fields.</summary>
    [Fact]
    public void Each_kind_has_its_option_fields()
    {
        Assert.Equal(["yes", "evidence"], NodeKit.Of(NodeKinds.QuestHolds)!.Options.Select(field => field.Name));
        Assert.Equal(["line", "proof"], NodeKit.Of(NodeKinds.ContentHolds)!.Options.Select(field => field.Name));
    }
}
