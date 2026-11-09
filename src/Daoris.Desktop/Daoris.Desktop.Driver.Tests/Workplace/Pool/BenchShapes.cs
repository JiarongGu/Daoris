using System.Diagnostics;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The repository a bench replays: its clone (the root a slot is made from, with no checkout of its own), its day of commits oldest
/// first, whether it is the web shape, and the file of a slot a running process holds between two takes.
/// </summary>
internal sealed record BenchShape(string Root, IReadOnlyList<string> Commits, bool Web, string Held);

/// <summary>The bench's two repository shapes made in its scratch: this repository over a day of its main, and the generated web one.</summary>
internal static class BenchShapes
{
    /// <summary>The web shape's day: as many commits as the design's first bench replayed.</summary>
    private const int WebCommits = 104;

    internal static async Task<BenchShape> DaorisAsync(string scratch, string source, string tip, BenchRuns run)
    {
        var root = Path.Combine(scratch, "engine");
        var at = (await GitAsync(source, "rev-parse", tip)).Trim();
        await CloneAsync(scratch, source, root);
        var watch = Stopwatch.StartNew();
        var commits = await BenchDay.OfAsync(root, at);
        var standIns = await BenchDay.StandInsAsync(root, at);
        run.Say($"this repository's day: {commits.Count} first-parent commits of {tip} ({at[..10]}) over 24 hours, each made again with the tip's "
            + $"rule file in {watch.Elapsed.TotalSeconds:F0} s; stand-ins at {string.Join(", ", standIns)}");
        return new BenchShape(root, commits, false, "src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/bin/Debug/net10.0/Daoris.Desktop.Driver.dll");
    }

    internal static async Task<BenchShape> WebAsync(string scratch, string source, BenchRuns run)
    {
        var origin = Path.Combine(scratch, "web-origin");
        var commits = await BenchWebRepository.MakeAsync(origin, source, WebCommits, run);
        var root = Path.Combine(scratch, "engine");
        await CloneAsync(scratch, origin, root);
        return new BenchShape(root, commits, true, "dist/index.html");
    }

    private static async Task CloneAsync(string scratch, string source, string root)
    {
        await GitAsync(scratch, "clone", "--quiet", "--no-checkout", source, root);
        await GitAsync(root, "config", "user.email", "bench@example.test");
        await GitAsync(root, "config", "user.name", "Bench");
    }
}
