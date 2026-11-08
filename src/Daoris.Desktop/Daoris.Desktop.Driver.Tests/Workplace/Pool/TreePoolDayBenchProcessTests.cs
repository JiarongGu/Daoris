using Xunit.Abstractions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The measurements TREEPOOL1's row b asks of the built pool, taken on a working machine over a day of a repository's main
/// (<see cref="TreePoolDayBench"/>). It does nothing unless <c>TREEPOOL_BENCH</c> names a scratch folder under
/// <c>_fixtures/treepool-bench/</c> holding <c>day.txt</c>, the day's commits newest first; <c>TREEPOOL_BENCH_STRIDE</c> and
/// <c>TREEPOOL_BENCH_BUILDS</c> set the replay's step and whether the builds are timed. The names carry no <c>DAORIS_</c> prefix, which the
/// host's environment strips. It runs alone, since its figures are times.
/// </summary>
[Collection(Alone.Name)]
[Trait(Category.Name, Category.Process)]
public sealed class TreePoolDayBenchProcessTests(ITestOutputHelper output)
{
    [Fact]
    public async Task A_day_of_main_replayed_through_one_kept_slot_is_measured()
    {
        if (Environment.GetEnvironmentVariable("TREEPOOL_BENCH") is not { Length: > 0 } name) return;
        var stride = int.TryParse(Environment.GetEnvironmentVariable("TREEPOOL_BENCH_STRIDE"), out var each) && each > 0 ? each : 1;
        var builds = Environment.GetEnvironmentVariable("TREEPOOL_BENCH_BUILDS") == "1";
        var bench = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "treepool-bench");
        var commits = File.ReadAllLines(Path.Combine(bench, "day.txt")).Where(line => line.Length > 0).Reverse().ToList();
        var scratch = Path.Combine(bench, name);
        Directory.CreateDirectory(scratch);

        await new TreePoolDayBench(scratch, commits, stride, builds, new BenchRuns(Path.Combine(bench, name + ".log"), output.WriteLine)).RunAsync();
    }
}
