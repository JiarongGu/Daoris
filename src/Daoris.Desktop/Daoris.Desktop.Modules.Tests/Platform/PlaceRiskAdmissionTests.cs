using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The roles' admission reads the place's risk record at each act, so a place the owner holds high admits no appointed role's
/// act and its decisions wait on the owner role; and the record is the owner's, pressed by an agent only on the owner's card.
/// </summary>
public sealed class PlaceRiskAdmissionTests : IDisposable
{
    private static readonly RolePlace Place = new("work", "engine");

    private readonly string _home = Path.Combine(AppContext.BaseDirectory, "place-risk-admission", Guid.NewGuid().ToString("N")[..8]);

    public PlaceRiskAdmissionTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private StandingConfirmation Standing(PlaceRiskBook risk)
    {
        var role = RoleKit.Supervisor(acts: [SupervisorKinds.Qualified(SupervisorKinds.Question, SupervisorKinds.Answer)], holder: RoleHolders.Agent);
        return new StandingConfirmation(_home, null, new StandingConfirmationTests.Placed(StandingPlace.Outside), walk: new StandingConfirmationTests.Walked())
        {
            Wider = () => RuleWiderPlaces.None,
            Slots = () => [RoleKit.Slot(role) with { Revision = 1 }],
            Risk = () => risk,
        };
    }

    [Fact]
    public void A_high_risk_place_admits_no_role_s_act_and_a_low_one_admits_the_granted_act()
    {
        var low = Standing(PlaceRiskBook.Unwritten.Apply(new PlaceRiskEdit { Workspace = "work", Risk = PlaceRisks.Low }));
        Assert.Null(low.Admit(SupervisorKinds.Question, Place, SupervisorKinds.Answer).Why);

        var high = Standing(PlaceRiskBook.Unwritten.Apply(new PlaceRiskEdit { Workspace = "work", Risk = PlaceRisks.High }));
        Assert.Equal(SupervisorWhy.HighRisk, high.Admit(SupervisorKinds.Question, Place, SupervisorKinds.Answer).Why);
        // A leave needs no grant: the subject passes to the owner role.
        Assert.Null(high.Admit(SupervisorKinds.Question, Place, act: null).Why);
    }

    [Fact]
    public void The_record_is_one_of_the_owner_s_presses()
    {
        Assert.True(PipeOwnerOnly.Governs(new IpcRequest { Module = DriverModule.Name, Type = "SET_PLACE_RISK" }));
        Assert.False(PipeOwnerOnly.Governs(new IpcRequest { Module = DriverModule.Name, Type = "SET_GROWTH" }));
    }

    [Fact]
    public void A_cast_and_a_step_handed_to_an_agent_are_the_owner_s_presses()
    {
        static IpcRequest Press(string type, string payload) =>
            new() { Module = DriverModule.Name, Type = type, Payload = System.Text.Json.JsonDocument.Parse(payload).RootElement };

        Assert.True(PipeOwnerOnly.Governs(Press("SET_PLACE_CAST", """{"role":"ask-reader","holder":"agent:claude"}""")));
        Assert.True(PipeOwnerOnly.Governs(Press("SET_PLACE_CAST", """{"role":"ask-reader"}""")));
        Assert.True(PipeOwnerOnly.Governs(Press("WORKFLOW_HOLD", """{"run":"q7","node":"holds","act":"hand","holder":"agent:claude"}""")));
        Assert.True(PipeOwnerOnly.Governs(Press("WORKFLOW_HOLD", """{"Run":"q7","Node":"holds","Act":"hand","Holder":"agent"}""")));
        Assert.False(PipeOwnerOnly.Governs(Press("WORKFLOW_HOLD", """{"run":"q7","node":"holds","act":"hand","holder":"person"}""")));
        Assert.False(PipeOwnerOnly.Governs(Press("WORKFLOW_HOLD", """{"run":"q7","node":"holds","act":"take"}""")));
        Assert.False(PipeOwnerOnly.Governs(Press("WORKFLOW_HOLD", """{"run":"q7","node":"holds","act":"give-back"}""")));
    }
}
