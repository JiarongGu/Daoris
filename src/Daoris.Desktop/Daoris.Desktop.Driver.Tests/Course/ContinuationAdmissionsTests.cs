using Daoris.Driver;
using Microsoft.Data.Sqlite;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// COMMS1g (the comms design §5.3 and §13 point 8; the context-counters design §2.3, §2.4): the answers-alone count kept as one serialized
/// admission per line in the desktop store. Seeded once from the line's events, zero where none read and at least where the history was cut
/// or trimmed; read on over the events after what it read for the person's and Daoris's words and for the go-ons taken; a go-on judged
/// inside one write, so two answers queued together are admitted once; and a waited quest's result reserved on its line.
/// </summary>
public sealed class ContinuationAdmissionsTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "continuation-admissions", Guid.NewGuid().ToString("N")[..8]);

    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-03T09:00:00Z");

    public ContinuationAdmissionsTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private ContinuationAdmissions Admissions => new(_home, () => StoreSettled.Yes);

    // ——— The events a part keeps, as the doors write them, numbered and timed as the log stamps them.

    private static SessionEvent Started() => new() { Kind = SessionEventKind.User, Origin = "target", Text = "The quest's instruction." };

    private static SessionEvent Helper(string id) => new() { Kind = SessionEventKind.User, Origin = "target", Helper = "h1", Id = id, Text = "Answered." };

    private static SessionEvent Person() => new() { Kind = SessionEventKind.User, Origin = "person", Text = "Go on." };

    private static SessionEvent Acts() => new() { Kind = SessionEventKind.Message, Text = "I went on." };

    private static SessionEvent Ended() => new() { Kind = SessionEventKind.Turn, StopReason = "end_turn" };

    private static IEnumerable<SessionEvent> Turn(params SessionEvent[] prompt) => [.. prompt, Acts(), Ended()];

    private static IReadOnlyList<SessionEvent> Log(IEnumerable<SessionEvent> events, long first = 1) =>
        [.. events.Select((e, index) => e with { Seq = first + index, At = At.AddSeconds(first + index) })];

    private static LineHistory History(IReadOnlyList<SessionEvent> events, bool trimmed = false, string? cut = null) =>
        new("s1", [new HistoryPart("s1", new PartEvents(events, trimmed))], cut);

    private static IReadOnlyList<SessionEvent> Twice() => Log([.. Turn(Started()), .. Turn(Helper("w1")), .. Turn(Helper("w2"))]);

    [Fact]
    public void A_line_with_no_admission_is_seeded_from_its_events_and_no_events_count_zero()
    {
        Assert.Equal((0, LineCounts.Exact), Count(ContinuationAdmissions.ReadOn(null, History([]), At)));
        Assert.Equal((2, LineCounts.Exact), Count(ContinuationAdmissions.ReadOn(null, History(Twice()), At)));
        Assert.Equal(AnswersAlone.Count(Twice()), ContinuationAdmissions.CountOf(ContinuationAdmissions.ReadOn(null, History(Twice()), At)).N);
    }

    /// <summary>A seed over a trimmed log or a cut walk is a lower bound, never unknown; the person's word after it makes the count exact.</summary>
    [Fact]
    public void A_trimmed_or_cut_history_seeds_at_least_and_a_reset_after_it_is_exact()
    {
        var trimmed = ContinuationAdmissions.CountOf(ContinuationAdmissions.ReadOn(null, History(Log(Turn(Helper("w1")), first: 5), trimmed: true), At));
        Assert.Equal((1, LineCounts.AtLeast, LineCounts.LogTrimmed), (trimmed.N, trimmed.Known, trimmed.Why));

        var cut = ContinuationAdmissions.CountOf(ContinuationAdmissions.ReadOn(null, History(Twice(), cut: LineKey.CycleCut), At));
        Assert.Equal((2, LineCounts.AtLeast, LineCounts.WalkCut), (cut.N, cut.Known, cut.Why));

        var missing = new LineHistory("s0", [new HistoryPart("s0", null), new HistoryPart("s1", new PartEvents(Log(Turn(Helper("w1"))), false))], null);
        Assert.Equal((1, LineCounts.AtLeast, LineCounts.EventsMissing), Known(ContinuationAdmissions.ReadOn(null, missing, At)));

        var seeded = ContinuationAdmissions.ReadOn(null, History(Log(Turn(Helper("w1")), first: 5), trimmed: true), At);
        var reset = ContinuationAdmissions.ReadOn(seeded, History(Log([.. Turn(Helper("w1")), .. Turn(Person())], first: 5), trimmed: true), At.AddMinutes(1));
        Assert.Equal((0, LineCounts.Exact, (string?)null), Known(reset));
    }

    /// <summary>Read on, a go-on the events show is counted once, whether it was reserved or not, and an event read once is never read again.</summary>
    [Fact]
    public void Read_on_counts_each_go_on_once_and_takes_a_reserved_one()
    {
        var seeded = ContinuationAdmissions.ReadOn(null, History(Log(Turn(Started()))), At);
        var (admitted, reserved) = ContinuationAdmissions.Judge(seeded, "answers:w1", ["w1"], "s1", 2, At);
        Assert.True(admitted.Admitted);
        Assert.Equal(1, admitted.Count.N);

        var taken = ContinuationAdmissions.ReadOn(reserved, History(Log([.. Turn(Started()), .. Turn(Helper("w1"))])), At.AddMinutes(1));
        var row = Assert.Single(taken.Admitted);
        Assert.NotNull(row.Taken);
        Assert.NotNull(row.Attempted);
        Assert.Equal(1, ContinuationAdmissions.CountOf(taken).N);

        // A go-on no admission reserved (an older build's, or a path that reserved nothing) is counted as the events show it.
        var unreserved = ContinuationAdmissions.ReadOn(taken, History(Log([.. Turn(Started()), .. Turn(Helper("w1")), .. Turn(Helper("w2"))])), At.AddMinutes(2));
        Assert.Equal(2, ContinuationAdmissions.CountOf(unreserved).N);
        Assert.Equal(2, ContinuationAdmissions.CountOf(ContinuationAdmissions.ReadOn(unreserved, History(Log([.. Turn(Started()), .. Turn(Helper("w1")), .. Turn(Helper("w2"))])), At.AddMinutes(3))).N);
    }

    /// <summary>A reserved answer the prompt took beside the person's word went on with them: cancelled, and the count starts again.</summary>
    [Fact]
    public void An_answer_taken_beside_the_persons_word_is_cancelled()
    {
        var seeded = ContinuationAdmissions.ReadOn(null, History(Log(Turn(Started()))), At);
        var (_, reserved) = ContinuationAdmissions.Judge(seeded, "answers:w1", ["w1"], "s1", 2, At);

        var read = ContinuationAdmissions.ReadOn(reserved, History(Log([.. Turn(Started()), .. Turn(Helper("w1"), Person())])), At.AddMinutes(1));

        Assert.NotNull(Assert.Single(read.Admitted).Cancelled);
        Assert.Equal((0, LineCounts.Exact, (string?)null), Known(read));
        Assert.Equal(2, read.Epochs.Count);
    }

    [Fact]
    public void A_go_on_judged_again_is_the_same_go_on_and_one_past_the_bound_is_held()
    {
        var seeded = ContinuationAdmissions.ReadOn(null, History(Log([.. Turn(Started()), .. Turn(Helper("w1"))])), At);

        var (first, once) = ContinuationAdmissions.Judge(seeded, "answers:w2", ["w2"], "s1", 2, At);
        var (again, same) = ContinuationAdmissions.Judge(once, "answers:w2,w3", ["w2", "w3"], "s1", 2, At);
        var (other, held) = ContinuationAdmissions.Judge(same, "answers:w4", ["w4"], "s1", 2, At);

        Assert.Equal((true, 2), (first.Admitted, first.Count.N));
        Assert.Equal((true, 2), (again.Admitted, again.Count.N));
        Assert.Equal(["w2", "w3"], Assert.Single(same.Admitted).Words);
        Assert.Equal((false, 2), (other.Admitted, other.Count.N));
        Assert.Single(held.Admitted);
        // With no `at` handed, the go-on is reserved whatever the count: a waited quest's result, which no hold judges yet.
        Assert.True(ContinuationAdmissions.Judge(held, "quest:q9", [], "s1", null, At).Answer.Admitted);
    }

    // ——— Kept in the store.

    [Fact]
    public void Two_answers_queued_together_are_admitted_once()
    {
        var history = History(Log([.. Turn(Started()), .. Turn(Helper("w1"))]));

        Assert.True(Admissions.Admit(history, "answers:a", ["a"], "s1", 2)!.Admitted);
        Assert.False(new ContinuationAdmissions(_home, () => StoreSettled.Yes).Admit(history, "answers:b", ["b"], "s1", 2)!.Admitted);
        Assert.True(Admissions.Admit(history, "answers:a", ["a"], "s1", 2)!.Admitted);
        Assert.Equal(2, Admissions.Count(history)!.N);
    }

    /// <summary>Two judgements meeting, as two executors' would: one write each, under the store's lock, and one admitted.</summary>
    [Fact]
    public async Task Two_judgements_at_once_admit_one()
    {
        var history = History(Log([.. Turn(Started()), .. Turn(Helper("w1"))]));
        using var gate = new Barrier(2);

        var answers = await Task.WhenAll(new[] { "a", "b" }.Select(word => Task.Run(() =>
        {
            var admissions = new ContinuationAdmissions(_home, () => StoreSettled.Yes);
            gate.SignalAndWait();
            return admissions.Admit(history, $"answers:{word}", [word], "s1", 2)!.Admitted;
        })));

        Assert.Equal(1, answers.Count(admitted => admitted));
    }

    /// <summary>The count is the store's: a restart reads it equal, and a log gone or trimmed afterwards resets nothing.</summary>
    [Fact]
    public void A_restart_reads_the_count_kept_and_a_lost_log_resets_nothing()
    {
        Assert.True(Admissions.Admit(History(Twice()), "answers:w3", ["w3"], "s1", 3)!.Admitted);

        var restarted = new ContinuationAdmissions(_home, () => StoreSettled.Yes);
        Assert.Equal((3, LineCounts.Exact), (restarted.Count(History(Twice()))!.N, restarted.Count(History(Twice()))!.Known));
        Assert.Equal(3, restarted.Count(History([]))!.N);
        Assert.Equal(3, restarted.Count(new LineHistory("s1", [new HistoryPart("s1", null)], null))!.N);
        Assert.Equal("admission", restarted.Count(History([]))!.From);
        Assert.False(restarted.Admit(History([]), "answers:w4", ["w4"], "s1", 3)!.Admitted);
    }

    [Fact]
    public void A_go_on_judged_again_with_nothing_new_writes_nothing()
    {
        var history = History(Twice());
        Assert.False(Admissions.Admit(history, "answers:w3", ["w3"], "s1", 2)!.Admitted);
        var revision = Revision();

        Assert.False(Admissions.Admit(history, "answers:w3", ["w3"], "s1", 2)!.Admitted);
        Assert.Equal(2, Admissions.Count(history)!.N);
        Assert.Equal(revision, Revision());
    }

    [Fact]
    public void A_waited_quests_result_is_reserved_once()
    {
        var history = History(Log([.. Turn(Started()), .. Turn(Helper("w1"))]));

        Assert.True(Admissions.Admit(history, "quest:q9", [], "s1", null)!.Admitted);
        Assert.True(Admissions.Admit(history, "quest:q9", [], "s1", null)!.Admitted);

        Assert.Equal(2, Admissions.Count(history)!.N);
        Assert.Equal("quest:q9", Admissions.Of("s1")!.Admitted.Last().Source);
    }

    /// <summary>A store a build before the step left is carried to it at the next open, and its first read seeds the line from the events.</summary>
    [Fact]
    public void A_store_before_the_step_is_carried_to_it_and_seeded_from_the_events()
    {
        using (var older = DesktopStore.Open(DesktopStore.LocationIn(_home), new DesktopStoreOptions { Steps = [.. DesktopStore.Steps.Take(19)], Settled = () => StoreSettled.Yes }))
        {
            Assert.Equal(19, older.Stamp.Version);
        }

        Assert.Null(Admissions.Count(History(Twice())));
        Assert.False(Admissions.Admit(History(Twice()), "answers:w3", ["w3"], "s1", 2)!.Admitted);

        var epoch = Assert.Single(Admissions.Of("s1")!.Epochs);
        Assert.Equal(("seed", 2, LineCounts.Exact), (epoch.Cause, epoch.N, epoch.Known));
    }

    /// <summary>A start whose confirmation is not settled keeps the store short of its raise, so it holds no admissions and answers none.</summary>
    [Fact]
    public void A_store_short_of_the_step_answers_no_admission()
    {
        var unsettled = new ContinuationAdmissions(_home, () => StoreSettled.Not("the swap journal is at `swapping`"));

        Assert.Null(unsettled.Admit(History(Twice()), "answers:w3", ["w3"], "s1", 2));
        Assert.Null(unsettled.Count(History(Twice())));
    }

    /// <summary>The seed's end (ONEWAY1af): once the admissions are kept and no event log here was begun before them.</summary>
    [Fact]
    public void The_seed_ends_once_no_log_here_was_begun_before_the_admissions()
    {
        var entry = TransitionalRegister.Entries.Single(each => each.Id == "answers-alone-events");
        var log = Path.Combine(DaorisHome.Entry(_home, HomeEntry.Sessions), "s0.events.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(log)!);
        File.WriteAllText(log, "");
        File.SetCreationTimeUtc(log, DateTime.UtcNow.AddDays(-1));
        Assert.False(entry.Ended(_home));

        Assert.NotNull(Admissions.Admit(History(Twice()), "answers:w3", ["w3"], "s1", 2));
        Assert.False(entry.Ended(_home));

        File.Delete(log);
        Assert.True(entry.Ended(_home));
    }

    private long Revision()
    {
        using var store = DesktopStoreReader.Open(DesktopStore.LocationIn(_home))!;
        return store.Query("SELECT revision FROM store_revisions WHERE family = $family", row => row.GetInt64(0), ("$family", ContinuationAdmissions.Family)).Single();
    }

    private static (int, string) Count(AloneRows rows) => ContinuationAdmissions.CountOf(rows) is var count ? (count.N, count.Known) : default;

    private static (int, string, string?) Known(AloneRows rows) => ContinuationAdmissions.CountOf(rows) is var count ? (count.N, count.Known, count.Why) : default;
}
