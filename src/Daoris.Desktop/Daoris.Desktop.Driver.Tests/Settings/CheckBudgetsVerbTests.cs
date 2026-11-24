using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris settings checks &lt;repository&gt; …</c>: a repository's check budgets said and changed through the door's one judge, acting
/// alone on the home and refused from a session's terminal; the machine's own <c>checks &lt;n&gt;|auto</c> is a word apart.
/// </summary>
public sealed class CheckBudgetsVerbTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "check-budgets-verb", Guid.NewGuid().ToString("N")[..8]);
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public CheckBudgetsVerbTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    private DriverChoicesWorld World(params (string Name, string Value)[] variables) => new()
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
        ConfigPath = DaorisHome.Entry(_home, HomeEntry.DriverConfig),
    };

    private Task<int> RunAsync(string words, params (string Name, string Value)[] variables) =>
        DriverChoicesCommand.RunAsync(words.Split(' '), World(variables), CancellationToken.None);

    [Fact]
    public async Task A_pin_is_set_with_its_reason_and_the_record_says_it()
    {
        Assert.Equal(0, await RunAsync("checks engine"));
        Assert.Contains("nothing", _out.ToString(), StringComparison.Ordinal);

        Assert.Equal(0, await DriverChoicesCommand.RunAsync(["checks", "engine", "pin", "merge", "33", "--why", "slow disk"], World(), CancellationToken.None));

        var kept = CheckBudgets.Of(_home, "engine").Gate("merge");
        Assert.Equal(new GateTimeout(33, "pin", "slow disk"), CheckBudgets.TimeoutOf(kept, "merge"));
        _out.GetStringBuilder().Clear();
        Assert.Equal(0, await RunAsync("checks engine"));
        Assert.Contains("gate merge: 33 min (pinned: slow disk)", _out.ToString(), StringComparison.Ordinal);
        Assert.Equal("", _error.ToString());
    }

    [Theory]
    [InlineData("checks engine alone merge on", "alone")]
    [InlineData("checks engine memoryGb merge 12", "12 GB")]
    [InlineData("checks engine timeoutMs cli/unit 45000", "set cli/unit: 45000 ms")]
    [InlineData("checks engine treeBaseline node tools/base.mjs", "treeBaseline: node tools/base.mjs")]
    [InlineData("checks engine devkit 0.0.1", "devkit: 0.0.1")]
    [InlineData("checks engine localInputs local/a.txt local/b.txt", "localInputs: local/a.txt, local/b.txt")]
    [InlineData("checks engine patternsFile local/p.txt", "patternsFile: local/p.txt")]
    public async Task Each_member_is_set_and_said(string words, string said)
    {
        Assert.Equal(0, await RunAsync(words));

        Assert.Contains(said, _out.ToString(), StringComparison.Ordinal);
        Assert.Equal("", _error.ToString());
    }

    [Fact]
    public async Task A_clear_takes_a_member_away_and_a_pin_is_unpinned()
    {
        await RunAsync("checks engine devkit 0.0.1");
        await DriverChoicesCommand.RunAsync(["checks", "engine", "pin", "merge", "33", "--why", "x"], World(), CancellationToken.None);

        Assert.Equal(0, await RunAsync("checks engine devkit --clear"));
        Assert.Equal(0, await RunAsync("checks engine pin merge --clear"));

        var kept = CheckBudgets.Of(_home, "engine");
        Assert.Null(kept.Devkit);
        Assert.Null(kept.Gate("merge").Pin);
    }

    [Theory]
    [InlineData("checks engine pin merge 33", "--why")]
    [InlineData("checks engine pin merge many --why x", "no number")]
    [InlineData("checks engine verifyBudget merge 3", "no member")]
    [InlineData("checks engine alone merge maybe", "on or off")]
    [InlineData("checks engine localInputs secrets/token.txt", "`local/`")]
    [InlineData("checks engine pin", "name it")]
    public async Task What_the_door_does_not_take_is_refused_in_a_sentence_and_nothing_is_written(string words, string said)
    {
        var exit = await RunAsync(words);

        Assert.Equal(2, exit);
        Assert.Contains(said, _error.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(CheckBudgets.PathOf(_home)));
    }

    [Fact]
    public async Task A_change_from_a_sessions_terminal_is_refused_and_a_read_is_said()
    {
        var exit = await RunAsync("checks engine devkit 0.0.1", ("DAORIS_SESSION_ID", "s1a2b3c4"));

        Assert.Equal(1, exit);
        Assert.False(File.Exists(CheckBudgets.PathOf(_home)));
        _error.GetStringBuilder().Clear();
        Assert.Equal(0, await RunAsync("checks engine", ("DAORIS_SESSION_ID", "s1a2b3c4")));
        Assert.Equal("", _error.ToString());
    }

    [Fact]
    public async Task A_file_that_does_not_read_is_named_and_the_machine_s_own_setting_is_untouched()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CheckBudgets.PathOf(_home))!);
        await File.WriteAllTextAsync(CheckBudgets.PathOf(_home), "{ not json");

        Assert.Equal(2, await RunAsync("checks engine"));
        Assert.Contains("check-budgets", _error.ToString(), StringComparison.Ordinal);

        Assert.Equal(0, await RunAsync("checks 3"));
        Assert.Equal(3, DriverConfig.Load(DaorisHome.Entry(_home, HomeEntry.DriverConfig)).Checks);
    }
}
