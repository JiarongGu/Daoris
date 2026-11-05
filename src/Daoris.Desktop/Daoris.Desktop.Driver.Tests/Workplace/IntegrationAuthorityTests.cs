using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// INTEGRATE1e part e4b (D215's note; the integration design §5.2 step 8, §8): the requester's authority judged again at promotion, from the
/// line's policy and the place's workflow's <c>integrate</c> step as they stand now, and the appointment seam. A person's request stands on its
/// own, as does a version's own step; Daoris's sweep stands while the step says <c>request: done</c>, and a steward's while it says
/// <c>steward</c> and the seam says its appointment is live. A place whose workflow names no requester reads <c>request: owner</c>, and the
/// sweep and a steward's request lapse under it before the appointment is asked. Nothing here starts a process.
/// </summary>
public sealed class IntegrationAuthorityTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);

    private const string Tip = "1111111111111111111111111111111111111111";

    private static readonly LandingIntegrate Stewarded = new(Request: LandingIntegrate.Steward, Batch: 8, Gates: LandingIntegrate.Declared);

    private static readonly LandingIntegrate Gated = new(Batch: 8, Gates: LandingIntegrate.Declared);

    /// <summary>The home whose saved workflows the recheck reads the place's <c>integrate</c> step from.</summary>
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "integration-authority", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() => GitSeed.Discard(_home);

    private static DriverConfig Rule(LandingIntegrate? integrate) => DriverConfig.Empty.WithLinePolicy("engine", integrate);

    /// <summary>The gated line, its place following a workflow whose <c>integrate</c> says <paramref name="request"/>.</summary>
    private DriverConfig Following(string request) => IntegratingWorkflow.Follow(_home, Rule(Gated), request);

    private Task<string?> LapsedAsync(IntegrationRequest request, DriverConfig config, IIntegrationAppointments appointments) =>
        IntegrationAuthority.LapsedAsync(request, config, _home, appointments, default);

    private static IntegrationRequest Request(IntegrationRequester requester) =>
        new("r1", "engine", "aurora", "feature/q1-fix", Tip, IntegrationRoutes.Recorded, "main", requester, Stewarded.Resolved(), At);

    private sealed class Seam(string? lapsed) : IIntegrationAppointments
    {
        public int Asked { get; private set; }

        public Task<string?> LapsedAsync(IntegrationRequest request, CancellationToken ct)
        {
            Asked++;
            return Task.FromResult(lapsed);
        }
    }

    [Fact]
    public async Task A_person_s_request_stands_whatever_the_rule_says_now_and_asks_no_appointment()
    {
        var seam = new Seam("revoked");

        Assert.Null(await LapsedAsync(Request(IntegrationRequester.Card), DriverConfig.Empty, seam));
        Assert.Null(await LapsedAsync(Request(IntegrationRequester.Card), Rule(new LandingIntegrate()), seam));
        Assert.Equal(0, seam.Asked);
    }

    /// <summary>
    /// INTEGRATE1g: a unit's <c>integrate</c> node asks on its landing's acceptance, which the door judged. The person's Accept stands on its own,
    /// as their card does; a version's own step, whose landing kept the version as its gate proof, stands whatever the line says, since a bound
    /// version is never edited; an automatic landing with no version behind it stands while the place's workflow says <c>request: done</c>, and
    /// lapses where it names no requester.
    /// </summary>
    [Fact]
    public async Task A_nodes_request_stands_on_the_acceptance_its_landing_recorded()
    {
        IntegrationRequest Node(string acceptedBy, LandingWorkflow? workflow = null) =>
            Request(IntegrationRequester.NodeStart) with { Route = IntegrationRoutes.Node, Proof = new IntegrationProof(acceptedBy) { Workflow = workflow } };
        var owner = Rule(Gated);

        Assert.Null(await LapsedAsync(Node(AcceptedBy.Person), owner, new Seam("revoked")));
        Assert.Null(await LapsedAsync(Node(AcceptedBy.Auto), Following(LandingIntegrate.Done), new Seam(null)));
        Assert.Contains("request: owner", await LapsedAsync(Node(AcceptedBy.Auto), owner, new Seam(null)), StringComparison.Ordinal);
        Assert.Null(await LapsedAsync(Node(AcceptedBy.Auto, new LandingWorkflow("integrated", 1, WorkflowLevels.Repository)), Rule(null), new Seam(null)));
    }

    /// <summary>
    /// Daoris's sweep stands while the place's workflow's <c>integrate</c> still says <c>request: done</c>; a step saying <c>owner</c>, a place
    /// whose workflow names none, and a line that no longer carries a policy each lapse it.
    /// </summary>
    [Fact]
    public async Task Daoris_s_sweep_stands_only_while_the_places_workflow_still_says_done()
    {
        var sweep = Request(IntegrationRequester.Sweep);

        Assert.Null(await LapsedAsync(sweep, Following(LandingIntegrate.Done), new Seam(null)));
        Assert.Contains("request: owner", await LapsedAsync(sweep, Following(LandingIntegrate.Owner), new Seam(null)), StringComparison.Ordinal);
        Assert.Contains("request: owner", await LapsedAsync(sweep, Rule(Gated), new Seam(null)), StringComparison.Ordinal);
        Assert.Contains("no longer carries `integrate`", await LapsedAsync(sweep, Rule(null), new Seam(null)), StringComparison.Ordinal);
    }

    /// <summary>A steward's request stands while the place's workflow's <c>integrate</c> says <c>steward</c> and the seam says its appointment is live.</summary>
    [Fact]
    public async Task A_steward_s_request_stands_only_while_the_places_workflow_says_steward_and_its_appointment_is_live()
    {
        var request = Request(IntegrationRequester.StewardAct("act-1", 3));
        var stewarded = Following(LandingIntegrate.Steward);
        var live = new Seam(null);

        Assert.Null(await LapsedAsync(request, stewarded, live));
        Assert.Equal(1, live.Asked);

        var revoked = new Seam("the appointment's generation 3 was revoked");
        Assert.Equal("the appointment's generation 3 was revoked", await LapsedAsync(request, stewarded, revoked));
        Assert.Equal(1, revoked.Asked);
    }

    [Fact]
    public async Task A_steward_s_request_lapses_before_the_appointment_is_asked_where_the_workflow_changed_or_the_act_names_no_generation()
    {
        var seam = new Seam(null);
        var request = Request(IntegrationRequester.StewardAct("act-1", 3));

        Assert.Contains("request: done", await LapsedAsync(request, Following(LandingIntegrate.Done), seam), StringComparison.Ordinal);
        Assert.Contains("request: owner", await LapsedAsync(request, Rule(Gated), seam), StringComparison.Ordinal);
        Assert.Contains("no longer carries `integrate`", await LapsedAsync(request, Rule(null), seam), StringComparison.Ordinal);
        var nameless = Request(new IntegrationRequester(IntegrationRequester.Steward, IntegrationDoors.Act));
        Assert.Contains("names no act", await LapsedAsync(nameless, Following(LandingIntegrate.Steward), seam), StringComparison.Ordinal);
        Assert.Equal(0, seam.Asked);
    }

    [Fact]
    public async Task With_no_appointment_to_read_the_seam_says_so_and_an_unknown_requester_is_none()
    {
        var why = await IntegrationAppointments.Unread.LapsedAsync(Request(IntegrationRequester.StewardAct("act-1", 3)), default);

        Assert.Contains("nothing in this build reads", why, StringComparison.Ordinal);
        Assert.Contains("act-1", why, StringComparison.Ordinal);
        var stranger = Request(new IntegrationRequester("agent", "somewhere"));
        Assert.Contains("no requester this build knows", await LapsedAsync(stranger, Rule(Gated), new Seam(null)), StringComparison.Ordinal);
    }
}
