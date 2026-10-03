using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Whether an answered park resumes its own conversation (ANSWER1a, D131 §1–§2), judged before anything is spawned: the
/// record still parked, the same adapter, the same account, its tree standing, a kept id, and a door that can resume.
/// Every other case is today's carry-on, said by a code and one line that names no account. Pure, so the fast half.
/// </summary>
public sealed class ContinuationTests : IDisposable
{
    private readonly string _tree = Path.Combine(Path.GetTempPath(), "daoris-continue-" + Guid.NewGuid().ToString("N")[..8]);

    public ContinuationTests() => Directory.CreateDirectory(_tree);

    public void Dispose()
    {
        try { Directory.Delete(_tree, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private PriorSession Park(string? profile = "account-1", string adapter = "claude-code-acp", string state = "awaiting-person") =>
        new("s1", _tree, state, "It stopped with its quest still taken, to ask you:\n\nWhich port?", "engine", Answer: "Port 8080.")
        {
            Profile = profile,
            Adapter = adapter,
        };

    private static readonly HarnessConversation Kept = new("claude-code-acp", "0b5e7c1a");

    [Fact]
    public void The_same_adapter_account_and_tree_with_a_kept_id_resume()
    {
        Assert.Null(Continuations.Judge(Park(), "claude-code-acp", doorResumes: true, profile: "account-1", Kept));
    }

    /// <summary>The tool's own home is one configuration home too: null against null is the same account.</summary>
    [Fact]
    public void The_tools_own_sign_in_against_itself_is_the_same_account()
    {
        Assert.Null(Continuations.Judge(Park(profile: null), "claude-code-acp", doorResumes: true, profile: null, Kept));
    }

    /// <summary>Account names are compared as the wiring compares them, case aside.</summary>
    [Fact]
    public void An_account_named_in_another_case_is_the_same_account()
    {
        Assert.Null(Continuations.Judge(Park(profile: "Account-1"), "claude-code-acp", doorResumes: true, profile: "account-1", Kept));
    }

    /// <summary>
    /// 🔴 A different account never resumes (D131 §1): the conversation lives in the account it ran on, and the record
    /// names one account. A limit cooling it, a rotation and D130's list all land here. The line names neither.
    /// </summary>
    [Theory]
    [InlineData("account-1", "account-2")]
    [InlineData("account-1", null)]
    [InlineData(null, "account-2")]
    public void A_different_account_never_resumes(string? ranOn, string? startsOn)
    {
        var why = Continuations.Judge(Park(profile: ranOn), "claude-code-acp", doorResumes: true, profile: startsOn, Kept);

        Assert.Equal(ContinueWhy.Account, why?.Code);
        Assert.Equal("its conversation stays with the account it ran on, and this start runs on another", why?.Sentence);
        Assert.DoesNotContain("account-", why!.Sentence);
    }

    [Fact]
    public void A_changed_adapter_falls_back_naming_both()
    {
        var why = Continuations.Judge(Park(adapter: "claude-code"), "claude-code-acp", doorResumes: true, profile: "account-1", Kept);

        Assert.Equal(ContinueWhy.Adapter, why?.Code);
        Assert.Equal("it ran on `claude-code`, and starts here now run on `claude-code-acp`", why?.Sentence);
    }

    [Fact]
    public void A_tree_that_is_gone_falls_back()
    {
        Directory.Delete(_tree);

        var why = Continuations.Judge(Park(), "claude-code-acp", doorResumes: true, profile: "account-1", Kept);

        Assert.Equal((ContinueWhy.Tree, "its tree is gone"), (why?.Code, why?.Sentence));
    }

    [Fact]
    public void A_park_with_no_tree_named_falls_back_as_gone()
    {
        var why = Continuations.Judge(Park() with { Tree = null }, "claude-code-acp", doorResumes: true, profile: "account-1", Kept);

        Assert.Equal(ContinueWhy.Tree, why?.Code);
    }

    /// <summary>A park from before this build, or a wire that never said an id: nothing to resume.</summary>
    [Fact]
    public void No_kept_id_falls_back()
    {
        var why = Continuations.Judge(Park(), "claude-code-acp", doorResumes: true, profile: "account-1", kept: null);

        Assert.Equal((ContinueWhy.Unkept, "Daoris kept no id for its conversation"), (why?.Code, why?.Sentence));
    }

    /// <summary>An id another adapter opened is not this one's to resume.</summary>
    [Fact]
    public void An_id_another_adapter_kept_is_not_kept_for_this_one()
    {
        var why = Continuations.Judge(
            Park(), "claude-code-acp", doorResumes: true, profile: "account-1", new HarnessConversation("claude-code", "abc"));

        Assert.Equal(ContinueWhy.Unkept, why?.Code);
    }

    [Fact]
    public void A_door_that_cannot_resume_falls_back_naming_the_adapter()
    {
        var why = Continuations.Judge(
            Park(adapter: "stub"), "stub", doorResumes: false, profile: "account-1", new HarnessConversation("stub", "abc"));

        Assert.Equal((ContinueWhy.Unable, "`stub` cannot resume a conversation"), (why?.Code, why?.Sentence));
    }

    /// <summary>
    /// A service from before ANSWER1b ends the record as it takes the answer, and a finished record does not move: the
    /// carry-on is today's, said so.
    /// </summary>
    [Fact]
    public void A_record_the_answer_already_ended_falls_back()
    {
        var why = Continuations.Judge(Park(state: "completed"), "claude-code-acp", doorResumes: true, profile: "account-1", Kept);

        Assert.Equal((ContinueWhy.Ended, "its record had already ended"), (why?.Code, why?.Sentence));
    }

    /// <summary>The first reason that holds is the one said, in this order: the record, then what the start runs as.</summary>
    [Fact]
    public void The_record_having_ended_is_said_before_anything_else()
    {
        var why = Continuations.Judge(
            Park(state: "completed", profile: "account-2", adapter: "claude-code"), "claude-code-acp", doorResumes: false, profile: "account-1", kept: null);

        Assert.Equal(ContinueWhy.Ended, why?.Code);
    }

    /// <summary>The wire's own reasons, which only a spawn learns: each a code and a line, never the agent's words.</summary>
    [Fact]
    public void The_wires_reasons_have_their_lines()
    {
        Assert.Equal("the agent offers no way to resume a conversation", ContinueWhy.Of(ContinueWhy.Offered).Sentence);
        Assert.Equal("the agent no longer has its conversation", ContinueWhy.Of(ContinueWhy.Gone).Sentence);
        Assert.Equal("its conversation could not be resumed", ContinueWhy.Of(ContinueWhy.Refused).Sentence);
    }

    /// <summary>
    /// The park a fallback ends keeps what it asked and says why the answer went to a new session; the new session's note
    /// says the same in one line. Neither names an account.
    /// </summary>
    [Fact]
    public void A_fallback_is_said_on_the_park_it_ends_and_on_the_session_that_carries_on()
    {
        var why = ContinueWhy.Of(ContinueWhy.Account);

        Assert.Equal(
            "It stopped with its quest still taken, to ask you:\n\nWhich port?\n\nCarried on in a new session, because "
            + "its conversation stays with the account it ran on, and this start runs on another.",
            Continuations.EndedNote(Park(), why));
        Assert.Equal(
            " A new session, because its conversation stays with the account it ran on, and this start runs on another.",
            Continuations.CarriedOn(why));
        Assert.Equal("It stopped to ask you.\n\nCarried on in a new session, because its tree is gone.",
            Continuations.EndedNote(Park() with { Note = null }, ContinueWhy.Of(ContinueWhy.Tree)));
    }

    /// <summary>
    /// The resumed run's first line names the door it resumed on, and the harness version where it moved since the record
    /// opened (D131 §1): the record names the version it opened on, and a resume is never refused for a newer one.
    /// </summary>
    [Fact]
    public void The_resumed_runs_first_line_names_its_door_and_a_version_that_moved()
    {
        Assert.Equal(
            "— your answer is the next turn of its own conversation, resumed on `claude-code-acp`.",
            Continuations.Opening("claude-code-acp", now: "0.84.0", then: "0.84.0"));
        Assert.Equal(
            "— your answer is the next turn of its own conversation, resumed on `claude-code` 2.1.300; it opened on 2.1.281.",
            Continuations.Opening("claude-code", now: "2.1.300", then: "2.1.281"));
        Assert.Equal(
            "— your answer is the next turn of its own conversation, resumed on `claude-code`.",
            Continuations.Opening("claude-code", now: "2.1.300", then: null));
    }

    /// <summary>
    /// <c>session.answered</c> (D131 §2, D94 §4): once per answer taken up, the parked session, the adapter, whether it
    /// resumed, and why not as an identifier — never the answer, the agent's words or an account.
    /// </summary>
    [Fact]
    public void The_log_line_says_whether_an_answer_resumed_and_why_not_by_code()
    {
        var resumed = Continuations.Answered("s1", "claude-code-acp", why: null);
        var carried = Continuations.Answered("s1", "claude-code-acp", ContinueWhy.Of(ContinueWhy.Gone));

        Assert.Equal("session.answered", resumed.Event);
        Assert.Equal(
            [("session", (object?)"s1"), ("adapter", "claude-code-acp"), ("resumed", true), ("why", null)],
            resumed.Data);
        Assert.Equal(
            [("session", (object?)"s1"), ("adapter", "claude-code-acp"), ("resumed", false), ("why", "gone")],
            carried.Data);
    }

    /// <summary>A park the person has not answered is no continuation at all; only an answered one is.</summary>
    [Fact]
    public void Only_a_parked_record_with_an_answer_is_an_answered_park()
    {
        Assert.True(Park().AnsweredPark);
        Assert.False((Park() with { Answer = null }).AnsweredPark);
        Assert.False(Park(state: "completed").AnsweredPark);
    }

    private static readonly SaidWordView Word = new("w1", "Also log the port.", DateTimeOffset.Parse("2026-10-03T09:00:00Z"), [], Reopens: true);

    /// <summary>A record of this machine's with the person's words waiting (MSG1a's <c>said</c>), as the last run reads it.</summary>
    private PriorSession Written(string state, params SaidWordView[] said) =>
        Park(state: state) with { Answer = string.Join("\n\n", said.Select(word => word.Text)), Said = said };

    /// <summary>
    /// 🔴 Words to an ended session go on in its own record (MSG1b, D137 §2.2): completed, declined, stopped and failed each
    /// resume its own conversation where the adapter, the account, the tree and the kept id are the same.
    /// </summary>
    [Theory]
    [InlineData("completed")]
    [InlineData("declined")]
    [InlineData("stopped")]
    [InlineData("failed")]
    public void Words_waiting_on_an_ended_record_resume_its_own_conversation(string state)
    {
        Assert.Null(Continuations.Judge(Written(state, Word), "claude-code-acp", doorResumes: true, profile: "account-1", Kept));
    }

    /// <summary>An ended record with no words waiting has nothing to go on with: it ended, as a record from before ANSWER1b did.</summary>
    [Fact]
    public void An_ended_record_with_no_words_waiting_has_ended()
    {
        var why = Continuations.Judge(
            Park(state: "completed") with { Answer = null, Said = [] }, "claude-code-acp", doorResumes: true, profile: "account-1", Kept);

        Assert.Equal(ContinueWhy.Ended, why?.Code);
    }

    /// <summary>
    /// The rows of D137 §2.2 that never go on, judged before anything else: a teammate's record, an intake and a stand-down.
    /// Each is a reason that carries nothing on, said in a line that names no machine and no account.
    /// </summary>
    [Fact]
    public void What_never_goes_on_is_refused_before_anything_else()
    {
        var teammate = Continuations.Judge(
            Written("completed", Word) with { Session = "laptop/s1", Profile = "account-9" }, "claude-code", doorResumes: false, profile: null, kept: null);
        var intake = Continuations.Judge(Written("completed", Word) with { Ask = "a1" }, "claude-code-acp", doorResumes: true, "account-1", Kept);
        var stoodDown = Continuations.Judge(Written("stood-down", Word), "claude-code-acp", doorResumes: true, "account-1", Kept);

        Assert.Equal(
            [(ContinueWhy.Teammate, true), (ContinueWhy.Intake, true), (ContinueWhy.StoodDown, true)],
            new[] { teammate, intake, stoodDown }.Select(why => (why!.Code, why.Never)));
        Assert.Equal("it ran on another machine, where its conversation is", teammate!.Sentence);
        Assert.Equal("an intake is answered through its ask", intake!.Sentence);
        Assert.Equal("it stood down, so it has nothing to go on with", stoodDown!.Sentence);
        Assert.DoesNotContain("laptop", teammate.Sentence);
    }

    /// <summary>Every other reason carries the words on, or leaves them waiting: none of them is a never.</summary>
    [Fact]
    public void Only_the_three_rows_are_nevers()
    {
        Assert.False(ContinueWhy.Of(ContinueWhy.Account).Never);
        Assert.False(ContinueWhy.Of(ContinueWhy.Elsewhere).Never);
        Assert.False(ContinueWhy.AdapterChanged("a", "b").Never);
    }

    /// <summary>
    /// <c>elsewhere</c> (D137 §2.2): the agent refused because another client holds the conversation, read from its data and
    /// never its sentence. Its line names no client.
    /// </summary>
    [Fact]
    public void Another_client_holding_the_conversation_has_its_line()
    {
        Assert.Equal("elsewhere", ContinueWhy.Elsewhere);
        Assert.Equal("its conversation is open in another client of its agent", ContinueWhy.Of(ContinueWhy.Elsewhere).Sentence);
    }

    /// <summary>
    /// Words wait for a record where its <c>said</c> holds any; a host from before MSG1a answers no <c>said</c>, and there an
    /// answered park is the one case that waits.
    /// </summary>
    [Fact]
    public void Words_wait_where_said_holds_any_or_on_an_answered_park_from_before_said()
    {
        Assert.True(Written("completed", Word).WordsWaiting);
        Assert.False((Park(state: "completed") with { Answer = null, Said = [] }).WordsWaiting);
        Assert.True(Park().WordsWaiting);
        Assert.False(Park(state: "completed").WordsWaiting);
        Assert.Equal(["Port 8080."], Park().Waiting.Select(word => word.Text));
        Assert.Equal(["w1", "w2"], Written("failed", Word, Word with { Id = "w2" }).Waiting.Select(word => word.Id));
    }

    /// <summary>The resumed run's first line says words to an ended record as the person's words, not an answer.</summary>
    [Fact]
    public void A_resumed_run_on_an_ended_record_opens_with_your_words()
    {
        Assert.Equal(
            "— your words are the next turn of its own conversation, resumed on `claude-code`.",
            Continuations.Opening("claude-code", now: "2.1.300", then: "2.1.300", answer: false));
    }

    /// <summary>
    /// The notes a resume that cannot happen leaves on an ended record: on a closed quest it cannot go on, and nothing else
    /// carries the words on by itself; on a taken or open one they went to a new session.
    /// </summary>
    [Fact]
    public void An_ended_record_that_cannot_go_on_says_why_on_its_note()
    {
        var record = Written("completed", Word) with { Note = "the quest reached done." };

        Assert.Equal(
            "the quest reached done.\n\nIt cannot go on in this session, because its tree is gone.",
            Continuations.CannotNote(record, ContinueWhy.Of(ContinueWhy.Tree)));
        Assert.Equal(
            "the quest reached done.\n\nYour words went to a new session, because its tree is gone.",
            Continuations.WentNote(record, ContinueWhy.Of(ContinueWhy.Tree)));
    }

    /// <summary>
    /// An open quest whose last session could not go on with the person's words is started again, handed them (D137 §2.2):
    /// a first start's instruction, since the quest is not yet anyone's, with their words quoted, verbatim, beneath it.
    /// </summary>
    [Fact]
    public void An_open_quests_start_is_handed_the_words_its_session_could_not_go_on_with()
    {
        var target = new SessionTarget("q1", "Serve the report", "It needs a port.", "Asker", "engine", _tree, "http://localhost:5177")
        {
            PersonSaid = "Also log the port.\n\nAnd use 9090.",
        };

        var prompt = TargetPrompt.Compose(target);

        Assert.Contains("First take the quest", prompt);
        Assert.DoesNotContain("do not take it again", prompt);
        Assert.Contains("could not go on with their words", prompt);
        Assert.Contains("  > Also log the port.\n  >\n  > And use 9090.", prompt);
        Assert.DoesNotContain("could not go on with their words", TargetPrompt.Compose(target with { PersonSaid = null }));
    }

    /// <summary>
    /// A closed quest's session that went on with the person's words ends as its process does (D137 §2.3): as it was before
    /// on a clean exit, failed otherwise, saying its quest stays as it closed.
    /// </summary>
    [Fact]
    public void A_closed_quests_session_ends_as_it_was_or_failed()
    {
        Assert.Equal("completed", Observation.WentOn(0, "completed").State);
        Assert.Equal("declined", Observation.WentOn(0, "declined").State);
        var failed = Observation.WentOn(3, "completed");
        Assert.Equal(("failed", "it went on with your words and exited 3; its quest stays as it closed."), (failed.State, failed.Note));
    }

    /// <summary>
    /// 🔴 Whether a resumed run's quest had closed is read from the quest as the look planned it, never as the run left it
    /// (the merge of 2026-10-03). An answered park's run that closes its own quest done is a completed record, as ANSWER1a
    /// concluded it: read after the run, the closed quest sent it down a closed quest's ending, which ended it as it was
    /// before, still parked, its tree and its slot held for ever.
    /// </summary>
    [Fact]
    public void A_park_whose_resumed_run_closes_its_quest_ends_completed()
    {
        var concluded = Observation.Resumed(0, before: "awaiting-person", startedOn: "Taken", questStatus: "Done");

        Assert.Equal(("completed", "the quest reached done."), (concluded.State, concluded.Note));
    }

    /// <summary>A record whose quest had closed before it went on ends as a closed quest's, whatever its quest says after.</summary>
    [Fact]
    public void A_record_whose_quest_had_closed_ends_as_a_closed_quests()
    {
        Assert.Equal("completed", Observation.Resumed(0, before: "completed", startedOn: "Done", questStatus: "Done").State);
        Assert.Equal("declined", Observation.Resumed(0, before: "declined", startedOn: "Declined", questStatus: "Declined").State);
        Assert.Equal("failed", Observation.Resumed(1, before: "completed", startedOn: "Done", questStatus: "Done").State);
    }

    /// <summary>A resumed run on a taken quest that ends still holding it is waiting on the person again, as ANSWER1a has it.</summary>
    [Fact]
    public void A_resumed_run_that_ends_still_holding_its_quest_parks_again()
    {
        Assert.Equal(
            "awaiting-person",
            Observation.Resumed(0, before: "awaiting-person", startedOn: "Taken", questStatus: "Taken", lastWords: "Which port, again?").State);
    }

    /// <summary>
    /// A closed quest's ending never leaves a record live: it cannot park, holding no quest (D83), so a record that was live
    /// when it went on ends completed on a clean exit.
    /// </summary>
    [Theory]
    [InlineData("awaiting-person")]
    [InlineData("working")]
    public void A_closed_quests_ending_never_leaves_a_record_live(string before)
    {
        Assert.Equal("completed", Observation.WentOn(0, before).State);
    }

    /// <summary>
    /// <c>session.reopened</c> (D137 §3.3): once per reopen taken up, from which state, whether its own conversation resumed,
    /// and why not by code. Never the words.
    /// </summary>
    [Fact]
    public void The_reopen_line_says_from_where_whether_it_resumed_and_why_not()
    {
        var resumed = Continuations.Reopened("s1", "claude-code-acp", "completed", why: null, door: "screen");
        var carried = Continuations.Reopened("s1", "claude-code-acp", "failed", ContinueWhy.Of(ContinueWhy.Account), door: null);

        Assert.Equal("session.reopened", resumed.Event);
        Assert.Equal(
            [("session", (object?)"s1"), ("kind", "driven"), ("adapter", "claude-code-acp"), ("from", "completed"), ("resumed", true), ("why", null), ("door", "screen")],
            resumed.Data);
        Assert.Equal(
            [("session", (object?)"s1"), ("kind", "driven"), ("adapter", "claude-code-acp"), ("from", "failed"), ("resumed", false), ("why", "account"), ("door", null)],
            carried.Data);
    }

    /// <summary>
    /// MSG1d (D137 §3.1, §5.1): the driver's notes about the person's words carry their ids and the reason's code beside the
    /// line, since a reason is chrome the page words itself: where they went, naming the session, and where they cannot go on.
    /// </summary>
    [Fact]
    public void A_note_about_the_persons_words_names_them_and_the_reason_by_its_code()
    {
        var went = Continuations.Went(["w1"], "s2", ContinueWhy.Of(ContinueWhy.Gone));
        var cannot = Continuations.Cannot(["w1", "w2"], ContinueWhy.CannotResume("dsh"));
        var none = Continuations.Cannot([], ContinueWhy.Of(ContinueWhy.StoodDown));

        Assert.Equal((SessionEventKind.Note, "— your words went to session `s2`, because the agent no longer has its conversation."), (went.Kind, went.Text));
        Assert.Equal(["w1"], went.Words!);
        Assert.Equal(("s2", "gone"), (went.To, went.Why));
        Assert.Equal("— It cannot go on in this session, because `dsh` cannot resume a conversation.", cannot.Text);
        Assert.Equal(["w1", "w2"], cannot.Words!);
        Assert.Equal(((string?)null, "unable"), (cannot.To, cannot.Why));
        Assert.Null(none.Words);
    }
}
