using Daoris.Driver;
using Lyntai.Agents;
using Lyntai.Processes;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The two containers Daoris composes of the layer (the agent-lyntai design §5.5): the registry's, every layer-run door's registration for
/// its descriptor and claims alone, which never spawns; a spawn's, one registration over its own runner, so two starts share none. Daoris
/// refuses a door registered twice before the layer's registry would let the later replace the earlier. On the tests' provider.
/// </summary>
public sealed class LayerContainerTests
{
    private static readonly IReadOnlyList<string> Command = ["node", "scenario.mjs"];

    private static LauncherRunner Runner() => new(new LauncherSeat(ContainedKind.Agent, StartClass.Background, Home: null, Log: null), exists: _ => true);

    [Fact]
    public async Task The_registry_s_container_states_each_door_and_never_spawns()
    {
        await using var container = LayerContainer.ForRegistry([LayerTestProvider.Registration(Command)]);

        Assert.Equal([LayerTestProvider.Id], container.Registry.Ids);
        Assert.Equal(AgentDoorKinds.Cli, Assert.Single(container.Registry.Get(LayerTestProvider.Id).Describe().Doors).Id);
        var refused = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in container.Open(LayerTestProvider.Id, AgentDoorKinds.Cli).StreamAsync(new AgentSessionOptions { Prompt = "p" }))
            {
            }
        });
        Assert.Contains("never spawns", refused.Message);
    }

    [Fact]
    public void A_door_registered_twice_is_refused_naming_both_before_the_layer_would_replace_one()
    {
        var refused = Assert.Throws<DriverException>(() => LayerContainer.ForRegistry(
            [LayerTestProvider.Registration(Command), LayerTestProvider.Registration(Command, door: "LAYER-STUB")]));

        Assert.Contains("layer-stub", refused.Message);
        Assert.Contains("LAYER-STUB", refused.Message);
    }

    [Fact]
    public void A_registration_that_registers_another_id_than_its_door_is_refused()
    {
        var refused = Assert.Throws<DriverException>(() => LayerContainer.ForRegistry([LayerTestProvider.Registration(Command, door: "elsewhere")]));

        Assert.Contains("elsewhere", refused.Message);
    }

    [Fact]
    public async Task Each_spawn_s_container_holds_its_one_registration_over_its_own_runner()
    {
        var first = Runner();
        var second = Runner();
        await using var one = LayerContainer.ForSpawn(LayerTestProvider.Registration(Command), first);
        await using var two = LayerContainer.ForSpawn(LayerTestProvider.Registration(Command), second);

        Assert.Same(first, one.Runner);
        Assert.Same(second, two.Runner);
        Assert.Equal([LayerTestProvider.Id], one.Registry.Ids);
    }

    [Fact]
    public async Task A_container_disposed_answers_nothing_more()
    {
        var container = LayerContainer.ForSpawn(LayerTestProvider.Registration(Command), Runner());
        await container.DisposeAsync();

        Assert.True(container.Disposed);
        Assert.Throws<ObjectDisposedException>(() => container.Runner);
    }
}
