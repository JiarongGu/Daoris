using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The week's capped reviewer replayed (2026-11-16): every declared reviewer of a second opinion cooling makes a vacancy on the reporting
/// line, which waits on the repository's supervisor; an agent-held one re-casts it on another family, journalled beside the gate, and the
/// next walk reads it; with none staffed it passes up naming why; a level's leave is its pass up, and the machine's supervisor's ends it
/// as gave up.
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
    private static readonly RoleSlot AtEngine = Supervisor("engine-supervisor", new RoleScope(RoleScope.Repository, Engine), RoleHolders.Agent, Recast);

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
    public async Task Every_declared_reviewer_cooling_waits_on_the_repositorys_supervisor_which_recasts_on_another_family_journalled()
    {
        var capped = await Walk(["codex-acp"]);
        var vacancy = OpinionVacancy.Of(capped);

        Assert.NotNull(vacancy);
        Assert.Equal((ReviewerUnavailable.Cooling, "codex-acp", Reset), (vacancy.Code, vacancy.Back, vacancy.Until));
        var waits = Line(AtEngine, Machine).Climb(ownerClass: false);
        Assert.Equal(
            "waiting on the repository's supervisor: `engine-supervisor` · second opinion: nobody free, `codex-acp` back 07:02", vacancy.Said(waits, TimeZoneInfo.Utc));

        var casts = new OpinionCasts(_home);
        casts.Keep(Chain, Engine, new OpinionCast(At, "s1", LineLevels.Repository, "engine-supervisor", SupervisorKinds.Recast, "dsh", "codex is cooling until 07:02; dsh reads now"));

        var kept = Assert.Single(casts.Read(Chain, Engine, "s1"));
        Assert.Equal((LineLevels.Repository, "engine-supervisor", SupervisorKinds.Recast, "dsh"), (kept.Level, kept.Role, kept.Act, kept.Agent));
        var reviewers = OpinionCasting.Reviewers(["codex-acp"], kept, Working);
        Assert.Equal(["dsh", "codex-acp"], reviewers);
        var recast = await Walk(reviewers, free: "dsh");
        Assert.Equal("dsh", recast.Reviewer);
    }

    [Fact]
    public async Task With_no_repositorys_supervisor_staffed_the_vacancy_passes_up_naming_why()
    {
        var vacancy = OpinionVacancy.Of(await Walk(["codex-acp"]))!;

        var waits = Line(Machine).Climb(ownerClass: false);

        Assert.Equal(LineLevels.Machine, waits.At!.Level);
        Assert.Equal(
            "waiting on the machine's supervisor: you · passed up: the repository's supervisor: no supervisor of the repository `engine` or its workspace"
            + " decides `vacancy` · second opinion: nobody free, `codex-acp` back 07:02",
            vacancy.Said(waits, TimeZoneInfo.Utc));
    }

    [Fact]
    public void A_levels_leave_passes_it_up_and_the_machines_supervisors_ends_it_as_gave_up()
    {
        var machine = Supervisor(AppointedRoles.Supervisor, RoleScope.Everywhere, RoleHolders.Agent, Recast);
        var casts = new OpinionCasts(_home);
        casts.Keep(Chain, Engine, new OpinionCast(At, "s1", LineLevels.Repository, "engine-supervisor", LinePasses.Left, null, "no other family is installed"));

        Assert.Equal(LineLevels.Machine, Line(AtEngine, machine).Climb(false, OpinionCasting.Left(casts.Read(Chain, Engine, "s1"))).At!.Level);

        casts.Keep(Chain, Engine, new OpinionCast(At.AddMinutes(5), "s1", LineLevels.Machine, AppointedRoles.Supervisor, LinePasses.Left, null, "no account of another maker is signed in"));
        var ended = Line(AtEngine, machine).Climb(false, OpinionCasting.Left(casts.Read(Chain, Engine, "s1")));

        Assert.True(ended.GaveUp);
        Assert.Equal(
            "gave up: the repository's supervisor: no other family is installed; the machine's supervisor: no account of another maker is signed in", ended.Said());
        Assert.Empty(casts.Read(Chain, Engine, "s2"));
    }

    [Fact]
    public void Each_act_shapes_the_next_walk_and_a_vacancy_is_only_a_reviewer_held()
    {
        OpinionCast Cast(string act, string? agent = null) => new(At, "s1", LineLevels.Repository, "engine-supervisor", act, agent, "why");

        Assert.Equal(["codex-acp", "claude-code-acp"], OpinionCasting.Reviewers(["codex-acp"], Cast(SupervisorKinds.Relax), Working));
        Assert.Equal([OpinionCasting.PrepaidAgent, "codex-acp"], OpinionCasting.Reviewers(["codex-acp"], Cast(SupervisorKinds.Prepaid), Working));
        Assert.Equal(["codex-acp"], OpinionCasting.Reviewers(["codex-acp"], Cast(SupervisorKinds.Wait), Working));
        Assert.Equal(["codex-acp"], OpinionCasting.Reviewers(["codex-acp"], null, Working));
        Assert.Null(OpinionVacancy.Of(new ReviewerChoice("codex-acp", ReviewerLabels.AnotherMaker, null, "reads it")));
    }

    /// <summary>The look's <c>vacant</c> on <paramref name="casts"/>, offering the declared reviewers its walk found installed.</summary>
    private static void Vacant(OpinionCasts casts, IReadOnlyList<string> declared, params ReviewerTried[] tried)
    {
        var walk = new ReviewerChoice(null, null, ReviewerUnavailable.Cooling, "nobody free") { Tried = tried };
        casts.Keep(Chain, Engine, new OpinionCast(At, "s1", OpinionCasting.Step, OpinionCasting.Step, OpinionCasting.Vacant, "codex-acp", ReviewerUnavailable.Cooling)
        {
            Offered = OpinionCasting.Offered(declared, walk),
        });
    }

    private static ReviewerTried Tried(string agent, string held) => new(agent, ReviewerLabels.AnotherMaker, held, "held");

    [Fact]
    public void A_recast_names_a_declared_reviewer_installed_and_the_prepaid_one_only_where_its_act_is_granted()
    {
        var casts = new OpinionCasts(_home);
        Vacant(casts, ["codex-acp", "gemini", OpinionCasting.PrepaidAgent],
            Tried("codex-acp", ReviewerHeld.Cooling), Tried("gemini", ReviewerHeld.NotInstalled), Tried(OpinionCasting.PrepaidAgent, ReviewerHeld.Cooling));
        var kept = casts.Read(Chain, Engine, "s1");
        var recast = SupervisorKinds.Qualified(SupervisorKinds.Vacancy, SupervisorKinds.Recast);

        Assert.Equal(["codex-acp", OpinionCasting.PrepaidAgent], Assert.Single(kept).Offered);
        Assert.Equal(SupervisorWhy.NotOfferedChoice, OpinionCasting.Refusal(kept, SupervisorKinds.Recast, "claude-code-acp"));
        Assert.Equal(SupervisorWhy.NotOfferedChoice, OpinionCasting.Refusal(kept, SupervisorKinds.Recast, "gemini"));
        Assert.Null(OpinionCasting.Refusal(kept, SupervisorKinds.Recast, OpinionCasting.PrepaidAgent));
        // The prepaid one spends money: the census asks its covering grant too.
        Assert.Equal("vacancy.prepaid", ActSpends.Missing([[recast]], recast, OpinionCasting.Spends(SupervisorKinds.Recast, OpinionCasting.PrepaidAgent), _ => RiskVerdict.Gate));
        Assert.Null(ActSpends.Missing([[recast]], recast, OpinionCasting.Spends(SupervisorKinds.Recast, "codex-acp"), _ => RiskVerdict.Gate));
        Assert.Null(OpinionCasting.Refusal(kept, SupervisorKinds.Wait, null));
    }

    [Fact]
    public async Task A_filled_vacancy_ends_its_acts_and_the_next_walk_follows_the_rules_order()
    {
        IReadOnlyList<string> declared = ["codex-acp", "gemini"];
        var casts = new OpinionCasts(_home);
        Vacant(casts, declared, Tried("codex-acp", ReviewerHeld.Cooling), Tried("gemini", ReviewerHeld.Cooling));
        casts.Keep(Chain, Engine, new OpinionCast(At.AddMinutes(1), "s1", LineLevels.Repository, "engine-supervisor", LinePasses.Left, null, "no reset is stated"));
        casts.Keep(Chain, Engine, new OpinionCast(At.AddMinutes(2), "s1", LineLevels.Machine, AppointedRoles.Supervisor, SupervisorKinds.Recast, "gemini", "gemini resets first"));
        Assert.Equal(["gemini", "codex-acp"], OpinionCasting.Reviewers(declared, OpinionCasting.Newest(casts.Read(Chain, Engine, "s1")), Working));

        casts.Keep(Chain, Engine, new OpinionCast(At.AddMinutes(3), "s1", OpinionCasting.Step, OpinionCasting.Step, OpinionCasting.Filled, "gemini", "op1"));
        var filled = casts.Read(Chain, Engine, "s1");
        var reviewers = OpinionCasting.Reviewers(declared, OpinionCasting.Newest(filled), Working);

        Assert.Equal(declared, reviewers);
        Assert.False(OpinionCasting.Waiting(filled));
        Assert.Empty(OpinionCasting.Left(filled));
        Assert.Equal("codex-acp", (await Walk(reviewers, free: "codex-acp")).Reviewer);
    }
}
