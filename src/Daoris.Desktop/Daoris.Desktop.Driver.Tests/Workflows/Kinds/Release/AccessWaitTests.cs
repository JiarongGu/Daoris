using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// ACCESSSTEP1's driver half (W3c row 3): a plugin's wait on the credentials keeper that reaches its deadline climbs the reporting line instead
/// of ending as <c>timeout</c>: sign-ins are an owner-class kind, so it passes every level to the owner role and is held there, its step still
/// under way. A wait on any other role ends as before. Seen failing first.
/// </summary>
public sealed class AccessWaitTests
{
    private const string Unit = "unit-r";
    private const string Plugin = "daoris-azure-devops";
    private const string Tip = "0123456789abcdef0123456789abcdef01234567";
    private static readonly DateTimeOffset At = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

    private static readonly NodeDeclaration Create = new("release-create", Plugin + "/release-create", 1, NodeReach.Performed)
    {
        Perform = new NodePerform(Idempotent: true, Reconcile: true, Transient: []),
    };

    private static NodeRecord Waiting(string role)
    {
        var call = new WorkPerformCall(Unit, Plugin + "/release-create", 1, "i-1", "k-1", Tip, [], At.AddHours(1));
        var attempt = new NodeAttempt(Create.Id, 1, Plugin, call, AttemptStates.Sent)
        {
            RecordedAt = At, Generation = 1, Deliveries = 1,
            Progress = new StepProgress(role, "Waiting on the credentials keeper: Azure DevOps refused the key.", "https://dev.example/_usersSettings/tokens", null, At),
        };
        var status = new NodeStatus(Unit, Create.Id, NodeStates.Active)
        {
            Attempt = 1, Tries = 1, Unreconciled = 1, Binding = new NodeAcceptance(Unit, Tip, "graph-1", Create.Id, 1, 1),
        };
        return new NodeRecord(Unit) { Attempts = [attempt], Statuses = new Dictionary<string, NodeStatus> { [Create.Id] = status } };
    }

    private static readonly WorkOutcome TimedOut = new(WorkStates.Failed, "timeout", new WorkEvidence(null, null, null, "The step's deadline passed."));

    [Fact]
    public void A_wait_on_the_credentials_keeper_at_its_deadline_climbs_to_the_owner_role_and_is_held_never_failed()
    {
        var record = Waiting(StepRoles.CredentialsKeeper);

        var (next, said) = new WorkOutcomes(() => At).Outcome(record, record.Attempts[0], Create, TimedOut, 1);

        var attempt = next!.Attempts.Single();
        Assert.Equal((PerformedWhat.Held, AccessWaits.Climbed), (said.What, said.Code));
        Assert.Equal((AttemptStates.Held, StepRoles.Owner), (attempt.State, attempt.Progress!.Actor));
        Assert.StartsWith("waiting on the owner: you · passed up: ", attempt.Progress.Words, StringComparison.Ordinal);
        Assert.Contains("owner-class", AccessWaits.Line().Passed.Select(pass => pass.Code));
        Assert.Equal("https://dev.example/_usersSettings/tokens", attempt.Progress.Url);
        Assert.Equal(NodeStates.Active, next.Statuses[Create.Id].State);
    }

    [Fact]
    public void A_wait_on_another_role_at_its_deadline_ends_as_timeout()
    {
        var record = Waiting(StepRoles.ReleaseManager);

        var (next, _) = new WorkOutcomes(() => At).Outcome(record, record.Attempts[0], Create, TimedOut, 1);

        Assert.Equal((NodeStates.Failed, "timeout"), (next!.Statuses[Create.Id].State, next.Statuses[Create.Id].Code));
    }
}
