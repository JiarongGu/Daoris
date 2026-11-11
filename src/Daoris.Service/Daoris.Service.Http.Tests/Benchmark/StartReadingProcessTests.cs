using System.Diagnostics;
using Daoris.Knowledge;
using Microsoft.Data.Sqlite;
using Xunit.Abstractions;

namespace Daoris.Service.Http.Tests.Benchmark;

/// <summary>
/// What a start's first read of the knowledge index costs over checkouts of a real size: four copies of this repository's own records,
/// committed, under the repository's gitignored scratch. Each process is composed as the installed host is, over one store kept
/// between them, and its first summary (what <c>/api/repositories</c> answers) is timed: the first ever, a start where every root is
/// read again (each kept reading's stamp cleared, which is what every start paid before the readings were kept), a start where none
/// moved, and one where one root moved.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class StartReadingProcessTests(ITestOutputHelper output) : IDisposable
{
    private static readonly string[] Copies = ["alpha", "beta", "gamma", "delta"];

    private static readonly string[] Records =
        ["docs", ".claude/knowledge", ".claude/skills", "daoris.json", "daoris.lock", "TASKS.md", "AGENTS.md", "CHANGELOG.md", "ROADMAP.md", "README.md"];

    private readonly string _scratch = Path.Combine(Repository(), "_fixtures", "start-reading-" + Guid.NewGuid().ToString("N")[..8]);

    private string Database => Path.Combine(_scratch, "knowledge.db");

    private string Family => Path.Combine(_scratch, "family");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (!Directory.Exists(_scratch)) return;
        foreach (var file in Directory.EnumerateFiles(_scratch, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(_scratch, recursive: true);
    }

    [Fact]
    public async Task A_start_over_kept_readings_reads_only_what_moved()
    {
        foreach (var copy in Copies) Copy(Path.Combine(Family, copy));

        var first = await FirstSummaryAsync("the first ever, an empty store");
        await ClearStampsAsync();
        var everyRoot = await FirstSummaryAsync("every root read again, as each start did");
        var unmoved = await FirstSummaryAsync("a start where nothing moved");
        File.WriteAllText(Path.Combine(Family, "beta", "docs", "decisions", "D9999.md"), "# D9999: A decision written while no process ran\n\nWhy.\n");
        var oneMoved = await FirstSummaryAsync("a start where one root moved");

        await using (var store = await SqliteKnowledgeStore.OpenAsync(Database))
        {
            IKnowledgeStore held = store;
            var clock = Stopwatch.StartNew();
            var all = await held.AllAsync();
            var read = clock.ElapsedMilliseconds;
            clock.Restart();
            var counts = await held.HeldCountsAsync();
            output.WriteLine($"the summary's count: every entry read {read} ms ({all.Count} entries), counted {clock.ElapsedMilliseconds} ms ({counts.Count} rows)");
        }

        SqliteConnection.ClearAllPools();
        Assert.True(unmoved < everyRoot, $"a start where nothing moved took {unmoved} ms, a start reading every root {everyRoot} ms");
        Assert.True(oneMoved < everyRoot, $"a start where one root moved took {oneMoved} ms, a start reading every root {everyRoot} ms");
        Assert.True(first > 0);
    }

    private async Task<long> FirstSummaryAsync(string which)
    {
        var clock = Stopwatch.StartNew();
        await using var composed = await ServiceFactory.CreateAsync(new ServiceOptions(Family, Database));
        var composedIn = clock.ElapsedMilliseconds;
        clock.Restart();
        var summary = await composed.Service.SummarizeAsync();
        var answered = clock.ElapsedMilliseconds;
        output.WriteLine($"{which}: composed {composedIn} ms, first summary {answered} ms, {summary.Sum(r => r.Total)} entries in {summary.Count} repositories");
        return answered;
    }

    private async Task ClearStampsAsync()
    {
        await using (var connection = new SqliteConnection($"Data Source={Database}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"UPDATE {SqliteKnowledgeStore.ReadingsTable} SET scanner = NULL;";
            await command.ExecuteNonQueryAsync();
        }

        SqliteConnection.ClearAllPools();
    }

    private static void Copy(string to)
    {
        var from = Repository();
        foreach (var record in Records)
        {
            var source = Path.Combine(from, record.Replace('/', Path.DirectorySeparatorChar));
            var target = Path.Combine(to, record.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(source))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(source, target);
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                var placed = Path.Combine(target, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(placed)!);
                File.Copy(file, placed);
            }
        }

        Git(to, "init", "-q", "-b", "main");
        Git(to, "add", "-A");
        Git(to, "-c", "user.name=Bench", "-c", "user.email=bench@example.invalid", "-c", "commit.gpgsign=false", "commit", "-q", "-m", "copy");
    }

    private static void Git(string folder, params string[] arguments)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = folder, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "-c", "core.autocrlf=false" }.Concat(arguments)) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        // Both read at once: a thousand files' warnings on the one not read fill its pipe and hold git there.
        var said = process.StandardError.ReadToEndAsync();
        process.StandardOutput.ReadToEnd();
        said.Wait();
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
    }

    private static string Repository()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no repository above the test's build");
    }
}
