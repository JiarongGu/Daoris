using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The terminal's doors that saved a list of steps refuse alike, in the one judge's words,
/// and write nothing: <c>apply</c> holding steps, <c>edit</c>, <c>new --from</c> a
/// version of steps or a step preset, and <c>import</c> of a file holding steps or a <c>handed</c>. A workflow saved as steps earlier is still read.
/// </summary>
public sealed class WorkflowAsGraphCommandTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "workflow-as-graph", Guid.NewGuid().ToString("N")[..8]);
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public WorkflowAsGraphCommandTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    private string Config => DaorisHome.Entry(_home, HomeEntry.DriverConfig);

    private string FileOf(string id) => WorkflowStore.PathOf(_home, id);

    private DriverChoicesWorld World() => new()
    {
        Verbs = DriverChoiceVerbs.All,
        Instance = new InstanceWorld { Home = _home, Environment = _ => null, Out = _out, Error = _error, Verb = "daoris" },
        ConfigPath = Config,
        Folder = _home,
        Now = () => new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero),
    };

    private Task<int> Run(string words) => DriverChoicesCommand.RunAsync(words.Split(' '), World(), CancellationToken.None);

    private const string Steps = """[ { "id": "work", "kind": "work" }, { "id": "land", "kind": "landing", "form": "merge", "accept": "you" } ]""";

    /// <summary>A workflow whose one version is the steps given, as an older build's file holds it.</summary>
    private void Stored(string steps)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FileOf("careful"))!);
        File.WriteAllText(FileOf("careful"), $$"""
            {
              "id": "careful",
              "name": "Careful",
              "versions": [
                { "version": 1, "at": "2026-10-09T09:12:00Z", "door": "screen", "steps": {{steps}} }
              ]
            }

            """.Replace("\r\n", "\n"));
    }

    private string Written(string name, string text)
    {
        var path = Path.Combine(_home, name);
        File.WriteAllText(path, text);
        return name;
    }

    private string Said => _error.ToString().Replace("\r\n", "\n").TrimEnd();

    [Fact]
    public async Task A_stored_steps_file_still_reads_and_shows_as_it_did()
    {
        Stored(Steps);

        Assert.Equal(0, await Run("workflow show careful"));

        Assert.Null(new WorkflowFiles(_home).Load("careful")!.Read.Versions[0].Problem);
        Assert.NotNull(new WorkflowFiles(_home).Load("careful")!.Read.Versions[0].Steps);
        Assert.Equal("", _error.ToString());
    }

    [Fact]
    public async Task Apply_as_graph_is_no_flag_and_names_no_conversion()
    {
        Stored(Steps);
        var before = File.ReadAllBytes(FileOf("careful"));

        Assert.NotEqual(0, await Run("workflow apply careful --as-graph"));

        Assert.Contains("`--as-graph` is not a flag", Said);
        Assert.DoesNotContain("convert", Said);
        Assert.Equal(before, File.ReadAllBytes(FileOf("careful")));
    }

    [Fact]
    public async Task Apply_of_a_file_holding_steps_is_refused_with_the_steps_words()
    {
        Stored(Steps);
        var before = File.ReadAllBytes(FileOf("careful"));
        var file = Written("steps.json", Steps);

        Assert.Equal(1, await Run($"workflow apply careful {file}"));

        Assert.Equal($"daoris: {WorkflowNamed.StepsRefused("careful")}", Said);
        Assert.Equal(before, File.ReadAllBytes(FileOf("careful")));
    }

    [Fact]
    public async Task Edit_is_refused_with_the_steps_words_and_writes_nothing()
    {
        Stored(Steps);
        var before = File.ReadAllBytes(FileOf("careful"));

        Assert.Equal(1, await Run("workflow edit careful add look --after work"));

        Assert.Equal($"daoris: {WorkflowNamed.StepsRefused("careful")}", Said);
        Assert.Equal(before, File.ReadAllBytes(FileOf("careful")));
    }

    [Fact]
    public async Task New_from_a_version_of_steps_is_refused_and_the_step_presets_are_gone()
    {
        Stored(Steps);

        Assert.Equal(1, await Run("workflow new copy --from careful@1"));
        Assert.Equal($"daoris: {WorkflowNamed.StepsRefused("copy")}", Said);
        Assert.False(File.Exists(FileOf("copy")));
        _error.GetStringBuilder().Clear();

        Assert.Equal(2, await Run("workflow new docs --from pull-request-no-press"));
        Assert.StartsWith("daoris: `pull-request-no-press` is no preset, no saved workflow and no `current:<repository>`", Said, StringComparison.Ordinal);
        Assert.DoesNotContain("pull-request-after-accept", Said, StringComparison.Ordinal);
        Assert.False(File.Exists(FileOf("docs")));
    }

    [Fact]
    public async Task Import_of_a_file_holding_steps_is_refused_with_the_steps_words()
    {
        Stored(Steps);
        var text = File.ReadAllText(FileOf("careful"));
        File.Delete(FileOf("careful"));
        var file = Written("careful.json", text);

        Assert.Equal(1, await Run($"workflow import {file}"));

        Assert.Equal($"daoris: {WorkflowNamed.StepsRefused("careful")}", Said);
        Assert.False(File.Exists(FileOf("careful")));
    }

}
