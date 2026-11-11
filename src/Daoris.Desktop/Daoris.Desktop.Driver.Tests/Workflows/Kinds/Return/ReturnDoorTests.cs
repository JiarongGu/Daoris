using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A run's <c>return</c> through its door: the copy its work's session worked in brought back and the node reached, the copy removed;
/// a file changed on both sides holding the node active on <c>both-changed</c> with the path, the copy kept, until the person's choice
/// is marked on the record; a place a turn runs in holding it on <c>place-busy</c>; and a run whose session worked in place reaching it
/// with nothing to bring back.
/// </summary>
public sealed class ReturnDoorTests : IDisposable
{
    private const string Unit = "run:r1";
    private static readonly NodeDeclaration Return = new(NodeKinds.Return, NodeKinds.Return, 1, NodeReach.Performed);

    private readonly string _scratch = ConformanceScratch.New("return-door");
    private readonly List<string> _said = [];
    private string Home => Path.Combine(_scratch, "home");
    private string Place => Path.Combine(_scratch, "reports");

    public void Dispose() => ConformanceScratch.Remove(_scratch);

    private FolderCopy Copy()
    {
        Directory.CreateDirectory(Place);
        File.WriteAllText(Path.Combine(Place, "notes.md"), "first\n");
        var copy = FolderCopies.Make(Home, "default", "reports", Place, ManifestBounds.Default).Copy!;
        FolderCopies.Owned(Home, copy.Folder, "rec-1");
        return copy;
    }

    private static WorkUnit Run() =>
        new(Unit, WorkUnitKind.Run, new WorkOwner("console", "default", null, WorkRecord.DriverWriter), new WorkSubject("r1", null),
            new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "running"), []) { Executors = ["rec-1"] };

    private void Active(params string[] fired) =>
        new NodeRecordFiles(Home, () => true).Write(new NodeRecord(Unit)
        {
            Statuses = new Dictionary<string, NodeStatus>
            {
                [Return.Id] = new(Unit, Return.Id, NodeStates.Active) { Attempt = 1, Tries = 1, Binding = new NodeAcceptance(Unit, null, "graph-1", Return.Id, 1, 1) },
            },
            Fired = fired,
        });

    private NodeStatus Status() => new NodeRecordFiles(Home, () => true).Read(Unit)!.Statuses[Return.Id];

    private ReturnDoor Door(bool busy = false) =>
        new(Home, _said.Add, turning: _ => Task.FromResult<IReadOnlySet<string>>(busy ? new HashSet<string>([Place]) : new HashSet<string>()), holdsLease: () => true);

    private Task PerformAsync(ReturnDoor door) =>
        door.PerformAsync(
            ReturnStart.Of(Run(), Return, Status(), new NodeRecordFiles(Home, () => true).Read(Unit)!, StepAuthority.OfDoor("r1", Return.Id, 1)), CancellationToken.None);

    [Fact]
    public async Task The_copy_s_changes_are_brought_back_the_node_reached_and_the_copy_removed()
    {
        var copy = Copy();
        File.WriteAllText(Path.Combine(copy.Folder, "notes.md"), "the session's\n");
        Active();

        await PerformAsync(Door());

        Assert.Equal(NodeStates.Reached, Status().State);
        Assert.Equal("the session's\n", File.ReadAllText(Path.Combine(Place, "notes.md")));
        Assert.False(Directory.Exists(copy.Folder));
    }

    [Fact]
    public async Task A_file_changed_on_both_sides_waits_for_the_person_s_choice_and_the_copy_is_kept_until_it_is_made()
    {
        var copy = Copy();
        File.WriteAllText(Path.Combine(copy.Folder, "notes.md"), "the session's\n");
        File.WriteAllText(Path.Combine(Place, "notes.md"), "the person's\n");
        Active();

        await PerformAsync(Door());

        var waiting = Status();
        Assert.Equal((NodeStates.Active, ReturnNode.BothChanged, "notes.md"), (waiting.State, waiting.Code, waiting.Holds));
        Assert.Equal([NodeWordActs.KeepYours, NodeWordActs.TakeSession, NodeWordActs.KeepBoth], new ReturnNode().Acts(Return, waiting));
        Assert.Equal(ReturnNode.BothChanged, new ReturnNode().WaitOf(waiting)!.Code);
        Assert.Equal("notes.md", new ReturnNode().WaitOf(waiting)!.Values["paths"]);
        Assert.True(Directory.Exists(copy.Folder));

        new NodeRecordFiles(Home, () => true).Edit<bool>(Unit, record =>
            (record with { Fired = [ReturnNode.ChoiceKey(Return.Id, 1, NodeWordActs.TakeSession, "notes.md")] }, true));
        await PerformAsync(Door());

        Assert.Equal(NodeStates.Reached, Status().State);
        Assert.Equal("the session's\n", File.ReadAllText(Path.Combine(Place, "notes.md")));
        Assert.False(Directory.Exists(copy.Folder));
    }

    [Fact]
    public async Task A_place_a_turn_runs_in_holds_the_return_on_place_busy()
    {
        var copy = Copy();
        File.WriteAllText(Path.Combine(copy.Folder, "notes.md"), "the session's\n");
        Active();

        await PerformAsync(Door(busy: true));

        Assert.Equal((NodeStates.Active, ReturnNode.PlaceBusy), (Status().State, Status().Code));
        Assert.Equal("first\n", File.ReadAllText(Path.Combine(Place, "notes.md")));
    }

    [Fact]
    public async Task A_run_whose_session_worked_in_place_reaches_it_with_nothing_to_bring_back()
    {
        Active();

        await PerformAsync(Door());

        Assert.Equal(NodeStates.Reached, Status().State);
        Assert.Contains(_said, said => said.Contains("nothing to bring back", StringComparison.Ordinal));
    }

    [Fact]
    public void A_graph_with_a_return_and_no_isolate_is_refused_at_its_save()
    {
        var graph = new NodeGraph(
            [new NodeDeclaration("c", NodeKinds.Claimed, 1, NodeReach.Observed), Return],
            [new Wire("w1", WireKinds.After, new WireEnd("c"), new WireEnd(Return.Id))],
            ["c"],
            Return.Id);

        Assert.Contains(new ReturnNode().Problems(graph, Return), problem => problem.Code == GraphRefusal.ReturnIsolate);
    }
}
