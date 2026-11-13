using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>What an ask delivery run's <c>check</c> sends the heavy executor: its repository at the ask's branch tip the attempt is bound to.</summary>
public sealed class AskRunChecksTests : IDisposable
{
    private const string Tip = "3333333333333333333333333333333333333333";

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-askchecks-" + Guid.NewGuid().ToString("N")[..8]);

    private static readonly string Run = AskRunKey.Of("a1", "engine")!;

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private static NodeAttempt Attempt(string run) =>
        new("check", 1, "", new WorkPerformCall(RunUnits.IdOf(run), "gates", 1, "inv-1", "key-1", Tip, [], DateTimeOffset.UnixEpoch), AttemptStates.Sent);

    private static readonly NodeDeclaration Check = new("check", NodeKinds.Check, 1, NodeReach.Performed);

    [Fact]
    public void A_check_verifies_the_repository_at_the_commit_its_attempt_is_bound_to()
    {
        Directory.CreateDirectory(_home);
        GitFolders.Remember(_home, [("checkout-engine", "engine", "default")]);
        Assert.True(WorkflowRunBindings.Bind(_home, new WorkflowRunBinding(Run, "engine", "default", "ask-delivery", WorkflowLevels.Task, DateTimeOffset.UnixEpoch) { Ask = "a1" }));

        var send = AskRunChecks.SendOf(_home, RunUnits.IdOf(Run), Attempt(Run), Check);

        Assert.NotNull(send);
        Assert.Equal((Run, "inv-1", "engine", Path.GetFullPath("checkout-engine"), Tip),(send.Run, send.Invocation, send.Repository, send.Checkout, send.Commit));
    }

    [Fact]
    public void A_run_that_is_no_ask_delivery_or_has_no_checkout_here_sends_nothing()
    {
        Directory.CreateDirectory(_home);
        Assert.True(WorkflowRunBindings.Bind(_home, new WorkflowRunBinding("q7", "engine", "default", "docs", WorkflowLevels.Repository, DateTimeOffset.UnixEpoch)));
        Assert.True(WorkflowRunBindings.Bind(_home, new WorkflowRunBinding(Run, "engine", "default", "ask-delivery", WorkflowLevels.Task, DateTimeOffset.UnixEpoch) { Ask = "a1" }));

        Assert.Null(AskRunChecks.SendOf(_home, RunUnits.IdOf("q7"), Attempt("q7"), Check));
        Assert.Null(AskRunChecks.SendOf(_home, RunUnits.IdOf(Run), Attempt(Run), Check));
    }
}
