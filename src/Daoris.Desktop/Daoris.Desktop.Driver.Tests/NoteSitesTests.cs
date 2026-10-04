using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Every note site the driver writes, held to two things at once (LANG1a, D142 point 2; the language design §3–§4): its English
/// byte for byte as the record always kept it, which the terminal, the next prompt, an older page and the rehearsals read; and
/// its parts, each line's code with its values, each coded part's text inside that English.
/// </summary>
public sealed class NoteSitesTests
{
    private static void Wrote(SessionConclusion conclusion, string state, string note, params string?[] codes)
    {
        Assert.Equal(state, conclusion.State);
        Assert.Equal(note, conclusion.Note);
        NoteAssert.Holds(conclusion.Note, conclusion.Parts);
        Assert.Equal(codes, NoteAssert.Codes(conclusion.Parts));
    }

    // ——— A session's end (Observation): rows 1–17.

    [Fact]
    public void A_question_asked_of_another_repository_names_it_and_any_exit()
    {
        var clean = Observation.Conclude(0, "Taken", awaitsAfter: "q9");
        Wrote(clean, "completed",
            "asked `#q9` of another repository and waits for its answer — the quest resumes, in the same tree, once that is answered.",
            "ended.awaits");
        Assert.Equal("q9", clean.Parts![0].Value("awaits"));

        var messy = Observation.Conclude(2, "Taken", awaitsAfter: "q9");
        Wrote(messy, "completed",
            "asked `#q9` of another repository and waits for its answer — the quest resumes, in the same tree, once that is answered (exit 2).",
            "ended.awaits", "ended.exit");
        Assert.Equal(2, messy.Parts![1].Value("exit"));
    }

    [Fact]
    public void A_refused_turn_is_Daoris_s_lead_in_and_the_agent_s_own_words()
    {
        var taken = Observation.Conclude(0, "Taken", turnFailed: "Internal error: Overloaded");
        Wrote(taken, "failed", "the agent's turn failed with the quest still taken: Internal error: Overloaded", "ended.turn-failed-taken", null);
        Assert.Equal(NoteBy.Agent, taken.Parts![1].By);

        Wrote(Observation.Conclude(0, "Open", turnFailed: "refused"), "failed",
            "the agent's turn failed before it took its quest: refused", "ended.turn-failed-open", null);
    }

    [Fact]
    public void A_park_quotes_the_agent_beneath_its_lead_in_or_points_at_the_transcript()
    {
        var quoted = Observation.Conclude(0, "Taken", took: true, lastWords: "Which branch should it land on?");
        Wrote(quoted, "awaiting-person", "It stopped with its quest still taken, to ask you:\n\nWhich branch should it land on?",
            "ended.parked-asked", null);
        Assert.Equal(("Which branch should it land on?", NoteBy.Agent), (quoted.Parts![1].Words, quoted.Parts[1].By));

        Wrote(Observation.Conclude(0, "Taken", took: true), "awaiting-person",
            "It stopped with its quest still taken, to ask you — what it needs is in the last words of its transcript.",
            "ended.parked-transcript");
    }

    [Fact]
    public void A_resume_or_a_carry_on_that_ended_still_taken_names_its_exit()
    {
        var answered = Observation.Conclude(3, "Taken", awaitsBefore: "q2");
        Wrote(answered, "failed", "resumed with `#q2` answered, and ended with the quest still taken (exit 3).", "ended.answered-unfinished");
        Assert.Equal(("q2", 3), ((string?)answered.Parts![0].Value("awaits"), (int?)answered.Parts[0].Value("exit")));

        Wrote(Observation.Conclude(1, "Taken", resumed: true), "failed",
            "carried the quest on, and ended with it still taken (exit 1).", "ended.carried-unfinished");
    }

    [Fact]
    public void Done_and_declined_keep_their_sentence_whole_and_say_a_bad_exit_beside_it()
    {
        Wrote(Observation.Conclude(0, "Done"), "completed", "the quest reached done.", "ended.done");
        Wrote(Observation.Conclude(4, "Done"), "completed", "the quest reached done (exit 4).", "ended.done", "ended.exit");
        Wrote(Observation.Conclude(0, "Declined"), "declined", "the session declined, with its reason on the quest.", "ended.declined");
        Wrote(Observation.Conclude(5, "Declined"), "declined", "the session declined (exit 5); the reason is on the quest.",
            "ended.declined", "ended.exit");
    }

    [Fact]
    public void A_stand_down_an_exit_with_the_quest_taken_and_an_untouched_quest_each_have_their_code()
    {
        Wrote(Observation.Conclude(0, "Taken"), "stood-down", "exited cleanly with the quest taken — someone else has it.", "ended.stood-down");
        Wrote(Observation.Conclude(2, "Taken"), "failed", "exit 2 with the quest still taken.", "ended.taken-exit");
        Wrote(Observation.Conclude(0, "Open"), "failed", "exited without touching its quest.", "ended.untouched");
        Wrote(Observation.Conclude(6, "Open"), "failed", "exit 6 before taking its quest.", "ended.untouched-exit");
    }

    [Fact]
    public void A_closed_quest_s_session_that_went_on_says_so()
    {
        Wrote(Observation.WentOn(0, "completed"), "completed", "it went on with your words and ended; its quest stays as it closed.", "ended.went-on");
        Wrote(Observation.WentOn(9, "completed"), "failed", "it went on with your words and exited 9; its quest stays as it closed.", "ended.went-on-exit");
    }

    // ——— The driver's own ends: rows 18–21, and a program's words.

    private static void Line(Noted noted, string note, params string?[] codes)
    {
        Assert.Equal(note, noted.Note);
        NoteAssert.Holds(noted);
        Assert.Equal(codes, NoteAssert.Codes(noted.Parts));
    }

    [Fact]
    public void The_driver_s_own_ends_each_have_their_code()
    {
        Line(Observation.TimedOut(30), "timed out after 30 minutes and was killed.", "ended.timeout");
        Assert.Equal(30, Observation.TimedOut(30).Parts[0].Value("minutes"));
        Line(Observation.Stopped, "the person stopped it.", "ended.stopped");
        Line(Observation.DriverClosed, "the driver was stopped while this ran; the session's process was ended with it.", "ended.driver-closed");
        Line(Daoris.Driver.Driver.LostClaimNoted, Daoris.Driver.Driver.LostClaim, "ended.lost-claim");

        // An exception's message is a program's words, shown as written: no code re-authors it (the design §2).
        var failed = Observation.Failure("the service refused moving session `s1` to working");
        Line(failed, "the service refused moving session `s1` to working", [null]);
        Assert.Equal(NoteBy.Program, failed.Parts[0].By);
    }

    // ——— An account's line, appended to a failure: rows 22–24.

    [Fact]
    public void A_cooling_account_s_line_carries_its_moment_and_why_by_code_and_names_no_account()
    {
        var until = new DateTimeOffset(2026, 10, 3, 9, 30, 0, TimeSpan.Zero);
        var entry = new CoolingEntry("claude-code", "work", until, Stated: true, Window: null, Seen: until, Session: "s1");
        var conclusion = Observation.Conclude(0, "Taken", turnFailed: "Usage limit reached")
            .Then(" ", CoolingWords.NoteOf(entry, TimeZoneInfo.Utc));

        Assert.Equal(
            "the agent's turn failed with the quest still taken: Usage limit reached "
            + CoolingWords.Note(entry, TimeZoneInfo.Utc),
            conclusion.Note);
        NoteAssert.Holds(conclusion.Note, conclusion.Parts);
        var cooling = conclusion.Parts![^1];
        Assert.Equal("account.cooling", cooling.Code);
        Assert.Equal(("2026-10-03T09:30:00Z", CoolingWhy.Stated), ((string?)cooling.Value("until"), (string?)cooling.Value("why")));
        Assert.DoesNotContain(cooling.Values, value => value.Value is string text && text.Contains("work", StringComparison.Ordinal));
    }

    [Fact]
    public void A_refused_credential_s_line_names_its_owner_as_a_value_and_its_account_only_in_the_English()
    {
        var named = Daoris.Driver.Driver.RefusedNote("claude-code", "work");
        Line(named,
            "Its provider refused the `claude-code` account `work` (401). Replace the key or sign in again — on Settings, or "
            + "`daoris agent` — and Daoris will start sessions on it again.",
            "account.refused");
        Assert.Equal("claude-code", Assert.Single(named.Parts[0].Values, value => value.Key == "owner").Value);
        Assert.Single(named.Parts[0].Values);

        Line(Daoris.Driver.Driver.RefusedNote("claude-code", null),
            "Its provider refused `claude-code`'s own sign-in (401). Replace the key or sign in again — on Settings, or "
            + "`daoris agent` — and Daoris will start sessions on it again.",
            "account.refused-own");
    }

    /// <summary>ROSTER1b: a refused sign-in's line names its owner as a value and its account, with its sign-in, only in the English.</summary>
    [Fact]
    public void A_refused_sign_in_s_line_names_its_owner_as_a_value_and_its_account_only_in_the_English()
    {
        var named = Daoris.Driver.Driver.SignedOutNote("claude-code", "work");
        Line(named,
            "The agent refused the `claude-code` account `work` for its sign-in, so it reads signed out and Daoris starts nothing "
            + "more on it until it is signed in: `daoris agent login claude-code --profile work`, or Agents → the agent's page → "
            + "Accounts.",
            "account.signed-out");
        Assert.Equal("claude-code", Assert.Single(named.Parts[0].Values, value => value.Key == "owner").Value);
        Assert.Single(named.Parts[0].Values);

        Line(Daoris.Driver.Driver.SignedOutNote("claude-code", null),
            "The agent refused `claude-code`'s own sign-in, so Daoris starts nothing more on it until you sign in again at your "
            + "terminal and read it again on the agent's page in Agents.",
            "account.signed-out-own");
    }

    // ——— While it starts and works: rows 25–38.

    [Fact]
    public void A_tree_s_opening_carries_its_branch_and_where_it_started_never_its_path()
    {
        var path = Path.Combine("data", "trees", "default", "engine", "s-1a2b3c4d");
        var opening = SessionTrees.OpeningOf(path, "daoris/s-1a2b3c4d", "`main` (the line set for `engine`)", "main", unrecorded: null);
        Line(opening,
            $"opened a session tree at {path} on `daoris/s-1a2b3c4d`, from `main` (the line set for `engine`) — a fresh tree holds "
            + "nothing git does not track: no installed dependencies, no build outputs. The repository's own setup cost is paid "
            + "here, and in exchange the root's uncommitted work holds nothing.",
            "started.tree");
        Assert.Equal(("daoris/s-1a2b3c4d", "main"), ((string?)opening.Parts[0].Value("branch"), (string?)opening.Parts[0].Value("basedOn")));
        Assert.DoesNotContain(opening.Parts[0].Values, value => value.Value is string text && text.Contains("trees", StringComparison.Ordinal));

        var unrecorded = SessionTrees.OpeningOf(path, "daoris/s-1a2b3c4d", "the root's HEAD (no canonical line is declared)", "HEAD", "access denied");
        Assert.EndsWith(
            " Daoris could not record where its branch started (access denied), so bringing it up to date will cut where its "
            + "work first differs from the line.",
            unrecorded.Note);
        NoteAssert.Holds(unrecorded);
        Assert.Equal(["started.tree", "started.tree-unrecorded", null], NoteAssert.Codes(unrecorded.Parts));
        Assert.Equal(NoteBy.Program, unrecorded.Parts[2].By);
    }

    private static QuestView Quest(string status = "Taken") => new("q1", "game", "engine", "Fix it", "Body", status);

    [Fact]
    public void A_resume_once_its_question_closed_says_which_and_in_which_tree()
    {
        var asked = new PriorSession("s0", "/t", "completed");
        Line(Daoris.Driver.Driver.StartingNote(null, Quest(), Quest() with { Id = "q2" }, null, false, asked, null, false, null)!,
            "resumes `#q1` now that `#q2` is answered.", "started.resumes-answered");
        var inTree = Daoris.Driver.Driver.StartingNote(null, Quest(), Quest() with { Id = "q2" }, "/t", false, asked, null, false, null)!;
        Line(inTree, "resumes `#q1` now that `#q2` is answered, in the tree session `s0` asked from.",
            "started.resumes-answered", "started.in-asking-tree");
        Assert.Equal(("q1", "q2"), ((string?)inTree.Parts[0].Value("quest"), (string?)inTree.Parts[0].Value("answered")));
        Assert.Equal("s0", inTree.Parts[1].Value("session"));
    }

    [Fact]
    public void A_carry_on_says_why_on_which_account_in_which_tree_and_why_its_words_did_not_go_on()
    {
        var cut = new PriorSession("s0", "/t", "failed");
        Line(Daoris.Driver.Driver.StartingNote(null, Quest(), null, null, true, cut, null, false, null)!,
            "carries `#q1` on after session `s0` was cut off.", "started.cut-off");

        var released = cut with { State = "stopped" };
        Line(Daoris.Driver.Driver.StartingNote(null, Quest(), null, "/t", true, released, null, true, null)!,
            "carries `#q1` on: you stopped session `s0`, and released it, on another account, in the tree it worked in.",
            "started.released", "started.other-account", "started.same-tree");

        var answered = new PriorSession("s0", "/t", "completed", Answer: "use main") { Adapter = "claude-code" };
        var fellBack = Daoris.Driver.Driver.StartingNote(
            null, Quest(), null, "/t", true, answered, answered with { State = "awaiting-person" }, false, ContinueWhy.Of(ContinueWhy.Elsewhere))!;
        Line(fellBack,
            "carries `#q1` on with your answer to session `s0`, in the tree it worked in. A new session, because its conversation "
            + "is open in another client of its agent.",
            "started.answer", "started.same-tree", "started.fell-back");
        Assert.Equal(("elsewhere", "claude-code"), ((string?)fellBack.Parts[2].Value("why"), (string?)fellBack.Parts[2].Value("agent")));

        var said = answered with { Said = [new SaidWordView("w1", "use main", DateTimeOffset.UnixEpoch, [], true)] };
        Line(Daoris.Driver.Driver.StartingNote(null, Quest("Open"), null, null, true, answered, said, false, null)!,
            "starts `#q1` with your words to session `s0`.", "started.words-open");
        Line(Daoris.Driver.Driver.StartingNote(null, Quest(), null, null, true, answered, said, false, null)!,
            "carries `#q1` on with your words to session `s0`.", "started.words-taken");

        Assert.Null(Daoris.Driver.Driver.StartingNote(null, Quest(), null, null, false, null, null, false, null));
    }

    [Fact]
    public void A_resumed_conversation_says_it_goes_on_while_it_works()
    {
        Line(Continuations.WorkingNoted, Continuations.Working, "working.resumes-answer");
        Line(Continuations.GoingOnNoted, Continuations.GoingOn, "working.goes-on");
    }

    // ——— An intake: rows 42–51.

    private static AskView Ask(string state = "Proposed", IReadOnlyList<string>? quests = null, string? note = null, string tier = "person") =>
        new("a1", "default", "Make it faster", state, tier) { Quests = quests ?? [], Note = note };

    [Fact]
    public void An_intake_s_end_names_its_ask_and_its_quests_as_values()
    {
        Wrote(IntakeObservation.Conclude(0, 0, null), "stood-down", "the ask it answered was deleted while it ran.", "intake.ask-deleted");
        Wrote(IntakeObservation.Conclude(1, 0, null), "stood-down", "the ask it answered was deleted while it ran (exit 1).",
            "intake.ask-deleted", "ended.exit");

        var published = IntakeObservation.Conclude(0, 0, Ask("Published", ["q1", "q2"], tier: IntakeObservation.ByIntake));
        Wrote(published, "completed", "published `#q1`, `#q2` onto ask `#a1`.", "intake.published");
        Assert.Equal(["q1", "q2"], (IEnumerable<string>)published.Parts![0].Value("quests")!);

        var closed = IntakeObservation.Conclude(0, 0, Ask("Closed", note: "not needed"));
        Wrote(closed, "stood-down", "ask `#a1` was closed while it ran (not needed).", "intake.ask-closed", null);
        Assert.Equal(NoteBy.Person, closed.Parts![1].By);

        Wrote(IntakeObservation.Conclude(0, 0, Ask("Published", ["q3"])), "stood-down",
            "ask `#a1` was answered while it ran — it became `#q3`.", "intake.ask-answered");
        Wrote(IntakeObservation.Conclude(0, 0, Ask(), "refused"), "failed",
            "the agent's turn failed before publishing anything onto ask `#a1`: refused", "intake.turn-failed", null);
        Wrote(IntakeObservation.Conclude(0, 0, Ask()), "awaiting-person",
            "published nothing: the declarations did not settle ask `#a1`, so it asks you rather than guess — its question ends "
            + "its transcript. `daoris-driver ask --publish a1 --to <repository>` answers it; `daoris-driver ask --close a1 "
            + "--reason \"…\"` ends it.",
            "intake.asks");
        Wrote(IntakeObservation.Conclude(4, 0, Ask()), "failed", "exit 4 before publishing anything onto ask `#a1`.", "intake.exit");
    }

    [Fact]
    public void A_parked_intake_the_person_settled_says_how()
    {
        Wrote(IntakeObservation.Answered(Ask("Published", ["q1"]))!, "completed", "the person answered ask `#a1` — it became `#q1`.",
            "intake.person-answered");
        Wrote(IntakeObservation.Answered(Ask("Closed", note: "done elsewhere"))!, "stopped", "the person closed ask `#a1`: done elsewhere",
            "intake.person-closed", null);
        Wrote(IntakeObservation.Deleted("a1"), "stopped", "the person deleted ask `#a1`, so there is nothing left for it to wait on.",
            "intake.person-deleted");
    }

    // ——— A stop: rows 52–63, and MSG1c's line.

    [Fact]
    public void A_pause_s_and_an_abandon_s_lines_name_their_work()
    {
        Line(new PausedBy(WorkScope.Ask, "a1").Noted, "paused with ask `#a1`.", "stopped.paused-ask");
        Line(new PausedBy(WorkScope.Quest, "q1").Noted, "paused with quest `#q1`.", "stopped.paused-quest");
        Assert.Equal("paused with quest `#q1`.", new PausedBy(WorkScope.Quest, "q1").Note);
        Line(WorkAbandoning.NoteOf(WorkScope.Ask, "a1"), "ask `#a1` abandoned.", "stopped.abandoned-ask");
        Line(WorkAbandoning.NoteOf(WorkScope.Quest, "q1"), "quest `#q1` abandoned.", "stopped.abandoned-quest");
        Assert.Equal("q1", WorkAbandoning.NoteOf(WorkScope.Quest, "q1").Parts[0].Value("quest"));
    }

    [Fact]
    public void A_checkpoint_move_an_orphan_and_a_conversation_s_ends_each_have_their_code()
    {
        Line(SessionMoves.ByThePersonNoted("completed"), "The person finished this at a checkpoint.", "stopped.checkpoint-finished");
        Line(SessionMoves.ByThePersonNoted("stopped"), "The person stopped this at a checkpoint.", "stopped.checkpoint-stopped");
        Line(SessionMoves.ByThePersonNoted("declined"), "The person moved this at a checkpoint.", "stopped.checkpoint-moved");
        Line(Orphans.Noted, Orphans.Note, "stopped.orphan");
        Line(ChatRunner.ClosedNoted, ChatRunner.ClosedNote, "chat.closed");
        Line(ChatRunner.EndedNote(stopped: true, notKept: false), "the person ended the conversation.", "chat.ended-by-person");
        Line(ChatRunner.EndedNote(stopped: false, notKept: false), "the conversation ended; its commits are its record.", "chat.ended");
        Line(ChatRunner.EndedNote(stopped: false, notKept: true), $"the conversation ended; its commits are its record. {ChatRunner.NotKept}",
            "chat.ended", "chat.not-kept");
    }

    // ——— What carries the parts: the client's move, a record read back, and a stop's request.

    [Fact]
    public async Task A_move_sends_the_parts_beside_the_note_and_a_note_without_them_sends_none()
    {
        var bodies = new List<string>();
        var standIn = new Recording(bodies);
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(standIn));

        await service.AdvanceAsync("s1", "stopped", Observation.Stopped);
        await service.AdvanceAsync("s1", "failed", note: "an older line");
        await service.AdvanceAsync("s1", "working");

        using (var coded = System.Text.Json.JsonDocument.Parse(bodies[0]))
        {
            Assert.Equal("the person stopped it.", coded.RootElement.GetProperty("note").GetString());
            Assert.Equal(["ended.stopped"], NoteAssert.Codes(NotePart.Read(coded.RootElement)));
        }

        Assert.DoesNotContain("noteParts", bodies[1]);
        Assert.DoesNotContain("noteParts", bodies[2]);
    }

    [Fact]
    public void A_record_read_back_carries_its_parts_and_one_from_before_carries_none()
    {
        const string records = """
            [
              {"id":"s1","repository":"engine","state":"failed","note":"exit 2 with the quest still taken.",
               "noteParts":[{"code":"ended.taken-exit","values":{"exit":2},"text":"exit 2 with the quest still taken."}]},
              {"id":"s2","repository":"engine","state":"failed","note":"an older line"}
            ]
            """;

        var coded = ServiceClient.ReadRecord(records, "s1")!;
        Assert.Equal(["ended.taken-exit"], NoteAssert.Codes(coded.NoteParts));
        Assert.Equal(2L, coded.NoteParts![0].Value("exit"));
        var older = ServiceClient.ReadRecord(records, "s2")!;
        Assert.Null(older.NoteParts);
        Assert.Equal(NoteBy.Before, older.AsNoted().Parts.Single().By);
    }

    [Fact]
    public void A_stop_s_request_carries_the_pause_s_parts_beside_its_note()
    {
        var home = Path.Combine(Path.GetTempPath(), "daoris-notes-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var requests = new SessionRequests(home);
            var paused = new PausedBy(WorkScope.Ask, "a1").Noted;
            requests.Write(new SessionRequest("s1a2b3c4", SessionMove.Stop, DateTimeOffset.UtcNow) { Note = paused.Note, NoteParts = paused.Parts });

            var taken = requests.Take("s1a2b3c4")!;
            Assert.Equal(paused.Note, taken.Note);
            Assert.Equal(["stopped.paused-ask"], NoteAssert.Codes(taken.NoteParts));
            Assert.Equal("a1", taken.NoteParts![0].Value("ask"));
        }
        finally
        {
            if (Directory.Exists(home)) Directory.Delete(home, recursive: true);
        }
    }

    /// <summary>A service standing in for the record's door: each body recorded, each move answered as made.</summary>
    private sealed class Recording(List<string> bodies) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct));
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("""{"message":"moved"}""", System.Text.Encoding.UTF8, "application/json"),
            };
        }
    }
}
