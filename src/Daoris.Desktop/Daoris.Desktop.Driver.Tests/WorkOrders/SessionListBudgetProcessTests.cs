using System.Diagnostics;
using Daoris.Driver;
using Xunit.Abstractions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris session list</c> with no Daoris running, on a home shaped like an installed machine's (<see cref="SessionListHome"/>: 1,250
/// ended records, each with its events file, some two hundred megabytes in all), answers within <see cref="Budget"/> besides the git
/// walks of its own trees, its lines and its <c>--json</c> alike, each stage said in the output as the machine log's
/// <c>session.list</c> line says it. The walks are said, not held: a terminal alone has no facts to read them from.
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
    /// <summary>What the person waits on the list for, at most: the window's first screen.</summary>
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(3);

    private SessionListHome? _home;

    private SessionListHome Home => _home!;

    public async Task InitializeAsync()
    {
        var watch = Stopwatch.StartNew();
        _home = await SessionListHome.MakeAsync(1250, 4);
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
            // Its own trees' git walks aside, which a terminal with no Daoris running makes itself and the window's facts make apart
            // from the list (SessionListRouteProcessTests): every record's events and the grouping are the budget's.
            var walks = times.Steps.Where(step => step.Step == "trees").Sum(step => step.Ms);
            Assert.True(watch.ElapsedMilliseconds - walks < Budget.TotalMilliseconds,
                $"the list took {watch.ElapsedMilliseconds - walks} ms besides its trees' walks, past {Budget.TotalMilliseconds} ms");
        }
    }
}
