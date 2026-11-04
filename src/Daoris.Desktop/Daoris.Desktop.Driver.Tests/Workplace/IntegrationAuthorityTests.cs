using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// INTEGRATE1e part e4b (D215's note; the integration design §5.2 step 8, §8): the requester's authority judged again at promotion, from the
/// line's policy as it stands now and the appointment seam. A person's request stands on its own, as does a version's own step; a line's
/// policy names no requester, so it reads <c>request: owner</c>, and Daoris's sweep and a steward's request lapse under it before the
/// appointment is asked. Nothing here starts a process.
/// </summary>
public sealed class IntegrationAuthorityTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);

    private const string Tip = "1111111111111111111111111111111111111111";

    private static readonly LandingIntegrate Stewarded = new(Request: LandingIntegrate.Steward, Batch: 8, Gates: LandingIntegrate.Declared);

    private static readonly LandingIntegrate Gated = new(Batch: 8, Gates: LandingIntegrate.Declared);

    private static DriverConfig Rule(LandingIntegrate? integrate) => DriverConfig.Empty.WithLinePolicy("engine", integrate);

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

        Assert.Null(await IntegrationAuthority.LapsedAsync(Request(IntegrationRequester.Card), DriverConfig.Empty, seam, default));
        Assert.Null(await IntegrationAuthority.LapsedAsync(Request(IntegrationRequester.Card), Rule(new LandingIntegrate()), seam, default));
        Assert.Equal(0, seam.Asked);
    }

    /// <summary>
    /// INTEGRATE1g: a unit's <c>integrate</c> node asks on its landing's acceptance, which the door judged. The person's Accept stands on its own,
    /// as their card does; a version's own step, whose landing kept the version as its gate proof, stands whatever the line says, since a bound
    /// version is never edited; an automatic landing with no version behind it lapses, as the line's policy reads <c>request: owner</c>.
    /// </summary>
    [Fact]
    public async Task A_nodes_request_stands_on_the_acceptance_its_landing_recorded()
    {
        IntegrationRequest Node(string acceptedBy, LandingWorkflow? workflow = null) =>
            Request(IntegrationRequester.NodeStart) with { Route = IntegrationRoutes.Node, Proof = new IntegrationProof(acceptedBy) { Workflow = workflow } };
        var owner = Rule(Gated);

        Assert.Null(await IntegrationAuthority.LapsedAsync(Node(AcceptedBy.Person), owner, new Seam("revoked"), default));
        Assert.Contains("request: owner", await IntegrationAuthority.LapsedAsync(Node(AcceptedBy.Auto), owner, new Seam(null), default), StringComparison.Ordinal);
        Assert.Null(await IntegrationAuthority.LapsedAsync(Node(AcceptedBy.Auto, new LandingWorkflow("integrated", 1, WorkflowLevels.Repository)), Rule(null), new Seam(null), default));
    }

    [Fact]
    public async Task Daoris_s_sweep_lapses_under_a_lines_policy_and_where_the_line_carries_none()
    {
        var sweep = Request(IntegrationRequester.Sweep);

        Assert.Contains("request: owner", await IntegrationAuthority.LapsedAsync(sweep, Rule(Gated), new Seam(null), default), StringComparison.Ordinal);
        Assert.Contains("no longer carries `integrate`", await IntegrationAuthority.LapsedAsync(sweep, Rule(null), new Seam(null), default), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_steward_s_request_lapses_before_the_appointment_is_asked_under_a_lines_policy_or_none()
    {
        var seam = new Seam(null);
        var request = Request(IntegrationRequester.StewardAct("act-1", 3));

        Assert.Contains("request: owner", await IntegrationAuthority.LapsedAsync(request, Rule(Gated), seam, default), StringComparison.Ordinal);
        Assert.Contains("no longer carries `integrate`", await IntegrationAuthority.LapsedAsync(request, Rule(null), seam, default), StringComparison.Ordinal);
        Assert.Equal(0, seam.Asked);
    }

    [Fact]
    public async Task With_no_appointment_to_read_the_seam_says_so_and_an_unknown_requester_is_none()
    {
        var why = await IntegrationAppointments.Unread.LapsedAsync(Request(IntegrationRequester.StewardAct("act-1", 3)), default);

        Assert.Contains("nothing in this build reads", why, StringComparison.Ordinal);
        Assert.Contains("act-1", why, StringComparison.Ordinal);
        var stranger = Request(new IntegrationRequester("agent", "somewhere"));
        Assert.Contains("no requester this build knows", await IntegrationAuthority.LapsedAsync(stranger, Rule(Gated), new Seam(null), default), StringComparison.Ordinal);
    }
}
