using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The migration's <c>run-rebinds</c> step: a run bound to a version this build cannot read (Current's) is rebound to the place's choice today,
/// a copy of its binding kept; a run on a version that reads, and one whose record is kept, are left as they are.
/// </summary>
public sealed class RunRebindsStepTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "run-rebinds", Guid.NewGuid().ToString("N")[..8]);

    private const string Graph = """
        {"schemaVersion":2,"entries":["claimed"],"end":"landing",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"landing","kind":"land","version":1,"reach":"performed","options":{"method":"merge"}}],
         "wires":[{"id":"c>l","kind":"after","from":"claimed","to":"landing"}]}
        """;

    public void Dispose() => GitSeed.Discard(_home);

    private DriverConfig Config() => DriverConfig.Load(DaorisHome.Entry(_home, HomeEntry.DriverConfig));

    /// <summary>A saved graph workflow chosen as the repository's own, its version's digest.</summary>
    private string Chosen()
    {
        var digest = RunHome.SaveGraph(_home, Graph, "dev-line");
        WorkflowSelection.Apply(Config(), new WorkflowUse("dev-line", RunHome.Repository, null, null)).Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        return digest;
    }

    /// <summary>A run bound as an older build bound it, to Current's, with no version.</summary>
    private void BoundToCurrent(string run) =>
        Assert.True(new WorkflowRunFiles(_home).Bind(new WorkflowRunBinding(run, RunHome.Repository, "default", WorkflowSelection.Current, WorkflowLevels.Repository,
            DateTimeOffset.Parse("2026-11-01T09:00:00Z"))));

    private static HomeMigrationRun Run(string home) => new(home, HomeMigration.Name);

    [Fact]
    public void A_run_bound_to_currents_is_rebound_to_the_places_choice_and_its_binding_kept()
    {
        var digest = Chosen();
        BoundToCurrent("q1");
        var path = WorkflowRunBindings.PathOf(_home, "q1");
        var before = File.ReadAllText(path);
        var run = Run(_home);

        new RunRebindsStep().Run(run);

        var rebound = new WorkflowRunFiles(_home).Read("q1")!;
        Assert.Equal(("dev-line", 1, digest, (string?)null), (rebound.Workflow, rebound.Version, rebound.Digest, rebound.Problem));
        Assert.Equal(DateTimeOffset.Parse("2026-11-01T09:00:00Z"), rebound.At);
        Assert.True(GraphRun.Names(rebound));
        Assert.Equal(before, File.ReadAllText(Path.Combine(run.Copies, Path.GetRelativePath(DaorisHome.FolderOf(_home).Folder, path))));
    }

    [Fact]
    public void A_run_on_a_version_that_reads_and_one_whose_record_is_kept_are_left_as_they_are()
    {
        var digest = Chosen();
        RunHome.Bind(_home, "q1", digest, DateTimeOffset.UtcNow, workflow: "dev-line");
        BoundToCurrent("q2");
        new NodeRecordFiles(_home, () => true).Write(new NodeRecord(RunUnits.IdOf("q2")));
        var (one, two) = (File.ReadAllText(WorkflowRunBindings.PathOf(_home, "q1")), File.ReadAllText(WorkflowRunBindings.PathOf(_home, "q2")));
        var run = Run(_home);

        new RunRebindsStep().Run(run);

        Assert.Equal((one, two), (File.ReadAllText(WorkflowRunBindings.PathOf(_home, "q1")), File.ReadAllText(WorkflowRunBindings.PathOf(_home, "q2"))));
        Assert.Empty(run.Changed);
    }

    [Fact]
    public void A_run_whose_place_chooses_nothing_that_binds_stays_as_it_was()
    {
        BoundToCurrent("q1");
        var before = File.ReadAllText(WorkflowRunBindings.PathOf(_home, "q1"));

        new RunRebindsStep().Run(Run(_home));

        Assert.Equal(before, File.ReadAllText(WorkflowRunBindings.PathOf(_home, "q1")));
    }
}
