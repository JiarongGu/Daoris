using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests.Workflows;

/// <summary>
/// A wait is answered by its own role: the step's tenure holds the step's own role, and any other role a wait is on (a
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
        Assert.Equal((PersonWaits.Vacant, AppointedRoles.Supervisor, StepHolders.Agent), (row.Why, row.Role, row.Holder));
    }

    /// <summary>A check held not known waits on `operations`, which nothing casts here: its agent's, never a wait of the person's.</summary>
    [Fact]
    public void A_held_check_on_a_role_nothing_casts_waits_on_its_agent_and_never_on_the_person()
    {
        var check = new NodeDeclaration("check", NodeKinds.Check, 1, NodeReach.Observed);
        var record = Standing("check", new NodeStatus(Unit, "check", NodeStates.Unknown) { Attempt = 1, Since = Now });
        var cast = CastReading.At(PlaceCastBook.Empty, null, "aurora", "engine");

        var wait = StepWaiting.Read(record, check, record.Statuses["check"], null, cast);

        Assert.Equal((PersonWaits.Held, StepRoles.Operations, StepHolders.Agent), (wait?.Why, wait?.Role, wait?.Holder.Kind));
        Assert.Equal("Waiting on `operations`: `an agent`.", StepWaiting.Line(wait!.Role, wait.Holder));
    }

    [Fact]
    public void A_sign_in_wait_on_a_release_an_agent_holds_is_the_credentials_keepers_and_keeps_its_row()
    {
        var held = HeldBy("release", StepRoles.Operations, CastHolders.AgentNamed("beta"));

        var row = HeldSteps.Step(Run, "engine", "aurora", Run, Release, SignIn(), held);

        Assert.NotNull(row);
        Assert.Equal((StepRoles.CredentialsKeeper, StepHolders.Person), (row.Role, row.Holder));
    }

    [Fact]
    public void A_plugin_step_held_by_the_person_offers_its_stop_on_its_row()
    {
        var record = Standing("release", Active("release"), new NodeAttempt("release", 1, "acme.deploy",
            new WorkPerformCall(Unit, "acme.deploy/release", 1, "i-1", "k-1", "tip", [], Now.AddHours(1)), AttemptStates.Sent)
        {
            RecordedAt = Now, Generation = 1, Deliveries = 1,
            Progress = new StepProgress(StepRoles.Operations, "Make the release in Azure DevOps.", null, null, Now),
        });

        var row = HeldSteps.Step(Run, "engine", "aurora", Run, Release, record, HeldBy("release", StepRoles.Operations, CastHolders.Person));

        Assert.NotNull(row);
        Assert.Equal((PersonWaits.Actor, StepRoles.Operations), (row.Why, row.Role));
        Assert.Equal([NodeWordActs.Skip, NodeWordActs.Stop, HeldResolutions.DoneOutside], row.Presses);
    }

    private static readonly NodeDeclaration Review = new("review", NodeKinds.Review, 1, NodeReach.Pressed);

    /// <summary>A review handed to an agent waits with no settler, since no agent's channel speaks an Accept: it stays the person's row, to take.</summary>
    [Fact]
    public void A_step_an_agent_holds_that_no_agents_channel_settles_is_a_row_with_take()
    {
        var held = HeldBy("review", StepRoles.Acceptor, CastHolders.AgentNamed("beta"), epoch: 2, why: TenureWhy.HandOver);

        var row = HeldSteps.Step(Run, "engine", "aurora", Run, Review, Standing("review", Active("review")), held);

        Assert.NotNull(row);
        Assert.Equal((PersonWaits.Accept, StepRoles.Acceptor, StepHolders.Agent, "beta", 2), (row.Why, row.Role, row.Holder, row.HolderName, row.Epoch));
        Assert.Contains(CastActs.Take, row.Holds!);
    }

    /// <summary>A plugin's act an agent holds is no row while no vacancy is kept for it; a kept one's bound counts from when it became vacant.</summary>
    [Fact]
    public void A_wait_an_agents_channel_settles_is_no_row_of_the_persons_and_a_kept_vacancy_is_one_from_when_it_became_vacant()
    {
        var release = Release with { Role = StepRoles.ReleaseManager };
        var record = Standing("release", Active("release") with { Unreconciled = 1 }, SignIn().Attempts[0] with
        {
            Progress = new StepProgress(StepRoles.ReleaseManager, "Moving to Production.", "https://dev.azure.com/acme", null, Now.AddDays(-3)),
        });
        var held = HeldBy("release", StepRoles.ReleaseManager, CastHolders.Agent);
        StepVacancy[] kept = [new("release", record.Statuses["release"].Attempt, Now, true, false)];

        Assert.Null(HeldSteps.Step(Run, "engine", "aurora", Run, release, record, held));
        var row = HeldSteps.Step(Run, "engine", "aurora", Run, release, record, held, vacancies: kept, bound: TimeSpan.FromHours(5));
        Assert.Equal((true, Now.AddHours(5)), (row?.Vacant, row?.GivesUp));
    }

    [Fact]
    public void A_plugin_actor_cast_to_an_agent_with_no_live_session_is_a_vacancy()
    {
        var home = Path.Combine(AppContext.BaseDirectory, "_vacancy-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            new PlaceCastFiles(home, () => true).Cast("machine", StepRoles.ReleaseManager, "agent:bob", "bob releases", Now);
            var book = new PlaceCastFiles(home).Book();
            var wait = new RoleWait(PersonWaits.Actor, StepRoles.ReleaseManager, new StepHolder(StepHolders.Agent, "bob"), Now);

            Assert.True(StepVacancies.Is(wait, CastReading.Serves(book, null, null, "engine", (_, _) => false)));
            Assert.False(StepVacancies.Is(wait, CastReading.Serves(book, null, null, "engine", (holder, at) => holder == "agent:bob" && at == "engine")));
            Assert.True(StepVacancies.Is(wait, CastReading.Serves(book, null, null, "engine", (holder, at) => holder == "agent:alice")));
        }
        finally
        {
            if (Directory.Exists(home)) Directory.Delete(home, recursive: true);
        }
    }

    /// <summary>
    /// A go-ahead is answered by the grants, never the tenure, so its row offers no hold acts; a step the pass has not cast yet offers none
    /// either, since its Take would find the person holding it already. Seen failing first.
    /// </summary>
    [Fact]
    public void A_go_ahead_row_and_a_step_not_cast_yet_offer_no_hold_acts()
    {
        var go = new NodeDeclaration("go", NodeKinds.GoAhead, 1, NodeReach.Pressed);
        var asked = HeldSteps.Step(Run, "engine", "aurora", Run, go, Standing("go", Active("go")), HeldBy("go", StepRoles.Approver, CastHolders.Person));
        var sign = new NodeDeclaration("sign", NodeKinds.Task, 1, NodeReach.Pressed) { Outcomes = ["done"] };
        var uncast = HeldSteps.Step(Run, "engine", "aurora", Run, sign, Standing("sign", Active("sign")));

        Assert.Equal((PersonWaits.GoAhead, (IReadOnlyList<string>?)null), (asked?.Why, asked?.Holds));
        Assert.Equal((PersonWaits.Task, (IReadOnlyList<string>?)null), (uncast?.Why, uncast?.Holds));
    }

    [Fact]
    public void Only_the_owner_role_refuses_an_agent_and_an_accept_is_handed_to_one()
    {
        Assert.Equal([StepRoles.Owner], AgentSettles.Roles);

        Assert.NotNull(AgentSettles.Cast(StepRoles.Owner, "agent:beta"));
        Assert.Null(AgentSettles.Cast(StepRoles.Acceptor, "agent:beta"));
        Assert.Null(AgentSettles.Cast(StepRoles.Approver, CastHolders.Agent));
        Assert.Null(AgentSettles.Hand(Review, CastHolders.Agent));
        Assert.Null(AgentSettles.Hand(Release, "agent:beta"));
        Assert.Null(AgentSettles.Hand(Opinion, CastHolders.Agent));
    }
}
