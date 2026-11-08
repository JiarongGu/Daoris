using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>login</c>, <c>trust</c>, <c>key</c> and <c>settings</c> refuse a flag they do not take, as <c>account use</c> does: a dropped
/// <c>--profile work</c> would sign the order's first account in, or trust a folder for every account.
/// </summary>
public sealed class AgentVerbFlagsTests : IDisposable
{
    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "agent-verb-flags", Guid.NewGuid().ToString("N")[..8]);

    private readonly StringWriter _out = new();

    private readonly StringWriter _error = new();

    public AgentVerbFlagsTests()
    {
        Directory.CreateDirectory(Home);
        Directory.CreateDirectory(Folder);
        Directory.CreateDirectory(HarnessSettings.ProfileHome(Home, "claude-code", "work"));
    }

    public void Dispose() => GitSeed.Discard(_root);

    private string Home => Path.Combine(_root, "home");

    private string Folder => Path.Combine(_root, "work", "engine");

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

    [Theory]
    [InlineData("login", "agent login")]
    [InlineData("trust", "agent trust")]
    [InlineData("key", "agent key")]
    [InlineData("settings", "agent settings")]
    public async Task A_flag_the_verb_does_not_take_is_refused_and_nothing_is_done(string verb, string shown)
    {
        string[] words = verb switch
        {
            "trust" => ["trust", "claude-code", Folder, "--profile", "work", "--yes"],
            "settings" => ["settings", "claude-code", "--profile", "work", "model", "opus"],
            _ => [verb, "claude-code", "--profile", "work"],
        };

        var exit = await AgentsCommand.RunAsync(words, World(), CancellationToken.None);

        Assert.Equal(2, exit);
        Assert.Contains($"`--profile` is not a flag of `{shown}`", _error.ToString());
        var account = HarnessSettings.ProfileHome(Home, "claude-code", "work");
        Assert.Empty(Directory.GetFileSystemEntries(account));
        Assert.Empty(Directory.GetFileSystemEntries(Folder));
    }

    [Fact]
    public async Task The_flags_a_verb_takes_are_still_read()
    {
        var exit = await AgentsCommand.RunAsync(["trust", "claude-code", Folder, "--account", "work", "--dry-run"], World(), CancellationToken.None);

        Assert.Equal(0, exit);
        Assert.Contains("trusting", _out.ToString());
    }
}
