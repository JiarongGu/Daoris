using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris settings growth &lt;workspace&gt; [--repository &lt;name&gt;] mode|asked|off</c>: the person's own press of the
/// switch, a verb of the settings with a read form that says the value and where it comes from. It is never an agent's: from a session's terminal
/// or a process inside an agent's job it is refused before anything is pressed or written, in either form's change.
/// </summary>
public sealed class GrowthVerbTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "growth-verb", Guid.NewGuid().ToString("N")[..8]);
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public GrowthVerbTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Remove(_home);

    private string Config => DaorisHome.Entry(_home, HomeEntry.DriverConfig);

    private DriverChoicesWorld World(bool inAgentJob = false, params (string Name, string Value)[] variables) => new()
    {
        Verbs = DriverChoiceVerbs.All,
        Instance = new InstanceWorld
        {
            Home = _home,
            Environment = name => variables.FirstOrDefault(each => each.Name == name).Value,
            Out = _out,
            Error = _error,
            Verb = "daoris",
        },
        ConfigPath = Config,
        InAgentJob = () => inAgentJob,
    };

    private Task<int> RunAsync(string words, DriverChoicesWorld? world = null) =>
        DriverChoicesCommand.RunAsync(words.Split(' ', StringSplitOptions.RemoveEmptyEntries), world ?? World(), CancellationToken.None);

    [Fact]
    public async Task Setting_a_workspace_writes_its_key_and_says_what_the_value_does_and_where_it_is_kept()
    {
        var exit = await RunAsync("growth aurora off");

        Assert.Equal(0, exit);
        Assert.Equal("off", DriverConfig.Load(Config).WorkspaceGrowth["aurora"]);
        Assert.Contains("daoris: new repositories and rooms in the workspace `aurora`: off.", _out.ToString(), StringComparison.Ordinal);
        Assert.Contains("is refused, even in full access", _out.ToString(), StringComparison.Ordinal);
        Assert.Contains($"Written to {Config}.", _out.ToString(), StringComparison.Ordinal);
        Assert.Equal("", _error.ToString());
    }

    [Fact]
    public async Task Setting_a_repository_writes_its_own_key_and_leaves_its_workspaces()
    {
        await RunAsync("growth aurora off");

        var exit = await RunAsync("growth aurora --repository ledger asked");

        Assert.Equal(0, exit);
        var config = DriverConfig.Load(Config);
        Assert.Equal(("asked", "off"), (config.Growth["ledger"], config.WorkspaceGrowth["aurora"]));
        Assert.Contains("new rooms in `ledger`: asked (its own).", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Setting_a_workspace_back_to_mode_takes_its_key_away()
    {
        await RunAsync("growth aurora asked");

        var exit = await RunAsync("growth aurora mode");

        Assert.Equal(0, exit);
        Assert.Empty(DriverConfig.Load(Config).WorkspaceGrowth);
        Assert.DoesNotContain("workspaceGrowth", File.ReadAllText(Config), StringComparison.Ordinal);
    }

    [Fact]
    public async Task With_no_value_it_says_the_value_and_where_it_comes_from_and_writes_nothing()
    {
        File.WriteAllText(Config, """{"workspaceGrowth":{"aurora":"asked"},"growth":{"ledger":"off"}}""");
        var before = File.ReadAllText(Config);

        Assert.Equal(0, await RunAsync("growth aurora"));
        Assert.Equal(0, await RunAsync("growth aurora --repository ledger"));
        Assert.Equal(0, await RunAsync("growth aurora --repository atlas"));
        Assert.Equal(0, await RunAsync("growth other"));

        var said = _out.ToString();
        Assert.Contains("the workspace `aurora`: asked (its own).", said, StringComparison.Ordinal);
        Assert.Contains("`ledger`: off (its own).", said, StringComparison.Ordinal);
        Assert.Contains("`atlas`: asked (from the workspace `aurora`).", said, StringComparison.Ordinal);
        Assert.Contains("the workspace `other`: mode (the default).", said, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllText(Config));
    }

    [Theory]
    [InlineData("growth", "needs the workspace")]
    [InlineData("growth aurora sometimes", "`growth` is `mode`")]
    [InlineData("growth aurora off asked", "takes one value")]
    [InlineData("growth aurora --repository", "needs a name")]
    [InlineData("growth aurora --other off", "`--other`")]
    public async Task Words_the_verb_cannot_read_are_its_usage_and_write_nothing(string words, string said)
    {
        var exit = await RunAsync(words);

        Assert.Equal(2, exit);
        Assert.Contains(said, _error.ToString(), StringComparison.Ordinal);
        Assert.Contains("daoris settings growth <workspace> [--repository <name>] mode|asked|off", _error.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(Config));
    }

    [Fact]
    public async Task From_a_sessions_terminal_a_change_is_refused_as_the_persons_alone_and_nothing_is_written_but_a_read_is_said()
    {
        var session = World(false, ("DAORIS_SESSION_ID", "s1a2b3c4"));

        Assert.Equal(1, await RunAsync("growth aurora off", session));

        Assert.Contains("the growth switch is the person's alone", _error.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(Config));
        Assert.Equal(0, await RunAsync("growth aurora", session));
        Assert.Contains("the workspace `aurora`: mode (the default).", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task From_a_process_inside_an_agents_job_a_change_is_refused_and_nothing_is_written()
    {
        var exit = await RunAsync("growth aurora --repository ledger off", World(inAgentJob: true));

        Assert.Equal(1, exit);
        Assert.Contains("the growth switch is the person's alone", _error.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(Config));
    }

    [Fact]
    public void The_verb_is_in_the_programs_family_usage_and_route_census_as_the_persons_press_SET_GROWTH()
    {
        Assert.Contains("growth", DriverChoicesCommand.Verbs);
        Assert.Contains("daoris settings growth <workspace> [--repository <name>] mode|asked|off", DriverChoicesCommand.Usage, StringComparison.Ordinal);
        Assert.Equal("SET_GROWTH", ChoiceRoutes.Growth.Route);
        Assert.Contains(ChoiceRoutes.Verbs, row => row.Route == ChoiceRoutes.Growth && row.Verb == "driver growth");
        Assert.False(DriverChoicesCommand.Changes(["growth", "aurora"]));
        Assert.False(DriverChoicesCommand.Changes(["growth", "aurora", "--repository", "ledger"]));
        Assert.True(DriverChoicesCommand.Changes(["growth", "aurora", "off"]));
        Assert.True(DriverChoicesCommand.Changes(["growth", "aurora", "--repository", "ledger", "mode"]));
    }

    [Fact]
    public void The_press_the_verb_plans_is_the_routes_payload_naming_the_place_and_the_value()
    {
        var verb = new GrowthVerb();

        var press = verb.Plan(["growth", "aurora", "--repository", "ledger", "asked"], DriverConfig.Empty, World());

        Assert.Equal(ChoiceRoutes.Growth, press.Route);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse("""{"workspace":"aurora","repository":"ledger","value":"asked"}"""), press.Payload));
        var workspace = verb.Plan(["growth", "aurora", "off"], DriverConfig.Empty, World());
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse("""{"workspace":"aurora","value":"off"}"""), workspace.Payload));
    }

    [Fact]
    public async Task The_lists_line_says_what_is_set_and_how_to_set_it()
    {
        File.WriteAllText(Config, """{"workspaceGrowth":{"aurora":"off"},"growth":{"ledger":"asked"}}""");

        Assert.Equal(0, await RunAsync("list"));

        Assert.Contains("growth     workspace aurora  off", _out.ToString(), StringComparison.Ordinal);
        Assert.Contains("growth     ledger  asked", _out.ToString(), StringComparison.Ordinal);
    }
}
