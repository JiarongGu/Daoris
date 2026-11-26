using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A run's <c>land</c> refused for a cause that passes (the repository's trees being cleaned up, another of Daoris's writes
/// holding it) keeps the yes and is tried again by itself once the cause clears, never asked of the person again; each wait has a deadline,
/// past which it climbs the reporting line instead of reading as working. A refusal that does not pass keeps waiting on a change or a press.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class LandRetryTests : GraphRunFixture
{
    private sealed class RecordingDoor : ILandingDoor
    {
        public List<LandStart> Starts { get; } = [];

        public bool Start(LandStart start)
        {
            Starts.Add(start);
            return true;
        }
    }

    private DateTimeOffset _now = new(2026, 10, 3, 8, 0, 0, TimeSpan.Zero);

    private LandPerformer Performer() => new(
        _home, _world, new SessionTrees(_home), new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)), _lines.Add, () => _now)
    {
        HoldsLease = () => true,
    };

    private async Task<(string Root, string Tree, LandStart Start)> StartedAsync()
    {
        var (root, tree, branch) = await BoundRunAsync(BranchLand);
        var door = new RecordingDoor();
        await PassAsync(root, branch, tree, door: door);
        return (root, tree, door.Starts[^1]);
    }

    [Fact]
    public async Task A_landing_refused_while_the_trees_are_tidied_lands_once_the_tidy_ends_with_no_second_accept()
    {
        var (root, tree, start) = await StartedAsync();

        using (TreeLock.TryReplaying(_home, root, "engine", TreeLock.Tidying))
        {
            await Performer().PerformAsync(start);
        }

        Assert.Empty(await FeatureBranchesAsync(root));
        Assert.Equal(LandingCode.Busy, CodeOf(_lines.Last(line => line.Event == "landing.auto")));
        Assert.Equal(LandingCode.Busy, LandOutcomes.Of(start.Unit, start.Tip)?.Refusal);
        Assert.Contains("q1", LandStandings.Owed(_home, _now));

        // The tidy ended: the next look lands it, nothing pressed between.
        await Performer().PerformAsync(start);

        Assert.Equal(await HeadAsync(tree), (await GitAsync(root, "rev-parse", PushBranch)).Trim());
        Assert.Equal(LandingCode.Landed, CodeOf(_lines.Last(line => line.Event == "landing.auto")));
        Assert.Null(LandRetries.Waiting(start.Unit, start.Tip));
        Assert.DoesNotContain("q1", LandStandings.Owed(_home, _now));
    }

    [Fact]
    public async Task While_the_tidy_still_holds_the_repository_the_look_waits_without_trying_and_says_it_once()
    {
        var (root, _, start) = await StartedAsync();

        using (TreeLock.TryReplaying(_home, root, "engine", TreeLock.Tidying))
        {
            await Performer().PerformAsync(start);
            await Performer().PerformAsync(start);
            await Performer().PerformAsync(start);
        }

        Assert.Single(_lines, line => line.Event == "landing.auto");
        Assert.Single(Notes("s1"), note => note.Text!.Contains("tried again by itself", StringComparison.Ordinal));
        Assert.Equal(LandRetries.Busy, LandRetries.Waiting(start.Unit, start.Tip)?.Code);
    }

    [Fact]
    public async Task The_looks_tidy_does_not_start_on_a_repository_whose_landing_is_due()
    {
        var (root, _, start) = await StartedAsync();
        using (TreeLock.TryReplaying(_home, root, "engine", TreeLock.Tidying))
        {
            await Performer().PerformAsync(start);
        }

        var pass = await new SessionTrees(_home) { Clock = () => _now }.TidyEmptyAsync(root, "engine", null, _ => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>()));

        Assert.Equal(TidyKept.Landing, pass.Held);
        Assert.True(LandRetries.Due(_home, "engine", _now));
        Assert.False(LandRetries.Due(_home, "engine", _now + LandRetries.Fresh));
    }

    [Fact]
    public async Task Past_its_deadline_the_wait_climbs_the_reporting_line_and_never_reads_as_working()
    {
        var (root, tree, start) = await StartedAsync();

        using (TreeLock.TryReplaying(_home, root, "engine", TreeLock.Tidying))
        {
            await Performer().PerformAsync(start);
            _now += LandRetries.Deadline + TimeSpan.FromMinutes(1);
            await Performer().PerformAsync(start);
        }

        var waiting = LandRetries.Waiting(start.Unit, start.Tip);
        Assert.Equal(LandRetries.Climbed, waiting?.Code);
        Assert.StartsWith("waiting on ", waiting!.Line, StringComparison.Ordinal);
        Assert.Contains(_lines, line => line.Event == LandWaits.Event && (string?)Value(line, "reason") == LandWaits.Climbed);
        Assert.Single(Notes("s1"), note => note.Text!.Contains("past the landing's deadline", StringComparison.Ordinal));

        // The cause cleared after the climb: it still lands by itself, once.
        await Performer().PerformAsync(start);
        await Performer().PerformAsync(start);
        Assert.Equal(await HeadAsync(tree), (await GitAsync(root, "rev-parse", PushBranch)).Trim());
        Assert.Single(_lines, line => line.Event == "landing.auto" && CodeOf(line) == LandingCode.Landed);
    }

    /// <summary>
    /// A landing refused because its delivery is held here (being landed or settled by another of Daoris's doors) holds no tree lock a look
    /// reads, so each look tries it again: past its deadline the refusal itself climbs the reporting line, once, as every busy cause does.
    /// </summary>
    [Fact]
    public async Task A_landing_whose_delivery_is_held_past_its_deadline_climbs_the_reporting_line()
    {
        var (root, tree, start) = await StartedAsync();
        var held = DeliveryRuns.Hold(_home, DeliveryStore.KeyOf(Work.Session, start.Tip, DeliveryMethods.Branch, "main"));
        Assert.NotNull(held);

        // A home with no lock folder holds no lock: a look tries the landing at once.
        Assert.Null(LandRetries.StillHeld(_home, tree));
        using (held)
        {
            await Performer().PerformAsync(start);
            Assert.Equal(LandingCode.Busy, CodeOf(_lines.Last(line => line.Event == "landing.auto")));
            Assert.Equal(LandRetries.Busy, LandRetries.Waiting(start.Unit, start.Tip)?.Code);
            _now += LandRetries.Deadline + TimeSpan.FromMinutes(1);
            await Performer().PerformAsync(start);
            await Performer().PerformAsync(start);
        }

        var waiting = LandRetries.Waiting(start.Unit, start.Tip);
        Assert.Equal(LandRetries.Climbed, waiting?.Code);
        Assert.StartsWith("waiting on ", waiting!.Line, StringComparison.Ordinal);
        Assert.Single(Notes("s1"), note => note.Text!.Contains("past the landing's deadline", StringComparison.Ordinal));

        // The delivery let go: it lands by itself, once.
        await Performer().PerformAsync(start);
        Assert.Equal(await HeadAsync(tree), (await GitAsync(root, "rev-parse", PushBranch)).Trim());
        Assert.Single(_lines, line => line.Event == "landing.auto" && CodeOf(line) == LandingCode.Landed);
    }
}
