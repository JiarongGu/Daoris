using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TOOL4f (D125 §3.5, amending D80): a carry-on is handed, from Daoris's own record of the cut-off session, what it left, and after
/// a limit that the last session ran on another account, now cooling. What does not travel is the harness's own conversation,
/// which stays in the first account's home and is never read. SESSCTX1b (D189 point 4): what it left is Daoris's brief of its whole
/// line (<see cref="ContextBrief"/>), in place of the last plan and the last words alone.
/// </summary>
public sealed class CarryOnHandedTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-carry-on-" + Guid.NewGuid().ToString("N")[..8]);

    public CarryOnHandedTests() => Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Sessions));

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static SessionTarget Target() => new(
        QuestId: "abc123",
        Title: "Add the note field",
        Body: "The report needs a note column.",
        Asker: "ask #9f45",
        Repository: "console-ui",
        Root: "C:/somewhere/console-ui",
        ServiceUrl: "http://localhost:5177")
    {
        CutOff = "the agent's turn failed with the quest still taken.",
    };

    private static readonly PlanEntry[] Plan =
    [
        new("Read the report's columns", "completed"),
        new("Add the note column", "in_progress"),
        new("Run the report's tests", "pending"),
    ];

    // ——— The instruction.

    /// <summary>The brief of a line whose one part planned and said its last words, as its record keeps them.</summary>
    private static ComposedBrief Brief(string words = "The column is in.\nNext: the tests.") => ContextBrief.Compose(new BriefLine(
        HandOverWhy.CarryOn,
        [
            new BriefPart("s1", "claude-code",
            [
                new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Text = "You are the agent for `console-ui`…" },
                new SessionEvent { Kind = SessionEventKind.Plan, Entries = Plan },
                new SessionEvent { Kind = SessionEventKind.Message, Text = words },
            ]),
        ]));

    /// <summary>
    /// 🔴 SESSCTX1b: a carry-on is handed Daoris's brief of its line in its carry-on section: the session it continues and why, its
    /// plan as last kept with the turn it was written in, and its last words, quoted; never the instruction it was handed again.
    /// </summary>
    [Fact]
    public void A_carry_on_is_handed_the_brief_of_its_line_with_the_cut_off_session_s_plan_and_last_words()
    {
        var prompt = TargetPrompt.Compose(Target() with { Brief = Brief() });

        Assert.Contains("You continue session `s1`, which ran on claude-code; it moved because it stopped before its work was done.", prompt);
        Assert.Contains(
            "- The plan, as last kept, written in turn 1 of session `s1`:\n"
            + "    - [completed] Read the report's columns\n"
            + "    - [in_progress] Add the note column\n"
            + "    - [pending] Run the report's tests",
            prompt);
        Assert.Contains("  > The column is in.\n  > Next: the tests.", prompt);
        Assert.DoesNotContain("You are the agent for `console-ui`…", prompt);
    }

    [Fact]
    public void After_a_limit_a_carry_on_is_told_the_last_session_ran_on_another_account_now_cooling()
    {
        var prompt = TargetPrompt.Compose(Target() with { AccountChanged = true });

        Assert.Contains(
            "It ran on another account, which reached its limit and is cooling, and you run on a different one: nothing of "
            + "its own conversation carries over to you. This tree, the quest and what is above are what it left.",
            System.Text.RegularExpressions.Regex.Replace(prompt, @"\s+", " "));
        // The instruction is the agent's, and names no account.
        Assert.DoesNotContain("account-", prompt);
    }

    /// <summary>
    /// 🔴 MIXAGENT1a-ii (D163 point 1): a carry-on of a handed quest is told it was handed over from another agent's session and
    /// carries on from that session's plan and words, never that the person released it; and the accounting says the fact.
    /// </summary>
    [Fact]
    public void A_handed_over_carry_on_says_it_was_handed_over_from_the_agent_and_never_that_it_was_released()
    {
        var handed = Target() with { CutOff = "Handed over to codex-acp: its quest goes on with it, in the same tree.", Released = true, HandedOver = "claude-code", Brief = Brief("The column is in.") };

        var prompt = System.Text.RegularExpressions.Regex.Replace(TargetPrompt.Compose(handed), @"\s+", " ");

        Assert.Contains(
            "An earlier session on this quest, run by claude-code, was stopped so that the quest could go on with you: it was handed over, "
            + "and you carry on from Daoris's brief of its line below. Its record reads: Handed over to codex-acp",
            prompt);
        Assert.DoesNotContain("released", prompt);
        Assert.DoesNotContain("cut off before it closed it", prompt);
        Assert.Contains("The plan, as last kept", prompt);
        var counted = Assert.Single(TargetPrompt.Composed(handed).Account.Sections, section => section.Name == "carry-on");
        Assert.Contains("that it was handed over from another agent", counted.Said);
        Assert.DoesNotContain("released", counted.Said);
    }

    /// <summary>SYNCMERGE1b2: a brought-forward carry-on is told the press, never that the session before was cut off.</summary>
    [Fact]
    public void A_brought_forward_carry_on_says_the_person_brought_the_work_forward_and_never_that_it_was_cut_off()
    {
        var forward = Daoris.Driver.Driver.BroughtForwardTold(new ForwardTake("desk", 7, DateTimeOffset.UnixEpoch, "held"));

        var prompt = System.Text.RegularExpressions.Regex.Replace(
            TargetPrompt.Compose(Target() with { CutOff = forward, BroughtForward = true }), @"\s+", " ");

        Assert.Contains(
            "The person brought forward the work of an earlier session on this quest, whose take of it had lost. Its record reads: its take of the quest lost",
            prompt);
        Assert.DoesNotContain("cut off before it closed it", prompt);
    }

    [Fact]
    public void With_no_brief_the_carry_on_reads_as_it_did()
    {
        var before = TargetPrompt.Compose(Target());

        Assert.Equal(before, TargetPrompt.Compose(Target() with { Brief = null, AccountChanged = false }));
        Assert.Contains("it left nothing uncommitted.", before);
        Assert.DoesNotContain("Daoris's brief", before);
        Assert.DoesNotContain("another account", before);
    }

    [Fact]
    public void Only_a_carry_on_is_handed_the_brief()
    {
        var first = Target() with { CutOff = null, Brief = Brief("said."), AccountChanged = true };

        var prompt = TargetPrompt.Compose(first);

        Assert.DoesNotContain("You continue session", prompt);
        Assert.DoesNotContain("> said.", prompt);
    }

    [Fact]
    public void The_brief_comes_after_what_the_session_before_left_in_the_tree_and_before_finishing()
    {
        var prompt = TargetPrompt.Compose(Target() with { Brief = Brief("said."), AccountChanged = true });

        var tree = prompt.IndexOf("What it did is in this tree", StringComparison.Ordinal);
        var brief = prompt.IndexOf("You continue session", StringComparison.Ordinal);
        var words = prompt.IndexOf("> said.", StringComparison.Ordinal);
        var account = prompt.IndexOf("It ran on another account", StringComparison.Ordinal);
        var finish = prompt.IndexOf("Finish from there", StringComparison.Ordinal);
        Assert.True(tree < brief && brief < words && words < account && account < finish, prompt);
        // Its own uncommitted list is the brief's, read as it starts: never said twice.
        Assert.DoesNotContain("it left nothing uncommitted.", prompt);
    }

    // ——— What the driver hands it, from its own record of the cut-off session.

    [Fact]
    public void The_plan_kept_is_the_record_s_last()
    {
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        events.Append("s1", new SessionEvent { Kind = SessionEventKind.Plan, Entries = [new("an older plan", "pending")] });
        events.Append("s1", new SessionEvent { Kind = SessionEventKind.Message, Text = "The column " });
        events.Append("s1", new SessionEvent { Kind = SessionEventKind.Message, Text = "is in." });
        events.Append("s1", new SessionEvent { Kind = SessionEventKind.Plan, Entries = Plan });

        Assert.Equal(Plan, events.LastPlan("s1"));
        Assert.Equal("The column is in.", events.LastSaid("s1"));
    }

    [Theory]
    [InlineData("no-record")]
    [InlineData("../s1")]
    [InlineData("")]
    public void A_session_with_no_record_here_or_no_id_keeps_no_plan(string session)
    {
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));

        Assert.Empty(events.LastPlan(session));
    }

    [Theory]
    [InlineData("account-1", "account-2", true)]
    [InlineData("account-1", "Account-1", false)]
    [InlineData(null, null, false)]
    [InlineData(null, "account-1", true)]
    [InlineData("account-1", null, true)]
    public void Another_account_is_the_record_s_account_against_the_start_s(string? ran, string? runs, bool another)
    {
        Assert.Equal(another, Daoris.Driver.Driver.OnAnotherAccount(new PriorSession("s1", null, "failed") { Profile = ran }, runs));
    }

    // ——— The record's account and its limit, read with the last run.

    [Fact]
    public void The_last_run_says_which_account_it_ran_on_and_whether_a_limit_cut_it_off()
    {
        var last = ServiceClient.ReadLastRun("""
            [{ "id": "s1", "quest": "q1", "state": "failed", "profile": "account-1", "limit": true, "created": "2026-10-01T10:00:00Z" },
             { "id": "s2", "quest": "q2", "state": "failed", "created": "2026-10-01T10:00:00Z" }]
            """);

        Assert.Equal(("account-1", true), (last["q1"].Profile, last["q1"].Limit));
        Assert.Equal(((string?)null, false), (last["q2"].Profile, last["q2"].Limit));
    }
}
