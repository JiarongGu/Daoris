using Daoris.Driver;
using Microsoft.Data.Sqlite;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The go-ahead kind: an appointed role granted <c>goahead.answer</c> answers a go-ahead at low and medium risk, held for the
/// role's window with <i>Overrule</i> and pinned to its number, kind, target and act, then posted as the role's yes or no; a marked
/// production release, a high-risk place and a sign-in stay the owner role's, refused below it with nothing kept.
/// </summary>
public sealed class GoAheadAnswerTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "goahead-answer", Guid.NewGuid().ToString("N")[..8]);

    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-16T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    private static readonly SupervisorHolder Claude = SupervisorHolders.OfAgent(new("reports-supervisor", 2, "claude-acp", "opus", "high", "opus", "read-only", "t1"));

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private sealed class Holds(params string[] granted) : IDecisionAdmission
    {
        /// <summary>The home's risk record as it stands now; a test raises or marks it inside the window.</summary>
        public DriverConfig Now { get; set; } = Risk(PlaceRisks.Low);

        public DecisionAdmitted Admit(string kind, RolePlace place, string? act) =>
            act is null || granted.Contains(SupervisorKinds.Qualified(kind, act))
                ? new("g-1", null) { Window = TimeSpan.FromMinutes(10), Role = "reports-supervisor" }
                : DecisionAdmitted.Refused(SupervisorWhy.NotGranted);

        public string? GoAheadOwners(RolePlace place, string kind, string on, string? acting) => RiskAskHolds.GoAheadOwners(Now, place, kind, on);
    }

    private static DoorRelease Posting(List<SupervisorDoorDue> posted) => new(
        (_, _) => Task.FromResult<string?>(null),
        (due, _) =>
        {
            posted.Add(due);
            return Task.FromResult<string?>(null);
        },
        (_, _) => Task.FromResult<bool?>(true));

    private static readonly GoAheadPin Write = new(2, "write", "staging", "the dashboard's configuration");

    private static SupervisorDoorAnswer Answer(GoAheadPin pin, string choice = SupervisorDoorActs.Approved) => new(
        SupervisorSubject.GoAhead("s1", "a7", pin.Number), "reports", "g-1", SupervisorKinds.Answer, choice,
        [new SupervisorReason("The write is the plan's own step on staging.", "the ask's plan")], Claude) { Pin = pin.Said };

    private static DriverConfig Risk(string risk, params string[] marks) => DriverConfig.Parse(
        $$"""{ "placeRisk": { "repository/reports": { "risk": "{{risk}}", "marks": [{{string.Join(", ", marks.Select(mark => $"\"{mark}\""))}}] } } }""");

    private static string? Refusal(DriverConfig config, GoAheadPin pin) =>
        GoAheadAuthority.Refusal(pin.Kind, RiskAskHolds.GoAheadOutcome(RiskPolicy.Of(config), RiskPlace.Of(config, null, "reports"), pin.Kind, pin.On), [[GoAheadAuthority.Act]]);

    [Fact]
    public async Task An_appointed_role_answers_a_low_risk_go_ahead_and_it_holds_for_overrule_then_goes_out_as_the_roles()
    {
        var decisions = new QuestionSupervisorDecisions(_home, new Holds(GoAheadAuthority.Act));
        var refused = Refusal(Risk(PlaceRisks.Low), Write);

        var kept = await decisions.DoorAsync(Answer(Write), refused, At);

        Assert.True(kept.Kept, kept.Why);
        Assert.Equal(At.AddMinutes(10), kept.GoesOnAt);
        Assert.False(decisions.RefersDoor(SupervisorSubject.GoAhead("s1", "a7", 2)));
        var posted = new List<SupervisorDoorDue>();
        var release = new DoorRelease(
            (due, _) => Task.FromResult<string?>(due.Pin == Write.Said ? null : SupervisorWhy.SubjectChanged),
            (due, _) => { posted.Add(due); return Task.FromResult<string?>(null); },
            (_, _) => Task.FromResult<bool?>(true));
        Assert.Empty(await decisions.ReleaseDoorsAsync(release, At.AddMinutes(5)));
        Assert.Equal([SupervisorState.WentOn], await decisions.ReleaseDoorsAsync(release, At.AddMinutes(11)));
        var due = Assert.Single(posted);
        Assert.Equal(("reports-supervisor", true, "2|write|staging|the dashboard's configuration"), (due.Role, due.Yes, due.Pin));
    }

    [Fact]
    public async Task A_marked_production_release_is_refused_below_the_owner_role_and_nothing_is_kept()
    {
        var decisions = new QuestionSupervisorDecisions(_home, new Holds(GoAheadAuthority.Act));
        var release = new GoAheadPin(3, GoAheadAuthority.Release, "production", "release 4.2 of the reports app");
        var refused = Refusal(Risk(PlaceRisks.Low, PlaceMarks.ProductionReleases), release);

        var kept = await decisions.DoorAsync(Answer(release), refused, At);

        Assert.Equal((false, SupervisorWhy.PolicyOwner), (kept.Kept, kept.Why));
        Assert.True(decisions.RefersDoor(SupervisorSubject.GoAhead("s1", "a7", 3)));
    }

    /// <summary>
    /// 🔴 Risk and marks are judged again at the release: a place the owner marks or raises to high risk inside the window keeps the held
    /// yes from going out, nothing posted, and the go-ahead is the owner role's again.
    /// </summary>
    [Theory]
    [InlineData(PlaceRisks.Low, PlaceMarks.ProductionReleases)]
    [InlineData(PlaceRisks.High, null)]
    public async Task A_place_marked_or_raised_inside_the_window_keeps_the_held_yes_from_going_out(string risk, string? mark)
    {
        var admission = new Holds(GoAheadAuthority.Act);
        var decisions = new QuestionSupervisorDecisions(_home, admission);
        Assert.True((await decisions.DoorAsync(Answer(Write), null, At)).Kept);

        admission.Now = mark is null ? Risk(risk) : Risk(risk, mark);
        var posted = new List<SupervisorDoorDue>();

        Assert.Equal([SupervisorState.Stale], await decisions.ReleaseDoorsAsync(Posting(posted), At.AddMinutes(11)));
        Assert.Empty(posted);
        Assert.Equal(SupervisorState.Stale, Assert.Single(decisions.DoorsOf("s1")).State);
    }

    [Fact]
    public async Task A_role_without_the_grant_is_refused_and_a_choice_that_is_no_yes_or_no_is_not_offered()
    {
        var ungranted = await new QuestionSupervisorDecisions(_home, new Holds()).DoorAsync(Answer(Write), null, At);
        var unoffered = await new QuestionSupervisorDecisions(_home, new Holds(GoAheadAuthority.Act)).DoorAsync(Answer(Write, SupervisorDoorActs.Keep), null, At);

        Assert.Equal(SupervisorWhy.NotGranted, ungranted.Why);
        Assert.Equal(SupervisorWhy.NotOffered, unoffered.Why);
    }

    [Fact]
    public void The_census_names_what_the_answer_spends_and_its_pin()
    {
        Assert.Equal(GoAheadAuthority.Act, SupervisorKinds.Qualified(SupervisorKinds.GoAhead, SupervisorKinds.Answer));
        Assert.Contains(GoAheadAuthority.Act, SupervisorKinds.Acts);
        Assert.Equal("the go-ahead's number, kind, target and act", ActSpends.Of(GoAheadAuthority.Act)!.Pin);
    }
}
