using System.Diagnostics;
using System.Reflection;
using Daoris.Driver;
using Xunit.Abstractions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// PERF1b: what the driver's reads cost on a home of a working machine's size (<see cref="BenchHome"/>: 3,000 ended records, two dozen
/// trees, one holding 12,000 ignored files), each against its budget. The service's own list read is timed apart and held to nothing
/// here, since what it costs is the service's (PERF1a); every budget below is the driver's own part on top of it.
/// </summary>
/// <remarks>
/// <para><b>The budgets.</b> A look runs every 15 seconds and a wake runs one at once, so a look that takes more than half a second is
/// felt as the window lagging the ledger, and work beside it that takes seconds holds the next look's slot: the look's passes together
/// are held under 500 ms past the service's reads. A review is a person waiting on a click: under a second however large the tree's
/// ignored build. The tidy and the nodes' read run beside every look: each under a second, so neither is still running at the next.</para>
///
/// <para>Each read runs once cold and is timed warm, the second time, as a running driver reads it at every look after its first. Its
/// claims are bounds of under a second, which a neighbour's load would break, so it runs alone.</para>
/// </remarks>
[Collection(Alone.Name)]
[Trait(Category.Name, Category.Process)]
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

    public async Task InitializeAsync() => _home = await BenchHome.MakeAsync(Records, TreeCount, Built);

    public async Task DisposeAsync()
    {
        if (_home is not null) await _home.DisposeAsync();
    }

    [Fact]
    public async Task A_home_of_working_size_reads_within_its_budgets()
    {
        var list = await TimedAsync(() => Home.Client.SessionList.ListedAsync(true, CancellationToken.None));
        var driver = new Daoris.Driver.Driver(Home.Client, DriverConfig.Empty with { Adapter = "stub" }, AdapterSet.Built(), Home.Home);
        await PassesAsync(driver);
        var passes = await PassesAsync(driver);
        var look = passes.Sum(each => each.Ms);

        var tidying = new BranchTidying(Home.Home);
        await tidying.LookAsync(Home.Client, null, CancellationToken.None);
        var tidy = await TimedAsync(() => tidying.LookAsync(Home.Client, null, CancellationToken.None));
        var nodes = await TimedAsync(() => WatchNodes.ReadNodeUnitsAsync(Home.Home, Home.Client, CancellationToken.None));

        var trees = new SessionTrees(Home.Home);
        await ReviewAsync(trees, Home.Large);
        var large = await TimedAsync(() => ReviewAsync(trees, Home.Large));
        var small = await TimedAsync(() => ReviewAsync(trees, Home.Trees[^1]));

        output.WriteLine($"the service's list of {Records} records: {list} ms (the service's own, PERF1a)");
        output.WriteLine($"look: {look} ms over {passes.Count} passes");
        foreach (var (name, ms) in passes.Where(each => each.Ms >= 5).OrderByDescending(each => each.Ms)) output.WriteLine($"  {name}: {ms} ms");
        output.WriteLine($"tidy: {tidy} ms; nodes' read: {nodes} ms");
        output.WriteLine($"review of the tree with {Built} ignored files: {large} ms; of a small tree: {small} ms");

        Assert.True(look - 4 * list <= LookBudget.TotalMilliseconds, $"the look took {look} ms past the service's reads, over its {LookBudget.TotalMilliseconds} ms");
        Assert.True(large <= ReviewBudget.TotalMilliseconds, $"the large tree's review took {large} ms, over its {ReviewBudget.TotalMilliseconds} ms");
        Assert.True(tidy <= BesideBudget.TotalMilliseconds, $"the tidy took {tidy} ms, over its {BesideBudget.TotalMilliseconds} ms");
        Assert.True(nodes <= BesideBudget.TotalMilliseconds, $"the nodes' read took {nodes} ms, over its {BesideBudget.TotalMilliseconds} ms");
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

    private static async Task<long> TimedAsync<T>(Func<Task<T>> read)
    {
        var watch = Stopwatch.StartNew();
        await read();
        return watch.ElapsedMilliseconds;
    }
}
