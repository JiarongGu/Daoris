using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A folder beside an agent's accounts that is no account (a lock's folder) is named by <c>agent account list</c> with the verb that takes
/// it, and that verb removes it, its sign-in included; the account beside it stays.
/// </summary>
public sealed class AccountLeftoversTests : IDisposable
{
    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "account-leftovers", Guid.NewGuid().ToString("N")[..8]);

    private readonly StringWriter _out = new();

    private readonly StringWriter _error = new();

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
    };

    private void Folders()
    {
        Directory.CreateDirectory(HarnessSettings.ProfileHome(Home, "claude-code", "work"));
        var lockFolder = HarnessSettings.ProfileHome(Home, "claude-code", "account-3.lock");
        Directory.CreateDirectory(lockFolder);
        File.WriteAllText(Path.Combine(lockFolder, ".credentials.json"), "{}");
    }

    [Fact]
    public async Task The_list_names_a_lock_folder_and_the_verb_that_removes_it()
    {
        Folders();

        var exit = await AgentsCommand.RunAsync(["profile", "list"], World(), CancellationToken.None);

        Assert.Equal(0, exit);
        Assert.Contains("left over: account-3.lock", _out.ToString(), StringComparison.Ordinal);
        Assert.Contains("daoris agent account remove claude-code account-3.lock", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_remove_verb_takes_a_lock_folder_and_leaves_the_account()
    {
        Folders();

        var exit = await AgentsCommand.RunAsync(["profile", "remove", "claude-code", "account-3.lock"], World(), CancellationToken.None);

        Assert.Equal(0, exit);
        Assert.False(Directory.Exists(HarnessSettings.ProfileHome(Home, "claude-code", "account-3.lock")));
        Assert.True(Directory.Exists(HarnessSettings.ProfileHome(Home, "claude-code", "work")));
    }
}
