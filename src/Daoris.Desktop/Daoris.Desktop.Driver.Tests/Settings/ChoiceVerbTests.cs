using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The verbs of <c>daoris-driver driver</c> that the subdomains above the settings own (FOUNDATION1 mod-iii): the settings declare the
/// port, each subdomain implements its verbs, and Loop wires them into the terminal's world, so the settings' command names none of them.
/// </summary>
public sealed class ChoiceVerbTests : IDisposable
{
    private readonly string _home = Directory.CreateTempSubdirectory("daoris-choiceverbs-").FullName;
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private DriverChoicesWorld World(IReadOnlyList<ChoiceVerb> verbs) => new()
    {
        Verbs = verbs,
        Instance = new InstanceWorld { Home = _home, Environment = _ => null, Out = _out, Error = _error, Verb = "daoris" },
        ConfigPath = Path.Combine(_home, "driver.json"),
    };

    [Fact]
    public void Each_wired_verb_is_one_of_the_words_the_command_has_and_is_wired_once()
    {
        var words = DriverChoiceVerbs.All.Select(verb => verb.Word).ToList();

        Assert.Equal(words.Count, words.Distinct(StringComparer.Ordinal).Count());
        Assert.All(words, word => Assert.Contains(word, DriverChoicesCommand.Verbs));
    }

    [Fact]
    public void The_grammar_the_command_reads_before_any_verb_is_planned_is_the_verbs_own()
    {
        Assert.Equal(ChoiceGrammar.BrainstormMinuteFlags, BrainstormsVerb.MinuteFlags.Select(each => each.Flag));
        Assert.Equal(DriverWorkflowsCommand.Writes, ChoiceGrammar.WorkflowWrites);
        Assert.True(DriverChoicesCommand.Changes(["brainstorms", "--pass-minutes", "5"]));
        Assert.False(DriverChoicesCommand.Changes(["brainstorms"]));
        Assert.True(DriverChoicesCommand.Changes(["workflow", "use", "docs", "--repository", "engine"]));
        Assert.False(DriverChoicesCommand.Changes(["workflow", "use", "docs", "--repository", "engine", "--plan"]));
    }

    [Fact]
    public async Task A_verb_the_world_carries_is_planned_and_said_by_it()
    {
        var config = Path.Combine(_home, "driver.json");

        var exit = await DriverChoicesCommand.RunAsync(["helpers", "web-app", "--mode", "on"], World(DriverChoiceVerbs.All), CancellationToken.None);

        Assert.Equal(0, exit);
        Assert.Contains("helpers for `web-app`: on", _out.ToString());
        Assert.Equal(HelperKey.On, DriverConfig.Load(config).Helpers["web-app"].Mode);
    }

    [Fact]
    public async Task A_verb_no_wired_verb_carries_is_no_standing_choice_the_world_changes()
    {
        var exit = await DriverChoicesCommand.RunAsync(["helpers", "web-app", "--mode", "on"], World([]), CancellationToken.None);

        Assert.Equal(2, exit);
        Assert.Contains("`helpers` is not a standing choice this program changes", _error.ToString());
        Assert.False(File.Exists(Path.Combine(_home, "driver.json")));
    }
}
