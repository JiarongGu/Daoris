using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TOOL4f (D125 §3.5, amending D80): a carry-on is handed, from Daoris's own record of the cut-off session, its last plan
/// and its last words, and after a limit that the last session ran on another account, now cooling. What does not travel
/// is the harness's own conversation, which stays in the first account's home and is never read.
/// </summary>
public sealed class CarryOnHandedTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-carry-on-" + Guid.NewGuid().ToString("N")[..8]);

    public CarryOnHandedTests() => Directory.CreateDirectory(Path.Combine(_home, "sessions"));

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

    [Fact]
    public void A_carry_on_is_handed_the_cut_off_session_s_last_plan()
    {
        var prompt = TargetPrompt.Compose(Target() with { LastPlan = Plan });

        Assert.Contains(
            "Its plan, as its record last kept it:\n\n"
            + "    - [completed] Read the report's columns\n"
            + "    - [in_progress] Add the note column\n"
            + "    - [pending] Run the report's tests",
            prompt);
    }

    [Fact]
    public void A_carry_on_is_handed_the_cut_off_session_s_last_words_quoted()
    {
        var prompt = TargetPrompt.Compose(Target() with { LastWords = "The column is in.\nNext: the tests." });

        Assert.Contains("Its last words were:\n\n> The column is in.\n> Next: the tests.", prompt);
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

    [Fact]
    public void With_nothing_kept_the_carry_on_reads_as_it_did()
    {
        var before = TargetPrompt.Compose(Target());

        Assert.Equal(before, TargetPrompt.Compose(Target() with { LastPlan = [], LastWords = null, AccountChanged = false }));
        Assert.DoesNotContain("Its plan", before);
        Assert.DoesNotContain("Its last words", before);
        Assert.DoesNotContain("another account", before);
    }

    [Fact]
    public void Only_a_carry_on_is_handed_the_plan_and_the_words()
    {
        var first = Target() with { CutOff = null, LastPlan = Plan, LastWords = "said.", AccountChanged = true };

        var prompt = TargetPrompt.Compose(first);

        Assert.DoesNotContain("Its plan", prompt);
        Assert.DoesNotContain("Its last words", prompt);
    }

    [Fact]
    public void The_plan_and_the_words_come_after_what_the_tree_holds_and_before_finishing()
    {
        var prompt = TargetPrompt.Compose(Target() with { LastPlan = Plan, LastWords = "said.", AccountChanged = true });

        var tree = prompt.IndexOf("it left nothing uncommitted.", StringComparison.Ordinal);
        var plan = prompt.IndexOf("Its plan", StringComparison.Ordinal);
        var words = prompt.IndexOf("Its last words", StringComparison.Ordinal);
        var account = prompt.IndexOf("It ran on another account", StringComparison.Ordinal);
        var finish = prompt.IndexOf("Finish from there", StringComparison.Ordinal);
        Assert.True(tree < plan && plan < words && words < account && account < finish, prompt);
    }

    // ——— What the driver hands it, from its own record of the cut-off session.

    [Fact]
    public void The_plan_handed_on_is_the_record_s_last_and_the_words_its_last_message()
    {
        var events = new SessionEvents(Path.Combine(_home, "sessions"));
        events.Append("s1", new SessionEvent { Kind = SessionEventKind.Plan, Entries = [new("an older plan", "pending")] });
        events.Append("s1", new SessionEvent { Kind = SessionEventKind.Message, Text = "The column " });
        events.Append("s1", new SessionEvent { Kind = SessionEventKind.Message, Text = "is in." });
        events.Append("s1", new SessionEvent { Kind = SessionEventKind.Plan, Entries = Plan });

        var (plan, words) = Daoris.Driver.Driver.CarriedFrom(events, _home, "s1");

        Assert.Equal(Plan, plan);
        Assert.Equal("The column is in.", words);
        Assert.Equal(Plan, events.LastPlan("s1"));
    }

    [Fact]
    public void A_session_whose_record_kept_no_words_hands_on_its_transcript_s_last_plain_lines()
    {
        var events = new SessionEvents(Path.Combine(_home, "sessions"));
        File.WriteAllText(Path.Combine(_home, "sessions", "s1.log"), "→ a tool ran\n    its output\nI changed the column.\n— the driver's line\n");

        var (plan, words) = Daoris.Driver.Driver.CarriedFrom(events, _home, "s1");

        Assert.Empty(plan);
        Assert.Equal("I changed the column.", words);
    }

    [Theory]
    [InlineData("no-record")]
    [InlineData("../s1")]
    [InlineData("")]
    public void A_session_with_no_record_here_or_no_id_hands_on_nothing(string session)
    {
        var events = new SessionEvents(Path.Combine(_home, "sessions"));

        var (plan, words) = Daoris.Driver.Driver.CarriedFrom(events, _home, session);

        Assert.Empty(plan);
        Assert.Null(words);
        Assert.Empty(events.LastPlan(session));
    }

    [Fact]
    public void The_last_words_handed_on_are_bounded_from_the_end()
    {
        var events = new SessionEvents(Path.Combine(_home, "sessions"));
        events.Append("s1", new SessionEvent { Kind = SessionEventKind.Message, Text = new string('a', 6000) + "the end." });

        var (_, words) = Daoris.Driver.Driver.CarriedFrom(events, _home, "s1");

        Assert.EndsWith("the end.", words);
        Assert.True(words!.Length <= Daoris.Driver.Driver.LastWordsLimit + 1);
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
