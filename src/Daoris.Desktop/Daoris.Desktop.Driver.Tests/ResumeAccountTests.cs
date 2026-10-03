using System.Diagnostics;
using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.AccountRotationWalkTests;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// MSG1g (D137 §2.2's account paragraph, point 7): a resume asks for its record's own account, since a harness keeps a
/// conversation in the configuration home of the account it ran on (D131 §1). Ready, the resume runs there, whatever the walk
/// would choose for a start. Cooling, the words wait for its reset (D125 §2), and the person may choose a new session instead.
/// Unable to run there at all (gone from the machine, off the list, kept for conversations, refused, signed out), the words are
/// carried on at once on the walk's pick, saying why, by TOOL6e's codes (<see cref="NextHold"/>).
/// </summary>
/// <remarks>
/// The judgement is pure; the roster's half asks it with what it knows before any probe and asks the agent's word on a sign-in
/// only for an account still ready. Nothing here starts a process: the agent is present by a file look on a command this test
/// writes, as in <see cref="AccountNextStartTests"/>. A sign-in the agent says is gone needs a real probe, so it is the
/// <c>Process</c> half's (<see cref="AccountRotationTickTests"/>).
/// </remarks>
public sealed class ResumeAccountTests : IDisposable
{
    // ——— The judgement (pure).

    private static NextHold Judge(string? account, string scope, StartKind kind = StartKind.Driven, AccountReadiness readiness = AccountReadiness.Ready, string? absent = null)
    {
        var read = Scope(scope);
        // The machine has the scope's accounts and the record's own, but the one named absent.
        var present = read.List.Concat(read.Begins is { } begins ? [begins] : []).Concat(account is null ? [] : [account])
            .Where(name => name != absent).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var state = new AccountState(
            account, readiness,
            readiness == AccountReadiness.Cooling ? new CoolingEntry("fake", account, Now.AddHours(4), true, "weekly", Now.AddHours(-1), null) : null);
        return ResumeAccount.Judge(account, read, kind, state, present);
    }

    /// <summary>
    /// The record's own account, listed and ready, carries its words, wherever the walk would start: the scope's order and the
    /// goal's steps choose a start's account, never a resume's.
    /// </summary>
    [Theory]
    [InlineData("a", "a b c")]
    [InlineData("c", "a b c @a")]
    [InlineData("B", "a b c")]
    public void Its_own_account_listed_and_ready_carries_its_words(string account, string scope)
    {
        Assert.Equal(NextHold.Ready, Judge(account, scope));
    }

    /// <summary>A scope with no list is its one account, or the tool's own sign-in where it names none (D130 §3.1, D125 §3.7).</summary>
    [Fact]
    public void A_scope_with_no_list_is_its_one_account_or_the_tool_s_own_sign_in()
    {
        Assert.Equal(NextHold.Ready, Judge("a", "@a"));
        Assert.Equal(NextHold.Outside, Judge("b", "@a"));
        Assert.Equal(NextHold.Ready, Judge(null, ""));
        Assert.Equal(NextHold.Outside, Judge(null, "@a"));
        Assert.Equal(NextHold.Outside, Judge(null, "a b"));
    }

    /// <summary>Cooling, the words wait for its reset (D125 §2): a time, which passes by itself.</summary>
    [Fact]
    public void A_cooling_account_holds_its_words_for_its_reset()
    {
        Assert.Equal(NextHold.Cooling, Judge("b", "a b c", readiness: AccountReadiness.Cooling));
        Assert.Equal(NextHold.Cooling, Judge("b", "a b c", StartKind.Conversation, AccountReadiness.Cooling));
    }

    /// <summary>
    /// 🔴 What cannot run there at all wins over a cool-off: an account the list no longer names, one gone from the machine, one
    /// kept for conversations or one its provider refused would still be unable once its cool-off passed, so waiting for it
    /// would wait for nothing (D130: the list is the whole set; D125 §2.3).
    /// </summary>
    [Theory]
    [InlineData("d", "a b c", AccountReadiness.Cooling, null, NextHold.Outside)]
    [InlineData("b", "a b c", AccountReadiness.Cooling, "b", NextHold.Missing)]
    [InlineData("c", "a b c keep=c", AccountReadiness.Cooling, null, NextHold.Kept)]
    [InlineData("b", "a b c", AccountReadiness.Refused, null, NextHold.Refused)]
    [InlineData("b", "a b c", AccountReadiness.SignedOut, null, NextHold.SignedOut)]
    public void An_account_that_cannot_run_there_is_said_by_what_holds_it(
        string account, string scope, AccountReadiness readiness, string? absent, NextHold hold)
    {
        Assert.Equal(hold, Judge(account, scope, readiness: readiness, absent: absent));
    }

    /// <summary>
    /// A conversation's account is asked for as its picker names one (D130 point 4's exception): a list that does not name it, or
    /// keeps it for conversations, does not stop a conversation going on; a gone, refused or cooling one does.
    /// </summary>
    [Fact]
    public void A_conversation_goes_on_on_its_own_account_wherever_the_list_stands()
    {
        Assert.Equal(NextHold.Ready, Judge("d", "a b c", StartKind.Conversation));
        Assert.Equal(NextHold.Ready, Judge("c", "a b c keep=c", StartKind.Conversation));
        Assert.Equal(NextHold.Missing, Judge("b", "a b c", StartKind.Conversation, absent: "b"));
        Assert.Equal(NextHold.Refused, Judge("b", "a b c", StartKind.Conversation, AccountReadiness.Refused));
    }

    /// <summary>A kept account that would leave driven work none is read as none, as the walk reads it (§4.6).</summary>
    [Fact]
    public void A_kept_account_alone_in_its_list_is_read_as_none()
    {
        Assert.Equal(NextHold.Ready, Judge("a", "a keep=a"));
    }

    // ——— What is said.

    /// <summary>
    /// Each way its own account cannot carry the words has its line, coded (LANG1a) after the reason's own: the reason stays
    /// <c>account</c>, and the line says why, naming no account, since the note it joins travels (D125 §3.6).
    /// </summary>
    [Theory]
    [InlineData(NextHold.SignedOut, false, "account.resume-signed-out", "That account is not signed in any more.")]
    [InlineData(NextHold.Refused, false, "account.resume-refused", "Its provider refused that account.")]
    [InlineData(NextHold.Missing, false, "account.resume-gone", "That account is not on this machine any more.")]
    [InlineData(NextHold.Outside, false, "account.resume-outside", "This work no longer uses that account.")]
    [InlineData(NextHold.Kept, false, "account.resume-kept", "That account is kept for conversations, which driven work never runs on.")]
    [InlineData(NextHold.Cooling, true, "account.resume-new-session", "You chose a new session over waiting for that account.")]
    public void Why_its_own_account_could_not_carry_the_words_is_a_coded_line_after_the_reason(
        NextHold hold, bool chosen, string code, string line)
    {
        var why = ContinueWhy.AccountHeld(hold, chosen);

        Assert.Equal(ContinueWhy.Account, why.Code);
        Assert.Equal((code, line), (why.Detail!.Parts.Single().Code, why.Detail.Note));
        var carried = Continuations.CarriedOn(why);
        Assert.Equal(
            $"A new session, because its conversation stays with the account it ran on, and this start runs on another. {line}",
            carried.Note);
        Assert.Equal(["started.fell-back", code], NoteAssert.Codes(carried.Parts));
        NoteAssert.Holds(carried);
    }

    /// <summary>A hold that is no fallback has no line: ready runs there, and a cool-off nobody chose to leave waits.</summary>
    [Fact]
    public void A_hold_that_carries_nothing_on_has_no_line()
    {
        Assert.Null(ContinueWhy.AccountHeld(NextHold.Ready).Detail);
        Assert.Null(ContinueWhy.AccountHeld(NextHold.Cooling).Detail);
    }

    /// <summary>
    /// Wherever the reason is said, its line follows: the park a fallback ends, an ended record's note where it went on or cannot,
    /// and the driver's notes in its conversation, which keep the reason's code beside the line.
    /// </summary>
    [Fact]
    public void The_line_follows_the_reason_on_every_note_that_says_it()
    {
        var why = ContinueWhy.AccountHeld(NextHold.SignedOut);
        var park = new PriorSession("s1", "/trees/s-1", "awaiting-person", "It stopped to ask you.", "engine", Answer: "Port 8080.");
        var ended = new PriorSession("s1", "/trees/s-1", "completed", "The quest reached done.", "engine");

        Assert.EndsWith("this start runs on another. That account is not signed in any more.", Continuations.EndedNote(park, why).Note);
        Assert.Equal("account.resume-signed-out", Continuations.EndedNote(park, why).Parts[^1].Code);
        Assert.EndsWith("this start runs on another. That account is not signed in any more.", Continuations.WentNote(ended, why).Note);
        Assert.EndsWith("this start runs on another. That account is not signed in any more.", Continuations.CannotNote(ended, why).Note);
        NoteAssert.Holds(Continuations.CannotNote(ended, why));

        var went = Continuations.Went(["w1"], "s2", why);
        Assert.Equal(
            "— your words went to session `s2`, because its conversation stays with the account it ran on, and this start runs on "
            + "another. That account is not signed in any more.",
            went.Text);
        Assert.Equal(ContinueWhy.Account, went.Why);
        Assert.EndsWith("another. That account is not signed in any more.", Continuations.Cannot(["w1"], why).Text);
    }

    /// <summary>
    /// The hold's sentence names the account, its reset and why it lasts until then, on this machine only (a consideration and
    /// the conversation record are the machine's), and the door that leaves it: a new session where one carries the words on,
    /// a conversation where nothing does.
    /// </summary>
    [Fact]
    public void The_hold_s_sentence_names_the_account_its_reset_and_the_door_that_leaves_it()
    {
        var cooling = new CoolingEntry("claude-code", "account-1", new DateTimeOffset(2026, 10, 3, 16, 2, 0, TimeSpan.Zero), true, "session", Now, "s0");

        Assert.Equal(
            $"the `claude-code` account `account-1` is cooling until {CoolingWords.When(cooling.Until, TimeZoneInfo.Utc)}, as the "
            + "agent said, and its conversation is on that account, so your words wait to go on in it then.",
            ResumeWords.Waits(cooling, TimeZoneInfo.Utc));
        Assert.Equal(
            "To go on now in a new session instead, without that conversation: `daoris-driver sessions go-on-new s1`.",
            ResumeWords.NewSessionDoor("s1"));
        Assert.Equal(
            "To start a conversation with these words now instead, without that one: `daoris-driver chat --repository engine`.",
            ResumeWords.ChatDoor("engine"));
        Assert.EndsWith(
            "If you have signed in to another account at your own terminal since, refresh Settings → Agents.",
            ResumeWords.Waits(cooling with { Account = null }, TimeZoneInfo.Utc));
    }

    // ——— Through the roster: the same judgement, with what it knows of each account.

    private static readonly DateTimeOffset Seen = new(2026, 10, 3, 15, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-resume-" + Guid.NewGuid().ToString("N")[..8]);

    public ResumeAccountTests()
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(Command, "");
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Settings => Path.Combine(_home, "harnesses.json");

    private string Command => Path.Combine(_home, "agent-here");

    private sealed class Adapter(string name, HarnessToolchain? toolchain) : ISessionAdapter
    {
        public string Name => name;

        public SessionWire Wire => SessionWire.Pipe;

        public HarnessToolchain? Toolchain => toolchain;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => new("unused");
    }

    private HarnessRoster Roster() =>
        new(new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["fake"] = new Adapter("fake", new HarnessToolchain(
                Binary: [Command], VersionArguments: ["--version"], ProfileVariable: "FAKE_HOME", ProbeByPresence: true)),
        }), Settings)
        {
            Clock = () => Seen,
            Zone = TimeZoneInfo.Utc,
        };

    private static DriverConfig Config => DriverConfig.Empty with { Adapter = "fake" };

    private void Accounts(params string[] names)
    {
        foreach (var name in names) Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "fake", name));
    }

    private void Cool(string account) =>
        AccountCooling.Cool(_home, new CoolingEntry("fake", account, Seen.AddHours(2), true, "session", Seen.AddMinutes(-5), "s1"), Seen);

    /// <summary>
    /// 🔴 The owner's goal (D130) chooses a start's account; a resume asks for its record's own. The walk would take
    /// <c>account-3</c>, which Daoris has not started on yet, and a resume of a session that ran on <c>account-1</c> takes
    /// <c>account-1</c>, chosen by no step of the walk.
    /// </summary>
    [Fact]
    public async Task A_resume_runs_on_its_own_account_where_the_walk_would_start_on_another()
    {
        Accounts("account-1", "account-2", "account-3");
        new HarnessSettings().WithRotation("fake", ["account-1", "account-2", "account-3"]).Save(Settings);
        var roster = Roster();
        roster.Look(
        [
            new SessionStarted("fake", "account-1", Seen.AddHours(-3), Running: false),
            new SessionStarted("fake", "account-2", Seen.AddHours(-2), Running: false),
        ], roster.Mark());
        Assert.Equal("account-3", roster.Next("fake", null).Account);

        var resume = await roster.ResumeAsync("fake", Config, null, "account-1", StartKind.Driven);

        Assert.Equal((NextHold.Ready, true, "account-1"), (resume.Own, resume.Selection.Allowed, resume.Selection.Profile));
        Assert.Equal((false, (ContinueReason?)null), (resume.Waits, resume.Elsewhere));
        Assert.Equal((null, null), (resume.Selection.Choice, resume.Selection.Rotated));
    }

    /// <summary>
    /// Cooling, the resume is held, not moved: the hold carries the account's cool-off, so the look says the wait once and a
    /// screen shows the quest waiting for an account (TOOL4g's shape), and its sentence names the reset. Nothing is counted as
    /// a start chosen.
    /// </summary>
    [Fact]
    public async Task A_resume_whose_account_is_cooling_is_held_with_its_reset()
    {
        Accounts("account-1", "account-2");
        new HarnessSettings().WithRotation("fake", ["account-1", "account-2"]).Save(Settings);
        Cool("account-1");
        var roster = Roster();

        var resume = await roster.ResumeAsync("fake", Config, null, "account-1", StartKind.Driven);

        Assert.Equal((NextHold.Cooling, true, false), (resume.Own, resume.Waits, resume.Selection.Allowed));
        Assert.Equal(("account-1", Seen.AddHours(2)), (resume.Selection.Cooling!.Account, resume.Selection.Cooling.Until));
        Assert.StartsWith(
            $"the `fake` account `account-1` is cooling until {CoolingWords.When(Seen.AddHours(2), TimeZoneInfo.Utc)}, as the agent said",
            resume.Selection.Refusal);
        Assert.Null(resume.Elsewhere);
    }

    /// <summary>
    /// The person chose a new session while it cools (*Go on in a new session*): the walk's pick carries the words, which
    /// passes the cooling account by itself, and the reason says it was their choice.
    /// </summary>
    [Fact]
    public async Task A_new_session_the_person_chose_takes_the_walk_s_pick_while_its_account_cools()
    {
        Accounts("account-1", "account-2");
        new HarnessSettings().WithRotation("fake", ["account-1", "account-2"]).Save(Settings);
        Cool("account-1");

        var resume = await Roster().ResumeAsync("fake", Config, null, "account-1", StartKind.Driven, newSession: true);

        Assert.Equal((NextHold.Cooling, false, "account-2"), (resume.Own, resume.Waits, resume.Selection.Profile));
        Assert.Equal("account.resume-new-session", resume.Elsewhere!.Detail!.Parts.Single().Code);
    }

    /// <summary>
    /// An account that cannot run there at all carries the words on at once, on the walk's pick, saying why: off the list, gone
    /// from the machine, refused by its provider.
    /// </summary>
    [Fact]
    public async Task A_resume_whose_account_cannot_run_there_takes_the_walk_s_pick_at_once_saying_why()
    {
        Accounts("account-1", "account-2", "account-3");
        new HarnessSettings().WithRotation("fake", ["account-2", "account-3"]).Save(Settings);
        var roster = Roster();

        var outside = await roster.ResumeAsync("fake", Config, null, "account-1", StartKind.Driven);
        Assert.Equal((NextHold.Outside, true, "account-2"), (outside.Own, outside.Selection.Allowed, outside.Selection.Profile));
        Assert.Equal("account.resume-outside", outside.Elsewhere!.Detail!.Parts.Single().Code);

        var gone = await roster.ResumeAsync("fake", Config, null, "account-9", StartKind.Driven);
        Assert.Equal(NextHold.Missing, gone.Own);

        roster.Refuse("fake", "account-3", "its provider refused it");
        var refused = await roster.ResumeAsync("fake", Config, null, "account-3", StartKind.Driven);
        Assert.Equal((NextHold.Refused, "account-2"), (refused.Own, refused.Selection.Profile));
        Assert.Equal("account.resume-refused", refused.Elsewhere!.Detail!.Parts.Single().Code);
    }

    /// <summary>
    /// A conversation is never carried on by itself (D137 §2.2), so where its own account cannot run, nothing is walked and
    /// nothing counted: the answer says what holds it, and the chat runner says why it cannot go on.
    /// </summary>
    [Fact]
    public async Task A_conversation_whose_account_cannot_run_walks_nothing()
    {
        Accounts("account-1", "account-2");
        new HarnessSettings().WithRotation("fake", ["account-1", "account-2"]).Save(Settings);
        var roster = Roster();
        roster.Refuse("fake", "account-1", "its provider refused it");

        var resume = await roster.ResumeAsync("fake", Config, null, "account-1", StartKind.Conversation, walks: false);

        Assert.Equal((NextHold.Refused, false), (resume.Own, resume.Selection.Allowed));
        Assert.Null(resume.Selection.Profile);
        Assert.Equal("account.resume-refused", resume.Elsewhere!.Detail!.Parts.Single().Code);
    }

    /// <summary>The tool's own sign-in, where its scope still names no account, is its own account: the resume runs there.</summary>
    [Fact]
    public async Task A_resume_on_the_tool_s_own_sign_in_runs_there_while_nothing_is_named()
    {
        var resume = await Roster().ResumeAsync("fake", Config, null, null, StartKind.Driven);

        Assert.Equal((NextHold.Ready, true, (string?)null), (resume.Own, resume.Selection.Allowed, resume.Selection.Profile));
    }
}
