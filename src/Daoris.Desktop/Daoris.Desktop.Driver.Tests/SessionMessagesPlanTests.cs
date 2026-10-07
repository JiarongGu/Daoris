using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The planner's half of words going on in their session (MSG1b, D137 §2.2): a record of this machine's with the person's
/// words waiting, parked or ended, is planned `continuing` from itself, whatever its quest's state. Whatever holds a start
/// holds it (a pause, the person's hold, the cap), and it goes before every start the driver planned itself. Pure, so the
/// fast half.
/// </summary>
public sealed class SessionMessagesPlanTests
{
    private static readonly RepoView Repo = new("Game", true, "D:/fam/Game");

    private static QuestView Quest(string id, string status) => new(id, "Asker", "Game", $"Ask {id}", "Here is why.", status);

    private static readonly SaidWordView Word =
        new("w1", "Also log the port.", DateTimeOffset.Parse("2026-10-03T09:00:00Z"), [], Reopens: true);

    /// <summary>An ended record of this machine's that the person wrote to.</summary>
    private static PriorSession Written(string state, string session = "s1", params SaidWordView[] said) =>
        new(session, "D:/trees/s-1", state, "the quest reached done.", "Game",
            Answer: string.Join("\n\n", (said.Length == 0 ? [Word] : said).Select(word => word.Text)))
        {
            Said = said.Length == 0 ? [Word] : said,
            Adapter = "claude-code-acp",
        };

    private static DriverConfig Config(int cap = 2) => DriverConfig.Empty with { Drivable = ["Game"], Trees = ["Game"], Cap = cap };

    /// <summary>
    /// 🔴 The owner's case: a session in *To review* the person writes to goes on in its own record. Its quest closed done, so
    /// it is on no open list: the snapshot carries it beside them, and the plan starts it from the record itself.
    /// </summary>
    [Fact]
    public void Words_to_a_session_whose_quest_closed_go_on_in_that_session()
    {
        var record = Written("completed");
        var snapshot = new Snapshot([], [Repo], [])
        {
            Closed = [Quest("q1", "Done")],
            LastRun = new Dictionary<string, PriorSession> { ["q1"] = record },
        };

        var only = Assert.Single(Planner.Plan(snapshot, Config()));

        Assert.Equal((StartVerdict.Start, true), (only.Verdict, only.GoesOn));
        Assert.Equal(record, only.Resumes);
        Assert.Equal("going on in `Game` — you wrote to session `s1`: Also log the port.", only.Reason);
    }

    /// <summary>A failed record the person writes to goes on in itself, where without words it would be carried on in a new one.</summary>
    [Fact]
    public void Words_to_a_failed_session_go_on_in_it_rather_than_a_carry_on()
    {
        var snapshot = new Snapshot([Quest("q1", "Taken")], [Repo], [])
        {
            LastRun = new Dictionary<string, PriorSession> { ["q1"] = Written("failed") },
        };

        var only = Assert.Single(Planner.Plan(snapshot, Config()));

        Assert.True(only.GoesOn);
        Assert.Equal("s1", only.Resumes?.Session);
    }

    /// <summary>Without words the same failed record is today's carry-on, not going on.</summary>
    [Fact]
    public void A_failed_session_nobody_wrote_to_is_carried_on_as_before()
    {
        var snapshot = new Snapshot([Quest("q1", "Taken")], [Repo], [])
        {
            LastRun = new Dictionary<string, PriorSession> { ["q1"] = Written("failed") with { Said = [], Answer = null } },
        };

        var only = Assert.Single(Planner.Plan(snapshot, Config()));

        Assert.False(only.GoesOn);
        Assert.StartsWith("carrying on in `Game` — session `s1` was cut off", only.Reason);
    }

    /// <summary>An open quest's last session, written to after it ended, goes on too (D137 §2.2).</summary>
    [Fact]
    public void Words_to_the_last_session_of_an_open_quest_go_on_in_it()
    {
        var snapshot = new Snapshot([Quest("q1", "Open")], [Repo], [])
        {
            LastRun = new Dictionary<string, PriorSession> { ["q1"] = Written("failed") },
        };

        var only = Assert.Single(Planner.Plan(snapshot, Config()));

        Assert.Equal((StartVerdict.Start, true, "s1"), (only.Verdict, only.GoesOn, only.Resumes?.Session));
    }

    /// <summary>
    /// 🔴 Words to a session the person stopped release the stop's hold, as *Try again* does (D137 §2.2): the same session goes
    /// on with them, where without words the quest would wait for *Try again*.
    /// </summary>
    [Fact]
    public void Words_to_a_session_the_person_stopped_are_not_held_by_the_stop()
    {
        var stopped = Written("stopped") with { Note = "the person stopped it." };
        var snapshot = new Snapshot([Quest("q1", "Taken")], [Repo], [])
        {
            LastRun = new Dictionary<string, PriorSession> { ["q1"] = stopped },
        };

        Assert.Equal(StartVerdict.Start, Assert.Single(Planner.Plan(snapshot, Config())).Verdict);
        Assert.Equal(
            StartVerdict.Stopped,
            Assert.Single(Planner.Plan(snapshot with { LastRun = new Dictionary<string, PriorSession> { ["q1"] = stopped with { Said = [], Answer = null } } }, Config())).Verdict);
    }

    /// <summary>The person's words are their *Try again*: the strikes that parked a quest never hold words to its last session.</summary>
    [Fact]
    public void The_strikes_never_hold_words_to_a_session()
    {
        var snapshot = new Snapshot([Quest("q1", "Taken")], [Repo], [], new Dictionary<string, int> { ["q1"] = 3 })
        {
            LastRun = new Dictionary<string, PriorSession> { ["q1"] = Written("failed") },
        };

        Assert.Equal(StartVerdict.Start, Assert.Single(Planner.Plan(snapshot, Config() with { Strikes = 3 })).Verdict);
    }

    /// <summary>
    /// A reopen goes before every start the driver planned itself (D137 §2.2): with one slot, the record written to takes it
    /// and the oldest open quest waits, though the service lists that one first.
    /// </summary>
    [Fact]
    public void A_reopen_goes_before_every_start_the_driver_planned_itself()
    {
        var snapshot = new Snapshot([Quest("q2", "Open")], [Repo], [])
        {
            Closed = [Quest("q1", "Done")],
            LastRun = new Dictionary<string, PriorSession> { ["q1"] = Written("completed") },
        };

        var plan = Planner.Plan(snapshot, Config(cap: 1));

        Assert.Equal(["q1", "q2"], plan.Select(c => c.Quest.Id));
        Assert.Equal([StartVerdict.Start, StartVerdict.AtCapacity], plan.Select(c => c.Verdict));
    }

    /// <summary>An ended record holds no slot, so going on takes one, and a spent cap holds it, said.</summary>
    [Fact]
    public void An_ended_record_going_on_takes_a_slot_of_the_cap()
    {
        var other = new SessionView("s9", "Engine", "working") { Tree = "D:/trees/s-9", Quest = "q9" };
        var snapshot = new Snapshot([], [Repo], [other])
        {
            Closed = [Quest("q1", "Done")],
            LastRun = new Dictionary<string, PriorSession> { ["q1"] = Written("completed") },
        };

        Assert.Equal(StartVerdict.AtCapacity, Assert.Single(Planner.Plan(snapshot, Config(cap: 1))).Verdict);
    }

    /// <summary>Whatever holds a start holds a reopen (D137 §2.2): the work's pause, and the person's hold on the repository.</summary>
    [Fact]
    public void A_pause_and_a_hold_each_hold_a_reopen()
    {
        var snapshot = new Snapshot([], [Repo], [])
        {
            Closed = [Quest("q1", "Done")],
            LastRun = new Dictionary<string, PriorSession> { ["q1"] = Written("completed") },
        };

        Assert.Equal(StartVerdict.Held, Assert.Single(Planner.Plan(snapshot, Config() with { Holds = ["Game"] })).Verdict);
        Assert.Equal(
            StartVerdict.Paused,
            Assert.Single(Planner.Plan(snapshot with { Paused = new Dictionary<string, PausedBy> { ["q1"] = new(WorkScope.Quest, "q1") } }, Config())).Verdict);
    }

    /// <summary>
    /// 🔴 Words a reopen could not take (a closed quest's, a never) are marked, and a later look does not try them again
    /// (MSG1a's open point); a word said after the mark is tried, since what held it may have moved.
    /// </summary>
    [Fact]
    public void Words_already_judged_unable_to_go_on_are_not_tried_again_until_more_are_said()
    {
        var mark = new GoOnMark(["w1"], ContinueWhy.Tree, DateTimeOffset.Parse("2026-10-03T09:05:00Z"));
        var snapshot = new Snapshot([], [Repo], [])
        {
            Closed = [Quest("q1", "Done")],
            LastRun = new Dictionary<string, PriorSession> { ["q1"] = Written("completed") },
            Unable = new Dictionary<string, GoOnMark> { ["s1"] = mark },
        };

        Assert.Empty(Planner.Plan(snapshot, Config()));

        var later = Word with { Id = "w2", Text = "The tree is back now." };
        var again = snapshot with { LastRun = new Dictionary<string, PriorSession> { ["q1"] = Written("completed", "s1", Word, later) } };
        Assert.True(Assert.Single(Planner.Plan(again, Config())).GoesOn);
    }

    /// <summary>A parked record's answer is planned as before, saying the answer (ANSWER1a), and goes on through the same verdict.</summary>
    [Fact]
    public void An_answered_park_still_goes_on_saying_the_answer()
    {
        var park = new PriorSession("s1", "D:/trees/s-1", "awaiting-person", "Which port?", "Game", Answer: "Port 8080.")
        {
            Said = [Word with { Text = "Port 8080.", Reopens = false }],
        };
        var snapshot = new Snapshot([Quest("q1", "Taken")], [Repo], [new SessionView("s1", "Game", "awaiting-person") { Tree = "D:/trees/s-1", Quest = "q1" }])
        {
            LastRun = new Dictionary<string, PriorSession> { ["q1"] = park },
        };

        var only = Assert.Single(Planner.Plan(snapshot, Config(cap: 1)));

        Assert.Equal((StartVerdict.Start, true), (only.Verdict, only.GoesOn));
        Assert.Equal("carrying on in `Game` — you answered session `s1`: Port 8080.", only.Reason);
    }

    /// <summary>
    /// 🔴 ANSWER2, the owner's case on the install: a resumed run moves its record to working, and takes the person's words
    /// off it only as it concludes, so for the whole run the record works with the words still on it. A look meanwhile does not
    /// plan it to go on a second time, whose move to working the ledger refuses (working → working) and whose failure failed
    /// the session the person had answered: the record runs, and its quest is the running session's as any working one's is.
    /// </summary>
    [Theory]
    [InlineData("Taken")]
    [InlineData("Open")]
    [InlineData("Done")]
    public void A_record_already_going_on_with_the_words_is_not_planned_to_go_on_again(string status)
    {
        var going = new PriorSession("s1", "D:/trees/s-1", "working", Continuations.Working, "Game", Answer: "Port 8080.")
        {
            Said = [Word with { Text = "Port 8080.", Reopens = false }],
            Adapter = "claude-code-acp",
        };
        var quest = Quest("q1", status);
        var snapshot = new Snapshot(
            status == "Done" ? [] : [quest], [Repo], [new SessionView("s1", "Game", "working") { Tree = "D:/trees/s-1", Quest = "q1" }])
        {
            Closed = status == "Done" ? [quest] : [],
            LastRun = new Dictionary<string, PriorSession> { ["q1"] = going },
        };

        var plan = Planner.Plan(snapshot, Config());

        Assert.DoesNotContain(plan, c => c.Verdict == StartVerdict.Start || c.GoesOn);
        if (status == "Open")
        {
            Assert.Equal((StartVerdict.RepositoryBusy, "session `s1` is already working on `#q1`."), (Assert.Single(plan).Verdict, plan[0].Reason));
        }

        Assert.False(going.WordsWaiting);
    }

    /// <summary>
    /// ANSWER2: a record running with the person's words on it holds no closed quest beside the open ones, since nothing waits
    /// to go on there; once it ends with them still on it (a refusal on the wire sends it back), they wait again.
    /// </summary>
    [Fact]
    public void A_closed_quest_whose_session_runs_with_the_words_is_not_carried_beside_the_open_ones()
    {
        var last = new Dictionary<string, PriorSession> { ["q1"] = Written("working"), ["q2"] = Written("completed", "s2") };

        var closed = ServiceClient.ReadClosed("""
            [{ "id": "q1", "from": "Asker", "to": "Game", "title": "One", "body": "", "status": "Done" },
             { "id": "q2", "from": "Asker", "to": "Game", "title": "Two", "body": "", "status": "Done" }]
            """, last, open: []);

        Assert.Equal(["q2"], closed.Select(quest => quest.Id));
        Assert.True(Written("completed").WordsWaiting);
        Assert.All(new[] { "queued", "starting", "working" }, state => Assert.False(Written(state).WordsWaiting));
    }

    /// <summary>
    /// The last run reads the words waiting on each record, one by one with their ids, and an intake's ask; a host from before
    /// <c>said</c> answers none, which is not the same as nothing waiting.
    /// </summary>
    [Fact]
    public void The_last_run_reads_the_words_waiting_on_each_record()
    {
        var last = ServiceClient.ReadLastRun("""
            [{ "id": "s1", "quest": "q1", "state": "completed", "answer": "Also log the port.",
               "said": [{ "id": "w1", "text": "Also log the port.", "at": "2026-10-03T09:00:00Z", "files": ["log.txt"], "reopens": true }],
               "created": "2026-10-03T08:00:00Z" },
             { "id": "s2", "quest": "q2", "state": "completed", "said": [], "created": "2026-10-03T08:00:00Z" },
             { "id": "s3", "quest": "q3", "state": "awaiting-person", "answer": "Port 8080.", "created": "2026-10-03T08:00:00Z" }]
            """);

        var said = Assert.Single(last["q1"].Said!);
        Assert.Equal(("w1", "Also log the port.", true), (said.Id, said.Text, said.Reopens));
        Assert.Equal(["log.txt"], said.Files);
        Assert.Equal(DateTimeOffset.Parse("2026-10-03T09:00:00Z"), said.At);
        Assert.True(last["q1"].WordsWaiting);
        Assert.Equal((false, 0), (last["q2"].WordsWaiting, last["q2"].Said!.Count));
        Assert.Null(last["q3"].Said);
        Assert.True(last["q3"].WordsWaiting);
    }

    /// <summary>
    /// The snapshot carries the closed quests whose last session here has words waiting, read from every quest the service
    /// holds: an open one is already planned, and a closed one with nothing waiting is not looked at.
    /// </summary>
    [Fact]
    public void The_snapshot_carries_closed_quests_whose_last_session_has_words_waiting()
    {
        var last = new Dictionary<string, PriorSession>
        {
            ["q1"] = Written("completed"),
            ["q2"] = Written("completed", "s2") with { Said = [], Answer = null },
            ["q3"] = Written("failed", "s3"),
        };

        var closed = ServiceClient.ReadClosed("""
            [{ "id": "q1", "from": "Asker", "to": "Game", "title": "One", "body": "", "status": "Done" },
             { "id": "q2", "from": "Asker", "to": "Game", "title": "Two", "body": "", "status": "Done" },
             { "id": "q3", "from": "Asker", "to": "Game", "title": "Three", "body": "", "status": "Taken" }]
            """, last, open: ["q3"]);

        Assert.Equal(["q1"], closed.Select(quest => quest.Id));
    }
}
