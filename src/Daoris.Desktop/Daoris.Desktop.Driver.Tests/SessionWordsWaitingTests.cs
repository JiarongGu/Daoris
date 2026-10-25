using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// MSG1f3 (D137 §3.2, its MSG1f2 note): what the listing reads from this home where the planner cannot say what holds the
/// person's words. An ended chat they wait on goes on, its runner taking them up at once, unless its conversation's last line
/// is the runner's *it does not go on yet*; and where no loop has looked, a driven record whose words resume on its own account
/// waits for that account's reset, which a fresh plan cannot see.
/// </summary>
/// <remarks>Files under a scratch home and a stand-in cool-off only: the suite's fast half. The grouping itself is
/// <see cref="SessionGroupsTests"/>'s table.</remarks>
public sealed class SessionWordsWaitingTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Reset = T0.AddHours(2);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-waiting-" + Guid.NewGuid().ToString("N")[..8]);

    public SessionWordsWaitingTests() => Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Sessions));

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private SessionEvents Events => new(DaorisHome.Entry(_home, HomeEntry.Sessions));

    private static JsonObject Record(
        string id, string state, string? quest = null, string kind = "driven", string repository = "engine", string[]? said = null,
        string adapter = "claude-code", string? profile = "work") => new()
        {
            ["id"] = id, ["repository"] = repository, ["state"] = state, ["kind"] = kind, ["quest"] = quest,
            ["adapter"] = adapter, ["profile"] = profile,
            ["created"] = T0.ToString("O"), ["updated"] = T0.AddMinutes(5).ToString("O"),
            ["said"] = new JsonArray([.. (said ?? []).Select(word => (JsonNode)new JsonObject
            {
                ["id"] = word, ["text"] = $"the words {word}", ["at"] = T0.AddMinutes(10).ToString("O"), ["files"] = new JsonArray(), ["reopens"] = true,
            })]),
        };

    private static QuestView Quest(string id, string status) => new(id, "game", "engine", $"The work of #{id}", "A body.", status);

    /// <summary>The planner's start of a record the person's words wait on, as its <c>GoOn</c> says it: it resumes that record.</summary>
    private static Consideration GoesOn(QuestView quest, string json, string session) =>
        new(quest, StartVerdict.Start, "going on") { GoesOn = true, Resumes = ServiceClient.ReadRecord(json, session) };

    private static readonly CoolingEntry Cooling = new("claude", "work", Reset, Stated: true, Window: null, Seen: T0, Session: null);

    /// <summary>The home's cool-offs, standing in: the account <c>work</c> of <c>claude-code</c> cools, every other is ready.</summary>
    private static CoolingEntry? CoolingOf(string? adapter, string? profile) =>
        adapter == "claude-code" && profile == "work" ? Cooling : null;

    private static string Json(params JsonObject[] records) => new JsonArray([.. records]).ToJsonString();

    private SessionLook Waiting(
        string json, Consideration[]? considered = null, bool fresh = true, Func<string?, string?, CoolingEntry?>? coolingOf = null) =>
        SessionGroups.Waiting(
            SessionLook.From(json, [], considered ?? [], _ => 0), json, _home, "claude-code", fresh, coolingOf ?? CoolingOf, TimeZoneInfo.Utc);

    [Fact]
    public void An_ended_chat_whose_words_wait_is_waiting_and_held_by_nothing()
    {
        var json = Json(Record("c1", "completed", kind: "chat", said: ["w1"], profile: null));
        Events.Append("c1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "the words w1", Reaches = "resume" });

        var look = Waiting(json);

        Assert.Equal(["c1"], look.ChatsWaiting);
        Assert.Empty(look.Held);
    }

    /// <summary>The runner's line is the conversation's last: what holds the words is what it said after <c>it does not go on yet</c>.</summary>
    [Fact]
    public void A_chat_its_runner_held_is_held_in_the_runners_words()
    {
        var json = Json(Record("c1", "completed", kind: "chat", said: ["w1"], profile: null));
        Events.Append("c1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "the words w1", Reaches = "resume" });
        Events.Append("c1", new SessionEvent
        {
            Kind = SessionEventKind.Note, Text = ResumeWords.NotYet + "`engine` has no checkout on this machine any more. Your words wait on it.",
        });

        var held = Assert.Single(Waiting(json).Held);

        Assert.Equal("c1", held.Key);
        Assert.Equal(
            (WordsHold.Waits, "`engine` has no checkout on this machine any more. Your words wait on it.", (DateTimeOffset?)null),
            (held.Value.Why, held.Value.Reason, held.Value.Until));
    }

    /// <summary>A word said since the runner's line is taken up again: nothing holds it until the runner says so once more.</summary>
    [Fact]
    public void A_word_said_after_the_runners_line_waits_again()
    {
        var json = Json(Record("c1", "completed", kind: "chat", said: ["w1", "w2"], profile: null));
        Events.Append("c1", new SessionEvent { Kind = SessionEventKind.Note, Text = ResumeWords.NotYet + "the ledger refused it." });
        Events.Append("c1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "the words w2", Reaches = "resume" });

        var look = Waiting(json);

        Assert.Equal(["c1"], look.ChatsWaiting);
        Assert.Empty(look.Held);
    }

    /// <summary>The runner held a chat whose own account cools: the reset is the fact the row says, the runner's line its reason.</summary>
    [Fact]
    public void A_chat_held_while_its_own_account_cools_says_the_reset()
    {
        var json = Json(Record("c1", "completed", kind: "chat", said: ["w1"]));
        Events.Append("c1", new SessionEvent { Kind = SessionEventKind.Note, Text = ResumeWords.NotYet + "work is cooling until 11:00." });

        var held = Assert.Single(Waiting(json, fresh: false).Held).Value;

        Assert.Equal((WordsHold.Cooling, (DateTimeOffset?)Reset, "work is cooling until 11:00."), (held.Why, held.Until, held.Reason));
    }

    /// <summary>
    /// LIMITSWITCH2 (D137's note of 2026-10-25): a chat whose runner held its words because every account of its agent cools is held until the
    /// earliest reset of any, naming that account, never its own account's alone; Ask Daoris's room alone keeps its own.
    /// </summary>
    [Fact]
    public void A_chat_held_with_every_account_cooling_says_the_earliest_reset_of_any()
    {
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "claude", "work"));
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "claude", "home"));
        var json = Json(
            Record("c1", "completed", kind: "chat", said: ["w1"]),
            Record("he1p0000", "stopped", kind: "chat", repository: HelpRoom.Repository, said: ["w2"]));
        foreach (var session in new[] { "c1", "he1p0000" })
        {
            Events.Append(session, new SessionEvent { Kind = SessionEventKind.Note, Text = ResumeWords.NotYet + "the accounts are cooling." });
        }

        var earlier = new CoolingEntry("claude", "home", Reset.AddHours(-1), Stated: true, Window: null, Seen: T0, Session: null);
        var look = Waiting(json, fresh: false, coolingOf: (adapter, profile) => profile == "home" ? earlier : CoolingOf(adapter, profile));

        Assert.Equal(
            (WordsHold.Cooling, (DateTimeOffset?)Reset.AddHours(-1), "home"),
            (look.Held["c1"].Why, look.Held["c1"].Until, look.Held["c1"].Account));
        Assert.Equal(
            (WordsHold.Cooling, (DateTimeOffset?)Reset, "work"),
            (look.Held["he1p0000"].Why, look.Held["he1p0000"].Until, look.Held["he1p0000"].Account));
    }

    /// <summary>ASKHIST1: Ask Daoris's own conversation goes on with the words waiting on it, as a chat does, so it is listed so.</summary>
    [Fact]
    public void Ask_daoris_s_conversation_whose_words_wait_is_waiting()
    {
        var json = Json(Record("he1p0000", "stopped", kind: "chat", repository: HelpRoom.Repository, said: ["w2"], profile: null));

        Assert.Equal(["he1p0000"], Waiting(json).ChatsWaiting);
    }

    /// <summary>
    /// Not waiting: words every one of which its runner could not go on with (its marks), a teammate's, one still live, and one
    /// with no words.
    /// </summary>
    [Fact]
    public void Chats_whose_words_nothing_takes_up_are_not_waiting()
    {
        var json = Json(
            Record("c1", "completed", kind: "chat", said: ["w1"]),
            Record("laptop/c3", "completed", kind: "chat", said: ["w3"]),
            Record("c4", "working", kind: "chat", said: ["w4"]),
            Record("c5", "completed", kind: "chat"));
        new GoOnMarks(_home).Mark("c1", ["w1"], ContinueWhy.Of(ContinueWhy.Refused), T0);

        var look = Waiting(json);

        Assert.Empty(look.ChatsWaiting);
        Assert.Empty(look.Held);
    }

    /// <summary>
    /// Where no loop has looked, a driven record whose words resume on its own account waits for that account's reset, said as
    /// the loop's look would say it: the resume's words and the door out of the wait, here a new session, its quest taken.
    /// </summary>
    [Fact]
    public void Words_resumed_on_an_account_that_cools_are_held_until_its_reset_where_the_plan_is_fresh()
    {
        var json = Json(Record("s1", "completed", "q1", said: ["w1"]));
        var taken = Quest("q1", "Taken");

        var held = Assert.Single(Waiting(json, [GoesOn(taken, json, "s1")]).Held);

        Assert.Equal("s1", held.Key);
        Assert.Equal((WordsHold.Cooling, (DateTimeOffset?)Reset, "work"), (held.Value.Why, held.Value.Until, held.Value.Account));
        Assert.Equal(
            ResumeWords.Waits(Cooling, TimeZoneInfo.Utc, AccountNames.Of(_home, "claude")) + " " + ResumeWords.NewSessionDoor("s1"),
            held.Value.Reason);
    }

    /// <summary>
    /// LIMITSWITCH1 (D137's note of 2026-10-25): where no loop has looked, a taken quest's record whose own account cools is held by
    /// nothing while another account of its agent is ready, since the loop's look carries its words on there; with every account
    /// cooling it is held for the earliest reset of any, naming that account. LIMITSWITCH2: a closed quest's session is read the same, its
    /// door out of the wait the conversation with the words.
    /// </summary>
    [Fact]
    public void A_fresh_plan_holds_the_words_of_a_taken_quest_only_where_no_account_is_ready_and_for_the_earliest_reset()
    {
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "claude", "work"));
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "claude", "home"));
        var json = Json(Record("s1", "completed", "q1", said: ["w1"]));
        Consideration[] taken = [GoesOn(Quest("q1", "Taken"), json, "s1")];
        Consideration[] closed = [GoesOn(Quest("q1", "Done"), json, "s1")];

        // `home` is ready: the loop's look takes the words there, so nothing is held for a reset, and the shell's runner takes a closed
        // quest's session's words there too.
        Assert.Empty(Waiting(json, taken).Held);
        Assert.Empty(Waiting(json, closed).Held);
        var closedHeld = Waiting(json, closed, coolingOf: (adapter, profile) => new CoolingEntry("claude", profile, Reset, Stated: true, Window: null, Seen: T0, Session: null)).Held["s1"];
        Assert.EndsWith(ResumeWords.ChatDoor("s1"), closedHeld.Reason);

        // Every account cools, `home` first: the earliest reset of any holds them, naming it.
        var earlier = new CoolingEntry("claude", "home", Reset.AddHours(-1), Stated: true, Window: null, Seen: T0, Session: null);
        CoolingEntry? Both(string? adapter, string? profile) => profile == "home" ? earlier : CoolingOf(adapter, profile);
        var held = Assert.Single(Waiting(json, taken, coolingOf: Both).Held).Value;
        Assert.Equal((WordsHold.Cooling, (DateTimeOffset?)Reset.AddHours(-1), "home"), (held.Why, held.Until, held.Account));
        Assert.StartsWith(LimitResume.Waits(earlier, TimeZoneInfo.Utc, AccountNames.Of(_home, "claude")).Note, held.Reason);

        // The own account the earliest: its own words and its own account.
        var own = Assert.Single(Waiting(json, taken, coolingOf: (adapter, profile) => profile == "home"
            ? new CoolingEntry("claude", "home", Reset.AddHours(3), Stated: true, Window: null, Seen: T0, Session: null)
            : CoolingOf(adapter, profile)).Held).Value;
        Assert.Equal((Reset, "work"), (own.Until, own.Account));
        Assert.StartsWith(ResumeWords.Waits(Cooling, TimeZoneInfo.Utc, AccountNames.Of(_home, "claude")), own.Reason);
    }

    /// <summary>A closed quest's session has nothing to carry its words on by itself: its door is a conversation with them.</summary>
    [Fact]
    public void A_closed_quests_session_names_the_conversation_door()
    {
        var json = Json(Record("s1", "completed", "q1", said: ["w1"]));

        var held = Assert.Single(Waiting(json, [GoesOn(Quest("q1", "Done"), json, "s1")]).Held).Value;

        Assert.EndsWith(ResumeWords.ChatDoor("s1"), held.Reason);
    }

    /// <summary>
    /// Nothing is read where the loop's look says it, where the person chose a new session, where the words carry on in a new
    /// session on the machine's adapter, or where the account is ready.
    /// </summary>
    [Fact]
    public void A_driven_records_words_are_held_by_nothing_the_resume_would_not_wait_for()
    {
        var taken = Quest("q1", "Taken");
        var json = Json(
            Record("s1", "completed", "q1", said: ["w1"]),
            Record("s2", "completed", "q2", said: ["w2"], adapter: "codex"),
            Record("s3", "completed", "q3", said: ["w3"], profile: "home"),
            Record("s4", "completed", "q4", said: ["w4"]));
        new NewSessionChoices(_home).Choose("s4", ["w4"], T0);
        Consideration[] considered =
        [
            GoesOn(taken, json, "s1"), GoesOn(Quest("q2", "Taken"), json, "s2"), GoesOn(Quest("q3", "Taken"), json, "s3"),
            GoesOn(Quest("q4", "Taken"), json, "s4"),
        ];

        Assert.Empty(Waiting(json, considered, fresh: false).Held);
        Assert.Equal(["s1"], Waiting(json, considered).Held.Keys);
    }
}
