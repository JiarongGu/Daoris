using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;
using Xunit.Abstractions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What reading a session's events costs on a home of a working machine's size: 300 records, most a few hundred kilobytes, ten of a
/// megabyte and one of six, the shape the installed machine's record folder has. Each read runs once cold and is timed warm.
/// </summary>
[Collection(Alone.Name)]
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
public sealed class SessionEventReadsBenchProcessTests(ITestOutputHelper output) : IDisposable
{
    private const int Sessions = 300;
    // Held by the read in a process come up since: the first ask of a record already indexed. A whole read of the largest takes 230-300 ms.
    private static readonly TimeSpan OpenBudget = TimeSpan.FromMilliseconds(100);

    // 300 records of which 270 are read whole: held above the 0.6 s it takes so a neighbour's load does not break it.
    private static readonly TimeSpan SweepBudget = TimeSpan.FromSeconds(2);

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-events-bench-" + Guid.NewGuid().ToString("N")[..8]);

    private string _directory => DaorisHome.Entry(_home, HomeEntry.Sessions);

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private static string Id(int n) => $"bench{n:D4}";

    private static int LinesOf(int n) => n == 0 ? 24_000 : n <= 10 ? 4_000 : n <= 30 ? 2_000 : 1_000;

    /// <summary>A record of the kinds a working conversation keeps, written as the log writes it, a plan now and then and a call that failed.</summary>
    private void Make()
    {
        Directory.CreateDirectory(_directory);
        var start = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        for (var n = 0; n < Sessions; n++)
        {
            using var writer = new StreamWriter(Path.Combine(_directory, $"{Id(n)}.events.jsonl"));
            var seq = 0L;
            void Write(SessionEvent e) => writer.WriteLine(JsonSerializer.Serialize(e with { Seq = ++seq, At = start.AddSeconds(seq) }, SessionEventText.Json));
            Write(new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "Please do the work.", Id = "w1", Choice = "go" });
            for (var line = 0; line < LinesOf(n); line++)
            {
                switch (line % 20)
                {
                    case 0: Write(new SessionEvent { Kind = SessionEventKind.Plan, Entries = [new PlanEntry("step", "pending", null)] }); break;
                    case 7: Write(new SessionEvent { Kind = SessionEventKind.Tool, Id = $"c{line}", Status = "failed", Text = new string('x', 300) }); break;
                    case 19: Write(new SessionEvent { Kind = SessionEventKind.Turn, Tokens = new(Input: 10, Output: 2, CacheRead: null, CacheWrite: null) }); break;
                    default: Write(new SessionEvent { Kind = SessionEventKind.Message, Text = new string('y', 200) }); break;
                }
            }

            Write(new SessionEvent { Kind = SessionEventKind.Turn, Tokens = new(Input: 10, Output: 2, CacheRead: null, CacheWrite: null) });
        }
    }

    private TimeSpan Timed(string what, Action read)
    {
        var cold = Stopwatch.StartNew();
        read();
        var first = cold.ElapsedMilliseconds;
        // A process that came up since: what it kept in memory is gone, what the store kept stands.
        SessionEventHeads.LetGoOfMemory();
        var again = Stopwatch.StartNew();
        read();
        var reopened = again.ElapsedMilliseconds;
        var watch = Stopwatch.StartNew();
        read();
        output.WriteLine($"{what}: first {first} ms, in a process come up since {reopened} ms, warm {watch.ElapsedMilliseconds} ms");
        return again.Elapsed;
    }

    [Fact]
    public void Opening_a_long_record_and_sweeping_every_one_reads_within_budget()
    {
        var made = Stopwatch.StartNew();
        Make();
        output.WriteLine($"{Sessions} records, {new DirectoryInfo(_directory).GetFiles().Sum(file => file.Length) / 1_000_000} MB, made in {made.ElapsedMilliseconds} ms");
        var events = new SessionEvents(_directory);

        var page = Timed("the newest page of the largest record", () => events.Page(Id(0)));
        var older = Timed("the page before an event of the largest record", () => events.Page(Id(0), before: 12_000));
        var after = Timed("the events since a number near the largest record's end", () => events.After(Id(0), 23_000));
        var turns = Timed("the turns ended of the largest record", () => events.TurnsEnded(Id(0)));
        var choices = Timed("the choices of the largest record", () => events.ChoicesOf(Id(0)));
        var plan = Timed("the last plan of the largest record", () => events.LastPlan(Id(0)));
        var last = Timed("the last page of one event of every record", () =>
        {
            for (var n = 0; n < Sessions; n++) events.Page(Id(n), limit: 1);
        });
        output.WriteLine($"opened {page.TotalMilliseconds:0} older {older.TotalMilliseconds:0} after {after.TotalMilliseconds:0} turns {turns.TotalMilliseconds:0} choices {choices.TotalMilliseconds:0} plan {plan.TotalMilliseconds:0} sweep {last.TotalMilliseconds:0}");

        Assert.True(page < OpenBudget, $"the newest page of the largest record took {page.TotalMilliseconds:0} ms, past {OpenBudget.TotalMilliseconds} ms");
        Assert.True(older < OpenBudget, $"an earlier page took {older.TotalMilliseconds:0} ms, past {OpenBudget.TotalMilliseconds} ms");
        Assert.True(after < OpenBudget, $"the events since took {after.TotalMilliseconds:0} ms, past {OpenBudget.TotalMilliseconds} ms");
        Assert.True(turns < OpenBudget, $"the turns ended took {turns.TotalMilliseconds:0} ms, past {OpenBudget.TotalMilliseconds} ms");
        Assert.True(choices < OpenBudget, $"the choices took {choices.TotalMilliseconds:0} ms, past {OpenBudget.TotalMilliseconds} ms");
        Assert.True(plan < OpenBudget, $"the last plan took {plan.TotalMilliseconds:0} ms, past {OpenBudget.TotalMilliseconds} ms");
        Assert.True(last < SweepBudget, $"a page of one event of every record took {last.TotalMilliseconds:0} ms, past {SweepBudget.TotalMilliseconds} ms");
    }
}
