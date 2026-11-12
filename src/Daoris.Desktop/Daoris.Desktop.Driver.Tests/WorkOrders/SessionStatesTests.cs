using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The five states a session is in (needs you, failed, working, pending, done) with the finer reason inside, one case per
/// row of the design's mapping from today's record states, verdicts and waits; <c>archived</c> a mark on a done or failed one.
/// </summary>
public sealed class SessionStatesTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 11, 9, 0, 0, TimeSpan.Zero);

    private static JsonObject Record(
        string id, string state, string? quest = "q1", string kind = "driven", string? answer = null, string? noteCode = null,
        bool limit = false, string? tree = null) => new()
        {
            ["id"] = id,
            ["repository"] = "engine",
            ["adapter"] = "claude-code",
            ["state"] = state,
            ["kind"] = kind,
            ["quest"] = quest,
            ["tree"] = tree,
            ["limit"] = limit,
            ["noteParts"] = noteCode is null ? null : new JsonArray(new JsonObject { ["code"] = noteCode, ["values"] = new JsonObject(), ["text"] = "a line." }),
            ["said"] = answer is null ? new JsonArray() : new JsonArray(new JsonObject { ["id"] = "answer", ["text"] = answer, ["at"] = T0.AddMinutes(2).ToString("O"), ["files"] = new JsonArray() }),
            ["created"] = T0.ToString("O"),
            ["updated"] = T0.AddMinutes(1).ToString("O"),
        };

    private static readonly QuestView Taken = new("q1", "game", "engine", "The work of #q1", "A body.", "Taken");
    private static readonly QuestView Done = Taken with { Status = "Done" };

    /// <summary>A done that departed from the person's second requirement, accepted or not: the departure stays on it.</summary>
    private static readonly QuestView DoneLeft = Done with
    {
        Answers = [new(1, "met", null, null), new(2, null, "the part is held", "their words")],
    };

    private static Consideration Verdict(StartVerdict verdict) => new(Taken, verdict, "the planner's sentence");

    private static SessionLook Look(
        JsonObject record, Consideration? verdict = null, QuestView? quest = null, PersonWait[]? waits = null, AccountWait[]? accounts = null,
        bool archived = false, (string Session, WordsHold Holds)? held = null, (string Tree, TreeWork Work)? tree = null) =>
        SessionLook.From(new JsonArray(record).ToJsonString(), [quest ?? Taken], verdict is null ? [] : [verdict], _ => 0) with
        {
            RunWaits = waits ?? [],
            Waits = accounts ?? [],
            Archived = archived ? new Dictionary<string, DateTimeOffset> { ["s1"] = T0 } : new Dictionary<string, DateTimeOffset>(),
            ChatsWaiting = held is null ? new HashSet<string>() : new HashSet<string>([held.Value.Session], StringComparer.Ordinal),
            Held = held is null ? new Dictionary<string, WordsHold>() : new Dictionary<string, WordsHold> { [held.Value.Session] = held.Value.Holds },
            Trees = tree is null ? new Dictionary<string, TreeWork>() : new Dictionary<string, TreeWork> { [SessionGroups.Normal(tree.Value.Tree)] = tree.Value.Work },
        };

    private static PersonWait Wait(string why) => new("q1", "review", why, "engine", T0.AddMinutes(30));

    private static readonly WordsHold Cools = new(WordsHold.Cooling, "work is cooling until 11:00.") { Until = T0.AddHours(2) };

    private const string Tree = "X:/daoris/trees/engine/s-1";

    /// <summary>One case: the look, and the state, the reason and the list's group the session is read in.</summary>
    public sealed record Case(SessionLook Look, string State, string Reason, string? Group = null);

    private static readonly Dictionary<string, Case> Cases = new()
    {
        // Needs you: a human must act before it goes on.
        ["a park waits on your answer"] = new(Look(Record("s1", "awaiting-person")), SessionGroup.NeedsYou, SessionReason.Question),
        ["an archived park still needs you"] = new(Look(Record("s1", "awaiting-person"), archived: true), SessionGroup.NeedsYou, SessionReason.Question),
        ["a run waiting on an Accept is your review"] =
            new(Look(Record("s1", "completed"), quest: Done, waits: [Wait(PersonWaits.Accept)]), SessionGroup.NeedsYou, SessionReason.Review),
        ["a run waiting on a go-ahead behind a completed session needs you"] =
            new(Look(Record("s1", "completed"), quest: Done, waits: [Wait(PersonWaits.Accept), Wait(PersonWaits.GoAhead)]), SessionGroup.NeedsYou, PersonWaits.GoAhead),
        ["a policy's hold waits on your yes"] = new(Look(Record("s1", "failed"), Verdict(StartVerdict.PolicyHeld)), SessionGroup.NeedsYou, SessionReason.PolicyHeld),
        ["an unclosed quest waits on your done"] = new(Look(Record("s1", "completed"), Verdict(StartVerdict.Unclosed)), SessionGroup.NeedsYou, SessionReason.Unclosed),
        ["a steward's stop waits on a Try again"] =
            new(Look(Record("s1", "stopped"), Verdict(StartVerdict.Stopped) with { Said = new SittingFact(SittingFact.Steward) }), SessionGroup.NeedsYou, SessionReason.StewardStop),
        ["a start held for a sign-in needs you"] =
            new(Look(Record("s1", "failed"), Verdict(StartVerdict.Blocked), accounts: [new("claude-code", "Claude Code", null, "default", null, true, "no account is signed in.") { Quests = ["q1"] }]),
                SessionGroup.NeedsYou, SessionReason.SignIn),

        // Unfinished: a done's departure needs you until you accept it, then reads done, part left.
        ["a done that departed from a requirement and waits for your yes is unfinished, not done"] =
            new(Look(Record("s1", "completed"), quest: DoneLeft with { Held = true }), SessionGroup.NeedsYou, SessionReason.Left),
        ["an accepted departure reads done, part left, and needs nothing of you"] =
            new(Look(Record("s1", "completed"), quest: DoneLeft with { Held = false }), SessionGroup.Done, SessionReason.PartLeft),
        ["a done whose answers are all met is done"] =
            new(Look(Record("s1", "completed"), quest: Done with { Answers = [new(1, "met", null, null)] }), SessionGroup.Done, "completed"),
        ["an unfinished done you archived is set aside by you"] =
            new(Look(Record("s1", "completed"), quest: DoneLeft with { Held = true }, archived: true), SessionGroup.NeedsYou, SessionReason.Left, SessionGroup.Archived),
        ["an accepted departure you archived folds away like any done"] =
            new(Look(Record("s1", "completed"), quest: DoneLeft, archived: true), SessionGroup.Done, SessionReason.PartLeft, SessionGroup.Archived),
        ["a run whose step failed after its quest's done is unfinished"] =
            new(Look(Record("s1", "completed"), quest: Done, waits: [Wait(PersonWaits.Left)]), SessionGroup.NeedsYou, PersonWaits.Left),
        ["a run's failed step you archived is set aside by you"] =
            new(Look(Record("s1", "completed"), quest: Done, waits: [Wait(PersonWaits.Left)], archived: true), SessionGroup.NeedsYou, PersonWaits.Left, SessionGroup.Archived),
        ["a run's other wait still needs you though archived"] =
            new(Look(Record("s1", "completed"), quest: Done, waits: [Wait(PersonWaits.Left), Wait(PersonWaits.GoAhead)], archived: true), SessionGroup.NeedsYou, PersonWaits.GoAhead),

        // Failed: nothing goes on from it without the person.
        ["a parked quest's last session failed"] = new(Look(Record("s1", "failed"), Verdict(StartVerdict.Exhausted)), SessionGroup.Failed, ShownState.Parked),
        ["a kept failure is failed"] = new(Look(Record("s1", "failed"), quest: Done), SessionGroup.Failed, "failed"),
        ["an end past its transient bound is failed"] = new(Look(Record("s1", "failed"), Verdict(StartVerdict.TransientHeld)), SessionGroup.Failed, SessionReason.Transient),
        ["a start whose choice is not offered is failed"] = new(Look(Record("s1", "failed", noteCode: "ended.not-started"), quest: Done), SessionGroup.Failed, "failed"),
        ["a limit no reset resumes is failed"] = new(Look(Record("s1", "failed", limit: true), quest: Done), SessionGroup.Failed, ShownState.Limited),
        ["an archived failure is in the fold"] = new(Look(Record("s1", "failed"), quest: Done, archived: true), SessionGroup.Failed, "failed", SessionGroup.Archived),

        // Working: under way and needing no one, whatever it waits on.
        ["working is working"] = new(Look(Record("s1", "working")), SessionGroup.Working, "working"),
        ["starting is working"] = new(Look(Record("s1", "starting")), SessionGroup.Working, "starting"),
        ["an answered park goes on"] = new(Look(Record("s1", "awaiting-person", answer: "carry on.")), SessionGroup.Working, ShownState.Answered),
        ["a teammate's park waits on them"] = new(Look(Record("laptop/s1", "awaiting-person")), SessionGroup.Working, "awaiting-person"),
        ["an awaiting reply works on another session"] =
            new(Look(Record("s1", "completed"), Verdict(StartVerdict.Waiting) with { Quest = Taken with { Awaits = "q2" } }, Taken with { Awaits = "q2" }),
                SessionGroup.Working, ShownState.AwaitingReply),
        ["a helper's ask works on its helper"] =
            new(Look(Record("s1", "completed", noteCode: RecordNotes.EndedAwaitsHelper)), SessionGroup.Working, ShownState.AwaitingHelper),
        ["a failure the planner carries on is working"] = new(Look(Record("s1", "failed"), Verdict(StartVerdict.Start)), SessionGroup.Working, SessionReason.CarriedOn),
        ["a second opinion's wait is working"] = new(Look(Record("s1", "completed"), Verdict(StartVerdict.WaitsForOpinion)), SessionGroup.Working, SessionReason.Opinion),

        // Pending: not started, or its next start held.
        ["queued is pending"] = new(Look(Record("s1", "queued")), SessionGroup.Pending, "queued"),
        ["a paused one is pending"] = new(Look(Record("s1", "stopped"), Verdict(StartVerdict.Paused)), SessionGroup.Pending, WordsHold.Paused),
        ["a cooling account holds its words"] =
            new(Look(Record("s1", "completed", quest: null, kind: "chat"), held: ("s1", Cools)), SessionGroup.Pending, WordsHold.Cooling),
        ["a full cap holds its next start"] = new(Look(Record("s1", "failed"), Verdict(StartVerdict.AtCapacity)), SessionGroup.Pending, WordsHold.Cap),

        // Done: complete, a person's stop and an abandon included.
        ["completed is done"] = new(Look(Record("s1", "completed"), quest: Done), SessionGroup.Done, "completed"),
        ["a person's stop is done, stopped"] = new(Look(Record("s1", "stopped"), Verdict(StartVerdict.Stopped)), SessionGroup.Done, "stopped"),
        ["an abandon is done, stopped"] = new(Look(Record("s1", "stopped"), quest: Done with { Status = "Abandoned" }), SessionGroup.Done, "stopped"),
        ["declined is done"] = new(Look(Record("s1", "declined"), quest: Done), SessionGroup.Done, "declined"),
        ["stood down is done"] = new(Look(Record("s1", "stood-down"), quest: Done), SessionGroup.Done, "stood-down"),
        ["a tree holding commits no run waits on is said in its session, done"] =
            new(Look(Record("s1", "completed", tree: Tree), quest: Done, tree: (Tree, new TreeWork(2, 0))), SessionGroup.Done, "completed"),
        ["an archived done one is in the fold"] = new(Look(Record("s1", "completed"), quest: Done, archived: true), SessionGroup.Done, "completed", SessionGroup.Archived),
    };

    public static TheoryData<string> CaseNames => [.. Cases.Keys];

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void Each_session_is_in_one_of_five_states_with_its_reason(string name)
    {
        var expected = Cases[name];

        var row = SessionGroups.Read(expected.Look).Single();

        Assert.Equal((expected.State, expected.Reason, expected.Group ?? expected.State), (row.State, row.Reason, row.Group));
    }

    [Fact]
    public void Only_the_newest_session_of_an_unfinished_quest_is_unfinished_and_an_earlier_one_stays_done()
    {
        var older = Record("s1", "completed");
        older["created"] = T0.AddHours(-1).ToString("O");
        var look = SessionLook.From(new JsonArray(older, Record("s2", "completed")).ToJsonString(), [DoneLeft with { Held = true }], [], _ => 0);

        var rows = SessionGroups.Read(look).ToDictionary(row => row.Session, row => (row.State, row.Reason));

        Assert.Equal((SessionGroup.NeedsYou, SessionReason.Left), rows["s2"]);
        Assert.Equal((SessionGroup.Done, "completed"), rows["s1"]);
    }

    [Fact]
    public void The_groups_are_the_five_in_the_order_the_person_acts_on_them_then_the_archived_fold()
    {
        Assert.Equal(["needs-you", "working", "pending", "failed", "done", "archived"], SessionGroup.Order);
    }
}
