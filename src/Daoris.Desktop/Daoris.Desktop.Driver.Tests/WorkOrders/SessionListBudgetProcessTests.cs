using System.Diagnostics;
using Daoris.Driver;
using Xunit.Abstractions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris session list</c> with no Daoris running, on a home shaped like an installed machine's (<see cref="SessionListHome"/>: 1,250
/// ended records, each with its events file, some two hundred megabytes in all, three hundred archived, eight trees, one of them holding a
/// large ignored build), answers within <see cref="Budget"/>, its lines and its <c>--json</c> alike: no row of it needs the person, so it
/// walks no tree (<see cref="ListedTrees"/>; eight walks held it 50 s before). Each stage is said in the output as the machine log's
/// <c>session.list</c> line says it; the large tree's walk and the service's host started again over the home's store are said beside
/// it, the start's other costs.
/// </summary>
/// <remarks>
/// Alone, after the rest (<see cref="Alone"/>): its claim is a bound of a few seconds on a real service's answer, which the load of the
/// classes beside it could break.
/// </remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Collection(Alone.Name)]
public sealed class SessionListBudgetProcessTests(ITestOutputHelper output) : IAsyncLifetime
{
    /// <summary>What the person waits on the list for, at most, its walks included.</summary>
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(2);

    private SessionListHome? _home;

    private SessionListHome Home => _home!;

    public async Task InitializeAsync()
    {
        var watch = Stopwatch.StartNew();
        _home = await SessionListHome.MakeAsync(1250, 8, archived: 300, built: 30000);
        output.WriteLine($"the home: {Home.Bytes / (1024 * 1024)} MB of events, made in {watch.ElapsedMilliseconds} ms");
    }

    public async Task DisposeAsync()
    {
        if (_home is not null) await _home.DisposeAsync();
    }

    [Fact]
    public async Task The_list_of_a_machine_s_sessions_answers_within_its_budget()
    {
        foreach (var json in new[] { false, true })
        {
            var world = new SessionsWorld(Home.Bench.Client, Home.Bench.Home, DriverConfig.Empty, false, null);
            var times = new LookTimes();
            var lines = new StringWriter();

            var watch = Stopwatch.StartNew();
            var exit = await SessionsListing.ListAsync(world, new SessionsAsk("list") { Json = json }, lines, CancellationToken.None, times);
            watch.Stop();

            output.WriteLine($"session list{(json ? " --json" : "")}: {watch.ElapsedMilliseconds} ms; "
                + string.Join(", ", times.Steps.Select(step => $"{step.Step} {step.Ms} ms")) + $"; trees {times.Trees}");
            Assert.Equal(0, exit);
            Assert.Contains(json ? "\"entries\"" : "Done (", lines.ToString(), StringComparison.Ordinal);
            Assert.Equal(0, times.Trees);
            Assert.True(watch.Elapsed < Budget, $"the list took {watch.ElapsedMilliseconds} ms, past {Budget.TotalMilliseconds} ms");
        }

        var walk = Stopwatch.StartNew();
        var work = await new SessionTrees(Home.Bench.Home).WorkAsync(Home.Bench.Large.Tree);
        output.WriteLine($"the large tree's walk ({Home.Bench.Built} built files): {walk.ElapsedMilliseconds} ms; {work}");
        output.WriteLine($"the service's host started again: {await Home.Bench.HostAgainAsync()} ms");
    }
}
