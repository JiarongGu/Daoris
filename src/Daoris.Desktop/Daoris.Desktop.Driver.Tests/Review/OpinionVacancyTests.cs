using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The week's capped reviewer replayed (2026-11-16): every declared reviewer of a second opinion cooling makes a vacancy on the reporting
/// line, which waits on the lead; an agent-held lead re-casts it on another family, journalled beside the gate, and the next walk reads
/// it; with no lead staffed it passes up naming why; a level's leave is its pass up, and the head's ends it as gave up.
/// </summary>
public sealed class OpinionVacancyTests : IDisposable
{
    private const string Engine = "engine";
    private const string Chain = "q1";
    private static readonly DateTimeOffset Reset = new(2026, 11, 16, 7, 2, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset At = new(2026, 11, 16, 3, 40, 0, TimeSpan.Zero);
    private static readonly string Recast = SupervisorKinds.Qualified(SupervisorKinds.Vacancy, SupervisorKinds.Recast);
    private static readonly IReadOnlyList<string> Working = ["claude-code-acp"];

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-vacancy-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private static RoleSlot Supervisor(string id, RoleScope scope, string holder, params string[] acts)
    {
        var role = new RoleRecord(id, AuthoredText.Of(id), RoleRecord.Appointed, [], scope, new RoleHold(AppointedRoles.Supervisor, acts, holder));
        return new RoleSlot(id, RoleState.Set, role, null, 1, scope, Appointed: true);
    }

    private static readonly RoleSlot Machine = Supervisor(AppointedRoles.Supervisor, RoleScope.Everywhere, RoleHolders.Rules, [.. SupervisorKinds.Seeded]);
    private static readonly RoleSlot Lead = Supervisor("engine-lead", new RoleScope(RoleScope.Repository, Engine), RoleHolders.Agent, Recast);

    private static ReportingLine Line(params RoleSlot[] slots) =>
        ReportingLine.Of(slots, SupervisorKinds.Vacancy, new RolePlace(Repository: Engine), null, SupervisorKinds.Recast);

    /// <summary>The walk over <paramref name="reviewers"/> where every one but <paramref name="free"/> is cooling until the reset.</summary>
    private static Task<ReviewerChoice> Walk(IReadOnlyList<string> reviewers, string? free = null) =>
        ReviewerChoice.WalkAsync(
            ProviderRegistry.Built(), ActorChoices.Effective("opinion", DriverConfig.Empty, fence: [.. reviewers]), Working,
            entry => Task.FromResult<(HarnessSelection?, ReviewerTried?)>(entry.Family.Agent == free
                ? (new HarnessSelection(null), null)
                : (null, new ReviewerTried(entry.Family.Agent, entry.Label, ReviewerHeld.Cooling, "its accounts are cooling")
                {
                    Cooling = new CoolingEntry(entry.Family.Agent, "work", Reset, true, null, At, null),
                })));

    [Fact]
    public async Task Every_declared_reviewer_cooling_waits_on_the_lead_which_recasts_on_another_family_journalled()
    {
        var capped = await Walk(["codex-acp"]);
        var vacancy = OpinionVacancy.Of(capped);

        Assert.NotNull(vacancy);
        Assert.Equal((ReviewerUnavailable.Cooling, "codex-acp", Reset), (vacancy.Code, vacancy.Back, vacancy.Until));
        var waits = Line(Lead, Machine).Climb(ownerClass: false);
        Assert.Equal("waiting on lead: `engine-lead` · second opinion: nobody free, `codex-acp` back 07:02", vacancy.Said(waits, TimeZoneInfo.Utc));

        var casts = new OpinionCasts(_home);
        casts.Keep(Chain, Engine, new OpinionCast(At, "s1", LineActors.Lead, "engine-lead", SupervisorKinds.Recast, "dsh", "codex is cooling until 07:02; dsh reads now"));

        var kept = Assert.Single(casts.Read(Chain, Engine, "s1"));
        Assert.Equal((LineActors.Lead, "engine-lead", SupervisorKinds.Recast, "dsh"), (kept.Actor, kept.Role, kept.Act, kept.Agent));
        var reviewers = OpinionCasting.Reviewers(["codex-acp"], kept, Working);
        Assert.Equal(["dsh", "codex-acp"], reviewers);
        var recast = await Walk(reviewers, free: "dsh");
        Assert.Equal("dsh", recast.Reviewer);
    }

    [Fact]
    public async Task With_no_lead_staffed_the_vacancy_passes_up_naming_why()
    {
        var vacancy = OpinionVacancy.Of(await Walk(["codex-acp"]))!;

        var waits = Line(Machine).Climb(ownerClass: false);

        Assert.Equal(LineActors.Head, waits.At!.Actor);
        Assert.Equal(
            "waiting on head: you · passed up: lead: no lead is appointed: no supervisor of the repository `engine` or its workspace decides `vacancy`"
            + " · second opinion: nobody free, `codex-acp` back 07:02",
            vacancy.Said(waits, TimeZoneInfo.Utc));
    }

    [Fact]
    public void A_levels_leave_passes_it_up_and_the_heads_ends_it_as_gave_up()
    {
        var head = Supervisor(AppointedRoles.Supervisor, RoleScope.Everywhere, RoleHolders.Agent, Recast);
        var casts = new OpinionCasts(_home);
        casts.Keep(Chain, Engine, new OpinionCast(At, "s1", LineActors.Lead, "engine-lead", LinePasses.Left, null, "no other family is installed"));

        Assert.Equal(LineActors.Head, Line(Lead, head).Climb(false, OpinionCasting.Left(casts.Read(Chain, Engine, "s1"))).At!.Actor);

        casts.Keep(Chain, Engine, new OpinionCast(At.AddMinutes(5), "s1", LineActors.Head, AppointedRoles.Supervisor, LinePasses.Left, null, "no account of another maker is signed in"));
        var ended = Line(Lead, head).Climb(false, OpinionCasting.Left(casts.Read(Chain, Engine, "s1")));

        Assert.True(ended.GaveUp);
        Assert.Equal("gave up: lead: no other family is installed; head: no account of another maker is signed in", ended.Said());
        Assert.Empty(casts.Read(Chain, Engine, "s2"));
    }

    [Fact]
    public void Each_act_shapes_the_next_walk_and_a_vacancy_is_only_a_reviewer_held()
    {
        OpinionCast Cast(string act, string? agent = null) => new(At, "s1", LineActors.Lead, "engine-lead", act, agent, "why");

        Assert.Equal(["codex-acp", "claude-code-acp"], OpinionCasting.Reviewers(["codex-acp"], Cast(SupervisorKinds.Relax), Working));
        Assert.Equal([OpinionCasting.PrepaidAgent, "codex-acp"], OpinionCasting.Reviewers(["codex-acp"], Cast(SupervisorKinds.Prepaid), Working));
        Assert.Equal(["codex-acp"], OpinionCasting.Reviewers(["codex-acp"], Cast(SupervisorKinds.Wait), Working));
        Assert.Equal(["codex-acp"], OpinionCasting.Reviewers(["codex-acp"], null, Working));
        Assert.Null(OpinionVacancy.Of(new ReviewerChoice("codex-acp", ReviewerLabels.AnotherMaker, null, "reads it")));
    }
}
