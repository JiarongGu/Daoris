using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Daoris.Knowledge;
using Microsoft.Data.Sqlite;
using Xunit.Abstractions;

namespace Daoris.Service.Tests;

/// <summary>
/// FOUNDATION1 row p's landing measurement (D212, the design §10's row 1p, §19.13; O5-F8): the say door's p95 and the <c>sessions</c> write's
/// p95 within a tenth of before, and what a week's takes make of the settlement table, on a scratch service seeded at the scale the row names
/// (more than 50,000 settlements in a week), never the install and never its home.
/// </summary>
/// <remarks>
/// <para>It is written against the surface both sides have, so one copy of this file runs before the row's change and after it, on a
/// database file of its own under the repository's gitignored <c>_fixtures/</c>, which keeps the service's own default (a rollback journal,
/// <c>synchronous = FULL</c>) and so a commit is a sync to the disk, as the install's is. Where the table the row adds exists the seed fills
/// it to a week's scale, so the same reads and writes run beside it; where it does not, they run without it. The retry after a take is
/// asked with <c>settled</c> where the say door has that parameter, through reflection, which the base lacks.</para>
/// <para>A measurement: it asserts that every call landed and none was pathological, and says the percentiles in the test's output for
/// D212's note. Held to where it is asked for: <c>DAORIS_MEASURE_SAID</c> set, as <c>DAORIS_MEASURE_GITDOOR</c> holds the GitDoor's;
/// elsewhere each case holds nothing. <c>DAORIS_MEASURE_SESSIONS</c> sets the records seeded (5,000 by default) and
/// <c>DAORIS_MEASURE_SETTLED</c> the settlements (60,000). No variable of the process's environment is set.</para>
/// </remarks>
public sealed class SaidSettledMeasureTests : IDisposable
{
    private const string Variable = "DAORIS_MEASURE_SAID";

    private const int Samples = 600;

    private readonly ITestOutputHelper _said;

    private readonly string _root = Path.Combine(SourceFiles.WorkspaceFolder(), "_fixtures", "said-settled-measure", Guid.NewGuid().ToString("N")[..8]);

    public SaidSettledMeasureTests(ITestOutputHelper said)
    {
        _said = said;
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A file the scanner holds for a moment: the next prune of _fixtures takes it.
        }
    }

    private static int Setting(string name, int otherwise) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), CultureInfo.InvariantCulture, out var asked) && asked > 0 ? asked : otherwise;

    private static double Percentile(IReadOnlyList<double> sorted, double p) => sorted[Math.Clamp((int)Math.Ceiling(p * sorted.Count) - 1, 0, sorted.Count - 1)];

    private string Say(string label, IReadOnlyList<double> times)
    {
        var sorted = times.Order().ToArray();
        var line = string.Create(CultureInfo.InvariantCulture, $"{label}: {times.Count} calls, p50 {Percentile(sorted, 0.5):F2} ms, p95 {Percentile(sorted, 0.95):F2} ms, max {sorted[^1]:F2} ms");
        _said.WriteLine(line);
        return line;
    }

    private static async Task<(SqliteConnection Connection, SessionStore Sessions, SessionLedger Ledger)> OpenAsync(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWriteCreate }.ToString());
        await connection.OpenAsync();
        var quests = await QuestStore.OpenAsync(connection);
        var sessions = await SessionStore.OpenAsync(connection);
        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(), DisclosurePolicy.LocalOnly, registry: new Registry());
        return (connection, sessions, new SessionLedger(quests, sessions, service));
    }

    private static async Task<long> ScalarAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    /// <summary>The say door asked for a retry after a take, with <c>settled</c> where the door has that parameter.</summary>
    private static Task<SessionSayOutcome> RetryAsync(SessionLedger ledger, string id, string key, DateTimeOffset now)
    {
        var say = typeof(SessionLedger).GetMethod(nameof(SessionLedger.SayAsync))!;
        var arguments = say.GetParameters().Select(parameter => parameter.HasDefaultValue ? parameter.DefaultValue : null).ToArray();
        void Set(string name, object? value)
        {
            var at = Array.FindIndex(say.GetParameters(), parameter => parameter.Name == name);
            if (at >= 0) arguments[at] = value;
        }

        Set("id", id);
        Set("text", "the same words again");
        Set("now", now);
        Set("ct", CancellationToken.None);
        Set("key", key);
        Set("from", SaidFrom.Daoris);
        Set("settled", true);
        return (Task<SessionSayOutcome>)say.Invoke(ledger, arguments)!;
    }

    [Fact]
    public async Task The_say_door_and_the_sessions_write_at_a_weeks_settlements_read_before_and_after()
    {
        if (Environment.GetEnvironmentVariable(Variable) is null) return;

        var records = Setting("DAORIS_MEASURE_SESSIONS", 5_000);
        var settled = Setting("DAORIS_MEASURE_SETTLED", 60_000);
        var now = DateTimeOffset.UtcNow;
        var (connection, sessions, ledger) = await OpenAsync(Path.Combine(_root, "service.db"));
        await using var owned = connection;

        // The seed: ended conversation records a producer says to, each in the one transaction a batch of creates shares.
        var ids = new List<string>();
        for (var batch = 0; batch < records; batch += 500)
        {
            await sessions.ExclusiveAsync(async inside =>
            {
                for (var each = batch; each < Math.Min(records, batch + 500); each++)
                {
                    var session = await sessions.CreateAsync(
                        quest: null, "Owner", "stub", now.AddMinutes(-each), kind: SessionKind.Chat, ownEntry: true, ct: inside);
                    await sessions.SetStateAsync(session.Id, SessionState.Completed, null, null, null, now.AddMinutes(-each), inside);
                    ids.Add(session.Id);
                }

                return true;
            });
        }

        var has = await ScalarAsync(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'said_settled'") > 0;
        if (has)
        {
            // A week's settlements, spread over the seven days (more than the 50,000 the row names): keyed as a producer's are, by the
            // record it left, the word and its key, with the moment in seconds.
            var random = new Random(20261025);
            await using var seed = connection.CreateCommand();
            seed.CommandText = "BEGIN";
            await seed.ExecuteNonQueryAsync();
            await using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT OR REPLACE INTO said_settled (session, word, key, at) VALUES ($session, $word, $key, $at)";
            var pSession = insert.Parameters.Add("$session", SqliteType.Text);
            var pWord = insert.Parameters.Add("$word", SqliteType.Text);
            var pKey = insert.Parameters.Add("$key", SqliteType.Text);
            var pAt = insert.Parameters.Add("$at", SqliteType.Integer);
            var week = 7L * 24 * 3600;
            for (var each = 0; each < settled; each++)
            {
                pSession.Value = ids[random.Next(ids.Count)];
                pWord.Value = random.Next().ToString("x8", CultureInfo.InvariantCulture);
                pKey.Value = $"seed:{random.Next():x8}:{each}";
                pAt.Value = now.ToUnixTimeSeconds() - (long)(random.NextDouble() * (week - 600));
                await insert.ExecuteNonQueryAsync();
            }

            seed.CommandText = "COMMIT";
            await seed.ExecuteNonQueryAsync();
        }

        _said.WriteLine($"seeded {ids.Count} records, the settlement table {(has ? $"holding {await ScalarAsync(connection, "SELECT COUNT(*) FROM said_settled")}" : "absent")}");

        var pick = new Random(7);
        string Pick() => ids[pick.Next(ids.Count)];

        // Warm the pages and the statements both sides use.
        for (var each = 0; each < 100; each++) await ledger.SayAsync(Pick(), "warm", null, now, key: $"warm:{each}", from: SaidFrom.Daoris);

        // The say door: a new word under a new key, and the same key said again while its word waits.
        var keep = new List<double>();
        var again = new List<double>();
        var kept = new List<(string Id, string Word, string Key)>();
        for (var each = 0; each < Samples; each++)
        {
            var id = Pick();
            var key = $"measure:{each}";
            var clock = Stopwatch.StartNew();
            var outcome = await ledger.SayAsync(id, $"words {each}", null, now, key: key, from: SaidFrom.Daoris);
            keep.Add(clock.Elapsed.TotalMilliseconds);
            Assert.Equal(SessionSayRefusal.None, outcome.Refusal);
            kept.Add((id, outcome.Word!.Id, key));

            clock.Restart();
            var repeat = await ledger.SayAsync(id, $"words {each}", null, now, key: key, from: SaidFrom.Daoris);
            again.Add(clock.Elapsed.TotalMilliseconds);
            Assert.Equal(SessionSayRefusal.None, repeat.Refusal);
        }

        // A take (a sessions write that fires the row's trigger), then the producer's retry after it.
        var take = new List<double>();
        var retry = new List<double>();
        foreach (var (id, word, key) in kept)
        {
            var clock = Stopwatch.StartNew();
            await sessions.TakeSaidAsync(id, [word]);
            take.Add(clock.Elapsed.TotalMilliseconds);

            clock.Restart();
            var outcome = await RetryAsync(ledger, id, key, now);
            retry.Add(clock.Elapsed.TotalMilliseconds);
            Assert.Equal(SessionSayRefusal.None, outcome.Refusal);
        }

        // The sessions writes that move no word: a state move, which every record sees, and a word kept and taken off the record by the
        // statements the stores run.
        var state = new List<double>();
        var keeps = new List<double>();
        for (var each = 0; each < Samples; each++)
        {
            var id = Pick();
            var clock = Stopwatch.StartNew();
            await sessions.SetStateAsync(id, each % 2 == 0 ? SessionState.Working : SessionState.Completed, null, null, null, now);
            state.Add(clock.Elapsed.TotalMilliseconds);
            await sessions.SetStateAsync(id, SessionState.Completed, null, null, null, now);

            var word = new SaidWord(Guid.NewGuid().ToString("N")[..8], "kept", now, [], Key: $"write:{each}");
            clock.Restart();
            await sessions.KeepSaidAsync(id, word);
            keeps.Add(clock.Elapsed.TotalMilliseconds);
            await sessions.TakeSaidAsync(id, [word.Id]);
        }

        var lines = new[]
        {
            Say("say door, a new word under a new key", keep),
            Say("say door, the same key while its word waits", again),
            Say("say door, the retry after the take (settled asked where the door has it)", retry),
            Say("say door, all three pooled", [.. keep, .. again, .. retry]),
            Say("sessions write, a state move", state),
            Say("sessions write, a word kept", keeps),
            Say("sessions write, a word taken", take),
            Say("sessions write, all three pooled", [.. state, .. keeps, .. take]),
        };

        var pages = await ScalarAsync(connection, "PRAGMA page_count");
        var size = await ScalarAsync(connection, "PRAGMA page_size");
        _said.WriteLine($"database {pages * size / 1024} KiB ({pages} pages of {size} bytes)");
        if (has)
        {
            _said.WriteLine($"settlement rows now {await ScalarAsync(connection, "SELECT COUNT(*) FROM said_settled")}");
            try
            {
                var table = await ScalarAsync(connection, "SELECT SUM(pgsize) FROM dbstat WHERE name = 'said_settled'");
                var indexes = await ScalarAsync(connection, "SELECT SUM(pgsize) FROM dbstat WHERE name LIKE 'said_settled%' OR name LIKE 'sqlite_autoindex_said_settled%'");
                _said.WriteLine($"settlement table {table / 1024} KiB, with its indexes {indexes / 1024} KiB");
            }
            catch (SqliteException)
            {
                _said.WriteLine("settlement table's own size: no dbstat in this build of SQLite");
            }
        }

        Assert.NotEmpty(lines);
        Assert.True(keep.Max() < 5000 && again.Max() < 5000 && retry.Max() < 5000 && state.Max() < 5000, "a call took seconds");
    }
}
