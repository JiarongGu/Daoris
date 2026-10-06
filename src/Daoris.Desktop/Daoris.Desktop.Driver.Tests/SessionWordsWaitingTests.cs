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

    public SessionWordsWaitingTests() => Directory.CreateDirectory(Path.Combine(_home, "sessions"));

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private SessionEvents Events => new(Path.Combine(_home, "sessions"));

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

    private SessionLook Waiting(string json, Consideration[]? considered = null, bool fresh = true) =>
        SessionGroups.Waiting(
            SessionLook.From(json, [], considered ?? [], _ => 0), json, _home, "claude-code", fresh, CoolingOf, TimeZoneInfo.Utc);

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
    /// Not waiting: words every one of which its runner could not go on with (its marks), Ask Daoris's own conversation, a
    /// teammate's, one still live, and one with no words.
    /// </summary>
    [Fact]
    public void Chats_whose_words_nothing_takes_up_are_not_waiting()
    {
        var json = Json(
            Record("c1", "completed", kind: "chat", said: ["w1"]),
            Record("he1p0000", "stopped", kind: "chat", repository: HelpRoom.Repository, said: ["w2"]),
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
        Assert.Equal((WordsHold.Cooling, (DateTimeOffset?)Reset), (held.Value.Why, held.Value.Until));
        Assert.Equal(
            ResumeWords.Waits(Cooling, TimeZoneInfo.Utc, AccountNames.Of(_home, "claude")) + " " + ResumeWords.NewSessionDoor("s1"),
            held.Value.Reason);
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
