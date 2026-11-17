using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests.Workflows;

/// <summary>
/// A wait is answered by its own role (D312, amended): the step's tenure holds the step's own role, and any other role a wait is on (a
/// vacancy's line, a plugin's actor) is held by the place's cast of that role, never by the step's holder. Seen failing first: a vacancy on
/// an agent's opinion step, and a sign-in wait on a release an agent holds, both read the agent and left <i>What needs you</i>.
/// </summary>
public sealed class StepWaitHoldersTests
{
    private const string Run = "q7";
    private const string Unit = "run:q7";
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 9, 0, 0, TimeSpan.Zero);

    private static NodeRecord Standing(string node, NodeStatus status, params NodeAttempt[] attempts) => new(Unit)
    {
        Statuses = new Dictionary<string, NodeStatus> { [node] = status },
        Attempts = [.. attempts],
    };

    private static NodeStatus Active(string node) => new(Unit, node, NodeStates.Active) { Attempt = 1, Tries = 1, Since = Now };

    private static StepTenure HeldBy(string node, string role, string holder, int epoch = 1, string why = TenureWhy.Cast) =>
        new(node, 1, role, holder, epoch, Now, why);

    private static readonly NodeDeclaration Opinion = new("opinion", NodeKinds.Opinion, 1, NodeReach.Observed);

    private static readonly NodeDeclaration Release = new("release", "acme.deploy/release", 1, NodeReach.Performed);

    private static NodeRecord SignIn() => Standing("release", Active("release") with { Unreconciled = 1 }, new NodeAttempt("release", 1, "acme.deploy",
        new WorkPerformCall(Unit, "acme.deploy/release", 1, "i-1", "k-1", "tip", [], Now.AddHours(1)), AttemptStates.Sent)
    {
        RecordedAt = Now, Generation = 1, Deliveries = 1,
        Progress = new StepProgress(StepRoles.CredentialsKeeper, "Sign in to Azure DevOps again.", "https://dev.azure.com/acme", null, Now),
    });

    [Fact]
    public void A_vacancy_on_an_agents_opinion_step_is_the_lines_and_keeps_its_row()
    {
        var record = Standing("opinion", Active("opinion") with { Holds = OpinionGateReading.Vacant });
        var held = HeldBy("opinion", StepRoles.Opinion, CastHolders.Agent);

        var row = HeldSteps.Step(Run, "engine", "aurora", Run, Opinion, record, held);

        Assert.NotNull(row);
        Assert.Equal((PersonWaits.Vacant, AppointedRoles.Supervisor, StepHolders.Person), (row.Why, row.Role, row.Holder));
    }

    [Fact]
    public void A_sign_in_wait_on_a_release_an_agent_holds_is_the_credentials_keepers_and_keeps_its_row()
    {
        var held = HeldBy("release", StepRoles.Operations, CastHolders.AgentNamed("beta"));

        var row = HeldSteps.Step(Run, "engine", "aurora", Run, Release, SignIn(), held);

        Assert.NotNull(row);
        Assert.Equal((StepRoles.CredentialsKeeper, StepHolders.Person), (row.Role, row.Holder));
    }
}
