using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary><c>daoris agent account remove</c> where its folder cannot be deleted: said as the window says it, never as an edit lock.</summary>
public sealed class AgentAccountRemoveTests : IDisposable
{
    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "agent-account-remove", Guid.NewGuid().ToString("N")[..8]);

    private readonly StringWriter _out = new();

    private readonly StringWriter _error = new();

    public AgentAccountRemoveTests() => Directory.CreateDirectory(Home);

    public void Dispose() => GitSeed.Discard(_root);

    private string Home => Path.Combine(_root, "home");

    private AgentsWorld World() => new()
    {
        Instance = new InstanceWorld
        {
            Home = Home,
            Environment = variable => variable == DaorisHome.Variable ? Home : null,
            Out = _out,
            Error = _error,
            In = new StringReader(""),
            Verb = "daoris",
        },
        Folder = _root,
        UserHome = Path.Combine(_root, "user"),
        Adapters = _ => ProviderRegistry.Built(),
        Now = () => DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task A_folder_a_process_holds_open_is_said_not_removed_with_exit_1()
    {
        if (!OperatingSystem.IsWindows()) return;
        var account = HarnessSettings.ProfileHome(Home, "claude-code", "work");
        Directory.CreateDirectory(account);
        using var held = new FileStream(Path.Combine(account, "held.json"), FileMode.Create, FileAccess.ReadWrite, FileShare.None);

        var exit = await AgentsCommand.RunAsync(["profile", "remove", "claude-code", "work"], World(), CancellationToken.None);

        Assert.Equal(1, exit);
        Assert.Contains("was not removed", _error.ToString());
    }
}
