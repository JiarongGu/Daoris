using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A folder's run on the shipped <i>In-copy</i> graph, through the node runtime and the return's own door: files only the copy changed come
/// back while one changed on both sides waits, a choice holds only on the versions it names, and a copy that moved after its review
/// is not brought back.
/// </summary>
public sealed class CopyReturnRunTests : NodeRunFixture
{
    private const string Workspace = "default";
    private readonly List<string> _said = [];
    private ReturnDoor? _door;

    private string Place => Path.Combine(_home, "reports");

    private ReturnDoor Door => _door ??= new(_home, _said.Add, turning: _ => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>()), holdsLease: () => true);

    /// <summary>The door the runtime hands a return to: it keeps the start, for the test to perform once the copy has moved.</summary>
    private sealed class Held : IReturnDoor
    {
        public ReturnStart? Start { get; private set; }

        bool IReturnDoor.Start(ReturnStart start)
        {
            Start ??= start;
            return true;
        }
    }

    private NodeRuntime Returning(INodeDoor door) => new(Files, _port, () => _now, () => true) { Doors = NodeDoors.None.With(NodeKinds.Return, door) };

    private static void Write(string folder, string name, string text)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, name), text);
    }

    private FolderCopy Copy()
    {
        foreach (var name in new[] { "a.md", "b.md", "c.md" }) Write(Place, name, $"{name} first\n");
        var copy = FolderCopies.Make(_home, Workspace, RunHome.Repository, Place, ManifestBounds.Default).Copy!;
        FolderCopies.Owned(_home, copy.Folder, "rec-1", RunId);
        BoundTo(WorkflowDefaults.Of("in-copy")!.GraphJson);
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done") with { Submission = new QuestSubmissionView("m1", 1, _now, "answer 1") })];
        return copy;
    }

    private async Task TurnAsync(FolderCopy copy, Action work)
    {
        await PlaceTurns.StartAsync(_home, Workspace, RunHome.Repository, copy.Folder, "s-aaaaaaaa", null, CancellationToken.None, run: RunId);
        work();
        await PlaceTurns.EndAsync(_home, "s-aaaaaaaa", CancellationToken.None);
    }

    private async Task AcceptedAsync(NodeRuntime runtime)
    {
        await RunPassAsync(runtime);
        Assert.Equal((NodeStates.Active, ReviewKinds.Copy), (RunStatus("review").State, ReviewRecords.Read(_home, RunId, "review", RunStatus("review").Attempt)?.Kind));
        Assert.True(RunNodeWords.Word(Files, RunId, "review", NodeWordActs.Accept, null, () => _now).Done);
        await RunPassAsync(runtime);
    }

    private string Folder(string name) => File.ReadAllText(Path.Combine(Place, name));

    private async Task ChooseAsync(NodeRuntime runtime, string act, string path)
    {
        Assert.True(RunNodeWords.Word(Files, RunId, "return", act, path, () => _now).Done);
        await RunPassAsync(runtime);
        await Door.IdleAsync();
    }

    [Fact]
    public async Task A_partial_return_brings_back_the_files_only_the_copy_changed_and_the_run_ends_once_the_choice_is_made()
    {
        var copy = Copy();
        await TurnAsync(copy, () =>
        {
            Write(copy.Folder, "a.md", "a.md copy\n");
            Write(copy.Folder, "b.md", "b.md copy\n");
        });
        Write(Place, "b.md", "b.md folder\n");
        var runtime = Returning(Door);
        await AcceptedAsync(runtime);
        await Door.IdleAsync();

        var waiting = RunStatus("return");
        Assert.Equal((NodeStates.Active, ReturnNode.BothChanged), (waiting.State, waiting.Code));
        Assert.Equal(["b.md"], ReturnHolds.Read(waiting.Holds).Waiting.Keys);
        Assert.Equal("1", new ReturnNode().WaitOf(waiting)!.Values["returned"]);
        Assert.Equal(("a.md copy\n", "b.md folder\n"), (Folder("a.md"), Folder("b.md")));
        Assert.True(Directory.Exists(copy.Folder));
        Assert.Null(Files.Read(RunUnit)!.Ended);

        await ChooseAsync(runtime, NodeWordActs.TakeSession, "b.md");
        await RunPassAsync(runtime);

        Assert.Equal(NodeStates.Reached, RunStatus("return").State);
        Assert.Equal("b.md copy\n", Folder("b.md"));
        Assert.False(Directory.Exists(copy.Folder));
        Assert.Equal(RunOutcomes.Completed, Files.Read(RunUnit)!.Ended?.Outcome);
    }

    [Fact]
    public async Task A_choice_lapses_when_its_file_moves_after_it_was_shown_and_asks_again_on_the_new_versions()
    {
        var copy = Copy();
        await TurnAsync(copy, () => Write(copy.Folder, "b.md", "b.md copy\n"));
        Write(Place, "b.md", "b.md folder\n");
        var runtime = Returning(Door);
        await AcceptedAsync(runtime);
        await Door.IdleAsync();
        var shown = ReturnHolds.Read(RunStatus("return").Holds).Waiting["b.md"];

        Write(Place, "b.md", "b.md folder, mended\n");
        await ChooseAsync(runtime, NodeWordActs.KeepYours, "b.md");

        var asked = RunStatus("return");
        Assert.Equal((NodeStates.Active, ReturnNode.BothChanged), (asked.State, asked.Code));
        Assert.NotEqual(shown, ReturnHolds.Read(asked.Holds).Waiting["b.md"]);
        Assert.Contains(_said, line => line.Contains("lapsed", StringComparison.Ordinal));
        Assert.Equal("b.md folder, mended\n", Folder("b.md"));
        Assert.True(Directory.Exists(copy.Folder));

        await ChooseAsync(runtime, NodeWordActs.TakeSession, "b.md");
        await RunPassAsync(runtime);

        Assert.Equal(NodeStates.Reached, RunStatus("return").State);
        Assert.Equal("b.md copy\n", Folder("b.md"));
    }

    [Fact]
    public async Task A_copy_that_moved_after_its_review_read_it_fails_the_return_moved_and_its_newer_work_is_reviewed_again()
    {
        var copy = Copy();
        await TurnAsync(copy, () => Write(copy.Folder, "a.md", "a.md copy\n"));
        var held = new Held();
        var runtime = Returning(held);
        await AcceptedAsync(runtime);
        var reviewed = RunStatus("review").Attempt;

        await TurnAsync(copy, () => Write(copy.Folder, "a.md", "a.md copy, mended\n"));
        await Door.PerformAsync(held.Start!, CancellationToken.None);

        var failed = RunStatus("return");
        Assert.Equal((NodeStates.Failed, ReturnNode.Moved), (failed.State, failed.Code));
        Assert.Equal("a.md first\n", Folder("a.md"));
        Assert.True(Directory.Exists(copy.Folder));

        await RunPassAsync(runtime);

        Assert.Equal((NodeStates.Active, reviewed + 1), (RunStatus("review").State, RunStatus("review").Attempt));
    }
}
