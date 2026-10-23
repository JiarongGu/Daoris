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
    }

    /// <summary>
    /// A record begun in the person's own sign-in is judged against no scope (POOL1a, D197 point 3; the account-pool design §3 point 5,
    /// Fable's F1): the set, whose order names every account of Daoris's, never displaces it; its cooling, refused and signed-out
    /// branches carry its words as before.
    /// </summary>
    [Fact]
    public void A_record_begun_in_the_own_sign_in_goes_on_there_against_the_set()
    {
        Assert.Equal(NextHold.Ready, Judge(null, "@a"));
        Assert.Equal(NextHold.Ready, Judge(null, "a b"));
        Assert.Equal(NextHold.Ready, Judge(null, "a b keep=b"));
        Assert.Equal(NextHold.Cooling, Judge(null, "a b", readiness: AccountReadiness.Cooling));
        Assert.Equal(NextHold.Refused, Judge(null, "a b", readiness: AccountReadiness.Refused));
        Assert.Equal(NextHold.SignedOut, Judge(null, "a b", readiness: AccountReadiness.SignedOut));
        // A named account keeps its meaning: one the set does not hold is outside it.
        Assert.Equal(NextHold.Outside, Judge("c", "a b"));
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
            "To start a conversation with these words now instead, without that one: `daoris-driver sessions start-from s1`.",
            ResumeWords.ChatDoor("s1"));
        Assert.EndsWith(
            "If you have signed in to another account at your own terminal since, press Read again under Agents → the agent's "
            + "page → Accounts.",
            ResumeWords.Waits(cooling with { Account = null }, TimeZoneInfo.Utc));
    }

    /// <summary>
    /// CONVNOTE2g: the wait's line by code, its English the same to the byte, since the sessions listing and the driver read it
    /// by its prefix: the account by its owner and its id, never by the name the person gave it (D152's UX7d-1 note), until
    /// when as a moment, why by a cool-off's reason; the tool's own sign-in a line of its own, its refresh another.
    /// </summary>
    [Fact]
    public void The_wait_s_line_says_the_account_its_reset_and_why_by_code_with_the_english_unchanged()
    {
        var cooling = new CoolingEntry("claude-code", "account-1", new DateTimeOffset(2026, 10, 3, 16, 2, 0, TimeSpan.Zero), true, "session", Now, "s0");
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["account-1"] = "work" };

        var line = ResumeWords.Waiting(cooling, TimeZoneInfo.Utc, names, ResumeWords.NotYet);
        Assert.Equal(
            $"— it does not go on yet: the `claude-code` account `work` is cooling until {CoolingWords.When(cooling.Until, TimeZoneInfo.Utc)}, "
            + "as the agent said, and its conversation is on that account, so your words wait to go on in it then.",
            line.Note);
        Assert.Equal(ResumeWords.NotYet + ResumeWords.Waits(cooling, TimeZoneInfo.Utc, names), line.Note);
        var part = Assert.Single(line.Parts);
        Assert.Equal("waits.account", part.Code);
        Assert.Equal(
            ("claude-code", "account-1", "2026-10-03T16:02:00Z", CoolingWhy.Stated),
            (part.Value("owner"), part.Value("account"), part.Value("until"), part.Value("why")));
        Assert.Equal(line.Note, part.Text);

        var own = ResumeWords.Waiting(cooling with { Account = null, Stated = false }, TimeZoneInfo.Utc, lead: ResumeWords.NotYet);
        Assert.Equal(ResumeWords.NotYet + ResumeWords.Waits(cooling with { Account = null, Stated = false }, TimeZoneInfo.Utc), own.Note);
        Assert.StartsWith("— it does not go on yet: `claude-code`'s own sign-in is cooling until", own.Note);
        Assert.Equal(["waits.own", "waits.read-again"], NoteAssert.Codes(own.Parts));
        Assert.Equal(("claude-code", null, CoolingWhy.Default), (own.Parts[0].Value("owner"), own.Parts[0].Value("account"), own.Parts[0].Value("why")));
        Assert.EndsWith(own.Parts[1].Text!, own.Note);
        NoteAssert.Holds(own);

        // The roster's hold says the same sentence with no lead.
        Assert.Equal(ResumeWords.Waits(cooling, TimeZoneInfo.Utc, names), ResumeWords.Waiting(cooling, TimeZoneInfo.Utc, names).Note);
    }

    /// <summary>CONVNOTE2g: each door out of the wait is a line of its own, by code, the session its command names a value.</summary>
    [Fact]
    public void Each_door_out_of_the_wait_is_coded_with_its_english_unchanged()
    {
        var doors = new[] { ResumeWords.NewSessionDoorOf("s1"), ResumeWords.ChatDoorOf("s1"), ResumeWords.HelpDoorOf() };

        Assert.Equal(
            [ResumeWords.NewSessionDoor("s1"), ResumeWords.ChatDoor("s1"), ResumeWords.HelpDoor],
            doors.Select(door => door.Note));
        Assert.Equal(
            ["waits.door-new-session", "waits.door-chat", "waits.door-help"],
            doors.Select(door => Assert.Single(door.Parts).Code));
        Assert.Equal(["s1", "s1", null], doors.Select(door => door.Parts[0].Value("session")));
        Assert.All(doors, door => NoteAssert.Holds(door));
    }

    /// <summary>
    /// CONVNOTE2g: the conversation's line while its words wait is its wait's line, then its door, as it was always written; a
    /// wait nothing coded keeps that English with no parts, never half of it worded (D152's UX7d-1 note).
    /// </summary>
    [Fact]
    public void The_line_while_the_words_wait_is_the_wait_then_the_door_or_the_english_alone()
    {
        var cooling = new CoolingEntry("claude-code", "account-1", new DateTimeOffset(2026, 10, 3, 16, 2, 0, TimeSpan.Zero), true, "session", Now, "s0");
        var refusal = ResumeWords.Waits(cooling, TimeZoneInfo.Utc);
        var waits = new ResumeChoice(new HarnessSelection(refusal) { Cooling = cooling }, NextHold.Cooling)
        {
            Waiting = ResumeWords.Waiting(cooling, TimeZoneInfo.Utc, lead: ResumeWords.NotYet),
        };

        var line = ResumeWords.NotYetOn(waits, ResumeWords.ChatDoorOf("c1"));
        Assert.Equal($"{ResumeWords.NotYet}{refusal} {ResumeWords.ChatDoor("c1")}", line.Note);
        Assert.Equal(["waits.account", "waits.door-chat"], NoteAssert.Codes(line.Parts));
        NoteAssert.Holds(line);

        var uncoded = ResumeWords.NotYetOn(waits with { Waiting = null }, ResumeWords.HelpDoorOf());
        Assert.Equal($"{ResumeWords.NotYet}{refusal} {ResumeWords.HelpDoor}", uncoded.Note);
        Assert.Empty(uncoded.Parts);
    }

    /// <summary>
    /// CONVNOTE2g2: the chat runner's other holds, each by code, its English the line it always wrote: a reason of Daoris's own
    /// sentence is one coded line; a program's message is a words part between a lead and the tail.
    /// </summary>
    [Fact]
    public void The_runner_s_other_holds_are_coded_with_their_english_unchanged()
    {
        const string tail = " Your words wait on it.";

        var checkout = HeldWords.NoCheckout("engine");
        Assert.Equal("`engine` has no checkout on this machine any more", checkout.Why);
        Assert.Equal($"{ResumeWords.NotYet}`engine` has no checkout on this machine any more.{tail}", checkout.Line.Note);
        Assert.Equal(["held.no-checkout"], NoteAssert.Codes(checkout.Line.Parts));
        Assert.Equal("engine", checkout.Line.Parts[0].Value("repository"));
        NoteAssert.Holds(checkout.Line);

        var replaying = HeldWords.Replaying("engine");
        Assert.Equal(TreeLock.Replaying("engine"), replaying.Why);
        Assert.Equal($"{ResumeWords.NotYet}{TreeLock.Replaying("engine").TrimEnd('.', ' ')}.{tail}", replaying.Line.Note);
        Assert.Equal(["held.replaying"], NoteAssert.Codes(replaying.Line.Parts));
        Assert.Equal("engine", replaying.Line.Parts[0].Value("repository"));
        NoteAssert.Holds(replaying.Line);

        var answering = HeldWords.Answering("h2");
        Assert.Equal(
            $"Ask Daoris is answering in conversation `h2`; once its turn ends, a word said here takes these with it", answering.Why);
        Assert.Equal($"{ResumeWords.NotYet}{answering.Why}.{tail}", answering.Line.Note);
        Assert.Equal(["held.answering"], NoteAssert.Codes(answering.Line.Parts));
        Assert.Equal("h2", answering.Line.Parts[0].Value("session"));
        NoteAssert.Holds(answering.Line);

        // A program's message rides as its words, trimmed as the line always trimmed it, never a value.
        var unread = HeldWords.ConfigUnread("bad json at D:/x/driver.json.");
        Assert.Equal("this machine's driver.json could not be read: bad json at D:/x/driver.json.", unread.Why);
        Assert.Equal(
            $"{ResumeWords.NotYet}this machine's driver.json could not be read: bad json at D:/x/driver.json.{tail}", unread.Line.Note);
        Assert.Equal(["held.config-unread", null, "held.tail"], NoteAssert.Codes(unread.Line.Parts));
        Assert.Equal("bad json at D:/x/driver.json", unread.Line.Parts[1].Words);
        NoteAssert.Holds(unread.Line);

        var refused = HeldWords.Program("the ledger will not move it");
        Assert.Equal("the ledger will not move it", refused.Why);
        Assert.Equal($"{ResumeWords.NotYet}the ledger will not move it.{tail}", refused.Line.Note);
        Assert.Equal(["held.lead", null, "held.tail"], NoteAssert.Codes(refused.Line.Parts));
        Assert.Equal(("the ledger will not move it", "program"), (refused.Line.Parts[1].Words, refused.Line.Parts[1].By));
        NoteAssert.Holds(refused.Line);
    }

    /// <summary>
    /// ROSTERREFUSE1: the roster's refusal is the held note's reason by code where the selection carries its parts, between the lead
    /// and the tail, its English the line it always was; where it carries none, or its parts say something else, its words.
    /// </summary>
    [Fact]
    public void The_roster_s_refusal_is_the_held_note_by_code_where_the_selection_has_its_parts()
    {
        const string sentence = "`codex` is not installed on this machine, so there is nothing to spawn.";
        var coded = Noted.Of(NoteCodes.HeldNotInstalled, sentence, ("agent", "codex"));
        var held = HeldWords.Refused(new HarnessSelection(sentence) { RefusalNote = coded });

        Assert.Equal(HeldWords.Program(sentence).Line.Note, held.Line.Note);
        Assert.Equal(sentence, held.Why);
        Assert.Equal(["held.lead", "held.not-installed", "held.tail"], NoteAssert.Codes(held.Line.Parts));
        NoteAssert.Holds(held.Line);

        var stale = HeldWords.Refused(new HarnessSelection(sentence + " More.") { RefusalNote = coded });
        Assert.Equal(["held.lead", null, "held.tail"], NoteAssert.Codes(stale.Line.Parts));
        var bare = HeldWords.Refused(new HarnessSelection(sentence));
        Assert.Equal(["held.lead", null, "held.tail"], NoteAssert.Codes(bare.Line.Parts));
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

    private string Settings => DaorisHome.Entry(_home, HomeEntry.Harnesses);

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
        Assert.Equal("account-3", roster.Next("fake").Account);

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
        // CONVNOTE2g: and the conversation's line while it waits, by code, its English the hold's after *it does not go on yet*.
        Assert.Equal(ResumeWords.NotYet + resume.Selection.Refusal, resume.Waiting!.Note);
        Assert.Equal(("waits.account", "account-1"), (resume.Waiting.Parts[0].Code, resume.Waiting.Parts[0].Value("account")));
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
        Assert.Null(resume.Waiting);
    }

    /// <summary>
    /// An account that cannot run there at all carries the words on at once, on the walk's pick, saying why: gone from the machine,
    /// refused by its provider. Rewritten by POOL1a2 (D197 point 1): an account the stored list left out was off the list and
    /// carried on elsewhere (<c>account.resume-outside</c>); it is in the set now, so its record goes on on it, and no account is off.
    /// </summary>
    [Fact]
    public async Task A_resume_whose_account_cannot_run_there_takes_the_walk_s_pick_at_once_saying_why()
    {
        Accounts("account-1", "account-2", "account-3");
        new HarnessSettings().WithRotation("fake", ["account-2", "account-3"]).Save(Settings);
        var roster = Roster();

        var unlisted = await roster.ResumeAsync("fake", Config, null, "account-1", StartKind.Driven);
        Assert.Equal((NextHold.Ready, true, "account-1"), (unlisted.Own, unlisted.Selection.Allowed, unlisted.Selection.Profile));
        Assert.Null(unlisted.Elsewhere);

        var gone = await roster.ResumeAsync("fake", Config, null, "account-9", StartKind.Driven);
        Assert.Equal(NextHold.Missing, gone.Own);

        roster.Refuse("fake", "account-3", "its provider refused it");
        var refused = await roster.ResumeAsync("fake", Config, null, "account-3", StartKind.Driven);
        // The walk's pick: account-1 and account-2 each carry one start now (the first resume, and the gone one's words carried on),
        // so the one started on least recently, account-1, which the set holds since POOL1a2.
        Assert.Equal((NextHold.Refused, "account-1"), (refused.Own, refused.Selection.Profile));
        Assert.Equal("account.resume-refused", refused.Elsewhere!.Detail!.Parts.Single().Code);
    }

    /// <summary>
    /// MODELROLE1c2 (D205 §3.5(a), D175 point 3's note): a resume whose own account refused its asked model cannot carry the words, which
    /// go to a new record on another account of the same choice, the own account passed by its reading and never cooled; another model,
    /// or account default, resumes on the own account as before.
    /// </summary>
    [Fact]
    public async Task A_resume_whose_own_account_refused_its_model_carries_its_words_to_another_account()
    {
        Accounts("account-1", "account-2");
        new HarnessSettings().WithRotation("fake", ["account-1", "account-2"]).Save(Settings);
        var roster = Roster();
        var version = (await roster.ReportAsync("fake", Config))?.Version;
        Assert.Null(AgentOffers.Refused(_home, "fake", version, "account-1", "opus", DateTimeOffset.UtcNow));

        var refused = await roster.ResumeAsync("fake", Config, null, "account-1", StartKind.Driven, model: "opus");
        Assert.True(refused.NotOffered);
        Assert.Equal("account-2", refused.Selection.Profile);
        Assert.Equal("account.resume-not-offered", refused.Elsewhere!.Detail!.Parts.Single().Code);

        var other = await roster.ResumeAsync("fake", Config, null, "account-1", StartKind.Driven, model: "sonnet");
        Assert.Equal((false, "account-1"), (other.NotOffered, other.Selection.Profile));
        var asBefore = await roster.ResumeAsync("fake", Config, null, "account-1", StartKind.Driven);
        Assert.Equal("account-1", asBefore.Selection.Profile);
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
