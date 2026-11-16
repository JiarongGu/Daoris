using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// GRANTSPEND1 (D306): only the owner widens what agents may do. A role write that grants an agent acts is one of the owner's presses: an
/// agent's goes on the owner's card in every mode, and one from an agents' job proving no key is refused; any other role write is not.
/// </summary>
public sealed class PipeOwnerOnlyTests
{
    private static IpcRequest Press(string type, string json) =>
        new() { Module = DriverModule.Name, Type = type, Payload = JsonDocument.Parse(json).RootElement };

    [Fact]
    public void A_role_granting_an_agent_acts_is_the_owner_s_to_write()
    {
        Assert.True(PipeOwnerOnly.Governs(Press("SET_ROLE",
            """{ "change": "set", "id": "engine-lead", "role": { "kind": "supervisor", "authority": ["vacancy.recast"], "holder": "agent" } }""")));
        Assert.True(PipeOwnerOnly.Governs(Press("SET_ROLES",
            """{ "changes": [ { "change": "set", "id": "build", "role": { "takes": ["build"] } }, { "change": "set", "id": "steward", "role": { "kind": "steward", "authority": ["drive"], "holder": "agent" } } ] }""")));
    }

    /// <summary>🔴 A terminal is held by an agent too, and an unstaffed role granted acts is one start away: only rules receive acts with no card.</summary>
    [Fact]
    public void A_role_granting_acts_to_any_holder_but_rules_is_the_owner_s_to_write()
    {
        Assert.True(PipeOwnerOnly.Governs(Press("SET_ROLE",
            """{ "change": "set", "id": "supervisor", "role": { "kind": "supervisor", "authority": ["goahead.answer"], "holder": "terminal" } }""")));
        Assert.True(PipeOwnerOnly.Governs(Press("SET_ROLE",
            """{ "change": "set", "id": "landing", "role": { "kind": "supervisor", "authority": ["landing.accept"] } }""")));
        Assert.True(PipeOwnerOnly.Governs(Press("SET_ROLES",
            """{ "changes": [ { "change": "set", "id": "supervisor", "role": { "kind": "supervisor", "authority": ["landing.accept"], "holder": "terminal" } } ] }""")));
    }

    [Fact]
    public void A_role_held_by_rules_or_granting_nothing_is_not()
    {
        Assert.False(PipeOwnerOnly.Governs(Press("SET_ROLE",
            """{ "change": "set", "id": "supervisor", "role": { "kind": "supervisor", "authority": ["question.answer"], "holder": "rules" } }""")));
        Assert.False(PipeOwnerOnly.Governs(Press("SET_ROLE",
            """{ "change": "set", "id": "engine-lead", "role": { "kind": "supervisor", "authority": [], "holder": "agent" } }""")));
        Assert.False(PipeOwnerOnly.Governs(Press("SET_ROLE", """{ "change": "remove", "id": "engine-lead" }""")));
    }
}
