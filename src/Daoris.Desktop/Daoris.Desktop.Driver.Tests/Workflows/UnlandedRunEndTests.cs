using Daoris.Contracts.Quests;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A run whose session's tree is being rebased reads no branch of it, and its in-place end (the work done on the line, no branch to land) closed
/// it with its landing owed, its quest then read Done and its commits went unlanded with nothing said. A run is never ended while a branch its
/// home records for it holds commits the line does not: the held end is an error line naming the branch and the commits, the quest stays taken,
/// and the run lands once the branch reads.
/// </summary>
public sealed class UnlandedRunEndTests : NodeRunFixture
{
    private const string Branch = "daoris/s1";
    private const string Graph = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>h","kind":"after","from":"claimed","to":"handed"}]}
        """;

    private readonly List<(string Code, string Said)> _said = [];
    private readonly List<RunFinish> _finished = [];
    private readonly Door _door = new();
    private IReadOnlyList<LandingCommit>? _lacks = [new("aaaaaaaa11111111111111111111111111111111", "Mend the parser"), new("bbbbbbbb22222222222222222222222222222222", "Test it")];

    private sealed class Door : ILandingDoor
    {
        public List<LandStart> Starts { get; } = [];

        public bool Start(LandStart start)
        {
            Starts.Add(start);
            return true;
        }
    }

    private NodeRuntime Driving()
    {
        var runtime = Runtime();
        runtime.Doors = NodeDoors.None.With(NodeKinds.Land, _door).Saying((code, said) => _said.Add((code, said)));
        runtime.Finish = (end, _) =>
        {
            _finished.Add(end);
            return Task.FromResult<RunEndResponse?>(new RunEndResponse(null, "ended", [.. end.Quests.Select(quest => quest.Id)], end.Sessions));
        };
        runtime.SessionsRead = _ => Task.FromResult<IReadOnlyList<SessionRecord>?>([]);
        return runtime;
    }

    private Task<NodeRuntimePass> PassAsync(NodeRuntime runtime) => runtime.PassAsync(RunLook() with
    {
        Lacks = (_, _, _, _, _) => Task.FromResult(_lacks),
    }, CancellationToken.None);

    /// <summary>The home's record of the run's session branch, standing, as a pass of the run writes it.</summary>
    private void Recorded() => new SessionBranches(_home).Record(
        new GrownBranch(RunHome.Repository, "default", Branch, "main", "base", null, Opened) { Unit = "unit-b1", Run = RunId });

    /// <summary>The run's quest, submitted at <see cref="NodeRunFixture.Tip1"/> and still taken: its work is done, its landing is what is left.</summary>
    private WorkUnit Submitted() => RunHome.QuestUnit(RunHome.Quest(RunId) with
    {
        Closure = "run:" + RunId, Submission = new QuestSubmissionView("m1", 1, _now, "Built.", Commit: Tip1),
    });

    private void BranchUnread()
    {
        _branchTips[Branch] = Tip1;
        _units = [Submitted()];
    }

    private void BranchRead()
    {
        _branchTips[Branch] = Tip1;
        _units = [Submitted(), RunHome.Branch("unit-b1", Branch, Opened, [RunId])];
    }

    [Fact]
    public async Task A_run_whose_branch_is_not_read_and_the_home_holds_no_record_of_ends_in_place()
    {
        BoundTo(Graph);
        BranchUnread();

        await PassAsync(Driving());

        Assert.Equal(InPlaceRunEnds.Why, Files.Read(RunUnit)!.Ended?.Why);
        Assert.DoesNotContain(_said, line => line.Code == UnlandedRunEnds.Event);
    }

    [Fact]
    public async Task A_run_whose_recorded_branch_holds_commits_the_line_lacks_is_never_ended_and_says_so_as_an_error_naming_them_once()
    {
        BoundTo(Graph);
        Recorded();
        BranchUnread();
        var runtime = Driving();

        for (var pass = 0; pass < 3; pass++) await PassAsync(runtime);

        Assert.Null(Files.Read(RunUnit)!.Ended);
        Assert.Empty(_finished);
        var line = Assert.Single(_said, each => each.Code == UnlandedRunEnds.Event);
        Assert.Contains($"`{Branch}`", line.Said, StringComparison.Ordinal);
        Assert.Contains("2 commits", line.Said, StringComparison.Ordinal);
        Assert.Contains("aaaaaaaa Mend the parser", line.Said, StringComparison.Ordinal);
        Assert.Contains("bbbbbbbb Test it", line.Said, StringComparison.Ordinal);
        Assert.Contains("its quest stays taken", line.Said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_run_whose_recorded_branch_git_cannot_read_is_held_too_and_says_so()
    {
        BoundTo(Graph);
        Recorded();
        BranchUnread();
        _lacks = null;

        await PassAsync(Driving());

        Assert.Null(Files.Read(RunUnit)!.Ended);
        Assert.Contains("git could not say", Assert.Single(_said, each => each.Code == UnlandedRunEnds.Event).Said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_run_whose_recorded_branch_the_line_holds_whole_ends_in_place_as_before()
    {
        BoundTo(Graph);
        Recorded();
        BranchUnread();
        _lacks = [];

        await PassAsync(Driving());

        Assert.Equal(InPlaceRunEnds.Why, Files.Read(RunUnit)!.Ended?.Why);
        Assert.DoesNotContain(_said, line => line.Code == UnlandedRunEnds.Event);
    }

    [Fact]
    public async Task A_run_whose_recorded_branch_is_gone_from_git_ends_in_place_as_before()
    {
        BoundTo(Graph);
        Recorded();
        _units = [Submitted()];

        await PassAsync(Driving());

        Assert.Equal(InPlaceRunEnds.Why, Files.Read(RunUnit)!.Ended?.Why);
    }

    [Fact]
    public async Task A_held_run_lands_at_the_first_look_that_reads_its_branch_again_with_its_quest_still_taken()
    {
        BoundTo(Graph);
        Recorded();
        BranchUnread();
        var runtime = Driving();
        await PassAsync(runtime);
        await PassAsync(runtime);
        Assert.Null(Files.Read(RunUnit)!.Ended);
        Assert.Empty(_door.Starts);

        // The session's rebase is done: the look reads its branch again.
        BranchRead();
        await PassAsync(runtime);
        await PassAsync(runtime);

        var record = Files.Read(RunUnit)!;
        Assert.Null(record.Ended);
        Assert.Empty(_finished);
        // The door is idempotent by node and tip, so each keeping of the landing offers the same one.
        var start = Assert.Single(_door.Starts.DistinctBy(each => each.Key));
        Assert.Equal(Tip1, start.Tip);
        Assert.Equal("handed", start.Node);
    }
}
