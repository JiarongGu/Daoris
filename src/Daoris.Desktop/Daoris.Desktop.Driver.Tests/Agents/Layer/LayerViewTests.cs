using Daoris.Driver;
using Lyntai.Agents;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The registry's layer view (the agent-lyntai design §4.1, §4.2, §5.5): each bound door named by Daoris's registry, an id resolved without
/// case to its registered spelling and only that spelling reaching the layer's registry; the run kinds each binding covers; the bound door's
/// layer door and its claims read through the registry's container, which never spawns; and a start's registration, which its spawn's
/// container is built from. A door Daoris does not know, or spelled otherwise than its registry spells it, is refused. On the tests'
/// provider bound to the stand-in's native door, never a production door.
/// </summary>
public sealed class LayerViewTests
{
    private static readonly IReadOnlyList<string> Command = ["node", "scenario.mjs"];

    private static LayerView Bound(string door = "stub", params StartKind[] runs) => LayerView.Over(
        ProviderRegistry.Built(),
        [new LayerBinding(LayerTestProvider.Registration(Command, door), AgentDoorKinds.Cli, new HashSet<StartKind>(runs.Length == 0 ? [StartKind.Driven] : runs))]);

    [Fact]
    public async Task A_bound_door_is_read_through_the_view_by_the_run_kinds_it_covers()
    {
        await using var view = Bound();

        Assert.True(view.Binds("STUB", StartKind.Driven));
        Assert.False(view.Binds("stub", StartKind.Conversation));
        Assert.False(view.Binds("claude-code", StartKind.Driven));
        Assert.Equal("stub", view.RegistrationOf("Stub", StartKind.Driven)!.Door);
        Assert.Null(view.RegistrationOf("stub", StartKind.Conversation));
        Assert.Equal(["stub"], view.Registry.Ids);
    }

    [Fact]
    public async Task A_bound_door_s_layer_door_and_claims_are_read_from_the_registry_s_container()
    {
        await using var view = Bound();

        Assert.Equal(AgentDoorKinds.Cli, view.Describe("stub").Id);
        var resume = view.Claim("stub", AgentAbilities.Resume);
        Assert.Equal((true, true, KnownBy.Declared), (resume.IsKnown, resume.Value, resume.By));
        Assert.False(view.Claim("stub", AgentAbilities.Model).Value);
        Assert.False(view.Claim("stub", AgentAbilities.Steering).IsKnown);
    }

    [Fact]
    public void A_door_daoris_does_not_know_is_refused()
    {
        var refused = Assert.Throws<DriverException>(() => Bound("nowhere"));

        Assert.Contains("nowhere", refused.Message);
    }

    [Fact]
    public void A_door_spelled_otherwise_than_the_registry_spells_it_never_reaches_the_layer()
    {
        var refused = Assert.Throws<DriverException>(() => Bound("STUB"));

        Assert.Contains("`STUB`", refused.Message);
        Assert.Contains("`stub`", refused.Message);
    }

    [Fact]
    public void A_binding_covering_no_run_kind_is_refused()
    {
        Assert.Throws<DriverException>(() => LayerView.Over(
            ProviderRegistry.Built(), [new LayerBinding(LayerTestProvider.Registration(Command, "stub"), AgentDoorKinds.Cli, new HashSet<StartKind>())]));
    }
}
