using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// 🔴 Every git ask goes through one service that gives it an id, its repository, its read kind, its asker, its class and its deadline,
/// answers it by that id or fails it with why, and counts it by caller and read kind, wrapping the scheduler without a change of order or places.
/// </summary>
public sealed class GitServiceTests
{
    private static readonly TimeSpan Bound = Poll.Patience;
    private static readonly TimeSpan Soon = Poll.Patience;

    private static GitRunAsk Ask(string repository, params string[] arguments) =>
        GitRunAsk.Plain(Path.GetFullPath(Path.Combine("_service", repository)), GitWait.Read, GitReadKinds.Of(arguments));

    private static (GitService Service, GitScheduler Scheduler, GitAskTally Tally) Fixture(int places = 4, TimeSpan? allowance = null)
    {
        var tally = new GitAskTally();
        var scheduler = new GitScheduler(places, places, places) { Said = (_, _) => { }, Tally = tally };
        return (new GitService(scheduler) { Allowance = _ => allowance ?? TimeSpan.FromMinutes(5) }, scheduler, tally);
    }

    [Fact]
    public async Task An_ask_past_its_deadline_is_answered_as_failed_naming_its_id_kind_asker_and_class()
    {
        var (service, _, _) = Fixture(places: 1, allowance: TimeSpan.FromMilliseconds(500));
        var hold = new TaskCompletionSource<int>();
        var placed = new TaskCompletionSource();
        var holding = service.PlaceAsync(Ask("a", "status"), Bound, _ =>
        {
            placed.SetResult();
            return hold.Task;
        }, CancellationToken.None);
        await placed.Task.WaitAsync(Soon);

        GitUnplacedException late;
        using (GitAskers.Is(GitAskers.Facts))
        {
            late = await Assert.ThrowsAsync<GitUnplacedException>(() => service.PlaceAsync(Ask("b", "diff", "--stat"), Bound, _ => Task.FromResult(2), CancellationToken.None)).WaitAsync(Soon);
        }

        Assert.Equal("deadline", late.Word);
        Assert.Contains("diff", late.Message, StringComparison.Ordinal);
        Assert.Contains(GitAskers.Facts, late.Message, StringComparison.Ordinal);
        Assert.Contains("background", late.Message, StringComparison.Ordinal);
        Assert.Matches(@"ask #\d+", late.Message);

        hold.SetResult(1);
        Assert.Equal(1, await holding.WaitAsync(Soon));
        Assert.Empty(service.Open);
    }

    [Fact]
    public async Task An_open_ask_carries_its_id_repository_kind_asker_class_and_a_deadline_no_shorter_than_its_bound()
    {
        var (service, _, _) = Fixture();
        var hold = new TaskCompletionSource<int>();
        Task<int> asking;
        using (GitAskers.Is(GitAskers.Gate)) asking = service.PlaceAsync(Ask("a", "-c", "core.x=1", "merge-tree", "--write-tree", "a", "b"), Bound, _ => hold.Task, CancellationToken.None);

        var open = Assert.Single(service.Open);
        Assert.True(open.Id > 0);
        Assert.Equal("merge-tree", open.Kind);
        Assert.Equal(GitAskers.Gate, open.Asker);
        Assert.Equal(GitAskClass.Step, open.Class);
        Assert.True(open.Deadline >= Bound);
        Assert.EndsWith("a", open.Repository, StringComparison.OrdinalIgnoreCase);

        hold.SetResult(3);
        Assert.Equal(3, await asking.WaitAsync(Soon));
        Assert.Empty(service.Open);
    }

    [Theory]
    [InlineData(GitAskers.Steps, false, "Step")]
    [InlineData(GitAskers.Gate, false, "Step")]
    [InlineData(GitAskers.Check, false, "Step")]
    [InlineData(GitAskers.Look, false, "Background")]
    [InlineData(GitAskers.Facts, false, "Background")]
    [InlineData(GitAskers.Other, false, "Background")]
    [InlineData(GitAskers.Door, true, "Person")]
    [InlineData(GitAskers.Gate, true, "Person")]
    public void A_class_is_the_persons_where_the_flow_is_a_persons_else_a_steps_asker_or_the_backgrounds(string asker, bool person, string expected) =>
        Assert.Equal(expected, GitService.ClassOf(asker, person).ToString());

    [Theory]
    [InlineData("status", "--porcelain=v2", "status")]
    [InlineData("-c", "core.quotepath=off", "-C", "x", "diff", "--no-ext-diff", "diff")]
    [InlineData("merge-tree", "--write-tree", "merge-tree")]
    [InlineData("log", "--cherry-mark", "--left-right", "cherry")]
    [InlineData("log", "-n", "5", "log")]
    [InlineData("rev-list", "--count", "x..y", "rev-list")]
    [InlineData("cat-file", "--batch", "cat-file")]
    [InlineData("--version", "meta")]
    public void A_read_kind_is_the_git_verb_with_a_cherry_walk_told_apart(params string[] words) =>
        Assert.Equal(words[^1], GitReadKinds.Of(words[..^1]));

    [Fact]
    public async Task Each_ask_is_counted_by_its_caller_and_read_kind_with_the_same_per_caller_counts_as_the_scheduler_alone()
    {
        var through = Fixture();
        var alone = Fixture();
        string[][] reads = [["status"], ["status"], ["diff", "--no-ext-diff"], ["ls-tree", "x"], ["status"]];
        string[] callers = [GitAskers.Look, GitAskers.Look, GitAskers.Look, GitAskers.Gate, GitAskers.Door];

        for (var at = 0; at < reads.Length; at++)
        {
            using (GitAskers.Is(callers[at]))
            {
                await through.Service.PlaceAsync(Ask($"r{at}", reads[at]), Bound, _ => Task.FromResult(at), CancellationToken.None);
                await alone.Scheduler.RunAsync(Ask($"r{at}", reads[at]), _ => Task.FromResult(at), CancellationToken.None);
            }
        }

        var counted = through.Tally.Take();
        Assert.Equal(
            new Dictionary<string, long> { [$"{GitAskers.Look}/status"] = 2, [$"{GitAskers.Door}/status"] = 1, [$"{GitAskers.Gate}/ls-tree"] = 1, [$"{GitAskers.Look}/diff"] = 1 },
            counted.ToDictionary(row => $"{row.Caller}/{row.Kind}", row => row.Count));
        Assert.Equal(
            alone.Tally.Take().GroupBy(row => row.Caller).ToDictionary(group => group.Key, group => group.Sum(row => row.Count)),
            counted.GroupBy(row => row.Caller).ToDictionary(group => group.Key, group => group.Sum(row => row.Count)));
    }

    [Fact]
    public void A_look_line_names_each_callers_count_and_top_kinds_and_the_bus_is_told_once_for_each_caller_and_kind()
    {
        var tally = new GitAskTally();
        var told = new List<(string Caller, string Kind, long Count, long Waited)>();
        var stages = new LookStages { Asks = tally, Asked = (caller, kind, count, waited) => told.Add((caller, kind, count, waited)) };
        tally.Record(GitAskers.Look, "status", 300);
        tally.Record(GitAskers.Look, "status", 40);
        tally.Record(GitAskers.Look, "diff", 5);
        tally.Record(GitAskers.Look, "ls-tree", 1);
        tally.Record(GitAskers.Look, "grep", 1);
        tally.Record(GitAskers.Door, "log", 5);

        Assert.Equal("git=look:5/347ms[status:2,diff:1,grep:1],door:1/5ms[log:1]", stages.Take());
        Assert.Equal(5, told.Count);
        Assert.Contains((GitAskers.Look, "status", 2L, 340L), told);
        Assert.Null(stages.Take());
    }

    [Fact]
    public async Task An_ask_no_caller_named_is_counted_as_other_with_the_site_that_asked()
    {
        var (service, _, tally) = Fixture();

        await service.PlaceAsync(Ask("a", "status"), Bound, _ => Task.FromResult(1), CancellationToken.None);

        Assert.Equal(GitAskers.Other, Assert.Single(tally.Take()).Caller);
        var site = Assert.Single(tally.TakeSites());
        Assert.Contains(nameof(An_ask_no_caller_named_is_counted_as_other_with_the_site_that_asked), site.Site, StringComparison.Ordinal);
        Assert.Equal(1, site.Count);
    }
}
