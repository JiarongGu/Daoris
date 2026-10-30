using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// INTEGRATE1e part e4b (D215's note; the integration design §5.2 step 8, §8): the requester's authority judged again at promotion, from the
/// rule as it stands now and the appointment seam. A person's request stands on its own; Daoris's sweep stands while the rule still says
/// <c>request: done</c>; a steward's stands while the rule still says <c>request: steward</c>, its act names its generation, and the seam says
/// its appointment is live. The seam's default reads none, so a steward's request is held. Nothing here starts a process.
/// </summary>
public sealed class IntegrationAuthorityTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);

    private const string Tip = "1111111111111111111111111111111111111111";

    private static readonly LandingIntegrate Stewarded = new(Request: LandingIntegrate.Steward, Batch: 8, Gates: LandingIntegrate.Declared);

    private static DriverConfig Rule(LandingIntegrate? integrate)
    {
        // `request: done` is the quest's done through the rule's own auto-accept, which the rule must have on.
        var config = DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", AutoAccept: integrate?.Request == LandingIntegrate.Done));
        return integrate is null ? config : config.WithLandingIntegrate("engine", integrate);
    }

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
        Assert.Null(await IntegrationAuthority.LapsedAsync(Request(IntegrationRequester.Card), Rule(new LandingIntegrate(Request: LandingIntegrate.Owner)), seam, default));
        Assert.Equal(0, seam.Asked);
    }

    [Fact]
    public async Task Daoris_s_sweep_stands_only_while_the_rule_still_integrates_under_request_done()
    {
        var sweep = Request(IntegrationRequester.Sweep);

        Assert.Null(await IntegrationAuthority.LapsedAsync(sweep, Rule(new LandingIntegrate(Request: LandingIntegrate.Done, Batch: 8, Gates: LandingIntegrate.Declared)), new Seam(null), default));
        Assert.Contains("request: owner", await IntegrationAuthority.LapsedAsync(sweep, Rule(new LandingIntegrate(Request: LandingIntegrate.Owner)), new Seam(null), default), StringComparison.Ordinal);
        Assert.Contains("no longer carries `integrate`", await IntegrationAuthority.LapsedAsync(sweep, Rule(null), new Seam(null), default), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_steward_s_request_stands_only_while_the_rule_says_steward_and_its_appointment_is_live()
    {
        var request = Request(IntegrationRequester.StewardAct("act-1", 3));
        var live = new Seam(null);

        Assert.Null(await IntegrationAuthority.LapsedAsync(request, Rule(Stewarded), live, default));
        Assert.Equal(1, live.Asked);

        var revoked = new Seam("the appointment's generation 3 was revoked");
        Assert.Equal("the appointment's generation 3 was revoked", await IntegrationAuthority.LapsedAsync(request, Rule(Stewarded), revoked, default));
        Assert.Equal(1, revoked.Asked);
    }

    [Fact]
    public async Task A_steward_s_request_lapses_before_the_appointment_is_asked_where_the_rule_changed_or_the_act_names_no_generation()
    {
        var seam = new Seam(null);
        var request = Request(IntegrationRequester.StewardAct("act-1", 3));

        Assert.Contains("request: owner", await IntegrationAuthority.LapsedAsync(request, Rule(new LandingIntegrate(Request: LandingIntegrate.Owner)), seam, default), StringComparison.Ordinal);
        Assert.Contains("no longer carries `integrate`", await IntegrationAuthority.LapsedAsync(request, Rule(null), seam, default), StringComparison.Ordinal);
        var nameless = Request(new IntegrationRequester(IntegrationRequester.Steward, IntegrationDoors.Act));
        Assert.Contains("names no act", await IntegrationAuthority.LapsedAsync(nameless, Rule(Stewarded), seam, default), StringComparison.Ordinal);
        Assert.Equal(0, seam.Asked);
    }

    [Fact]
    public async Task With_no_appointment_to_read_a_steward_s_request_lapses_and_says_so_and_an_unknown_requester_is_none()
    {
        var request = Request(IntegrationRequester.StewardAct("act-1", 3));

        var why = await IntegrationAuthority.LapsedAsync(request, Rule(Stewarded), IntegrationAppointments.Unread, default);

        Assert.Contains("nothing in this build reads", why, StringComparison.Ordinal);
        Assert.Contains("act-1", why, StringComparison.Ordinal);
        var stranger = Request(new IntegrationRequester("agent", "somewhere"));
        Assert.Contains("no requester this build knows", await IntegrationAuthority.LapsedAsync(stranger, Rule(Stewarded), new Seam(null), default), StringComparison.Ordinal);
    }
}
