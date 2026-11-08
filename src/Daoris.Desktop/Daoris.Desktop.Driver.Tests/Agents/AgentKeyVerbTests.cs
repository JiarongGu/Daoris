using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris agent key dsh</c>: the key of an agent held by an API key alone, read from standard input, kept sealed, set into an
/// account that has none, and removed without removing the account.
/// </summary>
public sealed class AgentKeyVerbTests : IDisposable
{
    private const string Key = "sk-verb-dsh-0000-wxyz";

    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "agent-key-verb", Guid.NewGuid().ToString("N")[..8]);

    private readonly StringWriter _out = new();

    private readonly StringWriter _error = new();

    public AgentKeyVerbTests()
    {
        Directory.CreateDirectory(Home);
        Directory.CreateDirectory(Path.Combine(_root, "folder"));
    }

    public void Dispose() => GitSeed.Discard(_root);

    private string Home => Path.Combine(_root, "home");

    private AgentsWorld World(string input = "") => new()
    {
        Instance = new InstanceWorld
        {
            Home = Home,
            Environment = variable => variable == DaorisHome.Variable ? Home : null,
            Out = _out,
            Error = _error,
            In = new StringReader(input),
            Verb = "daoris",
        },
        Folder = Path.Combine(_root, "folder"),
        UserHome = Path.Combine(_root, "user"),
        Adapters = _ => ProviderRegistry.Built(),
        Now = () => DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task A_key_on_standard_input_makes_a_dsh_account_and_is_never_said_back()
    {
        if (!OperatingSystem.IsWindows()) return;

        Assert.Equal(0, await AgentsCommand.RunAsync(["key", "dsh"], World(Key + "\n"), CancellationToken.None));

        var account = Assert.Single(HarnessSettings.Profiles(Home, "dsh"));
        Assert.Equal(Key, HarnessKeys.Of(Home, "dsh", account));
        Assert.DoesNotContain(Key, _out + _error.ToString());
        Assert.DoesNotContain(Key, File.ReadAllText(DaorisHome.Entry(Home, HomeEntry.Keys)));
    }

    [Fact]
    public async Task A_key_on_the_command_line_is_refused_and_nothing_is_written()
    {
        var exit = await Record.ExceptionAsync(() => AgentsCommand.RunAsync(["key", "dsh", Key], World(), CancellationToken.None));

        Assert.True(exit is not null || _error.ToString().Length > 0);
        Assert.Empty(HarnessSettings.Profiles(Home, "dsh"));
    }

    [Fact]
    public async Task A_key_is_set_into_a_dsh_account_that_has_none_and_removed_with_the_account_kept()
    {
        if (!OperatingSystem.IsWindows()) return;
        Directory.CreateDirectory(HarnessSettings.ProfileHome(Home, "dsh", "acct-00000000"));
        HarnessSettings.Placed(HarnessSettings.ResolvePath(Home), "dsh", "acct-00000000");

        var set = await AgentsCommand.RunAsync(["key", "dsh", "--account", "acct-00000000"], World(Key), CancellationToken.None);
        Assert.True(set == 0, _error.ToString());
        Assert.Equal(Key, HarnessKeys.Of(Home, "dsh", "acct-00000000"));

        var removed = await AgentsCommand.RunAsync(["key", "dsh", "--account", "acct-00000000", "--remove"], World(), CancellationToken.None);
        Assert.True(removed == 0, _error.ToString());
        Assert.Null(HarnessKeys.Of(Home, "dsh", "acct-00000000"));
        Assert.Contains("acct-00000000", HarnessSettings.Profiles(Home, "dsh"));
    }
}
