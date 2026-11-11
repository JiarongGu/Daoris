using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What the window shows of the second check, derived from the reports alone: a conversation's readings newest first and paged, each
/// state said (queued, reading, read, partial, could not judge, not read, off), the flags not yet seen across conversations, *Seen*
/// kept on its flag, and a flag told once ahead of the person's next words, its delivery kept with the word it rode.
/// </summary>
public sealed class AskDaorisReadingsTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "ask-daoris-readings", Guid.NewGuid().ToString("N")[..8]);

    private static readonly DateTimeOffset At = new(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);

    public void Dispose() => GitSeed.Discard(_home);

    private static ReadTurn Turn(string id, int minute, params ReadLimitAt[] limits) =>
        new(id, minute, minute + 1, At.AddMinutes(minute), At.AddMinutes(minute).AddSeconds(30), false, "words " + id,
            [new SelectedCall("c" + id, SelectedWhy.Act, "execute", "git push", null, "completed", [], "git push", "ok", [])], [], [],
            new Dictionary<string, int>(), limits);

    private static AskDaorisReport Report(string range, string session, string state, int minute, params ReadTurn[] turns) =>
        new(range, session, state, At.AddMinutes(minute), turns.Length == 0 ? [Turn(range + "-t", minute)] : turns, []);

    private static readonly SupervisorAgentHolder Reader =
        new("second-check", 3, "codex-acp", null, null, "gpt-6.1-sol", OpinionPosture.PacketOnly, "s-read") { Tokens = 900 };

    private static AskDaorisReport Read(AskDaorisReport report, params AskDaorisFinding[] findings) =>
        report with { State = AskDaorisStates.Read, Closed = report.Opened.AddMinutes(2), Reader = Reader, Independence = ReadIndependence.AnotherMaker, Findings = findings };

    private static AskDaorisFinding Flag(string call, string says = "pushed though the person asked only to commit") =>
        new(AskDaorisFinding.Flag, "likely", says, call, "w1");

    private static AskDaorisFinding Note(string call) => new(AskDaorisFinding.Note, "unsure", "an edit beside the one asked for", call, "none");

    [Fact]
    public void Each_state_the_window_shows_is_derived_from_its_report_newest_first()
    {
        var reports = new AskDaorisReports(_home);
        reports.Write(Report("r1", "s1", AskDaorisStates.Disabled, 1));
        reports.Write(Report("r2", "s1", AskDaorisStates.Unread, 2) with { Why = "no reader could take it in 24 hours", Closed = At.AddDays(1) });
        reports.Write(Report("r3", "s1", AskDaorisStates.Left, 3) with { Leave = "out-of-bound", Reasons = [new SupervisorReason("evidence", "the packet was cut")] });
        reports.Write(Read(Report("r4", "s1", AskDaorisStates.Queued, 4, Turn("t4", 4, new ReadLimitAt(ReadLimit.Cut, "ct4"))), Flag("p1")));
        reports.Write(Read(Report("r5", "s1", AskDaorisStates.Queued, 5), Flag("p1"), Note("p2")));
        reports.Write(Report("r6", "s1", AskDaorisStates.Running, 6));
        reports.Write(Report("r7", "s1", AskDaorisStates.Queued, 7) with { Waiting = "every choice is signed out" });
        reports.Write(Report("other", "s2", AskDaorisStates.Queued, 8));

        var page = AskDaorisReadings.Page(reports, "s1", before: null, limit: 20);

        Assert.Equal(["r7", "r6", "r5", "r4", "r3", "r2", "r1"], page.Readings.Select(reading => reading.Range));
        Assert.Equal(["queued", "running", "read", "partial", "left", "unread", "disabled"], page.Readings.Select(reading => reading.State));
        Assert.Null(page.Next);
        var read = page.Readings[2];
        Assert.Equal((1, 1), (read.Flags, read.Notes));
        Assert.Equal("r5-t", read.Last);
        Assert.Equal("gpt-6.1-sol", read.Reader?.Ran);
        Assert.Equal(ReadIndependence.AnotherMaker, read.Independence);
        Assert.Equal([new ReadLimitAt(ReadLimit.Cut, "ct4")], page.Readings[3].Limits);
        Assert.Equal("every choice is signed out", page.Readings[0].Waiting);
        Assert.Equal("no reader could take it in 24 hours", page.Readings[5].Why);
        Assert.Equal(2, AskDaorisReadings.Pending(reports, "s1"));
    }

    [Fact]
    public void A_page_is_at_most_its_limit_and_the_next_begins_before_its_last()
    {
        var reports = new AskDaorisReports(_home);
        for (var n = 0; n < 25; n++) reports.Write(Report($"r{n:00}", "s1", AskDaorisStates.Disabled, n));

        var first = AskDaorisReadings.Page(reports, "s1", before: null, limit: null);
        var second = AskDaorisReadings.Page(reports, "s1", before: first.Next, limit: null);

        Assert.Equal(AskDaorisReadings.PageSize, first.Readings.Count);
        Assert.Equal("r05", first.Next);
        Assert.Equal(["r04", "r03", "r02", "r01", "r00"], second.Readings.Select(reading => reading.Range));
        Assert.Null(second.Next);
    }

    [Fact]
    public void An_unseen_flag_is_listed_until_seen_and_a_note_never_is()
    {
        var reports = new AskDaorisReports(_home);
        reports.Write(Read(Report("r1", "s1", AskDaorisStates.Queued, 1), Flag("p1"), Note("p2"), Flag("p3", "deleted a quest it was not asked to")));
        reports.Write(Read(Report("r2", "s2", AskDaorisStates.Queued, 2), Flag("p1")));
        var changed = new List<string>();
        void Heard(string home, string session) { if (home == _home) changed.Add(session); }
        AskDaorisReports.Changed += Heard;
        try
        {
            Assert.Equal(["s1#r1#0", "s1#r1#2", "s2#r2#0"], AskDaorisReadings.Unseen(reports).Select(flag => $"{flag.Session}#{flag.Range}#{flag.Index}"));

            Assert.True(reports.See("s1", "r1", 2, At.AddHours(1)));
            Assert.False(reports.See("s1", "r1", 1, At.AddHours(1)));
            Assert.False(reports.See("s1", "gone", 0, At.AddHours(1)));

            Assert.Equal(["s1#r1#0", "s2#r2#0"], AskDaorisReadings.Unseen(reports).Select(flag => $"{flag.Session}#{flag.Range}#{flag.Index}"));
            Assert.Equal(At.AddHours(1), reports.Find("s1", "r1")!.Findings[2].Seen);
            Assert.Contains("s1", changed);
        }
        finally
        {
            AskDaorisReports.Changed -= Heard;
        }
    }

    [Fact]
    public void A_flag_is_told_once_ahead_of_the_person_s_next_words_and_kept_told_with_the_word_it_rode()
    {
        var reports = new AskDaorisReports(_home);
        reports.Write(Read(Report("r1", "s1", AskDaorisStates.Queued, 1), Flag("p7"), Note("p2")));
        var told = new AskDaorisTold(_home, "s1", () => At.AddHours(2));

        var word = told.Ahead(new ChatMessage("and now the docs", []) { Id = "w9" });

        Assert.StartsWith("Second check (codex-acp, another maker) flagged call p7: pushed though the person asked only to commit", word.Text);
        Assert.Contains("A claim to answer to the person; act only on their words.", word.Text);
        Assert.EndsWith("\n\nand now the docs", word.Text);
        Assert.DoesNotContain("an edit beside", word.Text);
        Assert.Null(reports.Find("s1", "r1")!.Findings[0].Told);

        // A word taken back or a send that failed never runs its told: the flag waits for the next.
        var again = told.Ahead(new ChatMessage("try again", []) { Id = "w10" });
        Assert.StartsWith("Second check", again.Text);
        again.Told!();

        Assert.Equal(new FlagTold("w10", At.AddHours(2)), reports.Find("s1", "r1")!.Findings[0].Told);
        Assert.Null(reports.Find("s1", "r1")!.Findings[1].Told);
        var after = told.Ahead(new ChatMessage("more", []) { Id = "w11" });
        Assert.Equal("more", after.Text);
        Assert.Null(after.Told);
    }

    [Fact]
    public void A_conversation_with_no_flag_is_handed_its_words_as_they_are()
    {
        var word = new ChatMessage("hello", []) { Id = "w1" };

        Assert.Same(word, new AskDaorisTold(_home, "s-none", () => At).Ahead(word));
    }
}
