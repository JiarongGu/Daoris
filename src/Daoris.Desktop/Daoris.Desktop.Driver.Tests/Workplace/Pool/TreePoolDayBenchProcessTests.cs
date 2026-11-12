using Xunit.Abstractions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The tree pool design's measurements over a day of a repository's main (<see cref="TreePoolDayBench"/>), only where <c>TREEPOOL_BENCH</c>
/// names a scratch folder under <c>_fixtures/treepool-bench/</c> (removed at the end, its lines in <c>&lt;name&gt;.log</c>). <c>_SHAPE</c> is
/// <c>daoris</c> (the day up to <c>_TIP</c>, main by default) or <c>web</c>; <c>_ARM</c> <c>base</c>, <c>aside</c> or <c>fresh</c>;
/// <c>_STRIDE</c> 13 by default; <c>_BUILDS=0</c> builds nothing. No <c>DAORIS_</c> prefix, which the host strips. It runs alone, timed.
/// </summary>
[Collection(Alone.Name)]
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
public sealed class TreePoolDayBenchProcessTests(ITestOutputHelper output)
{
    [Fact]
    public async Task A_day_of_main_replayed_through_one_kept_slot_is_measured()
    {
        if (Environment.GetEnvironmentVariable("TREEPOOL_BENCH") is not { Length: > 0 } name) return;
        var stride = int.TryParse(Environment.GetEnvironmentVariable("TREEPOOL_BENCH_STRIDE"), out var each) && each > 0 ? each : 13;
        var arm = Environment.GetEnvironmentVariable("TREEPOOL_BENCH_ARM") is { Length: > 0 } named ? named : "base";
        if (arm is not ("base" or "aside" or "fresh")) throw new ArgumentException($"TREEPOOL_BENCH_ARM is base, aside or fresh, not {arm}");
        var bench = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "treepool-bench");
        var scratch = Path.Combine(bench, name);
        Directory.CreateDirectory(scratch);
        var run = new BenchRuns(Path.Combine(bench, name + ".log"), output.WriteLine);
        var shape = Environment.GetEnvironmentVariable("TREEPOOL_BENCH_SHAPE") == "web"
            ? await BenchShapes.WebAsync(scratch, WorkspaceRoot.Folder, run)
            : await BenchShapes.DaorisAsync(scratch, WorkspaceRoot.Folder, Environment.GetEnvironmentVariable("TREEPOOL_BENCH_TIP") is { Length: > 0 } tip ? tip : "main", run);

        await new TreePoolDayBench(scratch, shape, arm, stride, Environment.GetEnvironmentVariable("TREEPOOL_BENCH_BUILDS") != "0", run).RunAsync();
    }
}
