using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A run's <c>land</c>, active while the run's own session waits on its next step in the tree (its turn ended, <c>awaiting-step</c>), lands at
/// the next look: that session waits on the landing, so it never stands for a session using the tree. A different session working in the tree
/// still holds it, and the landing says so in the machine log once, by its code.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class AwaitingStepLandingTests : GraphRunFixture
{
    /// <summary>A door that keeps what the pass starts and lands nothing.</summary>
    private sealed class RecordingDoor : ILandingDoor
    {
        public List<LandStart> Starts { get; } = [];

        public bool Start(LandStart start)
        {
            Starts.Add(start);
            return true;
        }
    }

    private LandPerformer Performer() => new(
        _home, _world, new SessionTrees(_home), new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)), _lines.Add) { HoldsLease = () => true };

    /// <summary>Run q1 bound to a branch landing, its pass's start of the <c>land</c>, and its session s1 then waiting on the run's next step.</summary>
    private async Task<(string Root, string Tree, LandStart Start)> WaitingAsync()
    {
        var (root, tree, branch) = await BoundRunAsync(BranchLand);
        var door = new RecordingDoor();
        await PassAsync(root, branch, tree, door: door);
        _world.Records[0] = _world.Records[0] with { State = SessionStates.AwaitingStep };
        return (root, tree, door.Starts[^1]);
    }

    [Fact]
    public async Task A_land_whose_only_session_on_the_tree_is_the_runs_own_waiting_part_lands()
    {
        var (root, tree, start) = await WaitingAsync();

        await Performer().PerformAsync(start);

        Assert.Equal(await HeadAsync(tree), (await GitAsync(root, "rev-parse", PushBranch)).Trim());
        Assert.Contains(_lines, line => line.Event == "landing.auto" && CodeOf(line) == LandingCode.Landed);
        Assert.DoesNotContain(_lines, line => line.Event == LandWaits.Event);
    }

    [Fact]
    public async Task Another_session_working_in_the_tree_still_holds_it_and_is_said_once()
    {
        var (root, tree, start) = await WaitingAsync();
        _world.Records.Add(new SessionRecord("s9", "engine", "working") { Quest = "q9", Tree = tree, Created = DateTimeOffset.UtcNow });

        Assert.Null(await Performer().PerformAsync(start));
        Assert.Null(await Performer().PerformAsync(start));

        Assert.Empty(await FeatureBranchesAsync(root));
        var waits = Assert.Single(_lines, line => line.Event == LandWaits.Event);
        Assert.Equal(LandWaits.InUse, Value(waits, "reason"));
    }
}
