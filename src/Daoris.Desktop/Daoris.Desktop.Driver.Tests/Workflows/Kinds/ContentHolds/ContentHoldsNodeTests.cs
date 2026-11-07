using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.HoldFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary><c>content.holds</c> is reached where the work holds a commit nothing of the person's holds and the line does not hold it by content.</summary>
public sealed class ContentHoldsNodeTests
{
    [Fact]
    public void What_the_line_holds_is_reached_where_the_work_holds_a_commit_nothing_of_yours_holds()
    {
        var node = Hold(NodeKinds.ContentHolds);
        var seen = Observe(Branch, node, "tip-1", new HoldContent(null, Unlanded: 2, Ahead: 2));

        Assert.Equal((NodeStates.Reached, NodeSource.Holds), (seen.Outcome, seen.Source));
        Assert.Equal("tip-1", seen.Acceptance!.Evidence!.Revision);
    }

    [Fact]
    public void Unread_at_the_bound_commit_it_waits_and_is_never_reached_on_a_guess()
    {
        var unread = Observe(Branch, Hold(NodeKinds.ContentHolds), "tip-1");

        Assert.Null(unread.Outcome);
        Assert.Contains("not read", unread.Said);
    }

    [Theory]
    [InlineData(0, 0, "nothing to land")]
    [InlineData(0, 3, "a branch of yours already holds")]
    public void Dds_proof_keeps_it_active_where_no_commit_is_left_to_land_unless_proof_is_off(int unlanded, int ahead, string why)
    {
        var read = new HoldContent(null, unlanded, ahead);

        var waits = Observe(Branch, Hold(NodeKinds.ContentHolds), "tip-1", read);
        Assert.Null(waits.Outcome);
        Assert.Contains(why, waits.Said);

        Assert.Equal(NodeStates.Reached, Observe(Branch, Hold(NodeKinds.ContentHolds, """{"proof":false}"""), "tip-1", read).Outcome);
    }

    [Fact]
    public void Work_the_line_holds_by_content_keeps_it_active_in_the_heads_words_unless_line_is_off()
    {
        var read = new HoldContent("`main` already holds this work by content (a squash merge).", Unlanded: 1, Ahead: 1);

        var waits = Observe(Branch, Hold(NodeKinds.ContentHolds), "tip-1", read);
        Assert.Null(waits.Outcome);
        Assert.Contains("already holds this work by content", waits.Said);

        Assert.Equal(NodeStates.Reached, Observe(Branch, Hold(NodeKinds.ContentHolds, """{"line":false}"""), "tip-1", read).Outcome);
    }
}
