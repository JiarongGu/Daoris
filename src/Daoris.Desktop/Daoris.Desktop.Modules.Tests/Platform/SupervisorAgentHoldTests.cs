using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A supervisor an agent holds is admitted by its role's id and revision, read from the catalogue at each ask and never drawn or kept:
/// no press starts it, its grants still cover each act, a write of the role moves its generation, and a pipe act carrying one is refused
/// as an unknown generation is.
/// </summary>
public sealed class SupervisorAgentHoldTests : IDisposable
{
    private static readonly RolePlace Place = new(Repository: "engine");

    private readonly string _home = Path.Combine(AppContext.BaseDirectory, "supervisor-agent-hold", Guid.NewGuid().ToString("N")[..8]);

    public SupervisorAgentHoldTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private StandingConfirmation Standing(long revision, params string[] acts)
    {
        var role = RoleKit.Supervisor(acts: acts, holder: RoleHolders.Agent);
        return new StandingConfirmation(_home, null, new StandingConfirmationTests.Placed(StandingPlace.Outside), walk: new StandingConfirmationTests.Walked())
        {
            Wider = () => RuleWiderPlaces.None,
            Slots = () => [RoleKit.Slot(role) with { Revision = revision }],
        };
    }

    [Fact]
    public void An_agent_s_hold_is_its_role_s_id_and_revision_with_no_press_and_its_grants_still_cover_each_act()
    {
        var standing = Standing(3, SupervisorKinds.Qualified(SupervisorKinds.Question, SupervisorKinds.Answer));

        var admitted = standing.Admit(SupervisorKinds.Question, Place, SupervisorKinds.Answer);

        Assert.Equal("agent:supervisor@3", admitted.Generation);
        Assert.Equal(TimeSpan.FromMinutes(RoleHold.DefaultWindow), admitted.Window);
        Assert.Equal(SupervisorWhy.NotGranted, standing.Admit(SupervisorKinds.Course, Place, SupervisorKinds.GoOn).Why);
        Assert.Equal("agent:supervisor@3", standing.Admit(SupervisorKinds.Course, Place, act: null).Generation);
    }

    [Fact]
    public void A_write_of_the_role_from_any_process_moves_its_generation_so_a_decision_kept_under_the_old_one_is_role_changed()
    {
        var revision = 3L;
        var standing = Standing(0, SupervisorKinds.Qualified(SupervisorKinds.Question, SupervisorKinds.Answer));
        var role = RoleKit.Supervisor(acts: [SupervisorKinds.Qualified(SupervisorKinds.Question, SupervisorKinds.Answer)], holder: RoleHolders.Agent);
        standing.Slots = () => [RoleKit.Slot(role) with { Revision = revision }];
        var kept = standing.Admit(SupervisorKinds.Question, Place, act: null).Generation!;

        Assert.Null(standing.Covers(kept, SupervisorKinds.Question, "engine"));
        revision = 4;
        Assert.Equal(SupervisorWhy.RoleChanged, standing.Covers(kept, SupervisorKinds.Question, "engine"));
        standing.Slots = () => [RoleKit.Slot(role with { Hold = role.Hold! with { Holder = RoleHolders.Rules } }) with { Revision = 5 }];
        Assert.Equal(SupervisorWhy.NotStaffed, standing.Admit(SupervisorKinds.Question, Place, act: null).Why);
    }

    [Fact]
    public void A_pipe_act_naming_an_agent_s_generation_finds_no_terminal_s_hold_and_is_refused()
    {
        var standing = Standing(3, SupervisorKinds.Qualified(SupervisorKinds.Question, SupervisorKinds.Answer));
        var nonce = SupervisorProof.Nonce();
        var form = new SupervisorDecideForm(
            SupervisorSubject.Question("s1", "q-1"), "agent:supervisor@3", "", SupervisorKinds.Answer, "Monthly", [new("The ticket says monthly.", "the quest's line 2")]);
        var payload = JsonDocument.Parse(JsonSerializer.Serialize(form.Proved(HolderKey.Draw(), nonce).Payload())).RootElement.Clone();

        var judged = standing.JudgeRole(DriverModule.Name, SupervisorRoutes.Decide, payload, null, nonce);

        Assert.False(judged.Covered);
        Assert.Equal(SupervisorWhy.NotStaffed, judged.Why);
    }
}
