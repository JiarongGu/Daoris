using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A trust is granted to an account that is here: it never makes one, since a sign-in alone does.</summary>
public sealed class AgentTrustAccountTests : IDisposable
{
    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "agent-trust-account", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() => GitSeed.Discard(_root);

    [Fact]
    public async Task A_trust_for_an_account_that_is_not_here_is_refused_and_makes_no_account()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var running = new InstanceTestHome();
        var standIn = running.Serve();
        var folder = Path.Combine(_root, "work", "engine");
        Directory.CreateDirectory(folder);
        Directory.CreateDirectory(HarnessSettings.ProfileHome(running.Home, "claude-code", "work"));
        var instance = running.World("daoris");
        var world = new AgentsWorld
        {
            Instance = new InstanceWorld
            {
                Home = running.Home,
                Environment = variable => variable == DaorisHome.Variable ? running.Home : null,
                BaseDirectory = instance.BaseDirectory,
                Status = instance.Status,
                Out = running.Out,
                Error = running.Error,
                Verb = "daoris",
                Answer = instance.Answer,
                Wait = instance.Wait,
            },
            Folder = _root,
            UserHome = Path.Combine(_root, "user"),
            Adapters = _ => ProviderRegistry.Built(),
        };

        var exit = await AgentsCommand.RunAsync(["trust", "claude-code", folder, "--account", "wrok", "--yes"], world, CancellationToken.None);

        Assert.Equal(2, exit);
        Assert.Empty(standIn.Presses);
        Assert.False(Directory.Exists(HarnessSettings.ProfileHome(running.Home, "claude-code", "wrok")));
    }
}
