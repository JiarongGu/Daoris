using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris workflow use</c> refuses, writing nothing, a workflow with a step a department it reaches cannot run by its kind (<c>place-kind</c>),
/// naming the department, the step and the place; a workspace's use reaches the departments of that workspace whose own choice does not replace it.
/// </summary>
public sealed class WorkflowUsePlaceKindTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "workflow-use-place", Guid.NewGuid().ToString("N")[..8]);
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public WorkflowUsePlaceKindTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    private string Config => DaorisHome.Entry(_home, HomeEntry.DriverConfig);

    /// <summary>A saved workflow <paramref name="id"/>: the work and its landing, then a step of <paramref name="after"/> where one is named.</summary>
    private void Stored(string id, string? after = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(WorkflowStore.PathOf(_home, id))!);
        var last = after is null ? "" : $$""",{"id":"last","kind":"{{after}}","version":1,"reach":"performed"}""";
        var wire = after is null ? "" : """,{"id":"b","kind":"after","from":"landing","to":"last"}""";
        var end = after is null ? "landing" : "last";
        File.WriteAllText(WorkflowStore.PathOf(_home, id), $$$"""
            {"id":"{{{id}}}","name":"{{{id}}}","versions":[{"version":1,"at":"2026-11-10T09:00:00Z","door":"terminal","schema":2,"graph":
            {"schemaVersion":2,"entries":["work"],"end":"{{{end}}}",
             "nodes":[{"id":"work","kind":"claimed","version":1,"reach":"observed"},
                      {"id":"landing","kind":"land","version":1,"reach":"performed","options":{"method":"merge"}}{{{last}}}],
             "wires":[{"id":"a","kind":"after","from":"work","to":"landing"}{{{wire}}}]}}]}
            """);
    }

    private async Task<int> Run(string words, params (string Repository, string Kind)[] registry)
    {
        var views = registry.Select(each => new RepoView(each.Repository, false, null, "work") { Kind = each.Kind });
        var world = new DriverChoicesWorld
        {
            Verbs = DriverChoiceVerbs.All,
            Instance = new InstanceWorld { Home = _home, Environment = _ => null, Out = _out, Error = _error, Verb = "daoris" },
            ConfigPath = Config,
            Folder = _home,
            Now = () => new DateTimeOffset(2026, 11, 11, 9, 0, 0, TimeSpan.Zero),
            Registry = _ => Task.FromResult(ChoicesRegistry.Of(views)),
        };
        return await DriverChoicesCommand.RunAsync(words.Split(' '), world, CancellationToken.None);
    }

    private bool Chose() => File.Exists(Config) && File.ReadAllText(Config).Contains("\"workflows\"", StringComparison.Ordinal);

    private void Said(string words) => Assert.True(_error.ToString().Contains(words, StringComparison.Ordinal), _error.ToString());

    [Fact]
    public async Task A_landing_workflow_chosen_for_a_folder_is_refused_naming_the_step_and_the_place()
    {
        Stored("landing");

        var exit = await Run("workflow use landing --repository notes", ("notes", PlaceKind.Folder));

        Assert.Equal(1, exit);
        Said("`notes` cannot follow it: `landing` v1's step `landing` is a `land`, which a folder cannot run: it runs in a git repository alone.");
        Said("Nothing was written.");
        Assert.False(Chose());
    }

    [Fact]
    public async Task An_apply_chosen_for_a_git_repository_is_refused()
    {
        Stored("applying", NodeKinds.Apply);

        var exit = await Run("workflow use applying --repository web-app", ("web-app", PlaceKind.Git));

        Assert.Equal(1, exit);
        Said("`applying` v1's step `last` is a `apply`, which a git repository cannot run: it runs in a system alone.");
        Assert.False(Chose());
    }

    [Fact]
    public async Task A_workspace_s_use_is_refused_where_a_department_following_it_cannot_run_a_step()
    {
        Stored("landing");

        var exit = await Run("workflow use landing --workspace work", ("web-app", PlaceKind.Git), ("store", PlaceKind.System));

        Assert.Equal(1, exit);
        Said("`store` cannot follow it: `landing` v1's step `landing` is a `land`, which a system cannot run");
        Assert.DoesNotContain("`web-app` cannot", _error.ToString());
        Assert.False(Chose());
    }

    [Fact]
    public async Task A_workflow_whose_steps_its_place_runs_is_chosen()
    {
        Stored("landing");

        var exit = await Run("workflow use landing --repository web-app", ("web-app", PlaceKind.Git));

        Assert.True(exit == 0, _error.ToString());
        Assert.True(Chose());
    }
}
