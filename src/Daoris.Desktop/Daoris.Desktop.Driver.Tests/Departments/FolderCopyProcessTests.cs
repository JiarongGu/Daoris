using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Row 1f's proof on a real folder of documents: two sessions set apart at once, each in a copy of its own, both brought back through
/// the <c>return</c>'s door. The first comes back whole; the second, which changed a file the first also changed, waits on the person
/// with that path until their <i>Keep both</i>, and then writes its version beside the folder's, every other file of its back.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class FolderCopyProcessTests : IDisposable
{
    private static readonly NodeDeclaration Return = new("back", NodeKinds.Return, 1, NodeReach.Performed);

    private readonly string _scratch = ConformanceScratch.New("folder-copy-proc");
    private string Home => Path.Combine(_scratch, "home");
    private string Place => Path.Combine(_scratch, "reports");

    public void Dispose() => ConformanceScratch.Remove(_scratch);

    private static WorkUnit Run(string unit, string record) =>
        new(unit, WorkUnitKind.Run, new WorkOwner("console", "default", null, WorkRecord.DriverWriter), new WorkSubject(unit, null),
            new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "running"), []) { Executors = [record] };

    private NodeRecordFiles Records => new(Home, () => true);

    private void Active(string unit) => Records.Write(new NodeRecord(unit)
    {
        Statuses = new Dictionary<string, NodeStatus>
        {
            [Return.Id] = new(unit, Return.Id, NodeStates.Active) { Attempt = 1, Tries = 1, Binding = new NodeAcceptance(unit, null, "graph-1", Return.Id, 1, 1) },
        },
    });

    private async Task<NodeStatus> ReturnAsync(string unit, string record)
    {
        var door = new ReturnDoor(Home, holdsLease: () => true);
        var status = Records.Read(unit)!.Statuses[Return.Id];
        await door.PerformAsync(ReturnStart.Of(Run(unit, record), Return, status, Records.Read(unit)!, StepAuthority.OfDoor(unit, Return.Id, 1))
            with { Reviewed = EffectRecords.Copied(Home, [record]) }, CancellationToken.None);
        return Records.Read(unit)!.Statuses[Return.Id];
    }

    private FolderCopy Copy(string record)
    {
        var copy = FolderCopies.Make(Home, "default", "reports", Place, ManifestBounds.Default).Copy!;
        FolderCopies.Owned(Home, copy.Folder, record, null);
        return copy;
    }

    [Fact]
    public async Task A_return_waits_while_a_session_still_works_in_the_copy_and_never_removes_it()
    {
        Directory.CreateDirectory(Place);
        File.WriteAllText(Path.Combine(Place, "summary.md"), "# Summary\n");
        var copy = Copy("rec-a");
        File.WriteAllText(Path.Combine(copy.Folder, "summary.md"), "# Summary\nOctober closed.\n");
        Active("run:a");
        var door = new ReturnDoor(Home, holdsLease: () => true, turning: _ => Task.FromResult<IReadOnlySet<string>>(new HashSet<string> { copy.Folder }));
        var status = Records.Read("run:a")!.Statuses[Return.Id];

        await door.PerformAsync(ReturnStart.Of(Run("run:a", "rec-a"), Return, status, Records.Read("run:a")!, StepAuthority.OfDoor("run:a", Return.Id, 1))
            with { Reviewed = EffectRecords.Copied(Home, ["rec-a"]) }, CancellationToken.None);

        var held = Records.Read("run:a")!.Statuses[Return.Id];
        Assert.Equal((NodeStates.Active, ReturnNode.PlaceBusy), (held.State, held.Code));
        Assert.Equal("# Summary\n", File.ReadAllText(Path.Combine(Place, "summary.md")));
        Assert.True(Directory.Exists(copy.Folder));
    }

    [Fact]
    public async Task Two_sessions_work_apart_in_copies_and_a_file_both_changed_waits_for_the_person_s_choice()
    {
        Directory.CreateDirectory(Path.Combine(Place, "q4"));
        File.WriteAllText(Path.Combine(Place, "summary.md"), "# Summary\n");
        File.WriteAllText(Path.Combine(Place, "q4", "figures.csv"), "month,total\noct,10\n");
        File.WriteAllBytes(Path.Combine(Place, "q4", "chart.png"), [0x89, 0x50, 0x4e, 0x47, 0, 1, 2, 3]);

        var first = Copy("rec-a");
        var second = Copy("rec-b");
        Assert.NotEqual(first.Folder, second.Folder);

        File.WriteAllText(Path.Combine(first.Folder, "summary.md"), "# Summary\nOctober closed.\n");
        File.WriteAllText(Path.Combine(first.Folder, "q4", "notes.md"), "new notes\n");
        File.WriteAllText(Path.Combine(second.Folder, "summary.md"), "# Summary\nNovember opened.\n");
        File.WriteAllBytes(Path.Combine(second.Folder, "q4", "chart.png"), [0x89, 0x50, 0x4e, 0x47, 9, 9, 9, 9]);

        Active("run:a");
        Assert.Equal(NodeStates.Reached, (await ReturnAsync("run:a", "rec-a")).State);
        Assert.Equal("# Summary\nOctober closed.\n", File.ReadAllText(Path.Combine(Place, "summary.md")));
        Assert.True(File.Exists(Path.Combine(Place, "q4", "notes.md")));
        Assert.False(Directory.Exists(first.Folder));

        Active("run:b");
        var waiting = await ReturnAsync("run:b", "rec-b");
        Assert.Equal((NodeStates.Active, ReturnNode.BothChanged, "summary.md"), (waiting.State, waiting.Code, string.Join("\n", ReturnHolds.Read(waiting.Holds).Waiting.Keys)));
        Assert.Equal([0x89, 0x50, 0x4e, 0x47, 9, 9, 9, 9], File.ReadAllBytes(Path.Combine(Place, "q4", "chart.png")));
        Assert.True(Directory.Exists(second.Folder));

        Records.Edit<bool>("run:b", record => (record with { Fired = [ReturnNode.ChoiceKey(Return.Id, 1, NodeWordActs.KeepBoth, "summary.md")] }, true));
        Assert.Equal(NodeStates.Reached, (await ReturnAsync("run:b", "rec-b")).State);
        Assert.Equal("# Summary\nOctober closed.\n", File.ReadAllText(Path.Combine(Place, "summary.md")));
        Assert.Equal("# Summary\nNovember opened.\n", File.ReadAllText(Path.Combine(Place, $"summary ({second.Id}).md")));
        Assert.False(Directory.Exists(second.Folder));
    }
}
