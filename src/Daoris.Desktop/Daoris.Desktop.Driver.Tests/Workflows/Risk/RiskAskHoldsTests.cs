using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests.Risk;

/// <summary>
/// The holds outside a run's step in shadow: an open go-ahead, a parked question an agent marked as the owner's and a
/// permission request held under asking prompts each journal the verdict the judge would give, at the asker's place, beside what today
/// does, deciding nothing.
/// </summary>
public sealed class RiskAskHoldsTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "risk-ask-holds", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() => GitSeed.Discard(_home);

    private static readonly DateTimeOffset Now = new(2026, 11, 24, 9, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlyCollection<PriorSession> Records =
    [
        new("s-app", null, SessionStates.AwaitingPerson, Repository: "app"),
        new("s-ended", null, "completed", Repository: "lib"),
    ];

    private static string? WorkspaceOf(string repository) => repository == "app" ? "work" : null;

    private static GoAheadView GoAhead(int number, string kind, string on, string session = "s-app") =>
        new(number, kind, on, "the dashboard configuration", [new GoAheadRequestView(session, null, Now, "it needs it")]);

    private static AskView Ask(params GoAheadView[] goAheads) => new("#a1", "work", "do it", "open", "tier") { GoAheads = goAheads };

    private IReadOnlyList<RiskJudged> Journal() => RiskJournal.Read(_home);

    [Fact]
    public void Each_go_ahead_kind_reads_the_act_its_row_names_and_a_sign_in_none()
    {
        Assert.Equal("remote.push", RiskAskHolds.GoAheadAct("push", "origin")?.ToString());
        Assert.Equal("release.start/production", RiskAskHolds.GoAheadAct("release", "production")?.ToString());
        Assert.Equal("release.start/production", RiskAskHolds.GoAheadAct("write", "production")?.ToString());
        Assert.Equal("release.start/staging", RiskAskHolds.GoAheadAct("run", "Staging")?.ToString());
        // A target that is no destination's name is the act unqualified, never a qualifier of the asker's words.
        Assert.Equal("release.start", RiskAskHolds.GoAheadAct("write", "the team's wiki page")?.ToString());
        // A sign-in is a person in a browser: a dependency, no act of the policy.
        Assert.Null(RiskAskHolds.GoAheadAct("sign-in", "production"));
        Assert.Null(RiskAskHolds.GoAheadAct("dance", "production"));
    }

    [Fact]
    public void Each_owner_mark_reads_its_kind_and_a_sign_in_or_a_mark_this_build_does_not_know_none()
    {
        Assert.Equal(
            [RiskActs.DirectionSet, RiskActs.SpendMoney, null, RiskActs.ReleaseStart, RiskActs.HistoryRewrite, RiskActs.DataDelete, RiskActs.PublishOutside],
            OwnerClass.Kinds.Select(mark => RiskAskHolds.MarkAct(mark)?.Kind));
        Assert.Null(RiskAskHolds.MarkAct("whatever"));
        Assert.Null(RiskAskHolds.MarkAct(null));
    }

    [Fact]
    public void An_open_go_ahead_journals_its_verdict_at_the_askers_place_beside_todays_holder_and_an_answered_one_none()
    {
        var answered = GoAhead(3, "push", "origin") with { Answer = new GoAheadAnswerView(true, null, Now) };
        var config = DriverConfig.Parse("""{ "placeRisk": { "repository/app": { "risk": "high" } } }""");
        var holds = RiskAskHolds.GoAheads([Ask(GoAhead(1, "write", "production"), GoAhead(2, "sign-in", "production"), answered)], Records, config, WorkspaceOf);

        new RiskShadow(_home, () => Now).Look(RiskAskHolds.GoAheadDomain, RiskPolicy.Of(config), holds, _ => RiskAskHolds.GoAheadReleased);

        var line = Assert.Single(Journal());
        Assert.Equal(("goahead:a1/1", RiskActs.ReleaseStart, "production", "work", "app"), (line.Key, line.Kind, line.Qualifier, line.Workspace, line.Repository));
        Assert.Equal(("session:s-app", true), (line.Actor, line.TodayHeld));
        // A high-risk place keeps it the owner role's by the judge's own verdict: today and the shadow agree.
        Assert.Contains(SupervisorWhy.PolicyOwner, line.Today, StringComparison.Ordinal);
        Assert.Equal((RiskVerdict.OwnerOutcome, true), (line.Outcome, line.Agrees));
    }

    [Fact]
    public void A_go_ahead_is_placed_in_its_repositorys_own_workspace_as_its_door_places_it_not_the_asks()
    {
        var elsewhere = Ask(GoAhead(1, "write", "production")) with { Workspace = "other" };
        var hold = Assert.Single(RiskAskHolds.GoAheads([elsewhere], Records, DriverConfig.Parse("{}"), WorkspaceOf));
        Assert.Equal(("work", "app"), (hold.Place.Workspace, hold.Place.Repository));
    }

    [Fact]
    public void A_go_ahead_whose_askers_have_all_ended_is_no_hold_and_one_at_a_marked_place_waits_on_the_owner_today_and_by_the_judge()
    {
        var config = DriverConfig.Parse("""{ "placeRisk": { "workspace/work": { "risk": "low", "marks": ["release.production"] } } }""");

        Assert.Empty(RiskAskHolds.GoAheads([Ask(GoAhead(1, "release", "production", session: "s-ended"))], Records, config, WorkspaceOf));

        var hold = Assert.Single(RiskAskHolds.GoAheads([Ask(GoAhead(1, "release", "production"))], Records, config, WorkspaceOf));
        Assert.Contains(SupervisorWhy.PolicyOwner, hold.Today, StringComparison.Ordinal);
        Assert.Equal(RiskVerdict.OwnerOutcome, RiskGate.Judge(RiskPolicy.Of(config), hold.Act, hold.Actor, hold.Place, hold.Subject).Outcome);
    }

    [Fact]
    public void A_parked_question_marked_as_the_owners_journals_its_marks_kind_and_an_unmarked_one_none()
    {
        var marked = new ParkQuestion("q1", "s-app", "Which target?", [], null, null, Now, Owner: OwnerClass.Direction);
        var unmarked = new ParkQuestion("q2", "s-app", "Which file?", [], null, null, Now);
        var config = DriverConfig.Parse("{}");
        var holds = RiskAskHolds.Questions([marked, unmarked], Records, config, WorkspaceOf);

        new RiskShadow(_home, () => Now).Look(RiskAskHolds.QuestionDomain, RiskPolicy.Of(config), holds, _ => RiskAskHolds.QuestionReleased);

        var line = Assert.Single(Journal());
        Assert.Equal(("question:s-app/q1", RiskActs.DirectionSet, "app"), (line.Key, line.Kind, line.Repository));
        // Today a marked question is the owner role's; the seed would pass the act: a disagreement, measurable.
        Assert.Equal((true, RiskVerdict.Pass, false), (line.TodayHeld, line.Outcome, line.Agrees));
    }

    [Fact]
    public void A_held_permission_is_reach_widen_at_its_sessions_place_held_for_the_person_today_and_the_seeds_approver_restricted()
    {
        var held = new HeldPermission("s-app", "r7", "call-1", "Run the build", [new PermissionChoice("allow", "Allow", PermissionChoice.AllowOnce)], Now);
        var config = DriverConfig.Parse("""{ "access": { "app": { "permissions": "asking" } } }""");
        var holds = RiskAskHolds.Permissions([held], Records, config, WorkspaceOf);

        new RiskShadow(_home, () => Now).Look(RiskAskHolds.PermissionDomain, RiskPolicy.Of(config), holds, _ => RiskAskHolds.PermissionReleased);

        var line = Assert.Single(Journal());
        Assert.Equal(("permission:s-app/r7", RiskActs.ReachWiden, RiskPlace.RestrictedMode), (line.Key, line.Kind, line.Mode));
        Assert.Equal((RiskVerdict.Gate, RiskClasses.Role, StepRoles.Approver, true), (line.Outcome, line.Class, line.By, line.TodayHeld));
    }

    [Fact]
    public void A_hold_point_that_cannot_read_its_input_journals_nothing_and_fails_nothing()
    {
        var config = DriverConfig.Parse("{}");
        RiskAskHolds.Shadow(new RiskShadow(_home, () => Now), RiskAskHolds.GoAheadDomain, config, () => throw new IOException("unreadable"), _ => RiskAskHolds.GoAheadReleased);
        Assert.Empty(Journal());
    }
}
