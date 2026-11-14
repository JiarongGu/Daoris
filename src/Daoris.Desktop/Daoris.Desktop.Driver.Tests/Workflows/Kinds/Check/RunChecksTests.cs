using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>What a graph run's <c>check</c> sends the heavy executor: its repository at the branch tip the attempt is bound to, whether the run is an ask's delivery or a chain's.</summary>
public sealed class RunChecksTests : IDisposable
{
    private const string Tip = "3333333333333333333333333333333333333333";

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-runchecks-" + Guid.NewGuid().ToString("N")[..8]);

    private static readonly string Run = AskRunKey.Of("a1", "engine")!;

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private static NodeAttempt Attempt(string run) =>
        new("check", 1, "", new WorkPerformCall(RunUnits.IdOf(run), "gates", 1, "inv-1", "key-1", Tip, [], DateTimeOffset.UnixEpoch), AttemptStates.Sent);

    private static readonly NodeDeclaration Check = new("check", NodeKinds.Check, 1, NodeReach.Performed);

    private static NodeDeclaration Reached() =>
        Check with { Options = new Dictionary<string, string> { ["gates"] = "\"reached\"" } };

    [Fact]
    public void A_check_verifies_the_repository_at_the_commit_its_attempt_is_bound_to()
    {
        Directory.CreateDirectory(_home);
        GitFolders.Remember(_home, [("checkout-engine", "engine", "default")]);
        Assert.True(WorkflowRunBindings.Bind(_home, new WorkflowRunBinding(Run, "engine", "default", "ask-delivery", WorkflowLevels.Task, DateTimeOffset.UnixEpoch) { Ask = "a1" }));

        var send = RunChecks.SendOf(_home, RunUnits.IdOf(Run), Attempt(Run), Check);

        Assert.NotNull(send);
        Assert.Equal((Run, "inv-1", "engine", Path.GetFullPath("checkout-engine"), Tip), (send.Run, send.Invocation, send.Repository, send.Checkout, send.Commit));
        Assert.False(send.Reached);
    }

    [Fact]
    public void A_chain_runs_check_verifies_its_branch_tip_too_and_the_reached_choice_asks_the_hand_back_reading_and_the_fast_tier()
    {
        Directory.CreateDirectory(_home);
        GitFolders.Remember(_home, [("checkout-engine", "engine", "default")]);
        Assert.True(WorkflowRunBindings.Bind(_home, new WorkflowRunBinding("q7", "engine", "default", "docs", WorkflowLevels.Repository, DateTimeOffset.UnixEpoch)));

        var plain = RunChecks.SendOf(_home, RunUnits.IdOf("q7"), Attempt("q7"), Check);
        var reached = RunChecks.SendOf(_home, RunUnits.IdOf("q7"), Attempt("q7"), Reached());

        Assert.Equal(("q7", Tip), (plain!.Run, plain.Commit));
        Assert.False(plain.Reached);
        Assert.True(reached!.Reached);
        Assert.Null(reached.Gates);
    }

    [Fact]
    public void A_run_with_no_binding_or_no_checkout_here_sends_nothing()
    {
        Directory.CreateDirectory(_home);
        Assert.True(WorkflowRunBindings.Bind(_home, new WorkflowRunBinding("q7", "engine", "default", "docs", WorkflowLevels.Repository, DateTimeOffset.UnixEpoch)));

        Assert.Null(RunChecks.SendOf(_home, RunUnits.IdOf("q7"), Attempt("q7"), Check));
        Assert.Null(RunChecks.SendOf(_home, RunUnits.IdOf("q9"), Attempt("q9"), Check));
    }
}
