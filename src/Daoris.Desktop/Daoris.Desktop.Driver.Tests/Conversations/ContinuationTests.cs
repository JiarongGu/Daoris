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

    /// <summary>
    /// SUPERVISOR1a-i (D208; the supervisor design §6.1, §6.3): every list of words a go-on hands keeps a supervisor's word only where it
    /// is the admission's ticket's; a forged one beside a helper's and the owner's words, and one arriving after planning, never go.
    /// </summary>
    /// <summary>
    /// SUPERVISOR1a-i (the supervisor design §6.1): the working note and the opening follow who answered, never "your answer" where none
    /// of the owner's words wait, the authors in the fixed order.
    /// </summary>
    [Fact]
    public void The_working_note_and_the_opening_name_the_supervisor_never_your_answer()
    {
        SaidWordView Word(string id) => new(id, "words", DateTimeOffset.UnixEpoch, [], false);
        var authors = Continuations.AuthorsOf(
            [Word("d") with { From = DaorisWords.From }, Word("h") with { Helper = "h1" }, Word("w0") with { From = SupervisorHanded.From }]);

        Assert.Equal(["supervisor", "helper", "daoris"], authors);
        var noted = Continuations.WorkingWithOthersNoted(authors);
        Assert.Equal(
            "resumes its own conversation with the supervisor's answer, a helper's answer and Daoris's words, in the tree it worked in.",
            noted.Note);
        Assert.DoesNotContain("your", Continuations.WorkingWithOthersNoted(["supervisor"]).Note);
        Assert.StartsWith(
            "— the supervisor's answer is the next turn",
            Continuations.OpeningNoted("claude-code-acp", null, null, answer: true, persons: false, supervisor: true).Note);
        Assert.StartsWith("— your answer is", Continuations.OpeningNoted("claude-code-acp", null, null, supervisor: true).Note);
    }

    [Fact]
    public void A_go_on_hands_a_supervisors_word_only_where_it_is_the_tickets()
    {
        SaidWordView Word(string id, string? from = null) => new(id, "words", DateTimeOffset.UnixEpoch, [], false) { From = from };
        IReadOnlyList<SaidWordView> words =
        [
            Word("helped") with { Helper = "h1" }, Word("owner"), Word("w0", SupervisorHanded.From), Word("forged", SupervisorHanded.From),
            Word("late", SupervisorHanded.From),
        ];
        var ticket = new SupervisorTicket("s1", "d1", "w0", "go");

        Assert.Equal(["helped", "owner", "w0"], Continuations.Ticketed(words, ticket).Select(word => word.Id));
        Assert.Equal(["helped", "owner"], Continuations.Ticketed(words, null).Select(word => word.Id));
        IReadOnlyList<SaidWordView> plain = [.. words.Take(2)];
        Assert.Same(plain, Continuations.Ticketed(plain, null));
    }

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
    /// SESSCTX1c (D189's amendment, the session-context design §2): the native door's harness that ended on a failed exit before it
    /// named its conversation proves nothing of its loss, so it has a code of its own apart from <c>gone</c>, which stays the
    /// protocol door's <c>resource_not_found</c> and reads as it always did. Neither is a never.
    /// </summary>
    [Fact]
    public void The_native_doors_failed_exit_is_a_reason_of_its_own_apart_from_gone()
    {
        Assert.Equal("exited", ContinueWhy.Exited);
        Assert.NotEqual(ContinueWhy.Gone, ContinueWhy.Exited);
        Assert.Equal("its agent ended before it reopened its conversation", ContinueWhy.Of(ContinueWhy.Exited).Sentence);
        Assert.Equal("the agent no longer has its conversation", ContinueWhy.Of(ContinueWhy.Gone).Sentence);
        Assert.False(ContinueWhy.Of(ContinueWhy.Exited).Never);
    }

    /// <summary>
    /// 🔴 SESSCTX1c (D189's amendment): a driven record's fallback carries on by itself, `from` why <c>retry</c>, after any refusal of
    /// its resume but one: another client holding its conversation (<c>elsewhere</c>), since a second writer in its tree could redo its
    /// work. That one holds it for the person.
    /// </summary>
    [Fact]
    public void Only_another_client_holding_the_conversation_holds_a_driven_record_for_the_person()
    {
        Assert.True(ContinueWhy.Of(ContinueWhy.Elsewhere).Holds);
        foreach (var code in new[]
                 {
                     ContinueWhy.Account, ContinueWhy.Unkept, ContinueWhy.Tree, ContinueWhy.Offered, ContinueWhy.Gone,
                     ContinueWhy.Exited, ContinueWhy.Refused, ContinueWhy.Ended,
                 })
        {
            Assert.False(ContinueWhy.Of(code).Holds, code);
        }

        Assert.False(ContinueWhy.AdapterChanged("claude-code", "codex-acp").Holds);
        Assert.False(ContinueWhy.CannotResume("claude-code").Holds);
    }

    /// <summary>
    /// 🔴 SESSCTX1c part B (D189 point 3, the session-context design §2): a conversation, or a closed quest's session, goes on by itself
    /// in a new part only where its own conversation is proven lost: the protocol door's <c>resource_not_found</c> (<c>gone</c>), no id
    /// kept (<c>unkept</c>), a door that cannot resume (<c>unable</c>) or an agent that offers no resume (<c>offered</c>). Nothing else
    /// proves it lost, so everything else waits for the person.
    /// </summary>
    [Fact]
    public void Only_a_proven_loss_moves_a_conversation_by_itself()
    {
        foreach (var code in new[] { ContinueWhy.Gone, ContinueWhy.Unkept, ContinueWhy.Offered })
        {
            Assert.True(ContinueWhy.Of(code).Lost, code);
        }

        Assert.True(ContinueWhy.CannotResume("claude-code").Lost);
        foreach (var code in new[]
                 {
                     ContinueWhy.Account, ContinueWhy.Tree, ContinueWhy.Exited, ContinueWhy.Refused, ContinueWhy.Elsewhere,
                     ContinueWhy.Ended, ContinueWhy.Teammate, ContinueWhy.Intake, ContinueWhy.StoodDown, ContinueWhy.Started,
                 })
        {
            Assert.False(ContinueWhy.Of(code).Lost, code);
        }

        Assert.False(ContinueWhy.AdapterChanged("claude-code", "codex-acp").Lost);
    }

    /// <summary>
    /// The park a fallback ends keeps what it asked and says why the answer went to a new session; the new session's note
    /// says the same in one line. Neither names an account.
    /// </summary>
    [Fact]
    public void A_fallback_is_said_on_the_park_it_ends_and_on_the_session_that_carries_on()
    {
        var why = ContinueWhy.Of(ContinueWhy.Account);

        var ended = Continuations.EndedNote(Park(), why);
        Assert.Equal(
            "It stopped with its quest still taken, to ask you:\n\nWhich port?\n\nCarried on in a new session, because "
            + "its conversation stays with the account it ran on, and this start runs on another.",
            ended.Note);
        // The park's note had no parts, so it is carried whole before the line's code (LANG1a).
        Assert.Equal([null, "went.carried-on"], NoteAssert.Codes(ended.Parts));
        Assert.Equal(NoteBy.Before, ended.Parts[0].By);
        Assert.Equal("account", ended.Parts[1].Value("why"));
        // The session that carries on says it after a space its caller writes.
        var carried = Continuations.CarriedOn(why);
        Assert.Equal(
            "A new session, because its conversation stays with the account it ran on, and this start runs on another.",
            carried.Note);
        Assert.Equal(["started.fell-back"], NoteAssert.Codes(carried.Parts));
        var asked = Continuations.EndedNote(Park() with { Note = null }, ContinueWhy.Of(ContinueWhy.Tree));
        Assert.Equal("It stopped to ask you.\n\nCarried on in a new session, because its tree is gone.", asked.Note);
        NoteAssert.Holds(asked);
        Assert.Equal(["ended.parked-short", "went.carried-on"], NoteAssert.Codes(asked.Parts));
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

    /// <summary>
    /// SESSCTX1c (D189 point 8; the session-context design §7): a move to a new part is said by a coded line on both parts, the part
    /// it left naming where it went on and the new part where it came from, each with the agent and account, why, the context the
    /// part stood at (H) and the brief's characters (B); and <c>session.moved</c> in the machine log, by profile name, the new part
    /// and the one it continues, whether it was briefed. A line with no account or no context leaves that value out, and the page
    /// shows the line as recorded.
    /// </summary>
    [Fact]
    public void A_move_is_said_on_both_parts_and_in_the_machine_log()
    {
        var on = HandOverNotes.MovedOn("s2", "claude-code", "work", HandOverWhy.Retry, context: 182_000, brief: 12_345);
        Assert.Equal(
            "Went on in session s2 with claude-code as `work`, because its own conversation could not be resumed: its context stood "
            + "at 182000 tokens, and the new session was handed Daoris's brief of it, 12345 characters.",
            on.Note);
        Assert.Equal(["moved.on"], NoteAssert.Codes(on.Parts));
        Assert.Equal(
            new object?[] { "s2", "claude-code", "work", "retry", 182_000L, 12_345 },
            new[] { "session", "agent", "account", "why", "tokens", "count" }.Select(on.Parts[0].Value));
        NoteAssert.Holds(on);

        var from = HandOverNotes.MovedFrom("s1", "codex-acp", account: null, HandOverWhy.Retry, context: null, brief: 900);
        Assert.Equal(
            "Continues session s1, which ran on codex-acp, because its own conversation could not be resumed: it was handed Daoris's "
            + "brief of it, 900 characters.",
            from.Note);
        Assert.Equal(["moved.from"], NoteAssert.Codes(from.Parts));
        Assert.Null(from.Parts[0].Value("account"));
        Assert.Null(from.Parts[0].Value("tokens"));
        NoteAssert.Holds(from);

        var line = Continuations.Moved("s2", "s1", "codex-acp", "claude-code", from: "work", to: null, HandOverWhy.Retry, briefed: true);
        Assert.Equal("session.moved", line.Event);
        Assert.Equal(
            [("session", (object?)"s2"), ("continues", "s1"), ("ranOn", "codex-acp"), ("adapter", "claude-code"), ("from", "work"),
             ("to", null), ("why", "retry"), ("briefed", true)],
            line.Data);
    }

    /// <summary>
    /// SESSCTX1c (D189's amendment): words whose agent this machine no longer has wait on their record, said by a code that names the
    /// agent and the session, with the hand-over as the way on; the agent to name is the person's.
    /// </summary>
    [Fact]
    public void Words_whose_agent_is_no_longer_here_wait_with_the_hand_overs_offer()
    {
        var gone = Continuations.AgentGone("retired-agent", "s1");

        Assert.Equal(
            "It does not go on yet: `retired-agent` is no longer an agent on this machine, so its words cannot go on with it. They wait "
            + "on it: hand it over to another agent to go on with them, `daoris-driver sessions hand-over s1 --agent <agent>`.",
            gone.Note);
        Assert.Equal(["held.agent-gone"], NoteAssert.Codes(gone.Parts));
        Assert.Equal("retired-agent", gone.Parts[0].Value("agent"));
        Assert.Equal("s1", gone.Parts[0].Value("session"));
        NoteAssert.Holds(gone);
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
    /// MSG1d3 (D137 §2.4): words said with files are kept under the home, and the record names the files. The resumed run is
    /// handed where each word's files lie, as a conversation's message is: on the native door each word's paths beneath it
    /// in the one argument, on the protocol door a link per file after the words' blocks, which stay the words alone. A name
    /// nothing keeps any more is left out, never named as a path that is not there.
    /// </summary>
    [Fact]
    public void A_resumed_run_is_handed_the_files_said_with_its_words()
    {
        var kept = ChatFiles.Keep(_tree, "s1", [new ChatUpload("trace.txt", "exit 3"u8.ToArray())]);
        var words = new[]
        {
            new SaidWordView("w1", "See the trace.", DateTimeOffset.UnixEpoch, ["trace.txt", "gone.png"], Reopens: true),
            new SaidWordView("w2", "And log the port.", DateTimeOffset.UnixEpoch, [], Reopens: true),
        };

        var resume = new ResumeAsk("0b5e7c1a", words, "— your words are the next turn.", files: word => ChatFiles.Kept(_tree, "s1", word.Files));

        Assert.Equal("See the trace." + ChatFiles.PathLines(kept) + "\n\nAnd log the port.", resume.Prompt);
        Assert.Contains(kept[0].Path, resume.Prompt);
        Assert.DoesNotContain("gone.png", resume.Prompt);
        Assert.Equal(kept, resume.Files);
        Assert.Equal(["See the trace.", "And log the port."], resume.Blocks);
        // The run is handed a read of exactly where they lie, outside its tree, or every read there is asked and refused (D52).
        Assert.Equal(ChatFiles.Folder(_tree, "s1"), DriverContinuations.SaidFilesFolder(_tree, "s1", resume));

        // Handed nothing to find them by, it is handed the words alone, as before, and no read.
        var alone = new ResumeAsk("0b5e7c1a", words, "— your words are the next turn.");
        Assert.Equal("See the trace.\n\nAnd log the port.", alone.Prompt);
        Assert.Empty(alone.Files);
        Assert.Null(DriverContinuations.SaidFilesFolder(_tree, "s1", alone));
    }

    /// <summary>
    /// A file the record names is found where it was kept (MSG1d3): `&lt;first 12 of its hash&gt;-&lt;name&gt;` in the session's
    /// files folder, the newer of two given one name, never another name that ends the same way.
    /// </summary>
    [Fact]
    public void A_kept_file_is_found_by_the_name_the_record_gives_it()
    {
        var older = ChatFiles.Keep(_tree, "s1", [new ChatUpload("trace.txt", "exit 3"u8.ToArray())])[0];
        File.SetLastWriteTimeUtc(older.Path, DateTime.UtcNow.AddMinutes(-5));
        var newer = ChatFiles.Keep(_tree, "s1", [new ChatUpload("trace.txt", "exit 4"u8.ToArray())])[0];
        ChatFiles.Keep(_tree, "s1", [new ChatUpload("old-trace.txt", "exit 5"u8.ToArray())]);

        Assert.Equal(newer, ChatFiles.Find(_tree, "s1", "trace.txt"));
        Assert.Null(ChatFiles.Find(_tree, "s1", "race.txt"));
        Assert.Null(ChatFiles.Find(_tree, "s2", "trace.txt"));
        Assert.Equal([newer], ChatFiles.Kept(_tree, "s1", ["trace.txt", "gone.png"]));
    }

    /// <summary>
    /// 🔴 SAIDBACK1e (D137 §2.4): a quest's record goes on by the ledger's move to working before its run is spawned, so no word is
    /// taken back once the words are read, and where every word was taken back first it goes back as it was. An ended record goes
    /// back to how it ended, its note, parts and flags as they were; a park waits on the person again.
    /// </summary>
    [Fact]
    public void A_record_moved_to_working_goes_back_as_it_was_where_every_word_was_taken_back_first()
    {
        var stopped = Written("stopped", Word) with { Note = "the sweep stopped it.", Interrupted = true };
        var back = Continuations.PutBack(stopped);
        Assert.Equal(("stopped", "the sweep stopped it.", true, false), (back.State, back.Note.Note, back.Interrupted, back.Limit));
        Assert.Empty(back.Note.Parts);

        var limited = Written("failed", Word) with { Note = "the quest reached done.", NoteParts = [NoteCodes.EndedDone.Part("the quest reached done.")], Limit = true };
        back = Continuations.PutBack(limited);
        Assert.Equal(("failed", "the quest reached done.", false, true), (back.State, back.Note.Note, back.Interrupted, back.Limit));
        Assert.Equal(["ended.done"], NoteAssert.Codes(back.Note.Parts));

        var park = Written("awaiting-person", Word);
        back = Continuations.PutBack(park);
        Assert.Equal(("awaiting-person", park.Note), (back.State, back.Note.Note));
    }

    /// <summary>
    /// 🔴 SESSCTX1c (D189's amendment, the session-context design §2): a resumed run that could not start is a start failure, never a
    /// move. Its record ends <c>failed</c> with the spawn's own words, as a first start's failed spawn does, so the strikes count it,
    /// and its words wait on it to go again, said so; a park as well as an ended record, since a park moved back to waiting on the
    /// person would lose them. Where its note already said its words wait, they failed twice running and wait for the person, as a
    /// failed turn's do (TURNFAIL1).
    /// </summary>
    [Fact]
    public void A_resumed_run_that_could_not_start_is_a_failed_start_with_its_words_waiting()
    {
        foreach (var record in new[] { Written("completed", Word), Written("awaiting-person", Word) })
        {
            var (note, words) = Continuations.Unstarted(record, "The system cannot find the file specified.");
            Assert.Equal(WordsAfter.Wait, words);
            Assert.Equal("The system cannot find the file specified.\n\nYour words wait to go again at the next look.", note.Note);
            Assert.Equal([null, "ended.words-wait"], NoteAssert.Codes(note.Parts));
            Assert.Equal(NoteBy.Program, note.Parts[0].By);
            NoteAssert.Holds(note);
        }

        var once = Continuations.Unstarted(Written("completed", Word), "no such file").Note;
        var (twice, after) = Continuations.Unstarted(
            Written("failed", Word) with { Note = once.Note, NoteParts = once.Parts }, "no such file");
        Assert.Equal(WordsAfter.Held, after);
        Assert.EndsWith("Your words failed twice and wait for you: they go again with the next words you say.", twice.Note);
        Assert.Equal([null, "ended.words-wait-you"], NoteAssert.Codes(twice.Parts));
    }

    /// <summary>
    /// 🔴 SAIDBACK1f (D137 §2.4): a fallback hands its new session the words the record holds as the move left them, never those
    /// the look read, so a word taken back between the two does not go; the look's only where the record cannot be read again.
    /// </summary>
    [Fact]
    public void A_fallback_hands_on_the_words_the_record_holds_now_not_those_the_look_read()
    {
        var second = Word with { Id = "w2", Text = "And the tests." };
        var look = Written("completed", Word, second);

        var now = Continuations.HandedOn(look, Written("completed", second));
        Assert.Equal(["w2"], now.Waiting.Select(word => word.Id));
        Assert.Equal("completed", now.State);

        Assert.False(Continuations.HandedOn(look, Written("completed")).WordsWaiting);
        Assert.Same(look, Continuations.HandedOn(look, null));
        // A park's answer is not a said word: the look's record stands.
        var park = Written("awaiting-person", Word);
        Assert.Same(park, Continuations.HandedOn(park, Written("awaiting-person")));
    }

    /// <summary>
    /// The notes a resume that cannot happen leaves on an ended record: on a closed quest it cannot go on, and nothing else
    /// carries the words on by itself; on a taken or open one they went to a new session.
    /// </summary>
    [Fact]
    public void An_ended_record_that_cannot_go_on_says_why_on_its_note()
    {
        var record = Written("completed", Word) with { Note = "the quest reached done." };

        var cannot = Continuations.CannotNote(record, ContinueWhy.Of(ContinueWhy.Tree));
        Assert.Equal("the quest reached done.\n\nIt cannot go on in this session, because its tree is gone.", cannot.Note);
        Assert.Equal([null, "went.cannot"], NoteAssert.Codes(cannot.Parts));
        var went = Continuations.WentNote(record, ContinueWhy.Of(ContinueWhy.Tree));
        Assert.Equal("the quest reached done.\n\nYour words went to a new session, because its tree is gone.", went.Note);
        Assert.Equal([null, "went.new-session"], NoteAssert.Codes(went.Parts));

        // A record whose note has parts keeps them, and the line follows them (LANG1a).
        var coded = record with { NoteParts = [NoteCodes.EndedDone.Part("the quest reached done.")] };
        var after = Continuations.CannotNote(coded, ContinueWhy.Of(ContinueWhy.Tree));
        Assert.Equal(cannot.Note, after.Note);
        NoteAssert.Holds(after);
        Assert.Equal(["ended.done", "went.cannot"], NoteAssert.Codes(after.Parts));
        Assert.Equal("tree", after.Parts[1].Value("why"));
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

    private const string Dropped = "the ACP agent refused the call: Internal error: API Error: Connection dropped (ECONNRESET)";

    /// <summary>
    /// 🔴 TURNFAIL1: a closed quest's session went on with the person's words, its door failed after 27 minutes, and the agent
    /// exited 0 as stdin closed, so the record ended <c>completed</c>, "it went on with your words and ended" (the install's
    /// record, 2026-10-14). A failed turn is a failed run, said with the door's reason in the agent's words.
    /// </summary>
    [Fact]
    public void A_closed_quests_session_whose_turn_failed_going_on_ends_failed_with_the_doors_reason()
    {
        var failed = Observation.WentOn(0, "completed", Dropped);

        Assert.Equal("failed", failed.State);
        Assert.Equal($"it went on with your words, and its turn failed: {Dropped}", failed.Note);
        Assert.Equal(["ended.went-on-failed", null], NoteAssert.Codes(failed.Parts));
        Assert.Equal(NoteBy.Agent, failed.Parts![1].By);
        NoteAssert.Holds(failed.Note, failed.Parts);
        Assert.Equal("failed", Observation.Resumed(0, before: "completed", startedOn: "Done", questStatus: "Done", turnFailed: Dropped).State);
    }

    /// <summary>
    /// 🔴 TURNFAIL1 (D137 §2.2, as a cooling account's): the words a run carried when its turn failed were never answered, so
    /// they stay waiting on its record, unmarked, and the next look takes them again; the record's note says why. Words a run
    /// that went on with them after such a failure failed again are held for the person, so a door that keeps failing is not
    /// tried at every look. A run that did not fail, or whose quest reached its close all the same, took them.
    /// </summary>
    [Fact]
    public void The_words_a_failed_turn_carried_wait_to_go_again_and_after_a_second_failure_wait_for_the_person()
    {
        var failed = Observation.Resumed(0, before: "completed", startedOn: "Done", questStatus: "Done", turnFailed: Dropped);
        var ended = new PriorSession("s1", "D:/fam/Game", "completed", "it went on with your words and ended; its quest stays as it closed.");

        Assert.Equal(WordsAfter.Wait, FailedTurnWords.Judge(failed, Dropped, ended));
        var wait = FailedTurnWords.Line(WordsAfter.Wait)!;
        Assert.Equal(["ended.words-wait"], NoteAssert.Codes(wait.Parts));
        Assert.Equal("Your words wait to go again at the next look.", wait.Note);

        var again = ended with { State = "failed", Note = failed.Then("\n\n", wait).Note, NoteParts = failed.Then("\n\n", wait).Parts };
        Assert.Equal(WordsAfter.Held, FailedTurnWords.Judge(failed, Dropped, again));
        Assert.Equal(["ended.words-wait-you"], NoteAssert.Codes(FailedTurnWords.Line(WordsAfter.Held)!.Parts));

        Assert.Equal(WordsAfter.Taken, FailedTurnWords.Judge(Observation.Resumed(0, "awaiting-person", "Taken", "Done", turnFailed: Dropped), Dropped, ended));
        Assert.Equal(WordsAfter.Taken, FailedTurnWords.Judge(Observation.WentOn(3, "completed"), null, ended));
        Assert.Null(FailedTurnWords.Line(WordsAfter.Taken));
    }

    /// <summary>
    /// 🔴 LIMITRESUME1b (D206, design §3.3; D137's TURNFAIL1 note amended): a turn an account's limit refused leaves the person's words
    /// waiting, unmarked, however many limited ends run in a row, under a line of its own that neither bound reads as a first failure,
    /// so one limited end and then one ordinary failure is no *failed twice*.
    /// </summary>
    [Fact]
    public void Words_a_limited_turn_carried_always_wait_and_a_later_ordinary_failure_is_not_their_second()
    {
        var failed = Observation.Resumed(0, before: "completed", startedOn: "Taken", questStatus: "Taken", turnFailed: Dropped);
        var ended = new PriorSession("s1", "D:/fam/Game", "failed", "its account's limit ended it.") { Limit = true };
        var limitWait = FailedTurnWords.Line(WordsAfter.LimitWait)!;
        Assert.Equal(["ended.limit-words-wait"], NoteAssert.Codes(limitWait.Parts));
        Assert.Equal("Your words wait for the account's limit to lift.", limitWait.Note);

        // Two limited full resumes running: the words still wait, never held for the person.
        Assert.Equal(WordsAfter.LimitWait, FailedTurnWords.Judge(failed, Dropped, ended, limited: true));
        var limitedTwice = ended with { NoteParts = failed.Then("\n\n", limitWait).Parts };
        Assert.Equal(WordsAfter.LimitWait, FailedTurnWords.Judge(failed, Dropped, limitedTwice, limited: true));
        var waitedOnce = ended with { NoteParts = failed.Then("\n\n", FailedTurnWords.Line(WordsAfter.Wait)!).Parts };
        Assert.Equal(WordsAfter.LimitWait, FailedTurnWords.Judge(failed, Dropped, waitedOnce, limited: true));

        // One limited end, then one ordinary failure: its first, so they wait to go again, unmarked.
        Assert.Equal(WordsAfter.Wait, FailedTurnWords.Judge(failed, Dropped, limitedTwice));
        Assert.Equal(WordsAfter.Wait, Continuations.Unstarted(limitedTwice, "the spawn failed").Words);
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

    /// <summary>An ended chat the person wrote to says so on the same line (MSG1c, D137 §3.3): its kind is the record's.</summary>
    [Fact]
    public void A_chats_reopen_line_names_its_kind()
    {
        var line = Continuations.Reopened("c1", "acp-stub", "completed", why: null, door: "screen", kind: "chat");

        Assert.Contains(("kind", (object?)"chat"), line.Data);
        Assert.Contains(("resumed", (object?)true), line.Data);
    }

    /// <summary>
    /// 🔴 A chat's record is judged as a driven one's (MSG1c, D137 §2.2): its conversation goes on only on the adapter that
    /// opened it, the account it ran on, the tree it ran in, by the id kept for it. A chat whose agent named no conversation
    /// cannot go on, and says Daoris kept no id.
    /// </summary>
    [Fact]
    public void A_chats_record_goes_on_only_by_the_id_kept_for_it()
    {
        var chat = new PriorSession("c1", _tree, "completed", "the conversation ended; its commits are its record.", "engine")
        {
            Kind = "chat",
            Adapter = "acp-stub",
            Said = [new SaidWordView("w1", "Also log the port.", DateTimeOffset.UnixEpoch, [], Reopens: true)],
        };

        Assert.Null(Continuations.Judge(chat, "acp-stub", doorResumes: true, profile: null, new HarnessConversation("acp-stub", "acp-chat")));
        Assert.Equal(ContinueWhy.Unkept, Continuations.Judge(chat, "acp-stub", doorResumes: true, profile: null, kept: null)!.Code);
        Assert.Equal(ContinueWhy.Ended, Continuations.Judge(chat with { Said = [] }, "acp-stub", true, null, null)!.Code);
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
