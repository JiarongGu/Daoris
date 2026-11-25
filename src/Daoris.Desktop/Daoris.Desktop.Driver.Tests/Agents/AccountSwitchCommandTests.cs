using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris agent account on|off</c>: the terminal's twin of the window's switch, over the same record for every agent. A person's
/// terminal writes it and says it once; a session's terminal is refused, as for a sign-in, and writes nothing.
/// </summary>
public sealed class AccountSwitchCommandTests : IDisposable
{
    private const string Marker = "s1a2b3c4";

    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "account-switch-command", Guid.NewGuid().ToString("N")[..8]);

    private readonly StringWriter _out = new();

    private readonly StringWriter _error = new();

    public AccountSwitchCommandTests() => Directory.CreateDirectory(Home);

    public void Dispose() => GitSeed.Discard(_root);

    private string Home => Path.Combine(_root, "home");

    private string Present => Path.Combine(_root, "agent-here");

    private ProviderRegistry Agents()
    {
        File.WriteAllText(Present, "");
        return TestDoors.Registry(
        [
            TestDoors.Door("fake", new TestAgent(Binary: [Present], VersionArguments: ["--version"], ProfileVariable: "FAKE_HOME", ProbeByPresence: true)),
            TestDoors.Door("fake-two", new TestAgent(Binary: [Present], VersionArguments: ["--version"], ProfileVariable: "FAKE_TWO_HOME", ProbeByPresence: true)),
        ]);
    }

    private AgentsWorld World(string? marker = null) => new()
    {
        Instance = new InstanceWorld
        {
            Home = Home,
            Environment = variable => variable switch
            {
                DriverChoicesCommand.SessionVariable => marker,
                DaorisHome.Variable => Home,
                _ => null,
            },
            Out = _out,
            Error = _error,
            In = new StringReader(""),
            Verb = "daoris",
        },
        Folder = _root,
        UserHome = Path.Combine(_root, "user"),
        Adapters = _ => Agents(),
        Now = () => new DateTimeOffset(2026, 11, 24, 3, 0, 0, TimeSpan.Zero),
    };

    private Task<int> Run(string? marker, params string[] words) => AgentsCommand.RunAsync(words, World(marker), CancellationToken.None);

    private void Accounts(string agent)
    {
        Directory.CreateDirectory(HarnessSettings.ProfileHome(Home, agent, "work"));
        Directory.CreateDirectory(HarnessSettings.ProfileHome(Home, agent, "spare"));
    }

    [Theory]
    [InlineData("fake")]
    [InlineData("fake-two")]
    public async Task The_terminal_switches_an_account_off_and_on_and_agent_list_says_it_is_off(string agent)
    {
        Accounts(agent);

        Assert.Equal(0, await Run(null, "profile", "off", agent, "work"));

        Assert.True(AccountSwitch.IsOff(Home, agent, "work"));
        Assert.False(AccountSwitch.IsOff(Home, agent, "spare"));
        Assert.Contains($"the `{agent}` account `work` is switched off", _out.ToString());
        Assert.Contains($"`daoris agent account on {agent} work` switches it on", _out.ToString());

        _out.GetStringBuilder().Clear();
        Assert.Equal(0, await Run(null, "list"));
        var listed = _out.ToString().Split('\n');
        Assert.Contains(listed, line => line.Contains("work", StringComparison.Ordinal) && line.TrimEnd().EndsWith("OFF", StringComparison.Ordinal));
        Assert.DoesNotContain(listed, line => line.Contains("spare", StringComparison.Ordinal) && line.TrimEnd().EndsWith("OFF", StringComparison.Ordinal));

        _out.GetStringBuilder().Clear();
        Assert.Equal(0, await Run(null, "profile", "off", agent, "work"));
        Assert.Contains("already off — nothing changed", _out.ToString());

        _out.GetStringBuilder().Clear();
        Assert.Equal(0, await Run(null, "profile", "on", agent, "work"));
        Assert.False(AccountSwitch.IsOff(Home, agent, "work"));
        Assert.Contains("is switched on", _out.ToString());
    }

    [Fact]
    public async Task The_switch_refuses_an_account_that_is_not_here_and_writes_nothing()
    {
        Accounts("fake");

        Assert.NotEqual(0, await Run(null, "profile", "off", "fake", "nobody"));

        Assert.Contains("has no account `nobody`", _out.ToString() + _error);
        Assert.False(File.Exists(AccountNames.PathOf(Home)));
    }

    [Theory]
    [InlineData("off")]
    [InlineData("on")]
    public async Task A_session_s_terminal_is_refused_the_switch_and_nothing_is_written(string way)
    {
        Accounts("fake");

        Assert.Equal(1, await Run(Marker, "profile", way, "fake", "work"));

        Assert.Contains(DriverChoicesCommand.SessionsTerminal, _error.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(AccountNames.PathOf(Home)));
    }
}
