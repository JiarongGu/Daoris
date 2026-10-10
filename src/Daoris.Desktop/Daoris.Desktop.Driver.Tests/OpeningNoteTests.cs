using System.Globalization;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// UX7d-1 (D152's UX7d-1 note, D142 points 2–4): a conversation's first line, which names the account a start opened on and
/// why, carries its lines by code beside its English, so the page words it in the reader's language. The English stays byte
/// for byte what the terminal, the trace and an older page read; each step of the walk is a reason with its own values (a
/// moment, a percent, an account), never a sentence; and a line the codes cannot say is written with no parts at all.
/// </summary>
public sealed class OpeningNoteTests : IDisposable
{
    private static readonly DateTimeOffset Now = AccountRotationWalkTests.Now;

    private static readonly DateTimeOffset Until = Now.AddHours(30);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-opening-" + Guid.NewGuid().ToString("N")[..8]);

    public OpeningNoteTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static RotationScope Scope(string? @default = null) =>
        new(ChoiceFrom.Workspace, @default, ["account-1", "account-2", "account-3"], @default ?? "account-1", RotationUse.Default, []);

    private static string Values(StepSaid step) =>
        string.Join(" ", step.Values.Select(value => $"{value.Name}={Convert.ToString(value.Value, CultureInfo.InvariantCulture)}"));

    private static readonly CoolingEntry Cooling = new("claude-code", "account-1", Until, true, "weekly", Now, "s1");

    public static TheoryData<string, WalkStep, string?, string?, string, string, string> Steps => new()
    {
        // { ran on, step, over, workspace, facts, why, values }
        { "account-2", WalkStep.Fewest, null, "work", "", OpeningWhy.Fewest, "" },
        { "account-2", WalkStep.Near, "account-1", "work", "account-1:S95", OpeningWhy.NearUsed, "over=account-1 used=95 window=session near=90" },
        { "account-2", WalkStep.Near, "account-1", "work", "account-1:N", OpeningWhy.NearWord, "over=account-1 window=session" },
        { "account-2", WalkStep.Near, "account-1", "work", "account-1:X", OpeningWhy.NearReached, "over=account-1 window=weekly" },
        { "account-2", WalkStep.Near, "account-1", "work", "account-1:C", OpeningWhy.NearCredits, "over=account-1" },
        { "account-2", WalkStep.Near, "account-1", "work", "", OpeningWhy.Near, "over=account-1" },
        { "account-2", WalkStep.Pace, "account-1", "work", "account-1:W80@+4d account-2:W90@+4d", OpeningWhy.Ahead, "over=account-1 used=80 gone=43" },
        { "account-2", WalkStep.Pace, "account-1", "work", "account-1:W80@+4d account-2:W10@+4d", OpeningWhy.Behind, "used=10 gone=43" },
        { "account-2", WalkStep.Lapsing, null, "work", "account-2:w+20h", OpeningWhy.Lapsing, "at=2026-10-03T08:00:00Z" },
        { "account-2", WalkStep.LeastRecent, null, "work", "account-2:s-1d", OpeningWhy.LeastRecent, "" },
        { "account-2", WalkStep.LeastRecent, null, "work", "", OpeningWhy.NotStarted, "" },
        { "account-1", WalkStep.List, null, "work", "", OpeningWhy.First, "workspace=work" },
        { "account-1", WalkStep.List, null, null, "", OpeningWhy.FirstHere, "" },
        { "account-1", WalkStep.Kept, null, "work", "", OpeningWhy.KeptSelf, "" },
        { "account-2", WalkStep.Kept, null, "work", "", OpeningWhy.Kept, "over=account-1" },
    };

    /// <summary>Each step is its reason by code and that reason's own values, beside the clause the English always said.</summary>
    [Theory]
    [MemberData(nameof(Steps))]
    public void Each_step_is_a_reason_with_its_own_values_beside_its_english(
        string ran, WalkStep step, string? over, string? workspace, string facts, string why, string values)
    {
        var known = facts.Length == 0 ? [] : AccountRotationWalkTests.Facts(facts);
        var choice = new WalkChoice(step) { Over = over };

        var said = Assert.Single(RotationWords.Steps(choice, "claude-code", ran, Scope(), workspace, null, known, StartKind.Driven, Now, TimeZoneInfo.Utc));

        Assert.Equal((why, values), (said.Why, Values(said)));
        Assert.Equal(RotationWords.Clause(choice, "claude-code", ran, Scope(), workspace, null, known, StartKind.Driven, Now, TimeZoneInfo.Utc), said.Text);
        Assert.Contains(NoteCodes.Opening.All, reason => reason.Code == said.Why);
    }

    [Fact]
    public void A_default_a_conversation_s_kept_account_and_a_passed_account_are_reasons_of_their_own()
    {
        StepSaid One(WalkStep step, string ran, RotationScope scope, string? workspace, AccountState? passed = null, StartKind kind = StartKind.Driven) =>
            Assert.Single(RotationWords.Steps(new WalkChoice(step), "claude-code", ran, scope, workspace, passed, new Dictionary<string, AccountFacts>(), kind, Now, TimeZoneInfo.Utc));

        Assert.Equal((OpeningWhy.Default, "workspace=work"), (One(WalkStep.List, "account-2", Scope("account-2"), "work").Why, Values(One(WalkStep.List, "account-2", Scope("account-2"), "work"))));
        Assert.Equal(("it is this machine's default", OpeningWhy.DefaultHere), (One(WalkStep.List, "account-2", Scope("account-2"), null).Text, One(WalkStep.List, "account-2", Scope("account-2"), null).Why));
        var last = One(WalkStep.Kept, "account-2", Scope(), "work", kind: StartKind.Conversation);
        Assert.Equal(("`account-1` is kept for conversations, which take it last", OpeningWhy.KeptLast, "over=account-1"), (last.Text, last.Why, Values(last)));

        var cooling = One(WalkStep.Cooling, "account-2", Scope(), "work", new AccountState("account-1", AccountReadiness.Cooling, Cooling));
        Assert.Equal(
            ("the `claude-code` account `account-1` is cooling until Oct 3, 18:00 (UTC), as the agent said", OpeningWhy.Cooling),
            (cooling.Text, cooling.Why));
        Assert.Equal("agent=claude-code over=account-1 until=2026-10-03T18:00:00Z cooling=stated", Values(cooling));

        var refused = One(WalkStep.Refused, "account-2", Scope(), "work", new AccountState("account-1", AccountReadiness.Refused));
        Assert.Equal((OpeningWhy.Refused, "agent=claude-code over=account-1"), (refused.Why, Values(refused)));
        var signedOut = One(WalkStep.SignedOut, "account-2", Scope(), "work", new AccountState("account-1", AccountReadiness.SignedOut));
        Assert.Equal((OpeningWhy.SignedOut, "agent=claude-code over=account-1"), (signedOut.Why, Values(signedOut)));
    }

    /// <summary>The rest's step rides beside the first's, as the English joins them.</summary>
    [Fact]
    public void A_first_account_passed_for_itself_brings_the_step_that_chose_among_the_rest()
    {
        var choice = new WalkChoice(WalkStep.Cooling, WalkStep.LeastRecent);
        var passed = new AccountState("account-1", AccountReadiness.Cooling, Cooling);
        var facts = AccountRotationWalkTests.Facts("account-2:s-1d");

        var steps = RotationWords.Steps(choice, "claude-code", "account-2", Scope(), "work", passed, facts, StartKind.Driven, Now, TimeZoneInfo.Utc);

        Assert.Equal([OpeningWhy.Cooling, OpeningWhy.LeastRecent], steps.Select(step => step.Why));
        Assert.Equal(
            "the `claude-code` account `account-1` is cooling until Oct 3, 18:00 (UTC), as the agent said; of the rest, Daoris started on it least recently",
            RotationWords.Text(steps));
        Assert.Equal(RotationWords.Clause(choice, "claude-code", "account-2", Scope(), "work", passed, facts, StartKind.Driven, Now, TimeZoneInfo.Utc), RotationWords.Text(steps));
    }

    /// <summary>What each account said is a line per account and one per thing it said, each inside the English, which stays as it was.</summary>
    [Fact]
    public void What_each_account_said_is_an_account_s_line_and_one_per_thing_it_said()
    {
        var facts = AccountRotationWalkTests.Facts("account-1:S95,W30@+4d account-3:N,C");
        facts["account-4"] = new AccountFacts(Said: new AccountSaid([new WindowSaid("session", null, Now.AddHours(3), "clear", false, Now.AddHours(-3), "s0")]));

        var said = RotationWords.AccountsSaid(["account-1", "account-2", "account-3", "account-4"], facts, RotationUse.Default, Now)!;

        Assert.Equal(RotationWords.Said(["account-1", "account-2", "account-3", "account-4"], facts, RotationUse.Default, Now), said.Note);
        Assert.Equal(
            [
                "opening.said-at", "opening.said-used", "opening.said-used", "opening.said-near-at",
                "opening.said-nothing",
                "opening.said-at", "opening.said-near", "opening.said-credits",
                "opening.said-at", "opening.said-clear",
            ],
            NoteAssert.Codes(said.Parts));
        NoteAssert.Holds(said);
        Assert.Equal(("account-1", "2026-10-02T11:50:00Z"), (said.Parts[0].Value("account"), said.Parts[0].Value("seen")));
        Assert.Equal((95, "session"), (said.Parts[1].Value("used"), said.Parts[1].Value("window")));
        Assert.Equal((30, "weekly"), (said.Parts[2].Value("used"), said.Parts[2].Value("window")));
        Assert.Equal(90, said.Parts[3].Value("near"));
        Assert.Equal("account-2", said.Parts[4].Value("account"));
        Assert.Equal("session", said.Parts[6].Value("window"));
        Assert.Null(RotationWords.AccountsSaid(["account-1", "account-2"], AccountRotationWalkTests.Facts("account-1:r2"), RotationUse.Default, Now));
    }

    private static HarnessSelection Chosen(string to, WalkChoice choice, AccountState? passed, Dictionary<string, AccountFacts> facts, bool rotated)
    {
        var steps = RotationWords.Steps(choice, "claude-code", to, Scope(), "work", passed, facts, StartKind.Driven, Now, TimeZoneInfo.Utc);
        return new HarnessSelection(null, to)
        {
            Choice = new AccountChoice(choice.Step, RotationWords.Text(steps)),
            Rotated = rotated ? new RotatedStart("account-1", passed?.Cooling, steps[0].Text) { Step = choice.Step, Scope = "work" } : null,
            Steps = steps,
            AccountsSaid = RotationWords.AccountsSaid(["account-1", "account-2", "account-3"], facts, RotationUse.Default, Now),
        };
    }

    /// <summary>
    /// The kept note's English is the line it always was, and its parts say it by code: the account it opened on and the first
    /// step, the rest's step, the refused turn, and what each account said.
    /// </summary>
    [Fact]
    public void The_record_s_first_line_keeps_its_english_and_carries_its_lines_by_code()
    {
        using var ledger = new StandInLedger();
        using var service = ledger.Client();
        var events = new SessionEvents(Path.Combine(_home, "sessions"));
        var passed = new AccountState("account-1", AccountReadiness.Cooling, Cooling);
        var selection = Chosen("account-2", new WalkChoice(WalkStep.Cooling, WalkStep.LeastRecent), passed, AccountRotationWalkTests.Facts("account-2:s-1d"), rotated: true);

        RotatedOpening.Say(service, events, "c1", "claude-code-acp", selection, carried: null);
        RotatedOpening.Say(service, events, "c2", "claude-code-acp", selection, new RotatedOpening.Carried("s0", Turn: 3, Used: 370_104));

        var opened = Assert.Single(events.After("c1", 0).Events);
        Assert.Equal(
            RotationWords.Opened("account-2", selection.Choice!.Clause) + " " + RotationWords.Unsaid,
            opened.Text);
        Assert.Equal(["opening.opened", "opening.rest", "opening.unsaid"], NoteAssert.Codes(opened.Parts));
        NoteAssert.Holds(opened.Text, opened.Parts);
        Assert.Null(opened.Code);
        Assert.Equal(("account-2", OpeningWhy.Cooling, "account-1", "stated"), (opened.Parts![0].Value("account"), opened.Parts[0].Value("why"), opened.Parts[0].Value("over"), opened.Parts[0].Value("cooling")));
        Assert.Equal(OpeningWhy.LeastRecent, opened.Parts[1].Value("why"));

        var carried = Assert.Single(events.After("c2", 0).Events);
        Assert.Equal(
            "carried on from session `s0` on `account-2`; the `claude-code` account `account-1` is cooling until Oct 3, 18:00 (UTC), as the "
            + "agent said; of the rest, Daoris started on it least recently; its turn 3 was refused with 370,104 tokens of context. "
            + RotationWords.Unsaid,
            carried.Text);
        Assert.Equal(["opening.carried-on", "opening.rest", "opening.turn-context", "opening.unsaid"], NoteAssert.Codes(carried.Parts));
        NoteAssert.Holds(carried.Text, carried.Parts);
        Assert.Equal(("s0", "account-2"), (carried.Parts![0].Value("session"), carried.Parts[0].Value("account")));
        Assert.Equal((3L, 370_104L), (carried.Parts[2].Value("turn"), carried.Parts[2].Value("tokens")));
    }

    /// <summary>Under <c>order</c> a rotated start says its first step alone, then what each account said where one said anything.</summary>
    [Fact]
    public void A_start_rotated_under_order_says_its_first_step_and_what_each_account_said()
    {
        using var ledger = new StandInLedger();
        using var service = ledger.Client();
        var events = new SessionEvents(Path.Combine(_home, "sessions"));
        var facts = AccountRotationWalkTests.Facts("account-1:S95");
        var selection = Chosen("account-2", new WalkChoice(WalkStep.Near) { Over = "account-1" }, null, facts, rotated: true) with { Choice = null };

        RotatedOpening.Say(service, events, "c1", "claude-code-acp", selection, new RotatedOpening.Carried("s0", Turn: 2, Used: null));

        var opened = Assert.Single(events.After("c1", 0).Events);
        Assert.Equal(
            "carried on from session `s0` on `account-2`; `account-1` has used 95% of its session limit, at or over the 90% that counts as "
            + "near; its turn 2 was refused. What each account said: `account-1` 10 min ago, 95% of its session limit used, near at 90%; "
            + "`account-2` nothing yet; `account-3` nothing yet.",
            opened.Text);
        Assert.Equal(
            ["opening.carried-on", "opening.turn-refused", "opening.said-at", "opening.said-used", "opening.said-near-at", "opening.said-nothing", "opening.said-nothing"],
            NoteAssert.Codes(opened.Parts));
        NoteAssert.Holds(opened.Text, opened.Parts);
    }

    /// <summary>
    /// A line whose steps were not said by code (a selection built by hand, a step with no reason) keeps its English and no
    /// parts: the page draws it as the driver wrote it, never half worded.
    /// </summary>
    [Fact]
    public void A_line_with_no_steps_by_code_is_kept_with_no_parts()
    {
        using var ledger = new StandInLedger();
        using var service = ledger.Client();
        var events = new SessionEvents(Path.Combine(_home, "sessions"));
        var bare = new HarnessSelection(null, "account-3") { Choice = new AccountChoice(WalkStep.Fewest, "it runs the fewest of Daoris's sessions") };
        var mismatched = bare with { Steps = [new StepSaid("Daoris started on it least recently", OpeningWhy.LeastRecent, [])] };

        RotatedOpening.Say(service, events, "c1", "claude-code-acp", bare, carried: null);
        RotatedOpening.Say(service, events, "c2", "claude-code-acp", mismatched, carried: null);

        foreach (var session in new[] { "c1", "c2" })
        {
            var opened = Assert.Single(events.After(session, 0).Events);
            Assert.Equal("opened on `account-3`: it runs the fewest of Daoris's sessions. No account has said what it has left yet.", opened.Text);
            Assert.Null(opened.Parts);
        }
    }
}
