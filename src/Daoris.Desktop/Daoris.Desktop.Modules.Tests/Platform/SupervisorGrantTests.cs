using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A supervisor's decision is covered by its grants alone, read at the judge and at every admission: an act its <c>authority</c> does
/// not grant is refused <c>not-granted</c> and keeps nothing, a leave needs no grant, and the seed's three grants keep today's acts.
/// </summary>
/// <remarks>The judge alone, as <see cref="SupervisorScopeTests"/> holds it: a scratch home, the catalogue a list (<see cref="RoleKit.Staff"/>).</remarks>
public sealed class SupervisorGrantTests : IDisposable
{
    private const string Engine = "engine";

    private static readonly RolePlace Place = new(Repository: Engine);

    private static readonly SupervisorReason[] Reasons = [new("The ticket says monthly.", "the quest's line 2")];

    private readonly string _home = Path.Combine(AppContext.BaseDirectory, "supervisor-grant", Guid.NewGuid().ToString("N")[..8]);

    public SupervisorGrantTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    /// <summary>The judge with the machine's supervisor granted <paramref name="acts"/>, its terminal started: the key and the generation.</summary>
    private (StandingConfirmation Standing, string Key, string Generation) Started(IReadOnlyList<string> acts)
    {
        var standing = new StandingConfirmation(_home, null, new StandingConfirmationTests.Placed(StandingPlace.Outside), walk: new StandingConfirmationTests.Walked())
        {
            Wider = () => RuleWiderPlaces.None,
        }.Staff(RoleKit.Supervisor(acts: acts));
        var (change, key) = RoleKit.Start(standing, new HoldRequest(AppointedRoles.Supervisor, "own"));
        Assert.True(change.Answer.Done, change.Answer.Message);
        var generation = standing.Admit(SupervisorKinds.Question, Place, act: null).Generation ?? throw new InvalidOperationException("not admitted");
        return (standing, key!, generation);
    }

    private static JsonElement Payload(object form) => JsonDocument.Parse(JsonSerializer.Serialize(form)).RootElement.Clone();

    private static RoleJudgement Judge(StandingConfirmation standing, string route, Func<string, object> form)
    {
        var nonce = SupervisorProof.Nonce();
        return standing.JudgeRole(DriverModule.Name, route, Payload(form(nonce)), null, nonce);
    }

    private static RoleJudgement Answer(StandingConfirmation standing, string key, string generation) => Judge(standing, SupervisorRoutes.Decide, nonce =>
        new SupervisorDecideForm(SupervisorSubject.Question("s1", "q-1"), generation, "", SupervisorKinds.Answer, "Monthly", Reasons).Proved(key, nonce).Payload());

    private static RoleJudgement Defer(StandingConfirmation standing, string key, string generation) => Leave(standing, key, generation, SupervisorSubject.Question("s1", "q-1"));

    private static RoleJudgement Leave(StandingConfirmation standing, string key, string generation, SupervisorSubject subject) => Judge(standing, SupervisorRoutes.Leave, nonce =>
        new SupervisorLeaveForm(subject, generation, "", SupervisorBounds.All[0], Reasons).Proved(key, nonce).Payload());

    // A course's go-on is its decide; its hold is its leave.
    private static RoleJudgement Course(StandingConfirmation standing, string key, string generation, string act) => act == SupervisorCourseActs.Hold
        ? Leave(standing, key, generation, SupervisorSubject.Point("s1", "p-1"))
        : Judge(standing, SupervisorRoutes.Decide, nonce =>
            new SupervisorDecideForm(SupervisorSubject.Point("s1", "p-1"), generation, "", act, null, Reasons).Proved(key, nonce).Payload());

    [Fact]
    public void A_kind_supervisors_decide_on_a_kind_it_does_not_decide_is_refused_not_granted_and_its_own_kind_is_covered()
    {
        var gatekeeper = RoleKit.Supervisor("gatekeeper", decides: [SupervisorKinds.Course], acts: [SupervisorKinds.Qualified(SupervisorKinds.Course, SupervisorKinds.GoOn)]);
        var standing = new StandingConfirmation(_home, null, new StandingConfirmationTests.Placed(StandingPlace.Outside), walk: new StandingConfirmationTests.Walked())
        {
            Wider = () => RuleWiderPlaces.None,
        }.Staff(RoleKit.Supervisor(holder: RoleHolders.Rules), gatekeeper);
        var (change, key) = RoleKit.Start(standing, new HoldRequest("gatekeeper", "own"));
        Assert.True(change.Answer.Done, change.Answer.Message);
        var generation = standing.Admit(SupervisorKinds.Course, Place, act: null).Generation!;

        Assert.Equal(SupervisorWhy.NotGranted, Answer(standing, key!, generation).Why);
        Assert.True(Course(standing, key!, generation, SupervisorCourseActs.GoOn).Covered);
        Assert.Equal(SupervisorWhy.NotStaffed, standing.Admit(SupervisorKinds.Question, Place, act: null).Why);
    }

    [Fact]
    public void A_generic_supervisors_decide_where_a_kind_supervisor_is_effective_is_refused_role_changed_at_the_keeping()
    {
        var (standing, key, generic) = Started(SupervisorKinds.Seeded);
        standing.Staff(RoleKit.Supervisor(), RoleKit.Supervisor("asker", RoleKit.Repository(Engine), decides: [SupervisorKinds.Question],
            acts: [SupervisorKinds.Qualified(SupervisorKinds.Question, SupervisorKinds.Answer)]));
        var holder = Assert.Single(standing.Now().Holders!).Id;
        Assert.True(RoleKit.Start(standing, new HoldRequest("asker", holder)).Change.Answer.Done);
        var asker = standing.Admit(SupervisorKinds.Question, Place, SupervisorKinds.Answer).Generation!;
        Assert.NotEqual(generic, asker);

        // The judge admits the generic one's answer, its grants covering it; the keeping finds another supervisor answers the kind there.
        Assert.True(Answer(standing, key, generic).Covered);
        Assert.Equal(SupervisorWhy.RoleChanged, standing.Covers(generic, SupervisorKinds.Question, Engine));
        Assert.Null(standing.Covers(asker, SupervisorKinds.Question, Engine));
        Assert.Null(standing.Covers(generic, SupervisorKinds.Course, Engine));
    }

    [Fact]
    public void An_ungranted_answer_and_an_ungranted_go_on_are_refused_not_granted_and_a_leave_is_admitted_with_none()
    {
        var (standing, key, generation) = Started([]);

        Assert.Equal(SupervisorWhy.NotGranted, Answer(standing, key, generation).Why);
        Assert.Equal(SupervisorWhy.NotGranted, Course(standing, key, generation, SupervisorCourseActs.GoOn).Why);
        Assert.True(Defer(standing, key, generation).Covered);
        Assert.True(Course(standing, key, generation, SupervisorCourseActs.Hold).Covered);
        Assert.Equal(SupervisorWhy.NotGranted, standing.Admit(SupervisorKinds.Question, Place, SupervisorKinds.Answer).Why);
        Assert.Equal(SupervisorWhy.NotGranted, standing.Admit(SupervisorKinds.Course, Place, SupervisorKinds.GoOn).Why);
    }

    [Fact]
    public void The_seed_s_three_grants_keep_today_s_answer_and_go_on()
    {
        var (standing, key, generation) = Started(SupervisorKinds.Seeded);

        Assert.True(Answer(standing, key, generation).Covered);
        Assert.True(Course(standing, key, generation, SupervisorCourseActs.GoOn).Covered);
        Assert.Equal(generation, standing.Admit(SupervisorKinds.Course, Place, SupervisorKinds.GoOn).Generation);
        Assert.Equal(generation, standing.Admit(SupervisorKinds.Limit, Place, SupervisorKinds.Move).Generation);
    }

    [Fact]
    public void A_grant_taken_away_while_a_go_on_is_held_stops_its_release_from_the_next_ask()
    {
        var (standing, _, generation) = Started(SupervisorKinds.Seeded);
        Assert.Equal(generation, standing.Admit(SupervisorKinds.Course, Place, SupervisorKinds.GoOn).Generation);

        // Any process's write of the catalogue is read at the next ask: the release's own.
        var held = standing.Appointed().Single(role => role.Id == AppointedRoles.Supervisor);
        standing.Staff(held with { Hold = held.Hold! with { Acts = [SupervisorKinds.Qualified(SupervisorKinds.Question, SupervisorKinds.Answer)] } });

        Assert.Equal(SupervisorWhy.NotGranted, standing.Admit(SupervisorKinds.Course, Place, SupervisorKinds.GoOn).Why);
        Assert.Equal(generation, standing.Admit(SupervisorKinds.Question, Place, SupervisorKinds.Answer).Generation);
    }
}
