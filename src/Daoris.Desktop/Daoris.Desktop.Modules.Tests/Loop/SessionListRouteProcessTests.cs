using System.Diagnostics;
using Daoris.Desktop.Driver.Tests;
using Daoris.Driver;
using Xunit.Abstractions;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// <c>SESSION_GROUPS</c>, which the window's Sessions list and a terminal's <c>daoris session list</c> ask of a running Daoris, answers
/// within <see cref="Budget"/> on a home shaped like an installed machine's (<see cref="SessionListHome"/>): its look never waits on a
/// git walk of the trees, which the facts' parts judge apart from it. Each stage is said in the output as the machine log's
/// <c>session.list</c> line says it.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
public sealed class SessionListRouteProcessTests(ITestOutputHelper output) : DriverModuleBridge, IAsyncLifetime
{
    /// <summary>What the person waits on the list for, at most: the window's first screen.</summary>
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(3);

    private SessionListHome? _home;

    private BenchHome Bench => _home!.Bench;

    public async Task InitializeAsync() => _home = await SessionListHome.MakeAsync(1250, 4);

    public async Task DisposeAsync()
    {
        if (_home is not null) await _home.DisposeAsync();
    }

    [Fact]
    public async Task The_list_a_running_Daoris_answers_waits_on_no_tree_s_walk()
    {
        Redirect("DAORIS_HOME", Bench.Home);
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        await loop.ComeUpAsync(Bench.Client);
        await loop.Settling;

        var times = new LookTimes();
        var watch = Stopwatch.StartNew();
        await GroupsStateSlice.LookAsync(loop, Bench.Client, CancellationToken.None, times: times);
        output.WriteLine($"the route's look: {watch.ElapsedMilliseconds} ms; "
            + string.Join(", ", times.Steps.Select(step => $"{step.Step} {step.Ms} ms")) + $"; trees {times.Trees}");

        watch.Restart();
        var answered = await AskAsync(module, "SESSION_GROUPS", new { said = true });
        watch.Stop();
        output.WriteLine($"SESSION_GROUPS, said: {watch.ElapsedMilliseconds} ms");

        Assert.True(answered.Success, answered.Error?.Message);
        Assert.True(watch.Elapsed < Budget, $"the list took {watch.ElapsedMilliseconds} ms, past {Budget.TotalMilliseconds} ms");
    }
}
