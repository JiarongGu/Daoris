using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Which checks a new tip supersedes, read from a run's record alone: an attempt in flight at an older commit on a node that no longer holds
/// it, never one its node still runs, one at the tip, one on the landing's commit, or one already answered.
/// </summary>
public sealed class SupersededChecksTests
{
    private const string Old = "1111111111111111111111111111111111111111";
    private const string Tip = "2222222222222222222222222222222222222222";
    private static readonly IReadOnlySet<string> None = new HashSet<string>(StringComparer.Ordinal);

    private static NodeRecord Record(string state, string at, string attemptState = AttemptStates.Sent, int? unreconciled = 1) => new("run:mine")
    {
        Statuses = new Dictionary<string, NodeStatus> { ["check"] = new NodeStatus("run:mine", "check", state) { Attempt = 1, Unreconciled = unreconciled } },
        Attempts =
        [
            new NodeAttempt("check", 1, NodeKinds.Check, new WorkPerformCall("run:mine", NodeKinds.Check, 1, "i-old", "k-1", at, [], DateTimeOffset.UnixEpoch), attemptState),
        ],
    };

    [Fact]
    public void A_check_in_flight_at_an_older_commit_on_a_node_judged_again_is_superseded_with_both_commits_said()
    {
        var (attempt, why) = Assert.Single(SupersededChecks.Of(Record(NodeStates.Ready, Old), Tip, None));

        Assert.Equal("i-old", attempt.Call.Invocation);
        Assert.Equal("the work moved on to 22222222 after it was sent at 11111111", why);
        Assert.Single(SupersededChecks.Of(Record(NodeStates.Invalidated, Old), Tip, None));
    }

    [Fact]
    public void A_check_its_node_still_runs_one_at_the_tip_one_on_the_landing_and_one_answered_run_on()
    {
        Assert.Empty(SupersededChecks.Of(Record(NodeStates.Active, Old), Tip, None));
        Assert.Empty(SupersededChecks.Of(Record(NodeStates.Unknown, Old), Tip, None));
        Assert.Empty(SupersededChecks.Of(Record(NodeStates.Ready, Tip), Tip, None));
        Assert.Empty(SupersededChecks.Of(Record(NodeStates.Ready, Old), Tip, new HashSet<string>(["check"], StringComparer.Ordinal)));
        Assert.Empty(SupersededChecks.Of(Record(NodeStates.Ready, Old, AttemptStates.Settled), Tip, None));
        Assert.Empty(SupersededChecks.Of(Record(NodeStates.Ready, Old, unreconciled: null), Tip, None));
        Assert.Empty(SupersededChecks.Of(Record(NodeStates.Ready, Old), "not-a-commit", None));
    }
}
