using System.Text.Json;
using Daoris.Driver;
using Microsoft.Data.Sqlite;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// CONTEXTDRIVE1a (the context-counters design §2.2, §2.4): every count of a line from the one pure read, over
/// <c>fixtures/line-counts.json</c>. Equal named by any part of a line and read again; <i>at least</i> for a trimmed log, a cut walk and a
/// store before its step; <i>unknown</i> for a service that does not answer; <c>failures-on-choice</c> absent for a conversation; a
/// teammate's record left out; and each count the same as the reader that counts it today.
/// </summary>
public sealed class LineCountsTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "line-counts", Guid.NewGuid().ToString("N")[..8]);

    public LineCountsTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    public static TheoryData<string> Rows => [.. LineCountsFixture.Read().GetProperty("counts").EnumerateArray().Select(row => row.GetProperty("name").GetString()!)];

    private static JsonElement Row(string name) =>
        LineCountsFixture.Read().GetProperty("counts").EnumerateArray().Single(row => row.GetProperty("name").GetString() == name);

    private static IReadOnlyList<LineCount> Counts(JsonElement row, string session)
    {
        var events = LineCountsFixture.Events(row);
        var moves = row.TryGetProperty("moves", out var each)
            ? new MovesAnswer(each.GetProperty("n").GetInt32(), each.GetProperty("state").GetString()!)
            : new MovesAnswer(0, MovesAnswer.Read);
        var forgiven = row.TryGetProperty("forgiven", out var mark)
            ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["q1"] = mark.GetInt32() }
            : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // COMMS1g: a row with its admission kept answers alone from it, read on over the events as the store's reader reads it.
        Func<LineHistory, AloneCount?>? alone = row.TryGetProperty("admission", out _)
            ? history => ContinuationAdmissions.CountOf(ContinuationAdmissions.ReadOn(LineCountsFixture.Admission(row, history.Line), history, LineCountsFixture.Seeded))
            : null;
        var evidence = LineCountsRead.Gather(session, LineCountsFixture.Json(row), id => events.GetValueOrDefault(id), _ => moves, forgiven, alone);
        return LineCounts.Of(session, evidence);
    }

    /// <summary>COMMS1g: a line whose admission is kept counts answers alone from it, and every other from the events, as until it.</summary>
    [Fact]
    public void A_kept_admission_is_the_answers_alone_source()
    {
        foreach (var row in LineCountsFixture.Read().GetProperty("counts").EnumerateArray())
        {
            var named = row.GetProperty("named")[0].GetString()!;
            var source = Counts(row, named).SingleOrDefault(count => count.Count == LineCounts.AnswersAlone)?.From;
            if (source is null) continue;
            Assert.Equal(row.TryGetProperty("admission", out _) ? ContinuationAdmissions.Admission : "events", source);
        }
    }

    /// <summary>
    /// COMMS1g: the store's reader hands the admission's count only where one is kept, so a line not yet seeded counts from its events, and
    /// one seeded and read on in the store counts as the fixture's pure read does.
    /// </summary>
    [Fact]
    public void The_store_hands_the_admission_only_where_one_is_kept()
    {
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        var admissions = new ContinuationAdmissions(_home, () => StoreSettled.Yes);
        var records = new Dictionary<string, PriorSession>(StringComparer.Ordinal)
        {
            ["s1"] = new("s1", null, "completed") { Quest = "q1" },
            ["s9"] = new("s9", null, "completed") { Quest = "q9" },
        };
        events.Append("s1", new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Helper = "h1", Id = "w1", Text = "Answered." });
        events.Append("s1", new SessionEvent { Kind = SessionEventKind.Message, Text = "Went on." });
        var history = ContinuationAdmissions.History("s1", records, id => LineCountsRead.EventsOf(events, id));
        Assert.Null(admissions.Count(history));

        Assert.True(admissions.Admit(history, "answers:w2", ["w2"], "s1", 2)!.Admitted);

        var count = admissions.Count(history)!;
        Assert.Equal(("q1", 2, ContinuationAdmissions.Admission), (count.Line, count.N, count.From));
        Assert.Equal(ContinuationAdmissions.Seed, admissions.Count(ContinuationAdmissions.History("s9", records, id => LineCountsRead.EventsOf(events, id)))!.From);
    }

    [Theory]
    [MemberData(nameof(Rows))]
    public void Each_count_is_what_the_table_says_by_whichever_part_names_the_line(string name)
    {
        var row = Row(name);
        var expect = row.GetProperty("expect");

        foreach (var named in row.GetProperty("named").EnumerateArray().Select(each => each.GetString()!))
        {
            var counts = Counts(row, named);
            if (expect.EnumerateObject().Count() == 0) Assert.Empty(counts);

            foreach (var want in expect.EnumerateObject())
            {
                var found = counts.SingleOrDefault(count => count.Count == want.Name);
                if (want.Value.ValueKind == JsonValueKind.String)
                {
                    Assert.Null(found);
                    continue;
                }

                Assert.True(found is not null, $"{name}: {want.Name} is absent for {named}");
                Assert.Equal(want.Value.TryGetProperty("n", out var n) ? n.GetInt32() : null, found.N);
                Assert.Equal(want.Value.GetProperty("known").GetString(), found.Known);
                Assert.Equal(want.Value.TryGetProperty("why", out var why) ? why.GetString() : null, found.Why);
            }
        }
    }

    [Fact]
    public void A_line_read_again_after_a_restart_is_equal_and_names_one_key()
    {
        var row = Row("equal across a hand-over, a carry-on and a restart: named by any part of the line");

        var first = Counts(row, "s3");
        var again = Counts(row, "s3");

        Assert.Equal(first, again);
        Assert.All(first, count => Assert.Equal("q1", count.Line));
        Assert.Equal(first, Counts(row, "s1"));
    }

    [Fact]
    public void Each_count_is_the_one_its_reader_counts_today()
    {
        var row = Row("equal across a hand-over, a carry-on and a restart: named by any part of the line");
        var json = LineCountsFixture.Json(row)!;
        var events = LineCountsFixture.Events(row);

        var counts = Counts(row, "s3");

        Assert.Equal(
            ServiceClient.ReadStrikes(json)["q1"] - 1, counts.Single(count => count.Count == LineCounts.Failures).N);
        Assert.Equal(ServiceClient.ReadFailedRuns(json)["q1"], counts.Single(count => count.Count == LineCounts.FailuresOnChoice).N);
        Assert.Equal(
            AnswersAlone.Count(events.Values.SelectMany(part => part!.Events)), counts.Single(count => count.Count == LineCounts.AnswersAlone).N);
    }

    [Fact]
    public void Every_count_names_its_line_its_owner_its_window_and_its_source()
    {
        var counts = Counts(Row("equal across a hand-over, a carry-on and a restart: named by any part of the line"), "s2");

        Assert.Equal(
            ["answers-alone", "failures", "failures-on-choice", "quiet", "limits", "moves", "hand-overs"],
            counts.Select(count => count.Count));
        Assert.All(counts, count =>
        {
            Assert.False(string.IsNullOrEmpty(count.Window));
            Assert.False(string.IsNullOrEmpty(count.From));
            Assert.Contains(count.Of, new[] { "line", "quest" });
        });
        Assert.Equal("quest", counts.Single(count => count.Count == LineCounts.Failures).Of);
    }

    [Fact]
    public void A_home_with_no_store_reads_moves_at_least_and_makes_none()
    {
        var answer = LineCountsRead.MovesOf(_home, "q1", new StaffMoves(_home));

        Assert.Equal(MovesAnswer.BeforeStep, answer.State);
        Assert.False(File.Exists(DesktopStore.LocationIn(_home)));
    }

    [Fact]
    public void A_store_file_that_does_not_read_leaves_moves_unreadable()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DesktopStore.LocationIn(_home))!);
        File.WriteAllText(DesktopStore.LocationIn(_home), "this is not a database, and it is long enough that no header could be read from it.");

        Assert.Equal(MovesAnswer.Unreadable, LineCountsRead.MovesOf(_home, "q1", new StaffMoves(_home)).State);
    }

    [Fact]
    public void A_store_with_the_table_reads_the_moves_the_ledger_counts()
    {
        var moves = new StaffMoves(_home, () => StoreSettled.Yes);
        Assert.True(moves.Record("q1", "build", "s1", StaffConditions.Limit, StaffActions.Next, "choice 1"));
        Assert.True(moves.Record("q1", "build", "s2", StaffConditions.Limit, StaffActions.Next, "choice 1"));

        var answer = LineCountsRead.MovesOf(_home, "q1", moves);

        Assert.Equal(new MovesAnswer(moves.Of("q1").Moves, MovesAnswer.Read), answer);
        Assert.Equal(2, answer.N);
    }
}
