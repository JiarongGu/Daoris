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
        bool deletable = false, string[]? said = null, string? entry = null, string? from = null, string? fromWhy = null) => new()
        {
            // SESSONE1b: the line of work the service answers, and the session a hand-over continues, and why (SESSCTX1b).
            ["entry"] = entry,
            ["from"] = from,
            ["fromWhy"] = fromWhy,
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
        string[]? kept = null, (string Session, string[] Said)[]? unable = null, AccountWait[]? waits = null,
        string[]? chats = null, (string Session, WordsHold Holds)[]? held = null, (string Session, RunningCall Call)[]? calls = null,
        string[]? between = null) =>
        SessionLook.From(new JsonArray([.. records]).ToJsonString(), quests ?? [], considered ?? [], forgiven ?? (_ => 0)) with
        {
            Between = new HashSet<string>(between ?? [], StringComparer.Ordinal),
            Calls = (calls ?? []).ToDictionary(each => each.Session, each => each.Call, StringComparer.OrdinalIgnoreCase),
            Trees = (trees ?? []).ToDictionary(each => SessionGroups.Normal(each.Tree), each => each.Work, StringComparer.OrdinalIgnoreCase),
            Archived = (archived ?? []).ToDictionary(id => id, _ => T0, StringComparer.Ordinal),
            Kept = new HashSet<string>(kept ?? [], StringComparer.Ordinal),
            Unable = (unable ?? []).ToDictionary(
                each => each.Session, each => new GoOnMark(each.Said, ContinueWhy.Refused, T0), StringComparer.Ordinal),
            Waits = waits ?? [],
            ChatsWaiting = new HashSet<string>(chats ?? [], StringComparer.Ordinal),
            Held = (held ?? []).ToDictionary(each => each.Session, each => each.Holds, StringComparer.Ordinal),
        };

    /// <summary>The planner's start of a record the person's words wait on (MSG1b's <c>GoOn</c>): it goes on itself.</summary>
    private static Consideration GoesOn(QuestView quest) => Verdict(quest, StartVerdict.Start) with { GoesOn = true };

    /// <summary>A cool-off holding the starts of these quests (TOOL4g), ready at <see cref="Reset"/>.</summary>
    private static AccountWait Cooling(params string[] quests) =>
        new("claude-code", "Claude Code", "work", "default", Reset, Stated: true, "its account is cooling until 11:00.") { Quests = quests };

    private static readonly DateTimeOffset Reset = T0.AddHours(2);

    /// <summary>What the chat runner said held a chat's words (MSG1f3): its conversation's last line, in the hold's words.</summary>
    private static readonly WordsHold ChatHeld = new(WordsHold.Waits, "`engine` has no checkout on this machine any more. Your words wait on it.");

    /// <summary>A record's own account cooling, read from the home where no loop's look held its start (MSG1f3). After the reset it names.</summary>
    private static readonly WordsHold OwnCooling = new(WordsHold.Cooling, "work is cooling until 11:00.") { Until = Reset };

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

    /// <summary>A taken step whose standing reopen names session <c>s1</c> (FABRIC-CLOSE1c9), its submission's session.</summary>
    private static readonly QuestView ReopenNamesS1 = Taken with
    {
        Reopened = new QuestReopenView("desk", 3, new QuestSubmissionRef("desk", 2), "Do it again.", T0, Session: "s1"),
    };

    /// <summary>The ref an unforced discard of <see cref="Tree"/> keeps its commits at (SQUASHTIDY1c).</summary>
    private const string Kept = "refs/daoris/discarded/daoris/s-1a2b3c4d";

    /// <summary>A call the driver heard begin eight minutes into the session (WAITSAY1b). Before the table, which reads it.</summary>
    private static readonly RunningCall NpmTest = new("npm test", T0.AddMinutes(8));

    /// <summary>A squash merge onto <c>main</c> holds the tree's work, and its discard would go now (SQUASHTIDY1b). Before the table, which reads it.</summary>
    private static readonly HeldWork Squashed = new(new ContentHold("main", Squash: true), Kept, null);

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
        // WAITSAY1b (D152's UX7c amendment): a working session of this machine's says the call its agent is inside, as the
        // driver heard it; one that waits on the person, a teammate's and an ended one say none, whatever the memory holds.
        ["a working session says the call its agent is inside"] =
            new(Look([Record("s1", "working", "q1")], [Taken], calls: [("s1", NpmTest)]), "s1", SessionGroup.Working, "working")
            {
                Also = row => Assert.Equal(NpmTest, row.Calling),
            },
        ["a working session with no call open says none"] =
            new(Look([Record("s1", "working", "q1")], [Taken], calls: [("s2", NpmTest)]), "s1", SessionGroup.Working, "working")
            {
                Also = row => Assert.Null(row.Calling),
            },
        ["a parked session says no call"] =
            new(Look([Record("s1", "awaiting-person", "q1")], [Taken], calls: [("s1", NpmTest)]), "s1", SessionGroup.You, "awaiting-person")
            {
                Also = row => Assert.Null(row.Calling),
            },
        ["a teammate's working session says no call of this machine's"] =
            new(Look([Record("laptop/s1", "working", "q1")], [Taken], calls: [("laptop/s1", NpmTest)]), "laptop/s1", SessionGroup.Working, "working")
            {
                Also = row => Assert.Null(row.Calling),
            },
        ["an ended session says no call"] =
            new(Look([Record("s1", "failed", "q1")], [Taken], calls: [("s1", NpmTest)]), "s1", SessionGroup.Ended, "failed")
            {
                Also = row => Assert.Null(row.Calling),
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
        // RETRYWORK1: an open quest whose failed session committed work goes back into that tree, whatever holds its carry-on now.
        ["a failed first start whose committed work its open quest carries on is ended, not to review"] =
            new(Look([Record("s1", "failed", "q1", Tree)], [Open], [Verdict(Open, StartVerdict.AtCapacity) with { CarriesWork = 1 }],
                    [(Tree, new TreeWork(1, 0))]),
                "s1", SessionGroup.Ended, "failed"),
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
        // SAIDBACK1a (D137 §2.4): words taken back leave nothing waiting, so the record rests as it ended, even where the last
        // look planned it going on with them.
        ["an ended record whose words were taken back rests as it ended"] =
            new(Look([Record("s1", "completed", "q1", said: [])], [Done], [GoesOn(Done)]), "s1", SessionGroup.Ended, "completed"),
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
        // TICKSHOWN1: a start the service refused for the person's words is the verdict Blocked with the refusal's sentence.
        ["words whose start the service refused resume later, with the refusal's sentence"] =
            new(Look([Record("s1", "completed", "q1", said: ["w1"])], [Done],
                    [GoesOn(Done) with { Verdict = StartVerdict.Blocked, Reason = "refused  #q1 → engine: a session is already open." }]),
                "s1", SessionGroup.Later, "completed")
            {
                Also = row => Assert.Equal(
                    (WordsHold.Waits, "refused  #q1 → engine: a session is already open."), (row.Holds!.Why, row.Holds.Reason)),
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
        // FABRIC-CLOSE1c11 (D137 §3.2, D173's CLOSE1c9 note): the planner goes on with a reopen's words in the session the reopen
        // names though a later one is the quest's last record here, so that row reads going on, or later with the planner's hold.
        ["the session a reopen names goes on with its words though a later one is the quest's last"] =
            new(Look([Record("s1", "completed", "q1", Tree, said: ["w1"]), Record("s2", "completed", "q1", at: 10)], [ReopenNamesS1], [GoesOn(ReopenNamesS1)]),
                "s1", SessionGroup.Working, ShownState.GoingOn)
            {
                Also = row => Assert.Null(row.Holds),
            },
        ["the later last session of a quest whose reopen's words go on in an earlier one rests as it ended"] =
            new(Look([Record("s1", "completed", "q1", Tree, said: ["w1"]), Record("s2", "completed", "q1", at: 10)], [ReopenNamesS1], [GoesOn(ReopenNamesS1)]),
                "s2", SessionGroup.Ended, "completed"),
        ["a pause holding a reopen's words on the session it names resumes later, naming the pause"] =
            new(Look([Record("s1", "completed", "q1", said: ["w1"]), Record("s2", "completed", "q1", at: 10)], [ReopenNamesS1], [PausedVerdict(ReopenNamesS1)]),
                "s1", SessionGroup.Later, "completed")
            {
                Also = row =>
                {
                    Assert.Equal(WordsHold.Paused, row.Holds!.Why);
                    Assert.Equal(ByAsk, row.PausedBy);
                },
            },
        ["the person's stop of the last session holds the words on the session a reopen names"] =
            new(Look([Record("s1", "completed", "q1", said: ["w1"]), Record("s2", "stopped", "q1", at: 10)], [ReopenNamesS1], [Verdict(ReopenNamesS1, StartVerdict.Stopped)]),
                "s1", SessionGroup.Later, "completed")
            {
                Also = row => Assert.Equal((WordsHold.Waits, "the planner's sentence"), (row.Holds!.Why, row.Holds.Reason)),
            },
        // ACCEPTFOOT1b (D164's ACCEPTFOOT1 note): an open conversation's record stays `working` between turns. Once its turn has ended
        // with nothing waiting, its own tree offers its work to land as an ended session's does (LAND4), and it stays working; while a
        // turn runs, nothing. Never a discard while it is open: its tree is still in use.
        ["an open conversation between turns offers its tree's work to land, and stays working"] =
            new(Look([Record("s1", "working", kind: "chat", tree: Tree)], trees: [(Tree, new TreeWork(2, 1))], between: ["s1"]),
                "s1", SessionGroup.Working, "working")
            {
                Also = row =>
                {
                    Assert.Equal(new LandOffer("daoris/s-1a2b3c4d", "s-1a2b3c4d", 2, 1), row.Lands);
                    Assert.Null(row.Discards);
                },
            },
        ["an open conversation whose turn runs offers nothing to land"] =
            new(Look([Record("s1", "working", kind: "chat", tree: Tree)], trees: [(Tree, new TreeWork(2, 0))]),
                "s1", SessionGroup.Working, "working")
            {
                Also = row => Assert.Null(row.Lands),
            },
        ["an open conversation between turns whose work the line holds by content offers no discard"] =
            new(Look([Record("s1", "working", kind: "chat", tree: Tree)], trees: [(Tree, new TreeWork(2, 0) { Held = Squashed })], between: ["s1"]),
                "s1", SessionGroup.Working, "working")
            {
                Also = row => Assert.Equal((null, null), (row.Lands, row.Discards)),
            },
        // MSG1f3: a chat's words are its runner's, which takes them up at once, so a chat they wait on goes on, unless its
        // runner said what holds them; one whose every word could not go on is not among them, and rests as it ended.
        ["an ended chat whose words wait is going on: its runner takes them up at once"] =
            new(Look([Record("s1", "completed", kind: "chat", said: ["w1"])], chats: ["s1"]), "s1", SessionGroup.Working, ShownState.GoingOn)
            {
                Also = row => Assert.Null(row.Holds),
            },
        ["an archived chat whose words go on is working"] =
            new(Look([Record("s1", "stopped", kind: "chat", said: ["w1"])], archived: ["s1"], chats: ["s1"]),
                "s1", SessionGroup.Working, ShownState.GoingOn),
        ["a chat its runner held resumes later, in the runner's words"] =
            new(Look([Record("s1", "completed", kind: "chat", said: ["w1"])], chats: ["s1"], held: [("s1", ChatHeld)]),
                "s1", SessionGroup.Later, "completed")
            {
                Also = row => Assert.Same(ChatHeld, row.Holds),
            },
        ["a chat its account's cool-off held resumes later, until the reset"] =
            new(Look([Record("s1", "completed", kind: "chat", said: ["w1"])], chats: ["s1"], held: [("s1", OwnCooling)]),
                "s1", SessionGroup.Later, "completed")
            {
                Also = row => Assert.Equal((WordsHold.Cooling, (DateTimeOffset?)Reset), (row.Holds!.Why, row.Holds.Until)),
            },
        ["an archived chat its runner held is archived, saying what holds it"] =
            new(Look([Record("s1", "completed", kind: "chat", said: ["w1"])], archived: ["s1"], chats: ["s1"], held: [("s1", ChatHeld)]),
                "s1", SessionGroup.Archived, "completed")
            {
                Also = row => Assert.Same(ChatHeld, row.Holds),
            },
        ["a chat with no words its runner could take rests as it ended"] =
            new(Look([Record("s1", "completed", kind: "chat", said: ["w1"])]), "s1", SessionGroup.Ended, "completed")
            {
                Also = row => Assert.Null(row.Holds),
            },
        // MSG1f3: a fresh plan cannot see a cool-off, so the gatherer reads the record's own account's from the home; the
        // planner's own holds still come first, and the loop's look says its own.
        ["words whose own account cools resume later where no loop has looked, until its reset"] =
            new(Look([Record("s1", "completed", "q1", said: ["w1"])], [Done], [GoesOn(Done)], held: [("s1", OwnCooling)]),
                "s1", SessionGroup.Later, "completed")
            {
                Also = row => Assert.Equal((WordsHold.Cooling, (DateTimeOffset?)Reset, "work is cooling until 11:00."),
                    (row.Holds!.Why, row.Holds.Until, row.Holds.Reason)),
            },
        ["the planner's own hold comes before a cool-off read from the home"] =
            new(Look([Record("s1", "completed", "q1", said: ["w1"])], [Done], [Verdict(Done, StartVerdict.AtCapacity)], held: [("s1", OwnCooling)]),
                "s1", SessionGroup.Later, "completed")
            {
                Also = row => Assert.Equal(WordsHold.Cap, row.Holds!.Why),
            },

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

        // LAND4: a session that ended with commits no branch of the person's holds offers its landing, whatever its ending and
        // whichever group it rests in, naming its branch and its tree; the group is the reader's as before.
        ["a session finished at a checkpoint with commits offers its landing"] =
            new(Look([Record("s1", "completed", "q1", Tree)], [Taken], trees: [(Tree, new TreeWork(2, 0))]), "s1", SessionGroup.Review, "completed")
            {
                Also = row => Assert.Equal(new LandOffer("daoris/s-1a2b3c4d", "s-1a2b3c4d", 2, 0), row.Lands),
            },
        // TREEFOLLOW1: a tree its agent moved onto a branch of its own offers that branch, read from the tree, not the layout's name.
        ["a session whose tree moved onto its own branch offers that branch"] =
            new(Look([Record("s1", "completed", "q1", Tree)], [Taken], trees: [(Tree, new TreeWork(2, 0) { Branch = "feature/TK-1-engine" })]),
                "s1", SessionGroup.Review, "completed")
            {
                Also = row => Assert.Equal(new LandOffer("feature/TK-1-engine", "s-1a2b3c4d", 2, 0), row.Lands),
            },
        ["a failed session with commits its quest goes back into offers its landing, and rests ended"] =
            new(Look([Record("s1", "failed", "q1", Tree)], [Taken], [Verdict(Taken, StartVerdict.AtCapacity)], [(Tree, new TreeWork(3, 1))]),
                "s1", SessionGroup.Ended, "failed")
            {
                Also = row => Assert.Equal(new LandOffer("daoris/s-1a2b3c4d", "s-1a2b3c4d", 3, 1), row.Lands),
            },
        ["a parked quest's last session with commits offers its landing beside Try again"] =
            new(Look([Record("s1", "failed", "q1", Tree)], [Taken], [Verdict(Taken, StartVerdict.Exhausted)], [(Tree, new TreeWork(1, 0))]),
                "s1", SessionGroup.You, ShownState.Parked)
            {
                Also = row => Assert.Equal(1, row.Lands!.Commits),
            },
        ["a stopped session with commits offers its landing"] =
            new(Look([Record("s1", "stopped", "q1", Tree)], [Taken], [Verdict(Taken, StartVerdict.Stopped)], [(Tree, new TreeWork(2, 0))]),
                "s1", SessionGroup.Review, "stopped")
            {
                Also = row => Assert.Equal(2, row.Lands!.Commits),
            },
        ["the asker of a waiting quest with commits offers its landing while it resumes later"] =
            new(Look([Record("s1", "completed", "q1", Tree)], [Asking, Question], [Verdict(Asking, StartVerdict.Waiting)], [(Tree, new TreeWork(3, 0))]),
                "s1", SessionGroup.Later, ShownState.AwaitingReply)
            {
                Also = row => Assert.Equal(3, row.Lands!.Commits),
            },
        ["a session with no commits offers no landing"] =
            new(Look([Record("s1", "failed", "q1", Tree)], [Taken], [Verdict(Taken, StartVerdict.Start)], [(Tree, new TreeWork(0, 0))]),
                "s1", SessionGroup.Ended, "failed")
            {
                Also = row => Assert.Null(row.Lands),
            },
        ["uncommitted changes alone offer no landing: a branch would leave them behind"] =
            new(Look([Record("s1", "completed", "q1", Tree)], [Done], trees: [(Tree, new TreeWork(0, 2))]), "s1", SessionGroup.Review, "completed")
            {
                Also = row => Assert.Null(row.Lands),
            },
        ["commits git could not count offer no landing, though they stay to review"] =
            new(Look([Record("s1", "completed", "q1", Tree)], [Done], trees: [(Tree, new TreeWork(null, 0))]), "s1", SessionGroup.Review, "completed")
            {
                Also = row => Assert.Null(row.Lands),
            },
        ["an earlier session on a tree offers no landing: the newest stands for it"] =
            new(Look([Record("s1", "failed", "q1", Tree), Record("s2", "failed", "q1", Tree, at: 10)], [Taken], [Verdict(Taken, StartVerdict.Start)],
                    [(Tree, new TreeWork(2, 0))]),
                "s1", SessionGroup.Ended, "failed")
            {
                Also = row => Assert.Null(row.Lands),
            },
        ["a tree a live session holds offers no landing"] =
            new(Look([Record("s1", "failed", "q1", Tree), Record("s2", "working", "q1", Tree, at: 10)], [Taken], trees: [(Tree, new TreeWork(2, 0))]),
                "s1", SessionGroup.Ended, "failed")
            {
                Also = row => Assert.Null(row.Lands),
            },
        ["words going on in its tree offer no landing: it is working again"] =
            new(Look([Record("s1", "completed", "q1", Tree, said: ["w1"])], [Done], [GoesOn(Done)], [(Tree, new TreeWork(2, 0))]),
                "s1", SessionGroup.Working, ShownState.GoingOn)
            {
                Also = row => Assert.Null(row.Lands),
            },
        ["a teammate's record offers no landing: its tree is on its machine"] =
            new(Look([Record("laptop/s1", "failed", "q1", Tree)], [Taken], trees: [(Tree, new TreeWork(3, 0))]), "laptop/s1", SessionGroup.Ended, "failed")
            {
                Also = row => Assert.Null(row.Lands),
            },

        // SQUASHTIDY1b: commits the line holds by content (a squash merge, a cherry-pick) are not offered to land again, since
        // accepting would make a branch of work already there. The tree offers its discard instead, saying where the work is
        // in Discard's own words and naming the ref an unforced discard keeps its commits at.
        ["a session whose work the line holds by content offers its discard, not its landing"] =
            new(Look([Record("s1", "completed", "q1", Tree)], [Done], trees: [(Tree, new TreeWork(2, 0) { Held = Squashed })]),
                "s1", SessionGroup.Review, "completed")
            {
                Also = row =>
                {
                    Assert.Null(row.Lands);
                    Assert.Equal(new DiscardOffer("daoris/s-1a2b3c4d", "s-1a2b3c4d", Squashed), row.Discards);
                },
            },
        ["a failed session whose work a person's branch holds by content offers its discard wherever it rests"] =
            new(Look([Record("s1", "failed", "q1", Tree)], [Taken], [Verdict(Taken, StartVerdict.AtCapacity)],
                    [(Tree, new TreeWork(1, 0) { Held = new HeldWork(new ContentHold("feature/x", Squash: false), Kept, null) })]),
                "s1", SessionGroup.Ended, "failed")
            {
                Also = row =>
                {
                    Assert.Null(row.Lands);
                    Assert.Equal("feature/x", row.Discards!.Work.Held.Where);
                },
            },
        ["a held tree that holds what no commit does offers no landing, and says why its tree stays"] =
            new(Look([Record("s1", "completed", "q1", Tree)], [Done],
                    trees: [(Tree, new TreeWork(2, 0) { Held = Squashed with { Keeps = null, Stays = "it has 1 uncommitted path(s), which a discard would destroy" } })]),
                "s1", SessionGroup.Review, "completed")
            {
                Also = row =>
                {
                    Assert.Null(row.Lands);
                    Assert.Null(row.Discards!.Work.Keeps);
                    Assert.Null(row.Discards.KeptAt);
                },
            },
        ["a tree with commits held nowhere else offers its landing and no discard"] =
            new(Look([Record("s1", "completed", "q1", Tree)], [Done], trees: [(Tree, new TreeWork(2, 0))]), "s1", SessionGroup.Review, "completed")
            {
                Also = row =>
                {
                    Assert.NotNull(row.Lands);
                    Assert.Null(row.Discards);
                },
            },
        ["words going on in a held tree offer no discard: it is working again"] =
            new(Look([Record("s1", "completed", "q1", Tree, said: ["w1"])], [Done], [GoesOn(Done)], [(Tree, new TreeWork(2, 0) { Held = Squashed })]),
                "s1", SessionGroup.Working, ShownState.GoingOn)
            {
                Also = row => Assert.Null(row.Discards),
            },
        ["a teammate's held record offers no discard: its tree is on its machine"] =
            new(Look([Record("laptop/s1", "failed", "q1", Tree)], [Taken], trees: [(Tree, new TreeWork(3, 0) { Held = Squashed })]),
                "laptop/s1", SessionGroup.Ended, "failed")
            {
                Also = row => Assert.Null(row.Discards),
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
    /// git is asked only of a tree whose work the reader could put to review or offer to land: an ended session's own tree,
    /// this home's, the newest on it and held by nothing live. LAND4 judges a tree its quest goes back into too, a cut-off's
    /// and a parked quest's, since its commits are offered to land whatever its ending. A teammate's tree is on their machine.
    /// </summary>
    [Fact]
    public void Only_a_tree_whose_work_could_be_reviewed_or_landed_is_judged()
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

        Assert.Equal([SessionGroups.Normal(Tree), carried, parked, held], judged.Select(SessionGroups.Normal), StringComparer.OrdinalIgnoreCase);
        // Asked for one session, only its tree.
        Assert.Empty(SessionGroups.TreesToJudge(look, _ => true, only: ["bare", "old"]));
    }

    /// <summary>ACCEPTFOOT1b: an open conversation's own tree is judged between its turns alone, since only then does it offer its work.</summary>
    [Fact]
    public void An_open_conversations_tree_is_judged_between_its_turns_alone()
    {
        const string talking = "X:/daoris/trees/aurora/engine/s-talking";
        var look = Look([Record("rest", "working", kind: "chat", tree: Tree), Record("turn", "working", kind: "chat", tree: talking)], between: ["rest"]);

        Assert.Equal([SessionGroups.Normal(Tree)], SessionGroups.TreesToJudge(look, _ => true).Select(SessionGroups.Normal));
    }

    /// <summary>
    /// 🔴 COAGENT1b (D199 point 3; the co-agent design §2.4): a partner's ended record is never eligible to land, so no tree of its is
    /// judged and neither its own Accept nor an entry's offers it; a helper's ended record keeps its old reading; the press and the
    /// terminal's verb refuse it in words naming the lead's merge.
    /// </summary>
    [Fact]
    public void A_partners_record_is_never_eligible_to_land_at_any_door()
    {
        const string copy = "X:/daoris/trees/aurora/engine/p-pc000001";
        var partner = Record("pt1", "completed", kind: "chat", tree: copy);
        partner["serves"] = "lead1";
        partner["piece"] = "pc000001";
        var helper = Record("hp1", "completed", kind: "chat", tree: Tree);
        helper["serves"] = "lead1";
        var look = Look([partner, helper]);

        Assert.Equal([SessionGroups.Normal(Tree)], SessionGroups.TreesToJudge(look, _ => true).Select(SessionGroups.Normal));
        var refused = TreesWords.PartnerLands(look.Records, "pt1")!;
        Assert.Equal(1, refused.Code);
        Assert.Contains("git merge --no-ff refs/daoris/pieces/pc000001", Assert.Single(refused.Error));
        Assert.Null(TreesWords.PartnerLands(look.Records, "hp1"));
    }

    /// <summary>
    /// ACCEPTFOOT1b: which open conversations of this machine's are between turns. The word of the runner that holds a conversation
    /// first, as the page's foot reads it (CONV4a); where nothing in this process holds it (a terminal's listing, or a conversation
    /// another process runs), its own record of its turns, which its runner writes as each ends. Only a live conversation of this
    /// machine's: a driven session's turn is its run's, an ended one offers its work as any ended session does, and a teammate's runs
    /// on their machine.
    /// </summary>
    [Fact]
    public void A_conversation_is_between_turns_by_its_runners_word_else_by_its_record_of_its_turns()
    {
        var directory = Path.Combine(Path.GetTempPath(), "daoris-between-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var events = new SessionEvents(directory);
            foreach (var id in new[] { "said", "c2", "driven", "ended", "laptop/c9" }.Where(SessionEventText.IsLocalId))
            {
                events.Append(id, new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "hi" });
                events.Append(id, new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn" });
            }

            events.Append("said", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "and then?" });
            var records = SessionRecords.Parse(new JsonArray(
                Record("c1", "working", kind: "chat"), Record("runs", "working", kind: "chat"), Record("c2", "working", kind: "chat"),
                Record("said", "working", kind: "chat"), Record("driven", "working", kind: "driven"),
                Record("ended", "completed", kind: "chat"), Record("laptop/c9", "working", kind: "chat")).ToJsonString());
            // The runner here holds c1, its turn ended, and `runs`, whose turn runs though its record says it ended; nothing else.
            bool? Runner(string id) => id switch { "c1" => true, "runs" => false, _ => null };

            Assert.Equal(["c1", "c2"], SessionGroups.Between(records, Runner, events).Order(StringComparer.Ordinal));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
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

    /// <summary>
    /// XAGENT1g3: the opinion gates the last look judged are replaced whole each look, and are nothing before one, so the attention
    /// band's route answers from them where it once ran a gate per poll.
    /// </summary>
    [Fact]
    public void The_last_opinions_are_replaced_whole_and_are_nothing_before_a_look()
    {
        var kept = new LastOpinions();
        Assert.Null(kept.Latest);

        var gate = new OpinionGateState(OpinionGateStates.Disputed, "engine") { Session = "s1" };
        kept.Record([new OpinionJudged("s1", "q1", "engine", "work", DateTimeOffset.UnixEpoch, Auto: true, gate)]);
        var judged = Assert.Single(kept.Latest!);
        Assert.Equal(("s1", true, OpinionGateStates.Disputed), (judged.Session, judged.Auto, judged.Gate.State));

        kept.Record([]);
        Assert.Empty(kept.Latest!);
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

    // ——— SESSONE1b (D167, the co-working design §1.6, §3): the reader groups by the entry the service answers. An entry's group is
    // the first of D126's groups any of its parts qualifies for, its leading part is the one that put it there, its parts go
    // newest last each with how it began, and an ended intake whose ask became quests leaves the list.

    private const string AppKey = "ask:c98a7e/app";
    private const string DbKey = "ask:c98a7e/db";

    private static QuestView AskQuest(string id, string from, string to, string status = "Taken", string? publishedBy = null) =>
        new(id, from, to, $"The work of #{id}", "A body.", status) { PublishedBy = publishedBy };

    /// <summary>The design's §3 replay, with neutral names: ask #c98a7e over an app and its database.</summary>
    private static SessionLook Evidence() => Look(
        [
            Record("a1b2c3d4", "failed", repository: "ask #c98a7e", kind: "chat", ask: "c98a7e", entry: "ask:c98a7e/intake", at: 0),
            Record("ce81a888", "failed", "28b052ac", repository: "app", entry: AppKey, at: 5),
            Record("62c005bc", "completed", "28b052ac", repository: "app", entry: AppKey, at: 10),
            Record("283ef343", "completed", "3068f747", repository: "db", entry: DbKey, at: 12),
            Record("8eba4af2", "completed", "711d2524", repository: "app", entry: AppKey, at: 30),
            Record("e2ddbfc4", "completed", "01ba2c71", repository: "db", entry: DbKey, at: 40),
            Record("53416cea", "completed", "63df101b", repository: "db", entry: DbKey, at: 50),
            Record("2487c69d", "completed", "3068f747", repository: "db", entry: DbKey, at: 60),
            Record("486d9ca0", "working", "3068f747", repository: "db", entry: DbKey, at: 70),
            Record("cf8faff5", "working", "711d2524", repository: "app", entry: AppKey, at: 80),
        ],
        quests:
        [
            AskQuest("28b052ac", "ask #c98a7e", "app"),
            AskQuest("3068f747", "ask #c98a7e", "db"),
            AskQuest("711d2524", "db", "app", publishedBy: "283ef343"),
            AskQuest("01ba2c71", "app", "db", publishedBy: "8eba4af2"),
            AskQuest("63df101b", "app", "db", publishedBy: "8eba4af2"),
        ]);

    [Fact]
    public void The_records_carry_the_entry_the_service_answers_and_the_session_they_continue()
    {
        var json = new JsonArray(
            Record("s1", "completed", "q1", entry: "ask:a/app"),
            Record("s2", "completed", "q1", entry: "", from: "s1"),
            Record("s3", "completed", "q1")).ToJsonString();

        var records = SessionRecords.Parse(json);

        Assert.Equal<string?>(["ask:a/app", null, null], records.Select(record => record.Entry));
        Assert.Equal<string?>([null, "s1", null], records.Select(record => record.From));
        // A host from before the key answers none: the record is its own line of work, as the service derives it where a fact is gone.
        Assert.Equal(["ask:a/app", "session:s2/engine", "session:s3/engine"], records.Select(record => record.EntryKey));
    }

    /// <summary>
    /// COAGENT1b (the co-agent design §7.3): a record's <c>serves</c> and <c>piece</c> as the service answers them on loopback. A
    /// partner carries both; a helper serves alone; a piece with no lead is no partner, as the service keeps a piece only beside
    /// <c>serves</c>; a host from before, or a teammate's record, answers neither.
    /// </summary>
    [Fact]
    public void The_records_carry_a_partners_lead_and_piece_kept_together()
    {
        var partner = Record("s1", "working", kind: "chat");
        partner["serves"] = "lead1";
        partner["piece"] = "pc000001";
        var helper = Record("s2", "completed", kind: "chat");
        helper["serves"] = "lead1";
        var stray = Record("s3", "completed", kind: "chat");
        stray["piece"] = "pc000002";
        var json = new JsonArray(partner, helper, stray, Record("s4", "completed", "q1")).ToJsonString();

        var records = SessionRecords.Parse(json);

        Assert.Equal<string?>(["lead1", "lead1", null, null], records.Select(record => record.Serves));
        Assert.Equal<string?>(["pc000001", null, null, null], records.Select(record => record.Piece));
        Assert.Equal([true, false, false, false], records.Select(record => record.Partner));
    }

    [Fact]
    public void The_evidences_ask_reads_as_two_entries_and_its_intake_is_off_the_list()
    {
        var entries = SessionGroups.ReadEntries(Evidence());

        Assert.Equal([AppKey, DbKey], entries.Select(entry => entry.Key).Order(StringComparer.Ordinal));
        var app = entries.Single(entry => entry.Key == AppKey);
        var db = entries.Single(entry => entry.Key == DbKey);
        Assert.Equal(("ask:c98a7e", "app", "db"), (app.Origin, app.Repository, db.Repository));

        Assert.Equal(["ce81a888", "62c005bc", "8eba4af2", "cf8faff5"], app.Parts.Select(part => part.Session));
        Assert.Equal(
            [PartBegan.Opened, PartBegan.Retried, PartBegan.Asked, PartBegan.Resumed], app.Parts.Select(part => part.Began));
        Assert.Equal(("db", null), (app.Parts[2].AskedBy, app.Parts[3].AskedBy));
        Assert.Equal(("ce81a888", "8eba4af2"), (app.Parts[1].After, app.Parts[3].After));

        Assert.Equal(["283ef343", "e2ddbfc4", "53416cea", "2487c69d", "486d9ca0"], db.Parts.Select(part => part.Session));
        Assert.Equal(
            [PartBegan.Opened, PartBegan.Asked, PartBegan.Asked, PartBegan.Resumed, PartBegan.Resumed], db.Parts.Select(part => part.Began));

        // The live part leads, and the rows the page still reads are untouched, the intake's among them.
        Assert.Equal((SessionGroup.Working, "cf8faff5"), (app.Group, app.Leading));
        Assert.Equal((SessionGroup.Working, "486d9ca0"), (db.Group, db.Leading));
        Assert.Contains("a1b2c3d4", SessionGroups.Read(Evidence()).Select(row => row.Session));
        Assert.DoesNotContain(entries, entry => entry.Key.EndsWith("/intake", StringComparison.Ordinal));
    }

    /// <summary>The second ask of the evidence: an intake and one driven session resumed many times, which is one entry.</summary>
    [Fact]
    public void An_intake_and_one_session_resumed_many_times_are_one_entry()
    {
        var look = Look(
            [
                Record("1n7a8e00", "completed", repository: "ask #841c38", kind: "chat", ask: "841c38", entry: "ask:841c38/intake", at: 0),
                Record("5e55a000", "completed", "q9", repository: "engine", entry: "ask:841c38/engine", at: 10),
            ],
            quests: [AskQuest("q9", "ask #841c38", "engine", "Done")]);

        var entry = Assert.Single(SessionGroups.ReadEntries(look));

        Assert.Equal(("ask:841c38/engine", SessionGroup.Ended, "5e55a000"), (entry.Key, entry.Group, entry.Leading));
        Assert.Equal(["5e55a000"], entry.Parts.Select(part => part.Session));
    }

    [Fact]
    public void An_intake_stays_listed_while_it_runs_or_when_its_ask_never_became_quests()
    {
        var running = Look(
            [Record("1n7a8e00", "awaiting-person", repository: "ask #a1", kind: "chat", ask: "a1", entry: "ask:a1/intake")],
            quests: [AskQuest("q1", "ask #a1", "engine")]);
        var unpublished = Look(
            [Record("1n7a8e01", "completed", repository: "ask #a2", kind: "chat", ask: "a2", entry: "ask:a2/intake")]);

        Assert.Equal(SessionGroup.You, Assert.Single(SessionGroups.ReadEntries(running)).Group);
        Assert.Equal(SessionGroup.Ended, Assert.Single(SessionGroups.ReadEntries(unpublished)).Group);
    }

    /// <summary>An ask's quests are known by the sessions that worked them where the quest itself was cleared.</summary>
    [Fact]
    public void An_ended_intake_is_off_the_list_where_a_session_of_its_ask_remains()
    {
        var look = Look(
            [
                Record("1n7a8e02", "completed", repository: "ask #a3", kind: "chat", ask: "a3", entry: "ask:a3/intake"),
                Record("5e55a001", "completed", "q1", repository: "engine", entry: "ask:a3/engine", at: 5),
            ]);

        Assert.Equal(["ask:a3/engine"], SessionGroups.ReadEntries(look).Select(entry => entry.Key));
    }

    /// <summary>The first of D126's groups any part qualifies for, and the part that put it there leads: the newest of its group.</summary>
    [Fact]
    public void An_entry_is_in_the_first_group_any_part_qualifies_for_and_its_leading_part_put_it_there()
    {
        var look = Look(
        [
            Record("e1d00001", "awaiting-person", "q1", entry: "quest:q1/engine", at: 0),
            Record("e1d00002", "working", "q2", entry: "quest:q1/engine", at: 10),
            Record("e1d00003", "working", "q3", entry: "quest:q1/engine", at: 20),
            Record("e1d00004", "completed", "q4", repository: "game", entry: "quest:q4/game", at: 5),
            Record("e1d00005", "working", "q5", repository: "game", entry: "quest:q4/game", at: 6),
            Record("e1d00006", "working", "q6", repository: "game", entry: "quest:q4/game", at: 7),
        ]);

        var entries = SessionGroups.ReadEntries(look);

        Assert.Equal([("quest:q1/engine", SessionGroup.You, "e1d00001"), ("quest:q4/game", SessionGroup.Working, "e1d00006")],
            entries.Select(entry => (entry.Key, entry.Group, entry.Leading)));
    }

    [Fact]
    public void A_cut_off_part_is_carried_on_and_a_handed_over_part_says_whose_it_continues()
    {
        var look = Look(
            [
                Record("c0d00001", "stopped", "q1", entry: "quest:q1/engine", at: 0),
                Record("c0d00002", "completed", "q1", entry: "quest:q1/engine", at: 10),
                Record("c0d00003", "working", "q1", entry: "quest:q1/engine", at: 20, from: "c0d00002"),
                Record("c0d00004", "completed", "q2", entry: "quest:q1/engine", at: 30),
            ],
            quests: [AskQuest("q1", "game", "engine"), AskQuest("q2", "game", "engine") with { Parent = "q1" }]);

        var parts = Assert.Single(SessionGroups.ReadEntries(look)).Parts;

        Assert.Equal(
            [(PartBegan.Opened, null), (PartBegan.CarriedOn, "c0d00001"), (PartBegan.HandedOver, "c0d00002"), (PartBegan.CarriedOn, null)],
            parts.Select(part => (part.Began, part.After)));
    }

    /// <summary>
    /// 🔴 SESSCTX1b (the session-context design §7): every continuation names its link, so a part says how it began by its link's why,
    /// never every link as a hand-over. The five a hand-over is asked for are handed over; `carry-on` carried on; `retry` retried;
    /// `resume` a move of its own; a why this build has no word for, and a record from before with none, as before.
    /// </summary>
    [Theory]
    [InlineData("press", PartBegan.HandedOver)]
    [InlineData("near", PartBegan.HandedOver)]
    [InlineData("limit", PartBegan.HandedOver)]
    [InlineData("agent", PartBegan.HandedOver)]
    [InlineData("curve", PartBegan.HandedOver)]
    [InlineData("carry-on", PartBegan.CarriedOn)]
    [InlineData("retry", PartBegan.Retried)]
    [InlineData("resume", PartBegan.Moved)]
    [InlineData("moved-on", PartBegan.HandedOver)]
    [InlineData(null, PartBegan.HandedOver)]
    public void A_part_with_a_link_began_by_its_why(string? why, string began)
    {
        var look = Look(
            [
                Record("c0d00001", "completed", "q1", entry: "quest:q1/engine", at: 0),
                Record("c0d00002", "working", "q1", entry: "quest:q1/engine", at: 10, from: "c0d00001", fromWhy: why),
            ],
            quests: [AskQuest("q1", "game", "engine")]);

        var parts = Assert.Single(SessionGroups.ReadEntries(look)).Parts;

        Assert.Equal([(PartBegan.Opened, null), (began, "c0d00001")], parts.Select(part => (part.Began, part.After)));
    }

    /// <summary>A record with no key (a host from before) is its own line of work; the entry's place is read from the part alone.</summary>
    [Fact]
    public void A_record_with_no_entry_is_its_own_line_of_work()
    {
        var look = Look([Record("0ld00001", "completed", "q1", at: 0), Record("0ld00002", "completed", "q1", at: 5)]);

        Assert.Equal(["session:0ld00001/engine", "session:0ld00002/engine"], SessionGroups.ReadEntries(look).Select(entry => entry.Key).Order(StringComparer.Ordinal));
    }

    /// <summary>Asked of some sessions, an entry holding one is answered whole: its other parts decide its group.</summary>
    [Fact]
    public void Asked_for_some_sessions_it_answers_each_entry_they_are_parts_of_whole()
    {
        var entries = SessionGroups.ReadEntries(Evidence(), ["62c005bc"]);

        var app = Assert.Single(entries);
        Assert.Equal((AppKey, 4), (app.Key, app.Parts.Count));
    }

    /// <summary>The groups in the order a list shows them, each group's entries by its leading part as the rows are.</summary>
    [Fact]
    public void The_entries_go_in_the_groups_order()
    {
        var look = Look(
        [
            Record("0a000001", "completed", "q1", entry: "quest:q1/engine", at: 0),
            Record("0a000002", "awaiting-person", "q2", entry: "quest:q2/engine", at: 5),
            Record("0a000003", "working", "q3", entry: "quest:q3/engine", at: 6),
        ]);

        Assert.Equal(
            [SessionGroup.You, SessionGroup.Working, SessionGroup.Ended], SessionGroups.ReadEntries(look).Select(entry => entry.Group));
    }
}
