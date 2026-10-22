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

    /// <summary>
    /// SESSWATCH1a (D200, the session-watch design §2.3): the person's *End and carry on* ends a quiet run as their stop released in the
    /// same press, and the words they wrote to it go to the part that carries it on, never to a resume of the run that hung. Without the
    /// press's note, the same stopped and released record with words waiting goes on in itself, as before.
    /// </summary>
    [Fact]
    public void Words_on_a_run_the_person_ended_with_end_and_carry_on_go_to_the_carry_on_never_a_resume()
    {
        var pressed = Written("stopped") with { NoteParts = Observation.QuietPressed(31).Parts };
        var config = Config().WithReleased("q1", "s1");
        Snapshot With(PriorSession last) => new([Quest("q1", "Taken")], [Repo], [])
        {
            LastRun = new Dictionary<string, PriorSession> { ["q1"] = last },
        };

        var carried = Assert.Single(Planner.Plan(With(pressed), config), each => each.Quest.Id == "q1");

        Assert.Equal(StartVerdict.Start, carried.Verdict);
        Assert.False(carried.GoesOn);
        Assert.Equal(pressed, carried.Resumes);

        var wrote = Assert.Single(Planner.Plan(With(Written("stopped")), config), each => each.Quest.Id == "q1");
        Assert.True(wrote.GoesOn);
    }

    /// <summary>
    /// 🔴 The owner's case of 2026-10-15: a quest that once waited on another repository's answer and then closed still names
    /// the question it waited on (`awaits`), and the person's words to its last session sat unplanned. A closed quest waits on
    /// nothing: the words go on in that session as any closed quest's do. A taken quest waiting on its question still waits.
    /// </summary>
    [Fact]
    public void Words_to_a_session_whose_closed_quest_still_names_an_old_question_go_on()
    {
        var record = Written("completed");
        var snapshot = new Snapshot([], [Repo], [])
        {
            Closed = [Quest("q1", "Done") with { Awaits = "q0" }],
            LastRun = new Dictionary<string, PriorSession> { ["q1"] = record },
        };

        var only = Assert.Single(Planner.Plan(snapshot, Config()));

        Assert.Equal((StartVerdict.Start, true), (only.Verdict, only.GoesOn));
        Assert.Equal(record, only.Resumes);
    }

    /// <summary>
    /// 🔴 The same case at the start: planned to go on, the words were then sent down the open door, because the start read the
    /// closed quest's old question as a wait and skipped the record's own continuation, and the service refused a session on a
    /// done quest. The quest itself names no question unless it is taken, so no reader can take a closed quest for one waiting.
    /// </summary>
    [Fact]
    public void A_quest_names_the_question_it_waits_on_only_while_taken()
    {
        Assert.Equal("q0", (Quest("q1", "Taken") with { Awaits = "q0" }).Awaits);
        Assert.Null((Quest("q1", "Done") with { Awaits = "q0" }).Awaits);
        Assert.Null((Quest("q1", "Declined") with { Awaits = "q0" }).Awaits);
        Assert.Null((Quest("q1", "Open") with { Awaits = "q0" }).Awaits);
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

    /// <summary>
    /// 🔴 SESSCTX1c (D189's amendment): a driven record whose resume found its conversation held by another client waits for the
    /// person, its words marked <c>elsewhere</c>. Its quest is not carried on in a new session meanwhile, as a failed record's
    /// otherwise is (D80), since that would be the move the hold refuses; a word said since tries it again. A mark of any other
    /// reason leaves the carry-on as it was.
    /// </summary>
    [Fact]
    public void A_record_held_because_another_client_holds_its_conversation_is_not_carried_on_meanwhile()
    {
        var elsewhere = new GoOnMark(["w1"], ContinueWhy.Elsewhere, DateTimeOffset.Parse("2026-10-03T09:05:00Z"));
        var snapshot = new Snapshot([Quest("q1", "Taken")], [Repo], [])
        {
            LastRun = new Dictionary<string, PriorSession> { ["q1"] = Written("failed") },
            Unable = new Dictionary<string, GoOnMark> { ["s1"] = elsewhere },
        };

        Assert.Empty(Planner.Plan(snapshot, Config()));

        var later = Word with { Id = "w2", Text = "It is closed there now." };
        var again = snapshot with { LastRun = new Dictionary<string, PriorSession> { ["q1"] = Written("failed", "s1", Word, later) } };
        Assert.True(Assert.Single(Planner.Plan(again, Config())).GoesOn);

        var twice = snapshot with { Unable = new Dictionary<string, GoOnMark> { ["s1"] = elsewhere with { Why = GoOnMarks.FailedTwice } } };
        var carried = Assert.Single(Planner.Plan(twice, Config()));
        Assert.Equal((StartVerdict.Start, false, "s1"), (carried.Verdict, carried.GoesOn, carried.Resumes?.Session));
    }

    /// <summary>
    /// 🔴 COWORK1e (D188 point 4; the message-urgency design §2.3): a record whose answers the answers-alone bound holds for the person
    /// neither goes on with them nor has its quest carried on in a new session meanwhile, which would take the line on with nobody
    /// between; so with answers the person kept (*Not now*). A word of theirs said since tries it again, the answers beside it.
    /// </summary>
    [Theory]
    [InlineData(GoOnMarks.AnswersAlone)]
    [InlineData(GoOnMarks.NotNow)]
    public void A_record_whose_answers_wait_for_the_person_neither_goes_on_nor_is_carried_on(string why)
    {
        var answer = Word with { Helper = "h1", Text = "Another agent answered." };
        var held = new GoOnMark(["w1"], why, DateTimeOffset.Parse("2026-10-21T09:05:00Z"));
        var snapshot = new Snapshot([Quest("q1", "Taken")], [Repo], [])
        {
            LastRun = new Dictionary<string, PriorSession> { ["q1"] = Written("failed", "s1", answer) },
            Unable = new Dictionary<string, GoOnMark> { ["s1"] = held },
        };

        Assert.Empty(Planner.Plan(snapshot, Config()));

        var later = Word with { Id = "w2", Text = "Go on with it." };
        var again = snapshot with { LastRun = new Dictionary<string, PriorSession> { ["q1"] = Written("failed", "s1", answer, later) } };
        var goesOn = Assert.Single(Planner.Plan(again, Config()));
        Assert.Equal((true, 2), (goesOn.GoesOn, goesOn.Resumes!.Waiting.Count));
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
    /// 🔴 SAYHELD1, the owner's case on the install (2026-10-21): a quest waited on another repository's answer, was resumed in a
    /// new session once it came, and that session parked to ask the person. The service keeps naming the question the quest waited
    /// on for as long as it is taken, so the person's answer to the park was read as words waiting on the question: the quest went
    /// to its resume again, which the park itself held as a session working on it, and nothing went on. The answer goes on in the
    /// park's own conversation, as any answered park's does; its question was answered before it began.
    /// </summary>
    [Fact]
    public void An_answered_park_whose_quest_still_names_its_answered_question_goes_on()
    {
        var park = new PriorSession("s2", "D:/trees/s-1", "awaiting-person", "May I write to production?", "Game", Answer: "Use staging.")
        {
            Said = [Word with { Id = "w2", Text = "Use staging.", Reopens = false }],
        };
        var snapshot = new Snapshot(
            [Quest("q1", "Taken") with { Awaits = "q0" }], [Repo],
            [new SessionView("s2", "Game", "awaiting-person") { Tree = "D:/trees/s-1", Quest = "q1" }])
        {
            LastRun = new Dictionary<string, PriorSession> { ["q1"] = park },
        };

        var only = Assert.Single(Planner.Plan(snapshot, Config(cap: 1)));

        Assert.Equal((StartVerdict.Start, true, "s2"), (only.Verdict, only.GoesOn, only.Resumes?.Session));
        Assert.Equal("carrying on in `Game` — you answered session `s2`: Use staging.", only.Reason);
    }

    /// <summary>
    /// SAYHELD1: words to the session that asked wait with its quest while the question is open (D79, MSG1b), and once it is answered
    /// they go with the resume, which opens a new session handed the answer, never back into the asker's own conversation, which
    /// would not be. A park on a question still open waits with it too.
    /// </summary>
    [Fact]
    public void Words_to_the_session_that_asked_wait_while_its_question_is_open_and_go_with_its_resume()
    {
        var question = new QuestView("q0", "Game", "Backend", "What does it take?", "The contract.", "Open");
        var waiting = new Snapshot([Quest("q1", "Taken") with { Awaits = "q0" }, question], [Repo], [])
        {
            LastRun = new Dictionary<string, PriorSession> { ["q1"] = Written("completed") },
        };

        var waits = Assert.Single(Planner.Plan(waiting, Config()), c => c.Quest.Id == "q1");
        Assert.Equal((StartVerdict.Waiting, false), (waits.Verdict, waits.GoesOn));

        var answered = waiting with { Quests = [Quest("q1", "Taken") with { Awaits = "q0" }] };
        var resumes = Assert.Single(Planner.Plan(answered, Config()));
        Assert.Equal((StartVerdict.Start, false, "s1"), (resumes.Verdict, resumes.GoesOn, resumes.Resumes?.Session));
        Assert.Equal("w1", Assert.Single(resumes.Resumes!.Waiting).Id);
        Assert.Equal("resuming in `Game` — `#q0` is answered.", resumes.Reason);

        var parked = waiting with
        {
            LastRun = new Dictionary<string, PriorSession> { ["q1"] = Written("awaiting-person") with { Said = [Word with { Reopens = false }] } },
        };
        Assert.False(Assert.Single(Planner.Plan(parked, Config()), c => c.Quest.Id == "q1").GoesOn);
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
    /// 🔴 SAIDBACK1a, the owner's case of 2026-10-15: a closed quest's last session held two out-of-date words for seven hours,
    /// planned going on at every look. Once both are taken back, the record the service answers holds an empty <c>said</c> and no
    /// <c>answer</c>: nothing waits, so the closed quest is not carried beside the open ones and nothing goes on.
    /// </summary>
    [Fact]
    public void A_closed_quests_last_session_whose_words_were_taken_back_is_not_gone_on_with()
    {
        const string Quests = """[{ "id": "q1", "from": "Asker", "to": "Game", "title": "One", "body": "", "status": "Done" }]""";
        var waiting = ServiceClient.ReadLastRun("""
            [{ "id": "s1", "quest": "q1", "state": "completed", "answer": "Use the blue one.\n\nAnd the old port.",
               "said": [{ "id": "w1", "text": "Use the blue one.", "at": "2026-10-15T01:00:00Z", "files": [], "reopens": true },
                        { "id": "w2", "text": "And the old port.", "at": "2026-10-15T01:01:00Z", "files": [], "reopens": true }],
               "created": "2026-10-14T18:00:00Z" }]
            """);
        var takenBack = ServiceClient.ReadLastRun("""
            [{ "id": "s1", "quest": "q1", "state": "completed", "said": [], "created": "2026-10-14T18:00:00Z" }]
            """);

        Assert.Single(Planner.Plan(Snapshot(waiting), Config()), c => c.GoesOn);
        Assert.False(takenBack["q1"].WordsWaiting);
        Assert.Empty(ServiceClient.ReadClosed(Quests, takenBack, open: []));
        Assert.DoesNotContain(Planner.Plan(Snapshot(takenBack), Config()), c => c.GoesOn || c.Verdict == StartVerdict.Start);

        static Snapshot Snapshot(IReadOnlyDictionary<string, PriorSession> last) => new([], [Repo], [])
        {
            Closed = ServiceClient.ReadClosed(Quests, last, open: []),
            LastRun = last,
        };
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

    // ——— FABRIC-CLOSE1c9: the session a reopen names, when a later session is the quest's last here.

    /// <summary>A taken quest whose last session here (s2) ended with nothing waiting, and whose reopen names s1, told its words.</summary>
    private static Snapshot NamedByReopen(PriorSession? last = null, PriorSession? named = null) =>
        new([Quest("q1", "Taken")], [Repo], [])
        {
            LastRun = new Dictionary<string, PriorSession>
            {
                ["q1"] = last ?? Written("completed", "s2") with { Said = [], Answer = null, Tree = "D:/trees/s-2" },
            },
            NamedByReopen = new Dictionary<string, PriorSession> { ["q1"] = named ?? Written("completed") },
        };

    /// <summary>
    /// 🔴 The reopen's words go on in the session that said the reopened ready, though a later session ran on the quest: the
    /// planner's go-on read only the quest's last record, so the words told to it waited for good.
    /// </summary>
    [Fact]
    public void Words_on_the_session_a_reopen_names_go_on_in_it_though_a_later_session_is_the_quests_last()
    {
        var snapshot = NamedByReopen();

        var only = Assert.Single(Planner.Plan(snapshot, Config()));

        Assert.Equal((StartVerdict.Start, true, "s1"), (only.Verdict, only.GoesOn, only.Resumes?.Session));
        Assert.Equal("going on in `Game` — you wrote to session `s1`: Also log the port.", only.Reason);
    }

    /// <summary>The named session's words wait on a pause as the last session's do: the quest is planned, and the pause holds it.</summary>
    [Fact]
    public void The_named_sessions_words_are_held_by_a_pause()
    {
        var paused = NamedByReopen() with { Paused = new Dictionary<string, PausedBy> { ["q1"] = new(WorkScope.Quest, "q1") } };

        Assert.Equal(StartVerdict.Paused, Assert.Single(Planner.Plan(paused, Config())).Verdict);
    }

    /// <summary>
    /// The person's stop of the quest's last session still holds the quest: the reopen's words are Daoris's, and release no stop
    /// the person made on another session (D173's FABRIC-CLOSE1c9 note).
    /// </summary>
    [Fact]
    public void The_persons_stop_of_the_last_session_holds_the_named_sessions_words()
    {
        var stopped = NamedByReopen(last: Written("stopped", "s2") with { Said = [], Answer = null, Note = "the person stopped it." });

        var only = Assert.Single(Planner.Plan(stopped, Config()));

        Assert.Equal((StartVerdict.Stopped, null), (only.Verdict, only.Resumes?.Session));
    }

    /// <summary>Words on the named session it was found unable to go on with wait, as on the last one (MSG1f2): they are not tried again.</summary>
    [Fact]
    public void The_named_sessions_words_it_could_not_go_on_with_wait()
    {
        var mark = new GoOnMark(["w1"], ContinueWhy.Tree, DateTimeOffset.Parse("2026-10-03T09:05:00Z"));
        var judged = NamedByReopen() with { Unable = new Dictionary<string, GoOnMark> { ["s1"] = mark } };

        Assert.Empty(Planner.Plan(judged, Config()));
    }

    /// <summary>Words on both: the named session's go on first, and the last session's at a later look, once those were taken.</summary>
    [Fact]
    public void Words_on_the_named_session_go_on_before_the_last_sessions()
    {
        var both = NamedByReopen(last: Written("completed", "s2") with { Tree = "D:/trees/s-2" });

        Assert.Equal("s1", Assert.Single(Planner.Plan(both, Config())).Resumes?.Session);
        Assert.Equal(
            "s2",
            Assert.Single(Planner.Plan(both with { NamedByReopen = new Dictionary<string, PriorSession> { ["q1"] = Written("completed") with { Said = [], Answer = null } } }, Config())).Resumes?.Session);
    }
}
