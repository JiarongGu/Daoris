using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary><c>daoris agent login</c> on a protocol door signs in as the agent whose accounts the door runs as.</summary>
public sealed class AgentLoginDoorTests : IDisposable
{
    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "agent-login-door", Guid.NewGuid().ToString("N")[..8]);

    private readonly StringWriter _error = new();

    public AgentLoginDoorTests()
    {
        Directory.CreateDirectory(Home);
        Directory.CreateDirectory(Path.Combine(_root, "folder"));
    }

    public void Dispose() => GitSeed.Discard(_root);

    private string Home => Path.Combine(_root, "home");

    [Theory]
    [InlineData("codex-acp", "codex")]
    [InlineData("claude-code-acp", "claude-code")]
    public async Task A_protocol_door_signs_in_as_its_accounts_agent(string door, string agent)
    {
        var world = new AgentsWorld
        {
            Instance = new InstanceWorld
            {
                Home = Home,
                Environment = variable => variable == DaorisHome.Variable ? Home : null,
                Out = new StringWriter(),
                Error = _error,
                In = new StringReader(""),
                Verb = "daoris",
            },
            Folder = Path.Combine(_root, "folder"),
            UserHome = Path.Combine(_root, "user"),
            Adapters = _ => ProviderRegistry.Built(),
            Now = () => new DateTimeOffset(2026, 11, 22, 8, 0, 0, TimeSpan.Zero),
            Ports = _ => PortHold.InUse,
        };

        Assert.Equal(2, await AgentsCommand.RunAsync(["login", door, "--new"], world, CancellationToken.None));

        Assert.DoesNotContain("declares no login flow", _error.ToString());
        Assert.Contains($"`{agent}", _error.ToString());
        Assert.Empty(HarnessSettings.Profiles(Home, agent));
    }
}
