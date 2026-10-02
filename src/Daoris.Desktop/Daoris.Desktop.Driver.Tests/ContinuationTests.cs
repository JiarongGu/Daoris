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
}
