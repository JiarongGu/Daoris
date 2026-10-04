using System.Net;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The one reader of a session's group (SESSUX1a, D126 §2.4): which of the five groups each session is in, and
/// Archived, with the word it shows and the facts its second line says, from the records, the quests, the planner's
/// verdicts, the trees' judgement and the archive marks, never from what a session printed. One table holds every state
/// of the design's §1; the screen and the terminal both read this answer, so they cannot disagree.
/// </summary>
/// <remarks>
/// Files and in-process stand-ins only, so this is the suite's fast half. The trees' judgement here is handed in; the
/// judgement over real git is <see cref="SessionTreeWorkTests"/>.
/// </remarks>
public sealed class SessionGroupsTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);

    private const string Tree = "X:/daoris/trees/aurora/engine/s-1a2b3c4d";

    private static JsonObject Record(
        string id, string state, string? quest = null, string? tree = null, int at = 0, string repository = "engine",
        string kind = "driven", bool interrupted = false, string? ask = null, int? updated = null, string? answer = null,
        bool deletable = false, string[]? said = null) => new()
        {
            // The person's words waiting on it (MSG1a), each by its id, as the service answers this machine.
            ["said"] = new JsonArray([.. (said ?? []).Select(word => (JsonNode)new JsonObject
            {
                ["id"] = word, ["text"] = $"the words {word}", ["at"] = T0.AddMinutes(at + 2).ToString("O"), ["files"] = new JsonArray(), ["reopens"] = true,
            })]),
            ["deletable"] = deletable,
            ["id"] = id,
            ["repository"] = repository,
            ["adapter"] = "claude-code",
            ["state"] = state,
            ["kind"] = kind,
            ["quest"] = quest,
            ["tree"] = tree,
            ["ask"] = ask,
            ["created"] = T0.AddMinutes(at).ToString("O"),
            ["updated"] = T0.AddMinutes(updated ?? at + 1).ToString("O"),
            ["interrupted"] = interrupted,
            ["answer"] = answer,
        };

    private static QuestView Quest(string id, string status, string to = "engine", string? awaits = null) =>
        new(id, "game", to, $"The work of #{id}", "A body.", status) { Awaits = awaits };

    private static Consideration Verdict(QuestView quest, StartVerdict verdict) => new(quest, verdict, "the planner's sentence");

    private static readonly PausedBy ByAsk = new(WorkScope.Ask, "a1");

    /// <summary>The planner's <c>Paused</c> verdict (PAUSE1b), naming ask <c>a1</c>'s pause.</summary>
    private static Consideration PausedVerdict(QuestView quest) => Verdict(quest, StartVerdict.Paused) with { PausedBy = ByAsk };

    private static SessionLook Look(
        JsonObject[] records, QuestView[]? quests = null, Consideration[]? considered = null,
        (string Tree, TreeWork Work)[]? trees = null, string[]? archived = null, Func<string, int>? forgiven = null,
        string[]? kept = null, (string Session, string[] Said)[]? unable = null, AccountWait[]? waits = null) =>
        SessionLook.From(new JsonArray([.. records]).ToJsonString(), quests ?? [], considered ?? [], forgiven ?? (_ => 0)) with
        {
            Trees = (trees ?? []).ToDictionary(each => SessionGroups.Normal(each.Tree), each => each.Work, StringComparer.OrdinalIgnoreCase),
            Archived = (archived ?? []).ToDictionary(id => id, _ => T0, StringComparer.Ordinal),
            Kept = new HashSet<string>(kept ?? [], StringComparer.Ordinal),
            Unable = (unable ?? []).ToDictionary(
                each => each.Session, each => new GoOnMark(each.Said, ContinueWhy.Refused, T0), StringComparer.Ordinal),
            Waits = waits ?? [],
        };

    /// <summary>The planner's start of a record the person's words wait on (MSG1b's <c>GoOn</c>): it goes on itself.</summary>
    private static Consideration GoesOn(QuestView quest) => Verdict(quest, StartVerdict.Start) with { GoesOn = true };

    /// <summary>A cool-off holding the starts of these quests (TOOL4g), ready at <see cref="Reset"/>.</summary>
    private static AccountWait Cooling(params string[] quests) =>
        new("claude-code", "Claude Code", "work", "default", Reset, Stated: true, "its account is cooling until 11:00.") { Quests = quests };

    private static readonly DateTimeOffset Reset = T0.AddHours(2);

    /// <summary>One row of the table: what is on the machine, the session asked about, and where it is shown.</summary>
    public sealed record Case(SessionLook Look, string Session, string Group, string Shown)
    {
        public Action<SessionGrouping>? Also { get; init; }
    }

    private static readonly QuestView Done = Quest("q1", "Done");
    private static readonly QuestView Taken = Quest("q1", "Taken");
    private static readonly QuestView Open = Quest("q1", "Open");
    private static readonly QuestView Asking = Quest("q1", "Taken", awaits: "q2");
    private static readonly QuestView Question = Quest("q2", "Open", to: "game");

    private static readonly Dictionary<string, Case> Cases = new()
    {
        // Working: it is moving, and the person may watch it.
        ["queued is working"] = new(Look([Record("s1", "queued", "q1")], [Open]), "s1", SessionGroup.Working, "queued"),
        ["starting is working"] = new(Look([Record("s1", "starting", "q1")], [Open]), "s1", SessionGroup.Working, "starting"),
        ["working is working"] = new(Look([Record("s1", "working", "q1")], [Taken]), "s1", SessionGroup.Working, "working")
        {
            Also = row => Assert.Null(row.Strikes),
        },
        // UX7c (D152, design §5.2): a working session's head says it is another try, and how many before it failed.
        ["a working session of a quest whose earlier sessions failed says how many"] =
            new(Look([Record("s1", "failed", "q1"), Record("s2", "failed", "q1", at: 10), Record("s3", "working", "q1", at: 20)], [Taken]),
                "s3", SessionGroup.Working, "working")
            {
                Also = row => Assert.Equal(2, row.Strikes),
            },
        ["a state this build does not know is live, never archivable"] =
            new(Look([Record("s1", "thinking-hard")]), "s1", SessionGroup.Working, "thinking-hard"),
        ["a teammate's parked session waits on them, not on you"] =
            new(Look([Record("laptop/s1", "awaiting-person", "q1")], [Taken]), "laptop/s1", SessionGroup.Working, "awaiting-person")
            {
                Also = row => Assert.True(row.Teammate),
            },

        // Waiting on you: only the person's press moves it.
        ["a parked session waits on you"] =
            new(Look([Record("s1", "awaiting-person", "q1", Tree)], [Taken]), "s1", SessionGroup.You, "awaiting-person"),
        // ANSWER1c (D131): the person answered, and the same session goes on at the driver's next look, so nothing waits
        // on them. Until that look its record is still parked, with the answer set.
        ["an answered park goes on: working, and shown answered"] =
            new(Look([Record("s1", "awaiting-person", "q1", Tree, answer: "The second; apply it to dev.")], [Taken]),
                "s1", SessionGroup.Working, ShownState.Answered),
        ["an archived answered park stays working while it goes on"] =
            new(Look([Record("s1", "awaiting-person", "q1", Tree, answer: "carry on.")], [Taken], archived: ["s1"]),
                "s1", SessionGroup.Working, ShownState.Answered)
            {
                Also = row => Assert.True(row.Archived),
            },
        ["a park with an empty answer still waits on you"] =
            new(Look([Record("s1", "awaiting-person", "q1", Tree, answer: "")], [Taken]), "s1", SessionGroup.You, "awaiting-person"),
        ["an intake that asked waits on you"] =
            new(Look([Record("s1", "awaiting-person", repository: "ask #a1", kind: "chat", ask: "a1")]), "s1", SessionGroup.You, "awaiting-person"),
        ["the last session of a quest parked on its failed sessions is parked"] =
            new(Look([Record("s1", "failed", "q1"), Record("s2", "failed", "q1", at: 10)], [Taken], [Verdict(Taken, StartVerdict.Exhausted)]),
                "s2", SessionGroup.You, ShownState.Parked)
            {
                Also = row => Assert.Equal(2, row.Strikes),
            },
        ["an earlier session of a parked quest is ended"] =
            new(Look([Record("s1", "failed", "q1"), Record("s2", "failed", "q1", at: 10)], [Taken], [Verdict(Taken, StartVerdict.Exhausted)]),
                "s1", SessionGroup.Ended, "failed"),
        ["a parked quest's last session the sweep cut off is parked"] =
            new(Look([Record("s1", "stopped", "q1", interrupted: true)], [Taken], [Verdict(Taken, StartVerdict.Exhausted)]),
                "s1", SessionGroup.You, ShownState.Parked)
            {
                Also = row => Assert.Equal(1, row.Strikes),
            },
        ["the strikes a parked quest says count from its last Try again"] =
            new(Look([Record("s1", "failed", "q1"), Record("s2", "failed", "q1", at: 10), Record("s3", "failed", "q1", at: 20)],
                    [Open], [Verdict(Open, StartVerdict.Exhausted)], forgiven: quest => quest == "q1" ? 1 : 0),
                "s3", SessionGroup.You, ShownState.Parked)
            {
                Also = row => Assert.Equal(2, row.Strikes),
            },
        ["parked comes before to review"] =
            new(Look([Record("s1", "failed", "q1", Tree)], [Taken], [Verdict(Taken, StartVerdict.Exhausted)], [(Tree, new TreeWork(2, 0))]),
                "s1", SessionGroup.You, ShownState.Parked),

        // To review: an ended session whose own tree holds work no branch of the person's holds (D88's proof).
        ["unlanded commits in its own tree are to review"] =
            new(Look([Record("s1", "completed", "q1", Tree)], [Done], trees: [(Tree, new TreeWork(3, 0))]), "s1", SessionGroup.Review, "completed")
            {
                Also = row => Assert.Equal(new TreeWork(3, 0), row.Work),
            },
        ["uncommitted changes in its own tree are to review"] =
            new(Look([Record("s1", "completed", "q1", Tree)], [Done], trees: [(Tree, new TreeWork(0, 2))]), "s1", SessionGroup.Review, "completed"),
        ["a tree git could not read is kept to review, as the clean-up keeps it"] =
            new(Look([Record("s1", "completed", "q1", Tree)], [Done], trees: [(Tree, new TreeWork(null, null))]), "s1", SessionGroup.Review, "completed"),
        ["a tree whose work every commit of landed is ended"] =
            new(Look([Record("s1", "completed", "q1", Tree)], [Done], trees: [(Tree, new TreeWork(0, 0))]), "s1", SessionGroup.Ended, "completed"),
        ["a stopped session with unlanded work is to review"] =
            new(Look([Record("s1", "stopped", "q1", Tree)], [Taken], trees: [(Tree, new TreeWork(1, 0))]), "s1", SessionGroup.Review, "stopped"),
        // SESSUX1b: the planner now considers a taken quest the person's stop holds, and nothing goes back into its tree until
        // they release it, so the stop's work stays to review.
        ["a stop that holds its taken quest leaves its tree to review"] =
            new(Look([Record("s1", "stopped", "q1", Tree)], [Taken], [Verdict(Taken, StartVerdict.Stopped)], [(Tree, new TreeWork(2, 0))]),
                "s1", SessionGroup.Review, "stopped")
            {
                Also = row => Assert.True(row.HoldsQuest),
            },
        // PAUSE1b (D132 §6.1): a session the pause stopped is grouped as a stop's is, since nothing goes back into its tree
        // until Resume, and the person paused it to look; its line names the pause rather than a stop.
        ["a session the pause stopped leaves its tree to review, naming the pause"] =
            new(Look([Record("s1", "stopped", "q1", Tree)], [Taken], [PausedVerdict(Taken)], [(Tree, new TreeWork(2, 0))]),
                "s1", SessionGroup.Review, "stopped")
            {
                Also = row =>
                {
                    Assert.Equal(ByAsk, row.PausedBy);
                    Assert.False(row.HoldsQuest);
                },
            },
        ["a session the pause stopped with nothing in its tree is ended, naming the pause"] =
            new(Look([Record("s1", "stopped", "q1", Tree)], [Taken], [PausedVerdict(Taken)], [(Tree, new TreeWork(0, 0))]),
                "s1", SessionGroup.Ended, "stopped")
            {
                Also = row => Assert.Equal(ByAsk, row.PausedBy),
            },
        ["the asker of a paused waiting quest rests ended, naming the pause, and its tree is to review"] =
            new(Look([Record("s1", "completed", "q1", Tree)], [Asking, Question], [PausedVerdict(Asking)], [(Tree, new TreeWork(3, 0))]),
                "s1", SessionGroup.Review, "completed")
            {
                Also = row =>
                {
                    Assert.Equal(ByAsk, row.PausedBy);
                    Assert.Null(row.Awaits);
                },
            },
        ["an earlier session of a paused quest does not name the pause"] =
            new(Look([Record("s1", "failed", "q1"), Record("s2", "stopped", "q1", at: 10)], [Taken], [PausedVerdict(Taken)]),
                "s1", SessionGroup.Ended, "failed")
            {
                Also = row => Assert.Null(row.PausedBy),
            },
        ["a parked session of a paused ask still waits on you"] =
            new(Look([Record("s1", "awaiting-person", "q1")], [Taken], [PausedVerdict(Taken)]), "s1", SessionGroup.You, "awaiting-person")
            {
                Also = row => Assert.Null(row.PausedBy),
            },
        ["a released stop whose carry-on is planned is ended, not to review"] =
            new(Look([Record("s1", "stopped", "q1", Tree)], [Taken], [Verdict(Taken, StartVerdict.Start)], [(Tree, new TreeWork(2, 0))]),
                "s1", SessionGroup.Ended, "stopped")
            {
                Also = row => Assert.False(row.HoldsQuest),
            },
        ["a conversation's own tree with work is to review"] =
            new(Look([Record("s1", "completed", tree: Tree, kind: "chat")], trees: [(Tree, new TreeWork(2, 0))]), "s1", SessionGroup.Review, "completed"),
        ["the tree's separators and case are one tree"] =
            new(Look([Record("s1", "completed", "q1", Tree.Replace('/', '\\').ToUpperInvariant())], [Done], trees: [(Tree, new TreeWork(1, 0))]),
                "s1", SessionGroup.Review, "completed"),
        ["only the newest session on a tree is to review"] =
            new(Look([Record("s1", "failed", "q1", Tree), Record("s2", "completed", "q1", Tree, at: 10)], [Done], trees: [(Tree, new TreeWork(1, 0))]),
                "s2", SessionGroup.Review, "completed"),
        ["an earlier session on a tree to review is ended"] =
            new(Look([Record("s1", "failed", "q1", Tree), Record("s2", "completed", "q1", Tree, at: 10)], [Done], trees: [(Tree, new TreeWork(1, 0))]),
                "s1", SessionGroup.Ended, "failed"),
        ["a tree a live session holds is not to review"] =
            new(Look([Record("s1", "failed", "q1", Tree), Record("s2", "working", "q1", Tree, at: 10)], [Taken], trees: [(Tree, new TreeWork(1, 0))]),
                "s1", SessionGroup.Ended, "failed"),
        ["a cut-off its quest goes back into is ended, not to review"] =
            new(Look([Record("s1", "failed", "q1", Tree)], [Taken], [Verdict(Taken, StartVerdict.Start)], [(Tree, new TreeWork(2, 0))]),
                "s1", SessionGroup.Ended, "failed"),
        ["a cut-off whose carry-on waits for the cap is ended, not to review"] =
            new(Look([Record("s1", "failed", "q1", Tree)], [Taken], [Verdict(Taken, StartVerdict.AtCapacity)], [(Tree, new TreeWork(2, 0))]),
                "s1", SessionGroup.Ended, "failed"),
        ["a failed first start whose quest starts again in a new tree leaves its own to review"] =
            new(Look([Record("s1", "failed", "q1", Tree)], [Open], [Verdict(Open, StartVerdict.Start)], [(Tree, new TreeWork(1, 0))]),
                "s1", SessionGroup.Review, "failed"),
        ["a teammate's record is never to review: its tree is on its machine"] =
            new(Look([Record("laptop/s1", "completed", "q1", Tree)], [Done], trees: [(Tree, new TreeWork(3, 0))]),
                "laptop/s1", SessionGroup.Ended, "completed")
            {
                Also = row => Assert.True(row.Teammate),
            },

        // Resumes later: it moves by itself when what it waits on arrives.
        ["the session that asked another repository is awaiting reply"] =
            new(Look([Record("s1", "completed", "q1", Tree)], [Asking, Question], [Verdict(Asking, StartVerdict.Waiting)]),
                "s1", SessionGroup.Later, ShownState.AwaitingReply)
            {
                Also = row =>
                {
                    Assert.Equal("q2", row.Awaits);
                    Assert.Equal("game", row.AwaitsOf);
                },
            },
        ["awaiting reply with work in its tree resumes later: the quest goes back into it"] =
            new(Look([Record("s1", "completed", "q1", Tree)], [Asking, Question], [Verdict(Asking, StartVerdict.Waiting)], [(Tree, new TreeWork(3, 0))]),
                "s1", SessionGroup.Later, ShownState.AwaitingReply),
        ["an earlier session of a waiting quest is ended"] =
            new(Look([Record("s1", "failed", "q1"), Record("s2", "completed", "q1", at: 10)], [Asking, Question], [Verdict(Asking, StartVerdict.Waiting)]),
                "s1", SessionGroup.Ended, "failed"),
        ["the asker of a quest whose answer came back is ended while it resumes"] =
            new(Look([Record("s1", "completed", "q1", Tree)], [Asking], [Verdict(Asking, StartVerdict.Start)], [(Tree, new TreeWork(3, 0))]),
                "s1", SessionGroup.Ended, "completed"),

        // MSG1f2 (D137 §3.2): an ended record the person's words wait on goes on with them, or resumes later, its line naming
        // what holds them, by the planner's own verdict on its quest.
        ["an ended record whose words its run is planned for is going on"] =
            new(Look([Record("s1", "completed", "q1", Tree, said: ["w1"])], [Done], [GoesOn(Done)]),
                "s1", SessionGroup.Working, ShownState.GoingOn)
            {
                Also = row => Assert.Null(row.Holds),
            },
        ["words to a session to review go on: working, not to review"] =
            new(Look([Record("s1", "completed", "q1", Tree, said: ["w1"])], [Done], [GoesOn(Done)], [(Tree, new TreeWork(2, 0))]),
                "s1", SessionGroup.Working, ShownState.GoingOn),
        ["an archived record whose words go on is working"] =
            new(Look([Record("s1", "failed", "q1", said: ["w1"])], [Taken], [GoesOn(Taken)], archived: ["s1"]),
                "s1", SessionGroup.Working, ShownState.GoingOn),
        ["words the cap holds resume later, naming the cap"] =
            new(Look([Record("s1", "failed", "q1", Tree, said: ["w1"])], [Taken], [Verdict(Taken, StartVerdict.AtCapacity)], [(Tree, new TreeWork(2, 0))]),
                "s1", SessionGroup.Later, "failed")
            {
                Also = row => Assert.Equal((WordsHold.Cap, "the planner's sentence"), (row.Holds!.Why, row.Holds.Reason)),
            },
        ["words the person's hold holds resume later, naming the repository"] =
            new(Look([Record("s1", "completed", "q1", said: ["w1"])], [Done], [Verdict(Done, StartVerdict.Held)]),
                "s1", SessionGroup.Later, "completed")
            {
                Also = row => Assert.Equal((WordsHold.Hold, "engine"), (row.Holds!.Why, row.Holds.Repository)),
            },
        ["words a cooling account holds resume later, until its reset"] =
            new(Look([Record("s1", "completed", "q1", said: ["w1"])], [Done], [Verdict(Done, StartVerdict.Blocked)], waits: [Cooling("q1")]),
                "s1", SessionGroup.Later, "completed")
            {
                Also = row => Assert.Equal((WordsHold.Cooling, (DateTimeOffset?)Reset), (row.Holds!.Why, row.Holds.Until)),
            },
        ["words another hold at the spawn holds resume later, in its words"] =
            new(Look([Record("s1", "completed", "q1", said: ["w1"])], [Done], [Verdict(Done, StartVerdict.Blocked)], waits: [Cooling("q9")]),
                "s1", SessionGroup.Later, "completed")
            {
                Also = row => Assert.Equal((WordsHold.Waits, (DateTimeOffset?)null), (row.Holds!.Why, row.Holds.Until)),
            },
        ["words a pause holds resume later, naming the pause"] =
            new(Look([Record("s1", "stopped", "q1", said: ["w1"])], [Taken], [PausedVerdict(Taken)]),
                "s1", SessionGroup.Later, "stopped")
            {
                Also = row =>
                {
                    Assert.Equal(WordsHold.Paused, row.Holds!.Why);
                    Assert.Equal(ByAsk, row.PausedBy);
                },
            },
        ["words a session in the way holds resume later"] =
            new(Look([Record("s1", "completed", "q1", said: ["w1"])], [Done], [Verdict(Done, StartVerdict.RepositoryBusy)]),
                "s1", SessionGroup.Later, "completed")
            {
                Also = row => Assert.Equal(WordsHold.Busy, row.Holds!.Why),
            },
        ["an archived record whose words are held is archived, saying what holds them"] =
            new(Look([Record("s1", "completed", "q1", said: ["w1"])], [Done], [Verdict(Done, StartVerdict.AtCapacity)], archived: ["s1"]),
                "s1", SessionGroup.Archived, "completed")
            {
                Also = row => Assert.Equal(WordsHold.Cap, row.Holds!.Why),
            },
        ["words every one of which could not go on wait for nothing: ended"] =
            new(Look([Record("s1", "completed", "q1", said: ["w1"])], [Done], [Verdict(Done, StartVerdict.AtCapacity)], unable: [("s1", ["w1"])]),
                "s1", SessionGroup.Ended, "completed")
            {
                Also = row => Assert.Null(row.Holds),
            },
        ["a word said since the mark waits again"] =
            new(Look([Record("s1", "completed", "q1", said: ["w1", "w2"])], [Done], [GoesOn(Done)], unable: [("s1", ["w1"])]),
                "s1", SessionGroup.Working, ShownState.GoingOn),
        ["an earlier session of a quest whose words wait on a later one is ended"] =
            new(Look([Record("s1", "failed", "q1", said: ["w1"]), Record("s2", "completed", "q1", at: 10, said: ["w2"])], [Taken], [GoesOn(Taken)]),
                "s1", SessionGroup.Ended, "failed"),
        ["a conversation's words are its runner's: it is grouped as it ended"] =
            new(Look([Record("s1", "completed", kind: "chat", said: ["w1"])]), "s1", SessionGroup.Ended, "completed"),

        // Ended: a record.
        ["completed is ended"] = new(Look([Record("s1", "completed", "q1")], [Done]), "s1", SessionGroup.Ended, "completed"),
        ["declined is ended"] = new(Look([Record("s1", "declined", "q1")], [Quest("q1", "Declined")]), "s1", SessionGroup.Ended, "declined"),
        ["failed where the quest is not parked is ended"] =
            new(Look([Record("s1", "failed", "q1")], [Open], [Verdict(Open, StartVerdict.Held)]), "s1", SessionGroup.Ended, "failed"),
        ["stopped is ended"] = new(Look([Record("s1", "stopped", "q1")], [Taken]), "s1", SessionGroup.Ended, "stopped"),
        // SESSUX1b (D126 §2.2): its line says *held here until you try again*, from this fact.
        ["a stop that holds its quest is ended, and says it holds it"] =
            new(Look([Record("s1", "stopped", "q1")], [Taken], [Verdict(Taken, StartVerdict.Stopped)]), "s1", SessionGroup.Ended, "stopped")
            {
                Also = row => Assert.True(row.HoldsQuest),
            },
        ["a stop that holds its open quest says so too"] =
            new(Look([Record("s1", "stopped", "q1")], [Open], [Verdict(Open, StartVerdict.Stopped)]), "s1", SessionGroup.Ended, "stopped")
            {
                Also = row => Assert.True(row.HoldsQuest),
            },
        ["an earlier stop of a quest held by a later one does not hold it"] =
            new(Look([Record("s1", "stopped", "q1"), Record("s2", "stopped", "q1", at: 10)], [Taken], [Verdict(Taken, StartVerdict.Stopped)]),
                "s1", SessionGroup.Ended, "stopped")
            {
                Also = row => Assert.False(row.HoldsQuest),
            },
        ["stood down is ended"] = new(Look([Record("s1", "stood-down", "q1")], [Taken]), "s1", SessionGroup.Ended, "stood-down"),
        ["a conversation that ended is ended"] = new(Look([Record("s1", "completed", kind: "chat")]), "s1", SessionGroup.Ended, "completed"),

        // Archived: out of the way, kept whole, and never what needs the person.
        ["an archived ended session is archived"] =
            new(Look([Record("s1", "completed", "q1")], [Done], archived: ["s1"]), "s1", SessionGroup.Archived, "completed")
            {
                Also = row => Assert.True(row.Archived),
            },
        ["an archived session awaiting reply is archived"] =
            new(Look([Record("s1", "completed", "q1")], [Asking, Question], [Verdict(Asking, StartVerdict.Waiting)], archived: ["s1"]),
                "s1", SessionGroup.Archived, ShownState.AwaitingReply),
        ["a teammate's record may be archived here"] =
            new(Look([Record("laptop/s1", "completed", "q1")], [Done], archived: ["laptop/s1"]), "laptop/s1", SessionGroup.Archived, "completed"),
        ["an archived session waiting on you is never hidden"] =
            new(Look([Record("s1", "awaiting-person", "q1")], [Taken], archived: ["s1"]), "s1", SessionGroup.You, "awaiting-person")
            {
                Also = row => Assert.True(row.Archived),
            },
        ["an archived parked session is never hidden"] =
            new(Look([Record("s1", "failed", "q1")], [Taken], [Verdict(Taken, StartVerdict.Exhausted)], archived: ["s1"]),
                "s1", SessionGroup.You, ShownState.Parked),
        ["an archived session with work to review is never hidden"] =
            new(Look([Record("s1", "completed", "q1", Tree)], [Done], trees: [(Tree, new TreeWork(1, 0))], archived: ["s1"]),
                "s1", SessionGroup.Review, "completed"),
        ["an archived live session stays working"] =
            new(Look([Record("s1", "working", "q1")], [Taken], archived: ["s1"]), "s1", SessionGroup.Working, "working"),

        // SESSUX1f (D126 §5.4): deletable where the ledger would delete the record and this machine holds nothing of it.
        ["a conversation the ledger would delete is deletable"] =
            new(Look([Record("s1", "completed", kind: "chat", deletable: true)]), "s1", SessionGroup.Ended, "completed")
            {
                Also = row => Assert.True(row.Deletable),
            },
        ["an archived conversation the ledger would delete is deletable"] =
            new(Look([Record("s1", "stopped", kind: "chat", deletable: true)], archived: ["s1"]), "s1", SessionGroup.Archived, "stopped")
            {
                Also = row => Assert.True(row.Deletable),
            },
        ["a conversation whose tree or landing is still here is not deletable"] =
            new(Look([Record("s1", "completed", kind: "chat", deletable: true)], kept: ["s1"]), "s1", SessionGroup.Ended, "completed")
            {
                Also = row => Assert.False(row.Deletable),
            },
        ["a session the ledger keeps is not deletable"] =
            new(Look([Record("s1", "completed", "q1")], [Done]), "s1", SessionGroup.Ended, "completed")
            {
                Also = row => Assert.False(row.Deletable),
            },
    };

    public static TheoryData<string> CaseNames => [.. Cases.Keys];

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void Each_state_is_shown_in_its_group(string name)
    {
        var row = Cases[name];

        var read = SessionGroups.Read(row.Look).Single(each => each.Session == row.Session);

        Assert.Equal(row.Group, read.Group);
        Assert.Equal(row.Shown, read.Shown);
        row.Also?.Invoke(read);
    }

    /// <summary>
    /// The groups go in the order the person acts on them; within one, waiting and resuming go oldest first, to review
    /// and working by repository then start, so rows do not reshuffle as states move, and what ended newest first.
    /// </summary>
    [Fact]
    public void The_groups_and_the_rows_in_each_go_in_the_order_the_person_acts_on_them()
    {
        var look = Look(
        [
            Record("ended-old", "completed", at: 0, updated: 5),
            Record("ended-new", "completed", at: 1, updated: 50),
            Record("work-game", "working", repository: "game", at: 2),
            Record("work-engine-late", "working", at: 30),
            Record("work-engine", "working", at: 3),
            Record("you-new", "awaiting-person", at: 4, updated: 40),
            Record("you-old", "awaiting-person", at: 5, updated: 6),
            Record("review", "completed", "q1", Tree, at: 6),
            Record("archived", "completed", at: 7),
            Record("later", "completed", "q3", at: 8),
        ],
        [Done, Quest("q3", "Taken", awaits: "q2"), Question],
        [Verdict(Quest("q3", "Taken", awaits: "q2"), StartVerdict.Waiting)],
        [(Tree, new TreeWork(1, 0))],
        ["archived"]);

        var read = SessionGroups.Read(look);

        Assert.Equal(
            ["you-old", "you-new", "review", "work-engine", "work-engine-late", "work-game", "later", "ended-new", "ended-old", "archived"],
            read.Select(row => row.Session));
        Assert.Equal(
            [SessionGroup.You, SessionGroup.Review, SessionGroup.Working, SessionGroup.Later, SessionGroup.Ended, SessionGroup.Archived],
            read.Select(row => row.Group).Distinct());
    }

    /// <summary>The page may ask for some sessions; the rest still decide theirs, since a tree is one session's to review.</summary>
    [Fact]
    public void Asked_for_some_sessions_it_answers_those_judged_against_all_of_them()
    {
        var look = Look(
            [Record("s1", "failed", "q1", Tree), Record("s2", "completed", "q1", Tree, at: 10), Record("s3", "completed")],
            [Done], trees: [(Tree, new TreeWork(1, 0))]);

        var read = SessionGroups.Read(look, only: ["s1", "nobody"]);

        var row = Assert.Single(read);
        Assert.Equal("s1", row.Session);
        Assert.Equal(SessionGroup.Ended, row.Group);
    }

    /// <summary>
    /// git is asked only of a tree the reader would put to review: an ended session's own tree, this home's, the newest
    /// on it, held by nothing live and gone back into by no quest. A teammate's tree is on their machine.
    /// </summary>
    [Fact]
    public void Only_a_tree_that_could_be_to_review_is_judged()
    {
        const string carried = "X:/daoris/trees/aurora/engine/s-carried";
        const string busy = "X:/daoris/trees/aurora/engine/s-busy";
        const string parked = "X:/daoris/trees/aurora/engine/s-parked";
        const string elsewhere = "C:/checkouts/engine";
        // SESSUX1b: a taken quest the person's stop holds goes back into no tree until they release it.
        const string held = "X:/daoris/trees/aurora/engine/s-held";
        var carriedQuest = Quest("q2", "Taken");
        var parkedQuest = Quest("q3", "Taken");
        var heldQuest = Quest("q4", "Taken");
        var look = Look(
        [
            Record("old", "failed", "q1", Tree),
            Record("new", "completed", "q1", Tree.ToUpperInvariant(), at: 10),
            Record("cut", "failed", "q2", carried),
            Record("idle", "completed", tree: busy),
            Record("live", "working", tree: busy, at: 5),
            Record("park", "failed", "q3", parked),
            Record("stop", "stopped", "q4", held),
            Record("root", "completed", tree: elsewhere),
            Record("laptop/s9", "completed", tree: "X:/daoris/trees/aurora/engine/s-theirs"),
            Record("bare", "completed"),
        ],
        [Done, carriedQuest, parkedQuest, heldQuest],
        [Verdict(carriedQuest, StartVerdict.Start), Verdict(parkedQuest, StartVerdict.Exhausted), Verdict(heldQuest, StartVerdict.Stopped)]);

        var judged = SessionGroups.TreesToJudge(look, tree => tree.StartsWith("X:/daoris/trees/", StringComparison.OrdinalIgnoreCase));

        Assert.Equal([SessionGroups.Normal(Tree), held], judged.Select(SessionGroups.Normal), StringComparer.OrdinalIgnoreCase);
        // Asked for one session, only its tree.
        Assert.Empty(SessionGroups.TreesToJudge(look, _ => true, only: ["bare", "old"]));
    }

    /// <summary>The judgement is asked once per tree and kept by the tree, whatever the record spelled it as; a tree it cannot speak for is none.</summary>
    [Fact]
    public async Task The_trees_are_judged_once_each_and_read_by_the_tree()
    {
        const string other = "X:/daoris/trees/aurora/game/s-9f8e7d6c";
        var look = Look(
        [
            Record("s1", "completed", "q1", Tree.Replace('/', '\\')),
            Record("s2", "completed", tree: other, repository: "game"),
        ],
        [Done]);
        var asked = new List<string>();

        var judged = await SessionGroups.JudgeAsync(look, _ => true, (tree, _) =>
        {
            asked.Add(tree);
            return Task.FromResult(SessionGroups.Normal(tree) == SessionGroups.Normal(Tree) ? new TreeWork(4, 0) : null);
        });
        var read = SessionGroups.Read(judged);

        Assert.Equal(2, asked.Count);
        Assert.Equal(SessionGroup.Review, read.Single(row => row.Session == "s1").Group);
        Assert.Equal(4, read.Single(row => row.Session == "s1").Work!.Commits);
        Assert.Equal(SessionGroup.Ended, read.Single(row => row.Session == "s2").Group);
    }

    /// <summary>
    /// The records as the service answers them: a teammate's keyed `origin/id`, a record with no id skipped, the strikes
    /// and each quest's last session read by the planner's own readers (DRV6, D79), never a second copy of either rule.
    /// </summary>
    [Fact]
    public void The_records_are_read_as_the_service_answers_them()
    {
        var json = new JsonArray(
            Record("s1", "failed", "q1", Tree, at: 0),
            Record("s2", "stood-down", "q1", at: 20),
            Record("s3", "failed", "q1", at: 10),
            new JsonObject { ["repository"] = "engine", ["state"] = "completed" },
            Record("laptop/s4", "failed", "q1", at: 30)).ToJsonString();

        var look = SessionLook.From(json, [], [], _ => 0);

        Assert.Equal(["s1", "s2", "s3", "laptop/s4"], look.Records.Select(record => record.Id));
        var first = look.Records[0];
        Assert.Equal("engine", first.Repository);
        Assert.Equal("q1", first.Quest);
        Assert.Equal(Tree, first.Tree);
        Assert.Equal("driven", first.Kind);
        Assert.Equal(T0, first.Created);
        Assert.Equal(T0.AddMinutes(1), first.Updated);
        Assert.False(first.Live);
        Assert.True(look.Records[3].Teammate);
        // Two of this machine's failures; a stand-down is not a run, and a teammate's failure is theirs.
        Assert.Equal(2, look.Strikes["q1"]);
        Assert.Equal("s3", look.LastRun["q1"].Session);
    }

    /// <summary>
    /// ANSWER1c: the person's answer to a park, as the service answers it to this machine (ANSWER1b keeps the record
    /// parked with it). None, an empty one, or one that is not text is no answer.
    /// </summary>
    [Fact]
    public void A_parks_answer_is_read_as_the_service_answers_it()
    {
        var json = new JsonArray(
            Record("s1", "awaiting-person", "q1", answer: "Use the second."),
            Record("s2", "awaiting-person", "q2"),
            Record("s3", "awaiting-person", "q3", answer: ""),
            new JsonObject { ["id"] = "s4", ["state"] = "awaiting-person", ["answer"] = 7 }).ToJsonString();

        var records = SessionRecords.Parse(json);

        Assert.Equal("Use the second.", records[0].Answer);
        Assert.True(records[0].Answered);
        Assert.All(records.Skip(1), record =>
        {
            Assert.Null(record.Answer);
            Assert.False(record.Answered);
        });
    }

    /// <summary>
    /// The planner's verdicts, never a second copy of its rules: the loop's last look where a loop runs, and the planner
    /// over a fresh snapshot where none has looked yet (a terminal, or a desktop waiting on another driver's lock).
    /// </summary>
    [Fact]
    public async Task The_verdicts_are_the_last_looks_or_a_fresh_plans()
    {
        var ledger = new StandInLedger();
        ledger.Publish("q1", "engine");
        using var service = ledger.Client();
        var config = DriverConfig.Empty;
        var looked = new List<Consideration> { Verdict(Quest("q1", "Open"), StartVerdict.Exhausted) };

        var last = await SessionGroups.VerdictsAsync(service, config, SessionWire.Pipe, looked);
        var fresh = await SessionGroups.VerdictsAsync(service, config, SessionWire.Pipe, lastLook: null);

        Assert.Same(looked, last);
        var planned = Assert.Single(fresh);
        Assert.Equal("q1", planned.Quest.Id);
        Assert.Equal(StartVerdict.NotAdopted, planned.Verdict);
    }

    [Fact]
    public void The_last_look_is_replaced_whole_and_is_nothing_before_one()
    {
        var look = new LastLook();
        Assert.Null(look.Latest);

        look.Record([Verdict(Taken, StartVerdict.Exhausted)], [Cooling("q1")]);
        look.Record([Verdict(Open, StartVerdict.Start)]);

        Assert.Equal(StartVerdict.Start, Assert.Single(look.Latest!).Verdict);
        // MSG1f2: the cool-offs it held starts on go with it, so a look that held none leaves none.
        Assert.Empty(look.Waits);
        look.Record([Verdict(Open, StartVerdict.Blocked)], [Cooling("q1")]);
        Assert.Equal(["q1"], Assert.Single(look.Waits).Quests);
    }

    /// <summary>The records are asked of the service's own door, closed ones included; a refusal is the driver's sentence.</summary>
    [Fact]
    public async Task The_records_are_asked_of_the_services_door_with_the_closed_ones()
    {
        var handler = new Answering("""[{"id":"s1","repository":"engine","state":"completed"}]""");

        var json = await SessionRecords.ReadAsync("http://ledger.test/", null, handler);

        Assert.Equal("http://ledger.test/api/sessions?includeClosed=true", handler.Asked);
        Assert.Contains("\"s1\"", json);
        await Assert.ThrowsAsync<DriverException>(() =>
            SessionRecords.ReadAsync("http://ledger.test", null, new Answering("""{"error":"no"}""", HttpStatusCode.ServiceUnavailable)));
    }

    private sealed class Answering(string body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public string? Asked { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Asked = request.RequestUri!.ToString();
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }
}
