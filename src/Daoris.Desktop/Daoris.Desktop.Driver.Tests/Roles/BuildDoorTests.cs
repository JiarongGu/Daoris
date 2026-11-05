using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTPROVIDER1 s-i: the machine's door is one answer. The loop reads where any entry of Build's order rides the protocol, and the
/// window, the terminal's two and set-up read that same answer, where the window and set-up read Build's first agent alone before.
/// </summary>
public sealed class BuildDoorTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "build-door", Guid.NewGuid().ToString("N")[..8]);

    public BuildDoorTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    private static DriverConfig Build(params string[] order) =>
        ActorChoices.With(DriverConfig.Empty, "build", [.. order.Select(agent => new ActorChoice(agent))]);

    [Theory]
    [InlineData(new[] { "stub" }, false)]
    [InlineData(new[] { "acp-stub" }, true)]
    // The old and the new case: a later choice that rides the protocol, which the window and set-up once left unsaid.
    [InlineData(new[] { "stub", "acp-stub" }, true)]
    [InlineData(new[] { "nobody-knows" }, false)]
    public void Where_any_of_Builds_order_rides_the_protocol_the_repository_may_be_driven(string[] order, bool drives) =>
        Assert.Equal(drives, BuildDoor.Drives(_home, Build(order), AdapterSet.Built()));

    [Fact]
    public void Set_up_reads_the_same_answer_the_loop_does()
    {
        var config = Build("stub", "acp-stub");
        Assert.True(SetupWorld.Unadopted(config, _home));
        Assert.Equal(BuildDoor.Drives(_home, config, AdapterSet.Built()), SetupWorld.Unadopted(config, _home));
    }
}
