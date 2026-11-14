using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris workflow new &lt;id&gt; --from &lt;id&gt;@&lt;version&gt;</c> over a saved graph version saves the copy as a new workflow, or names why it
/// cannot (an unknown version, an id already saved) and writes nothing; <c>--plan</c> says it and saves nothing.
/// </summary>
public sealed class WorkflowCopyOfSavedTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "workflow-copy-of-saved", Guid.NewGuid().ToString("N")[..8]);
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public WorkflowCopyOfSavedTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    private Task<int> Run(string words) =>
        DriverChoicesCommand.RunAsync(words.Split(' '), new DriverChoicesWorld
        {
            Verbs = DriverChoiceVerbs.All,
            Instance = new InstanceWorld { Home = _home, Environment = _ => null, Out = _out, Error = _error, Verb = "daoris" },
            ConfigPath = DaorisHome.Entry(_home, HomeEntry.DriverConfig),
            Folder = _home,
            Now = () => new DateTimeOffset(2026, 11, 13, 9, 0, 0, TimeSpan.Zero),
            Registry = _ => Task.FromResult(new ChoicesRegistry([], null)),
        }, CancellationToken.None);

    private NamedVersion Saved(string id)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(WorkflowStore.PathOf(_home, id)));
        return WorkflowNamed.Read(document.RootElement).Versions.Single();
    }

    private string Said => _error.ToString().Replace("\r\n", "\n").TrimEnd();

    [Theory]
    [InlineData("made@1")]
    [InlineData("made")]
    public async Task A_saved_graph_version_is_copied_into_a_new_workflow(string source)
    {
        Assert.Equal(0, await Run("workflow new made --from merge-after-accept --with-opinion --name Mine"));
        _out.GetStringBuilder().Clear();

        var exit = await Run($"workflow new copy --from {source}");

        Assert.Equal((0, ""), (exit, Said));
        Assert.True(File.Exists(WorkflowStore.PathOf(_home, "copy")));
        var (original, copy) = (Saved("made"), Saved("copy"));
        Assert.Null(copy.Problem);
        Assert.Equal(original.Digest, copy.Digest);
        Assert.Equal(original.Graph!.Nodes.Select(node => node.Id), copy.Graph!.Nodes.Select(node => node.Id));
        Assert.Contains("`copy` v1, a new workflow", _out.ToString());
        Assert.Contains("from `made` v1", _out.ToString());
    }

    [Fact]
    public async Task A_copy_keeps_the_name_it_is_given()
    {
        await Run("workflow new made --from merge-after-accept");

        Assert.Equal(0, await Run("workflow new copy --from made@1 --name Second"));

        using var document = JsonDocument.Parse(File.ReadAllText(WorkflowStore.PathOf(_home, "copy")));
        Assert.Equal("Second", document.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task A_plan_of_a_copy_saves_nothing()
    {
        await Run("workflow new made --from merge-after-accept");

        Assert.Equal(0, await Run("workflow new copy --from made@1 --plan"));

        Assert.False(File.Exists(WorkflowStore.PathOf(_home, "copy")));
        Assert.Contains("`copy` v1", _out.ToString());
    }

    [Fact]
    public async Task A_version_the_source_does_not_keep_is_named_and_nothing_is_written()
    {
        await Run("workflow new made --from merge-after-accept");

        Assert.Equal(2, await Run("workflow new copy --from made@7"));

        Assert.Equal("daoris: `made` keeps no v7 — it keeps v1.", Said);
        Assert.False(File.Exists(WorkflowStore.PathOf(_home, "copy")));
    }

    [Fact]
    public async Task An_id_that_is_saved_already_is_refused_and_the_file_is_left_as_it_was()
    {
        await Run("workflow new made --from merge-after-accept");
        await Run("workflow new copy --from merge-automatic");
        var before = File.ReadAllBytes(WorkflowStore.PathOf(_home, "copy"));

        Assert.Equal(1, await Run("workflow new copy --from made@1"));

        Assert.Contains("is a workflow here already", Said);
        Assert.Equal(before, File.ReadAllBytes(WorkflowStore.PathOf(_home, "copy")));
    }
}
