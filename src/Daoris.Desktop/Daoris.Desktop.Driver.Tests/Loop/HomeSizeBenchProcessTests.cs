using System.Diagnostics;
using System.Reflection;
using Daoris.Driver;
using Xunit.Abstractions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What the driver's reads cost on a home of a working machine's size (<see cref="BenchHome"/>: 3,000 ended records, two dozen
/// trees, one holding 12,000 ignored files), each against its budget. The service's own list read is timed apart and held to nothing
/// here, since what it costs is the service's; every budget below is the driver's own part on top of it.
/// </summary>
/// <remarks>
/// <para><b>The budgets.</b> A look runs every 15 seconds and a wake runs one at once, so a look that takes more than half a second is
/// felt as the window lagging the ledger, and work beside it that takes seconds holds the next look's slot: the look's passes together
/// are held under 500 ms past the service's reads. The nodes' read runs beside every look: under a second past its list, so it is not
/// still running at the next. A review and the help room's look are a person waiting: a tree's ignored build adds under a second to
/// either, so a tree of gigabytes of build reads as a tracked tree does. The review and the tidy are each tens of git starts at a third
/// of a second apiece on a loaded machine; their times are said, not held, and the git starts and the requests for session records of the look,
/// the tidy, the nodes' read and a tree's walk are held.</para>
///
/// <para>Each read runs once cold and is timed warm, the second time, as a running driver reads it at every look after its first. Its
/// claims are bounds of under a second, which a neighbour's load would break, so it runs alone.</para>
/// </remarks>
[Collection(Alone.Name)]
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
public sealed class HomeSizeBenchProcessTests(ITestOutputHelper output) : IAsyncLifetime
{
    private const int Records = 3000;
    private const int TreeCount = 24;
    private const int Built = 12000;

    private static readonly TimeSpan LookBudget = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan ReviewBudget = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan BesideBudget = TimeSpan.FromSeconds(1);

    private BenchHome? _home;

    private BenchHome Home => _home!;

    private long _made;

    // Each read's git starts and the requests it made of the service, by the read's name.
    private readonly Dictionary<string, (int Starts, IReadOnlyDictionary<string, int> Asked)> _taken = new(StringComparer.Ordinal);

    public async Task InitializeAsync()
    {
        var watch = Stopwatch.StartNew();
        _home = await BenchHome.MakeAsync(Records, TreeCount, Built);
        _made = watch.ElapsedMilliseconds;
    }

    public async Task DisposeAsync()
    {
        if (_home is not null) await _home.DisposeAsync();
    }

    [Fact]
    public async Task A_home_of_working_size_reads_within_its_budgets()
    {
        output.WriteLine($"the home made in {_made} ms");
        var list = await MeasuredAsync("the service's list of every record (the service's own)", () => Home.Client.SessionList.ListedAsync(true, CancellationToken.None));
        var driver = new Daoris.Driver.Driver(Home.Client, DriverConfig.Empty with { Actors = StaffedRoles.On("stub") }, ProviderRegistry.Built(), Home.Home);
        await PassesAsync(driver);
        var look = await MeasuredAsync("look", async () =>
        {
            var passes = await PassesAsync(driver);
            foreach (var (name, ms) in passes.Where(each => each.Ms >= 5).OrderByDescending(each => each.Ms)) output.WriteLine($"  {name}: {ms} ms");
            return passes;
        });

        var tidying = new BranchTidying(Home.Home);
        await tidying.LookAsync(Home.Client, null, CancellationToken.None);
        var tidy = await MeasuredAsync("tidy", () => tidying.LookAsync(Home.Client, null, CancellationToken.None));
        var nodes = await MeasuredAsync("nodes' read", () => WatchNodes.ReadNodeUnitsAsync(Home.Home, Home.Client, CancellationToken.None));

        var trees = new SessionTrees(Home.Home);
        // The help room's walk of a tree, first as a tree that moved is walked, then as its kept read answers.
        await MeasuredAsync("the large tree's walk", () => TreeReadMemo.Of(Home.Home).Begin(trees).WorkAsync(Home.Large.Tree, CancellationToken.None));
        var kept = await MeasuredAsync("its kept read", () => TreeReadMemo.Of(Home.Home).Begin(trees).WorkAsync(Home.Large.Tree, CancellationToken.None));
        await ReviewAsync(trees, Home.Large);
        var large = await MeasuredAsync($"the review of the tree with {Built} ignored files", () => ReviewAsync(trees, Home.Large));
        var small = await MeasuredAsync("a small tree's review", () => ReviewAsync(trees, Home.Trees[^1]));

        // A start's own parts before its spawn that do not wait on a model: its place's manifest, its tree grown, its record opened.
        await MeasuredAsync("a turn's end in place", async () =>
        {
            await PlaceTurns.StartAsync(Home.Home, "default", BenchHome.Repository, Home.Root, "s-bench001", null, CancellationToken.None);
            return await PlaceTurns.EndAsync(Home.Home, "s-bench001", CancellationToken.None);
        });
        var manifest = await MeasuredAsync("a start's manifest", () =>
            PlaceTurns.StartAsync(Home.Home, "default", BenchHome.Repository, Home.Root, "s-bench002", "s-bench001", CancellationToken.None));
        await MeasuredAsync("a start's tree", () => trees.OpenAsync(Home.Root, BenchHome.Repository, "default"));
        await MeasuredAsync("a start's record", () => Home.Client.OpenChatAsync(BenchHome.Repository, "stub"));

        // The desktop's start waits on the host's answer before its window shows the page's data.
        output.WriteLine($"the service's host answering again over the store: {await Home.HostAgainAsync()} ms");

        // Each read that reads the session list once is allowed that read: the rest is the driver's own.
        Within("the look", look - list, LookBudget);
        Within("a start's manifest", manifest, BesideBudget);
        Within("the nodes' read", nodes - list, BesideBudget);
        // A tree's ignored build costs a read nothing: its kept read is its one status, and its review a tracked tree's.
        Within("the large tree's kept read", kept, ReviewBudget);
        Within("the large tree's review past a small tree's", large - small, ReviewBudget);
        output.WriteLine($"not yet held: the tidy {tidy} ms, past its {BesideBudget.TotalMilliseconds} ms beside a look");

        // What each read asks of git and of the service's session records, held at the size measured: the look asks the
        // service for the session records once (its change count, then the list), and git nothing; the tidy of two dozen trees asks git
        // six times, not once per tree; the nodes' read one worktree list; a tree's walk, the offer's judgement, the starts it made.
        StartsWithin("look", 0);
        SessionReadsWithin("look", 2);
        // Of those, every record once and only as its change, and the open ones once.
        Assert.True(BenchDoors.ListReads(_taken["look"].Asked) <= 1, $"the look read every record {BenchDoors.ListReads(_taken["look"].Asked)} times");
        Assert.Equal(0, _taken["look"].Asked.GetValueOrDefault(BenchDoors.WholeList));
        Assert.True(_taken["look"].Asked.GetValueOrDefault(BenchDoors.Open) <= 1, "the look read the open records more than once");
        StartsWithin("tidy", 8);
        SessionReadsWithin("tidy", 0);
        StartsWithin("nodes' read", 2);
        SessionReadsWithin("nodes' read", 2);
        StartsWithin("the large tree's walk", 6);
        StartsWithin("its kept read", 1);
    }

    private void StartsWithin(string what, int budget) =>
        Assert.True(_taken[what].Starts <= budget, $"{what} made {_taken[what].Starts} git starts, over its {budget}");

    /// <summary>The requests a read made of the service's session records: the list, one record, the changes since a count.</summary>
    private void SessionReadsWithin(string what, int budget)
    {
        var asked = _taken[what].Asked.Where(each => each.Key.StartsWith("/api/sessions", StringComparison.Ordinal)).Sum(each => each.Value);
        Assert.True(asked <= budget, $"{what} asked the service for session records {asked} times, over its {budget}");
    }

    private static void Within(string what, long ms, TimeSpan budget) =>
        Assert.True(ms <= budget.TotalMilliseconds, $"{what} took {ms} ms of its own, over its {budget.TotalMilliseconds} ms");

    /// <summary>A read timed, with what it asked of the service and of git said beside it.</summary>
    private async Task<long> MeasuredAsync<T>(string what, Func<Task<T>> read)
    {
        Home.Doors.Take();
        using var starts = GitLaunch.Count();
        var watch = Stopwatch.StartNew();
        await read();
        var ms = watch.ElapsedMilliseconds;
        var requests = Home.Doors.Take();
        _taken[what] = (starts.Starts.Count, Home.Doors.LastTaken);
        var git = starts.Starts.OrderByDescending(start => start.WholeTicks).Take(3)
            .Select(start => $"git {string.Join(' ', start.Arguments.Where(argument => !argument.StartsWith("--no-optional", StringComparison.Ordinal)).Take(3))} {(long)GitLaunched.Ms(start.WholeTicks)} ms");
        output.WriteLine($"{what}: {ms} ms; asked {requests}; {starts.Starts.Count} git starts, the longest {string.Join(", ", git)}");
        // Each command asked more than once, by its first words, so a read's starts say which fact it asked again.
        foreach (var asked in starts.Starts.GroupBy(start => string.Join(' ', start.Arguments.Where(argument => !argument.StartsWith("--no-optional", StringComparison.Ordinal)).Take(2)))
                     .Where(group => group.Count() > 1).OrderByDescending(group => group.Count()))
        {
            output.WriteLine($"  git {asked.Key}: {asked.Count()}");
        }

        return ms;
    }

    /// <summary>One look's passes in the driver's order, each timed by its name.</summary>
    private static async Task<List<(string Name, long Ms)>> PassesAsync(Daoris.Driver.Driver driver)
    {
        var round = new LookRound(driver.Part, [], new LookState([]));
        var times = new List<(string, long)>();
        foreach (var phase in new[] { LookPhase.Before, LookPhase.Local, LookPhase.Read, LookPhase.Planned, LookPhase.Begun, LookPhase.After })
        {
            foreach (var pass in driver.LookPasses.In(phase))
            {
                var watch = Stopwatch.StartNew();
                await pass.RunAsync(round, CancellationToken.None);
                times.Add((pass.GetType().GetCustomAttribute<LookPassAttribute>()!.Name, watch.ElapsedMilliseconds));
            }
        }

        return times;
    }

    private Task<SessionReview?> ReviewAsync(SessionTrees trees, (string Session, string Tree, string BaseCommit) of) =>
        SessionReviews.ReadAsync(of.Session, of.Tree, of.BaseCommit, trees, (_, _) => Task.FromResult<string?>(Home.Root), null, CancellationToken.None);
}
