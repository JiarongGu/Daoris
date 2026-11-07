using System.Text.RegularExpressions;
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
    public void Where_any_of_Builds_order_rides_the_protocol_the_repository_may_be_driven(string[] order, bool drives) =>
        Assert.Equal(drives, BuildDoor.Drives(_home, Build(order), ProviderRegistry.Built()));

    // An id nobody knows is refused, never read as the pipe, wherever it stands in the order, and by the window's and set-up's reads alike.
    [Theory]
    [InlineData(new[] { "nobody-knows" }, "nobody-knows")]
    [InlineData(new[] { "acp-stub", "nobody-knows" }, "nobody-knows")]
    [InlineData(new[] { "nobody-knows", "acp-stub" }, "nobody-knows")]
    public void A_door_nobody_knows_is_refused_never_read_as_the_pipe(string[] order, string unknown)
    {
        Assert.Contains(unknown, order);
        var refused = Assert.Throws<UnknownDoorException>(() => BuildDoor.Drives(_home, Build(order), ProviderRegistry.Built()));
        Assert.Equal("nobody-knows", refused.Door);
        Assert.Equal(UnknownDoorException.Words("nobody-knows"), refused.Message);
        Assert.Throws<UnknownDoorException>(() => SetupWorld.Unadopted(Build(order), _home));
    }

    // The terminal's consoles once asked Adoption.Unadopted of Build's first agent alone; the loop, set-up and the window ask the whole order.
    [Fact]
    public void No_terminal_console_asks_a_door_of_its_own_but_reads_Builds_whole_order_through_set_up()
    {
        var own = SourceFiles.All("Daoris.Desktop.Driver.Host", "*.cs")
            .Where(path => Regex.IsMatch(File.ReadAllText(path), @"\bAdoption\s*\.\s*Unadopted\s*\(|\bSessionsConsole\s*\.\s*Unadopted\s*\(|catch\s*\(DriverException[^)]*UnknownDoorException"))
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(own.Count == 0, "a console reads BuildDoor through SetupWorld.Unadopted, and lets an unknown door refuse:\n" + string.Join('\n', own));
    }

    [Fact]
    public void Set_up_reads_the_same_answer_the_loop_does()
    {
        var config = Build("stub", "acp-stub");
        Assert.True(SetupWorld.Unadopted(config, _home));
        Assert.Equal(BuildDoor.Drives(_home, config, ProviderRegistry.Built()), SetupWorld.Unadopted(config, _home));
    }
}
