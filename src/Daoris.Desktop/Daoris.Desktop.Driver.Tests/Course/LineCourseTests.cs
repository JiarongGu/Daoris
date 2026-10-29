using System.Globalization;
using System.Text.RegularExpressions;
using Daoris.Driver;
using T = Daoris.Driver.CourseTrigger;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1e part 2 (the foundation design §3.1, §3.2, §10 row 1e): <see cref="CourseDecision.Decide"/>, the one decision of how a line goes
/// on, read by nothing. Every case here is a case of today's suites, ported: its facts as the course reads them and its trigger, answered as
/// the path answers it today, pinned in <c>course-cases.txt</c> beside this file as <c>line-account-cases.txt</c> pins the account rule's.
/// </summary>
/// <remarks>
/// <para>The table's rows go by path in the design's order (the account rule's answers first, then B, E, J, D; A, F, K, N; G, H, I; C; the
/// answers-alone hold; a standing move). Each names the case it is ported from, and a case of the paths' suites is either a row's or left
/// out by why; each suite's count of cases is the table's too, so a case added to a suite is placed here as it lands.</para>
/// <para>Where two paths answer the same facts two ways, the table holds both rows and the decision tells them apart by the trigger or a
/// fact: the differences, each a decision for the row that adopts it (h for conversations, i for driven lines), in D212's note.</para>
/// </remarks>
public sealed class LineCourseTests
{
    private static readonly DateTimeOffset Seen = new(2026, 10, 3, 15, 0, 0, TimeSpan.Zero);

    private static string Tests => Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests");

    private static string TablePath => Path.Combine(Tests, "Course", "course-cases.txt");

    // ——— What a case is made of: the account rule's answers, the words, the heads.

    private const string Own = "account-1";

    private static CoolingEntry Cools(string account, double hours) =>
        new("fake", account, Seen.AddHours(hours), true, "session", Seen.AddMinutes(-5), "s1");

    private static HarnessSelection On(string account) => new(Refusal: null, Profile: account);

    private static HarnessSelection Refusal(CoolingEntry? cooling) => new("every account it may run on is cooling") { Cooling = cooling };

    private static LineAnswer Resumes => new LineAnswer.Resume();

    private static LineAnswer Switches(string to) => new LineAnswer.Elsewhere(On(to), NextHold.Cooling, Switched: true);

    private static LineAnswer Goes(string to, NextHold why = NextHold.Cooling, bool notOffered = false) =>
        new LineAnswer.Elsewhere(On(to), why, notOffered);

    private static LineAnswer WaitsOwn(double hours = 2)
    {
        var own = Cools(Own, hours);
        return new LineAnswer.Wait(own, [own], ResetSet.Switch, NextHold.Cooling, OwnFirst: true);
    }

    private static LineAnswer WaitsPick(double hours = 2)
    {
        var own = Cools(Own, hours);
        return new LineAnswer.Wait(own, [own], ResetSet.Walk, NextHold.Cooling, OwnFirst: true);
    }

    private static LineAnswer WaitsSooner(string account, double hours)
    {
        var (own, sooner) = (Cools(Own, 2), Cools(account, hours));
        return new LineAnswer.Wait(sooner, [own, sooner], ResetSet.Switch, NextHold.Cooling, Refusal(sooner));
    }

    private static LineAnswer WaitsWalk(string account = Own, double hours = 2, NextHold why = NextHold.Cooling)
    {
        var earliest = Cools(account, hours);
        return new LineAnswer.Wait(earliest, [earliest], ResetSet.Walk, why, Refusal(earliest));
    }

    private static LineAnswer Holds(NextHold why, LineRepair repair, bool walked = false, bool notOffered = false) =>
        new LineAnswer.Hold(why, repair, walked ? Refusal(null) : null, NotOffered: notOffered);

    private static CourseWord P(string id = "w1", bool afterEnd = false) => new(id, CourseAuthor.Person, AfterEnd: afterEnd);

    private static CourseWord Helped(string id = "h1") => new(id, CourseAuthor.Helper);

    private static CourseWord Supervised(string id = "v1") => new(id, CourseAuthor.Supervisor);

    private static CourseWord Rebased(string id = "r1") => new(id, CourseAuthor.Daoris, DaorisKeys.Rebase("c1", "abc1234"));

    private static CourseWord Restarted(string id = "x1") => new(id, CourseAuthor.Daoris, DaorisKeys.Restart("c1", "b7"));

    /// <summary>A repository conversation of this machine's on its own account, its conversation kept, its tree standing.</summary>
    private static CourseHead Chat(params CourseWord[] words) => new(
        "completed", Role: "chat", Adapter: "fake", Profile: Own, Tree: "trees/c1", Kept: true, DoorResumes: true, Conversation: true,
        Waiting: words, Session: "c1", Protocol: true, Revision: 7);

    /// <summary>Ask Daoris's own conversation, in its room.</summary>
    private static CourseHead Help(params CourseWord[] words) => Chat(words) with { Role = "ask-daoris", Session = "a1", AskDaoris = true };

    /// <summary>A driven quest's session of this machine's, on the protocol door, its quest as named.</summary>
    private static CourseHead Driven(string quest = "Taken", params CourseWord[] words) => new(
        "completed", Role: "build", Choice: "1", Adapter: "fake", Profile: Own, Tree: "trees/s1", Kept: true, DoorResumes: true,
        Waiting: words, Session: "s1", QuestStatus: quest, Protocol: true, Revision: 7);

    /// <summary>The line's part has exited, its words kept on its record, its account read as <paramref name="answer"/>.</summary>
    private static CourseFacts Ended(CourseHead head, LineAnswer? answer = null, bool picked = false) =>
        new(head, CourseBoundary.ProcessExited, new CourseAccount(answer, Picked: picked));

    /// <summary>The line's part runs: between turns, or in a turn.</summary>
    private static CourseFacts Live(CourseHead head, LineAnswer? answer = null, CourseBoundary at = CourseBoundary.BetweenTurns, bool picked = false) =>
        new(head with { State = "working" }, at, new CourseAccount(answer, Picked: picked));

    /// <summary>A park: its part waits on the person, between turns.</summary>
    private static CourseFacts Parked(CourseHead head, LineAnswer? answer = null) =>
        new(head with { State = "awaiting-person" }, CourseBoundary.BetweenTurns, new CourseAccount(answer));

    private static CourseFacts Picking(CourseFacts facts) => facts with { Account = (facts.Account ?? new CourseAccount()) with { Picked = true } };

    private static CourseFacts Alone(CourseFacts facts, int count, bool released = false, bool kept = false) =>
        facts with { Alone = new CourseAlone(count, Released: released, Kept: kept) };

    private static CourseFacts Pressed(CourseFacts facts, CoursePressKind kind, string? agent = null, string? account = null, CourseFromWhy? why = null, CourseFromBy? by = null, string? intent = null) =>
        facts with { Press = new CoursePress(kind, agent, account, intent, why, by) };

    private static CourseFacts Ruled(CourseFacts facts, bool admits = false, bool asks = false, bool switches = false, string? curve = null, bool runner = false) =>
        facts with { Rules = new CourseRules(admits, switches, curve, HandOverAsks: asks, Runner: runner) };

    private static CourseFacts Later(CourseFacts facts, string agent, string choice) =>
        facts with { Account = (facts.Account ?? new CourseAccount()) with { Later = new CourseLater(agent, choice) } };

    private static CourseFacts OnAgent(CourseFacts facts, string agent) =>
        facts with { Account = (facts.Account ?? new CourseAccount()) with { Agent = agent } };

    private static CourseFacts Earliest(CourseFacts facts, CoolingEntry earliest) =>
        facts with { Account = (facts.Account ?? new CourseAccount()) with { Earliest = earliest } };

    private static CourseFacts Curving(CourseFacts facts) => facts with { Account = (facts.Account ?? new CourseAccount()) with { Curve = true } };

    private static CourseFacts Woke(CourseFacts facts, double hours) => facts with { Limit = new CourseLimit(Woke: Seen.AddHours(hours)) };

    private const string Until = "2026-10-03T16:00:00Z";

    private static CourseFacts Cut(CourseFacts facts, string? until = Until, string? build = null, DateTimeOffset? ended = null) =>
        facts with { Limit = new CourseLimit(until, Build: build, Ended: ended) };

    private static CourseFacts Exhausted(CourseFacts facts) => facts with { Failure = new CourseFailure(AgentErrorKind.Transient, PastBound: true) };

    private static CourseFacts Carrying(CourseFacts facts, CourseCarryOn carryOn) => facts with { CarryOn = carryOn };

    private sealed record Case(T Trigger, CourseFacts Facts);

    /// <summary>Every row's case: its trigger and its facts, by the row's id in the table.</summary>
    private static Dictionary<string, Case> Cases() => new(StringComparer.Ordinal)
    {
        // ——— LA: the account rule's answers (line-account-cases.txt), read once, as the conversation's and the driven go-on read them.
        ["LA-01"] = new(T.WordsKept, Ended(Chat(P()), Resumes)),
        ["LA-02"] = new(T.WordsKept, Ended(Driven("Taken", P()), Resumes)),
        ["LA-03"] = new(T.WordsKept, Ended(Driven("Taken", P()), Switches("account-2"))),
        ["LA-04"] = new(T.WordsKept, Ended(Chat(P()), Switches("account-2"))),
        ["LA-05"] = new(T.WordsKept, Ended(Chat(P()), WaitsOwn())),
        ["LA-06"] = new(T.WordsKept, Ended(Driven("Taken", P()), WaitsOwn())),
        ["LA-07"] = new(T.WordsKept, Ended(Chat(P()), WaitsSooner("account-2", 1))),
        ["LA-08"] = new(T.WordsKept, Ended(Help(P()), WaitsOwn())),
        ["LA-09"] = new(T.WordsKept, Ended(Driven("Taken", P()), Goes("account-2"))),
        ["LA-10"] = new(T.WordsKept, Ended(Driven("Taken", P()), WaitsWalk())),
        ["LA-11"] = new(T.WordsKept, Ended(Chat(P()), Holds(NextHold.Cooling, LineRepair.None))),
        ["LA-12"] = new(T.WordsKept, Ended(Driven("Taken", P()) with { Limited = true }, Goes("account-2"))),
        ["LA-13"] = new(T.WordsKept, Ended(Driven("Taken", P()), Goes("account-2", NextHold.SignedOut))),
        ["LA-14"] = new(T.WordsKept, Ended(Driven("Taken", P()), Holds(NextHold.SignedOut, LineRepair.SignIn, walked: true))),
        ["LA-15"] = new(T.WordsKept, Ended(Chat(P()), Holds(NextHold.SignedOut, LineRepair.SignIn))),
        ["LA-16"] = new(T.WordsKept, Ended(Chat(P()), Holds(NextHold.Refused, LineRepair.ClearRefusal))),
        ["LA-17"] = new(T.WordsKept, Ended(Driven("Taken", P()), Goes("account-2", NextHold.Missing))),
        ["LA-18"] = new(T.WordsKept, Ended(Chat(P()), Holds(NextHold.Missing, LineRepair.AddAccount))),
        ["LA-19"] = new(T.WordsKept, Ended(Driven("Taken", P()), Goes("account-2", NextHold.Refused, notOffered: true))),
        ["LA-20"] = new(T.WordsKept, Ended(Chat(P()), Holds(NextHold.Refused, LineRepair.OfferModel, notOffered: true))),
        ["LA-21"] = new(T.WordsKept, Ended(Chat(P()), new LineAnswer.Resume(On(Own)))),
        ["LA-22"] = new(T.WordsKept, Ended(Driven("Taken", P()), WaitsWalk("account-2", 40, NextHold.Refused))),

        // ——— B: the conversation's go-on and Ask Daoris's.
        ["B-01"] = new(T.WordsKept, Ended(Chat(P()) with { Judged = ContinueWhy.Unkept }, Resumes)),
        ["B-02"] = new(T.WordsKept, Ended(Chat(P()) with { Judged = ContinueWhy.Tree }, Resumes)),
        ["B-03"] = new(T.WordsKept, Ended(Chat(P()), WaitsOwn())),
        ["B-04"] = new(T.WordsKept, Ended(Chat(P()), Holds(NextHold.Missing, LineRepair.AddAccount))),
        ["B-05"] = new(T.WordsKept, Alone(Ended(Chat(Helped()) with { Judged = ContinueWhy.Unkept }, Resumes), 0)),
        ["B-06"] = new(T.WordsKept, Live(Chat(P()), at: CourseBoundary.TurnRunning)),
        ["B-07"] = new(T.WordsKept, Ended(Chat(), Resumes)),
        ["B-08"] = new(T.Answer, Alone(Ended(Chat(Helped()), Resumes), 0)),
        ["B-09"] = new(T.Answer, Alone(Ended(Chat(Helped()), Resumes), 2)),
        ["B-10"] = new(T.Answer, Alone(Ended(Chat(Helped()), Resumes), 2, released: true)),
        ["B-11"] = new(T.ComeUp, Ended(Chat(P()), Resumes)),
        ["B-12"] = new(T.WordsKept, Ended(Chat(P()) with { SupersededBy = "c2" }, Resumes)),
        ["B-13"] = new(T.WordsKept, Ended(Chat(P()), Resumes)),
        ["B-14"] = new(T.ComeUp, Ended(Chat(P()), Resumes)),
        ["B-15"] = new(T.ConversationLost, Ended(Chat(P()) with { Judged = ContinueWhy.Gone }, Resumes)),
        ["B-16"] = new(T.WordsKept, Ended(Chat(P()) with { Judged = ContinueWhy.Refused }, Resumes)),
        ["B-17"] = new(T.BriefedPress, Ended(Chat(P()) with { Judged = ContinueWhy.Refused }, Resumes)),
        ["B-18"] = new(T.WordsKept, Ended(Chat(P()) with { Protocol = false }, Resumes)),
        ["B-19"] = new(T.WordsKept, Live(Chat(P()) with { Protocol = false }, at: CourseBoundary.TurnRunning)),
        ["B-20"] = new(T.Answer, Alone(Live(Chat(Helped())), 0)),
        ["B-21"] = new(T.Answer, Alone(Live(Chat(Helped()), at: CourseBoundary.TurnRunning), 0)),
        ["B-22"] = new(T.Answer, Alone(Ended(Chat(Helped()), Resumes), 0)),
        ["B-23"] = new(T.ConversationLost, Ended(Chat(P(), Restarted()) with { Judged = ContinueWhy.Gone }, Resumes)),
        ["B-24"] = new(T.ConversationLost, Ended(Chat(P(), Restarted()) with { Judged = ContinueWhy.Unkept }, Resumes)),
        ["B-25"] = new(T.WordsKept, Ended(Chat(P()) with { Judged = ContinueWhy.Gone }, Resumes)),
        ["B-26"] = new(T.WordsKept, Ended(Chat(P()) with { Judged = ContinueWhy.Exited }, Resumes)),
        ["B-27"] = new(T.WordsKept, Ended(Chat(P()) with { NotOffered = true }, Resumes)),
        ["B-28"] = new(T.WordsKept, Ended(Chat(P()), Resumes)),
        ["B-29"] = new(T.ConversationLost, Ended(Chat(P(), Rebased()) with { Judged = ContinueWhy.Gone }, Resumes)),
        ["B-30"] = new(T.WordsKept, Ended(Chat(P(), Rebased()), Switches("account-2"))),

        // ——— E: a conversation's limit wait and wake.
        ["E-01"] = new(T.LimitEnd, Live(Chat(P()) with { Limited = true }, WaitsWalk())),
        ["E-02"] = new(T.LimitWake, Woke(Live(Chat(P()) with { Limited = true }, Resumes), 2)),
        ["E-03"] = new(T.LimitWake, Woke(Live(Help(P()) with { Limited = true }, Resumes), 2)),
        ["E-04"] = new(T.LimitWake, Ended(Chat(P()) with { Limited = true }, Goes("account-2"))),
        ["E-05"] = new(T.LimitWake, Ended(Chat(P()), Resumes)),
        ["E-06"] = new(T.WordsKept, Ended(Chat(P()), Switches("account-2"))),
        ["E-07"] = new(T.LimitWake, Ended(Chat(P()), Switches("account-2"))),
        ["E-08"] = new(T.WordsKept, Ended(Chat(P()), WaitsPick(), picked: true)),
        ["E-09"] = new(T.LimitWake, Ended(Chat(P()), Resumes, picked: true)),
        ["E-10"] = new(T.LimitWake, Ended(Chat(P()) with { Limited = true }, WaitsPick(), picked: true)),
        ["E-11"] = new(T.WordsKept, Ended(Chat(P()), WaitsSooner("account-2", 1))),
        ["E-12"] = new(T.LimitWake, Ended(Chat(P()), Switches("account-2"))),
        ["E-13"] = new(T.WordsKept, Ended(Help(P()), WaitsOwn())),
        ["E-14"] = new(T.WordsKept, Ended(Chat(P()), Holds(NextHold.SignedOut, LineRepair.SignIn))),
        ["E-15"] = new(T.WordsKept, Ruled(Ended(Driven("Done", P()), Switches("account-2")), runner: true)),
        ["E-16"] = new(T.WordsKept, Ruled(Ended(Driven("Done", P(), Rebased()), Switches("account-2")), runner: true)),
        ["E-17"] = new(T.LimitWake, Ended(Chat(P()) with { Limited = true, Judged = ContinueWhy.Gone }, Resumes)),
        ["E-18"] = new(T.LimitWake, Ended(Chat(P()) with { Limited = true, Judged = ContinueWhy.Gone }, Resumes, picked: true)),
        ["E-19"] = new(T.LimitWake, Ended(Chat(P()) with { Limited = true, Judged = ContinueWhy.Unkept }, WaitsPick(), picked: true)),
        ["E-20"] = new(T.LimitWake, Ended(Chat(P()) with { Limited = true, Judged = ContinueWhy.Unkept }, Goes("account-2"))),
        ["E-21"] = new(T.LimitWake, Ended(Chat(P()) with { Limited = true, Judged = ContinueWhy.Refused }, Resumes)),
        ["E-22"] = new(T.LimitWake, Pressed(Ended(Chat(P()) with { Limited = true }, WaitsWalk()), CoursePressKind.Stop)),
        ["E-23"] = new(T.LimitEnd, Live(Chat(P()) with { Limited = true }, Goes("account-2"))),
        ["E-24"] = new(T.LimitWake, Later(Ended(Chat(P()) with { Limited = true }, WaitsWalk()), "other", "2")),
        ["E-25"] = new(T.LimitWake, Later(Ended(Chat(P()) with { Limited = true }, WaitsPick(), picked: true), "other", "2")),
        ["E-26"] = new(T.LimitWake, Later(Ended(Chat(P()) with { Limited = true, Choice = "pick" }, WaitsWalk()), "other", "2")),

        // ——— J: the briefed move, the person's and Daoris's on a lost conversation.
        ["J-01"] = new(T.BriefedPress, Ended(Chat(P()), Resumes)),
        ["J-02"] = new(T.BriefedPress, Ended(Chat(P()), Goes("account-2"))),
        ["J-03"] = new(T.BriefedPress, Ended(Chat(P()), WaitsWalk())),
        ["J-04"] = new(T.BriefedPress, Ended(Chat(P()), Resumes, picked: true)),
        ["J-05"] = new(T.BriefedPress, Ended(Chat(P()), Resumes, picked: true)),
        ["J-06"] = new(T.ConversationLost, Ended(Chat(P()) with { Judged = ContinueWhy.Gone }, Resumes, picked: true)),
        ["J-07"] = new(T.BriefedPress, Ended(Chat(P()), Goes("account-2"))),
        ["J-08"] = new(T.ConversationLost, Ended(Chat(P()) with { Judged = ContinueWhy.Gone }, Resumes)),
        ["J-09"] = new(T.ConversationLost, Ended(Chat(P()) with { Judged = ContinueWhy.Gone }, Goes("account-2"))),

        // ——— D: the limit move of a live conversation.
        ["D-01"] = new(T.LimitEnd, Live(Chat(P()) with { Limited = true }, Goes("account-2"))),
        ["D-02"] = new(T.LimitEnd, Live(Chat(P()) with { Limited = true }, WaitsPick(), picked: true)),
        ["D-03"] = new(T.LimitEnd, Live(Help(P()) with { Limited = true }, Goes("account-2"))),
        ["D-04"] = new(T.LimitEnd, Later(Live(Chat(P()) with { Limited = true }, WaitsWalk()), "other", "2")),
        ["D-05"] = new(T.LimitEnd, Later(Live(Help(P()) with { Limited = true }, WaitsWalk()), "other", "2")),
        ["D-06"] = new(T.LimitEnd, Later(Live(Chat(P()) with { Limited = true, Choice = "pick" }, WaitsWalk()), "other", "2")),
        ["D-07"] = new(T.LimitEnd, Live(Chat(P()) with { Limited = true }, WaitsWalk())),
        ["D-08"] = new(T.LimitEnd, Earliest(Live(Chat(P()) with { Limited = true }, Goes(Own)), Cools(Own, 2))),

        // ——— A: the driven go-on.
        ["A-01"] = new(T.WordsKept, Ended(Driven("Taken", P()), Resumes)),
        ["A-02"] = new(T.WordsKept, Ended(Driven("Taken", P()) with { Judged = ContinueWhy.Account }, Goes("account-2"))),
        ["A-03"] = new(T.WordsKept, Ended(Driven("Taken", P()) with { Judged = ContinueWhy.Adapter }, Resumes)),
        ["A-04"] = new(T.WordsKept, Ended(Driven("Taken", P()) with { Judged = ContinueWhy.Tree }, Resumes)),
        ["A-05"] = new(T.WordsKept, Ended(Driven("Taken", P()) with { Judged = ContinueWhy.Unkept }, Resumes)),
        ["A-06"] = new(T.WordsKept, Ended(Driven("Taken", P()) with { Judged = ContinueWhy.Unable }, Resumes)),
        ["A-07"] = new(T.WordsKept, Ended(Driven("Taken", P()) with { Judged = ContinueWhy.Elsewhere }, Resumes)),
        ["A-08"] = new(T.WordsKept, Ended(Driven("Taken", P()) with { AgentGone = true }, Resumes)),
        ["A-09"] = new(T.WordsKept, Ended(Driven("Taken", P()), Resumes)),
        ["A-10"] = new(T.WordsKept, Ended(Driven("Taken", P()) with { State = "declined" }, Resumes)),
        ["A-11"] = new(T.WordsKept, Ended(Driven("Taken", P()) with { State = "stopped" }, Resumes)),
        ["A-12"] = new(T.WordsKept, Ended(Driven("Taken", P()) with { State = "failed" }, Resumes)),
        ["A-13"] = new(T.WordsKept, Ended(Driven("Taken"), Resumes)),
        ["A-14"] = new(T.WordsKept, Ended(Driven("Taken", P()) with { Teammate = true }, Resumes)),
        ["A-15"] = new(T.WordsKept, Ended(Driven("Taken", P()) with { Intake = true }, Resumes)),
        ["A-16"] = new(T.WordsKept, Ended(Driven("Taken", P()) with { State = "stood-down", StoodDown = true }, Resumes)),
        ["A-17"] = new(T.ConversationLost, Ruled(Ended(Driven("Done", P()) with { Judged = ContinueWhy.Gone }, Resumes), runner: true)),
        ["A-18"] = new(T.ConversationLost, Ended(Driven("Done", P()) with { Judged = ContinueWhy.Gone }, Resumes)),
        ["A-19"] = new(T.WordsKept, Ended(Driven("Done", P()), Resumes)),
        ["A-20"] = new(T.WordsKept, Parked(Driven("Taken", P()), Resumes)),
        ["A-21"] = new(T.WordsKept, Ended(Driven("Done", P()) with { Judged = ContinueWhy.Elsewhere }, Resumes)),
        ["A-22"] = new(T.WordsKept, Ended(Driven("Taken", P()), Resumes)),
        ["A-23"] = new(T.WordsKept, Live(Driven("Taken", P()) with { Protocol = false }, at: CourseBoundary.TurnRunning)),
        ["A-24"] = new(T.WordsKept, Ended(Driven("Taken", P()) with { Judged = ContinueWhy.Elsewhere }, Resumes)),
        ["A-25"] = new(T.WordsKept, Parked(Driven("Taken", P()), Resumes)),
        ["A-26"] = new(T.WordsKept, Ended(Driven("Taken", P(), Rebased()) with { Judged = ContinueWhy.Tree }, Resumes)),

        // ——— F: the driven limit resume, decided in the carry-on's own start.
        ["F-01"] = new(T.CarryOn, Carrying(Cut(Ended(Driven("Taken") with { State = "stopped" }, Resumes), until: null, build: "b7"), CourseCarryOn.CutOff)),
        ["F-02"] = new(T.CarryOn, Carrying(Cut(Ended(Driven("Taken") with { State = "failed", Limited = true }, WaitsWalk(hours: 1))), CourseCarryOn.Limited)),
        ["F-03"] = new(T.CarryOn, Carrying(Cut(Ended(Driven("Taken") with { State = "failed", Limited = true }, Resumes)), CourseCarryOn.Limited)),
        ["F-04"] = new(T.CarryOn, Carrying(Cut(Ended(Driven("Taken") with { State = "failed", Limited = true, Judged = ContinueWhy.Gone }, Resumes)), CourseCarryOn.Limited)),
        ["F-05"] = new(T.CarryOn, Carrying(Cut(Ended(Driven("Taken") with { State = "failed", Limited = true, Judged = ContinueWhy.Elsewhere }, Resumes)), CourseCarryOn.Limited)),
        ["F-06"] = new(T.CarryOn, Carrying(Cut(Ended(Driven("Taken", P()) with { State = "failed", Limited = true }, Resumes)), CourseCarryOn.Limited)),
        ["F-07"] = new(T.CarryOn, Carrying(Cut(Ended(Driven("Taken") with { State = "failed", Limited = true }, WaitsWalk(hours: 1))), CourseCarryOn.Limited)),
        ["F-08"] = new(T.CarryOn, Carrying(Cut(Ended(Driven("Taken") with { State = "failed", Limited = true, Protocol = false }, Goes(Own))), CourseCarryOn.Limited)),

        // ——— K: the driven carry-on.
        ["K-01"] = new(T.CarryOn, Carrying(Pressed(OnAgent(Ended(Driven("Taken"), Goes("account-2")), "other"), CoursePressKind.HandOver, "other", why: CourseFromWhy.Agent, by: CourseFromBy.Agent), CourseCarryOn.CutOff)),
        ["K-02"] = new(T.CarryOn, Carrying(Ended(Driven("Taken") with { Limited = true }, Goes("account-2")), CourseCarryOn.Limited)),
        ["K-03"] = new(T.CarryOn, Carrying(OnAgent(Ended(Driven("Taken") with { Limited = true }, Goes(Own)), "other"), CourseCarryOn.Limited)),
        ["K-04"] = new(T.CarryOn, Carrying(Ended(Driven("Taken"), Resumes), CourseCarryOn.Retried)),
        ["K-05"] = new(T.CarryOn, Carrying(Ended(Driven("Taken"), Goes("account-2")), CourseCarryOn.StopReleased)),
        ["K-06"] = new(T.CarryOn, Carrying(Ended(Driven("Taken"), Goes("account-2")), CourseCarryOn.CutOff)),
        ["K-07"] = new(T.CarryOn, Carrying(Curving(Ended(Driven("Taken"), Goes("account-2"))), CourseCarryOn.CutOff)),
        ["K-08"] = new(T.CarryOn, Carrying(Ended(Driven("Taken"), Goes(Own)), CourseCarryOn.CutOff)),
        ["K-09"] = new(T.CarryOn, Carrying(Ended(Driven("Taken") with { State = "stopped" }, Goes(Own)), CourseCarryOn.CutOff)),
        ["K-10"] = new(T.CarryOn, Carrying(Ended(Driven("Open") with { Limited = true }, Goes("account-2")), CourseCarryOn.Limited)),
        ["K-11"] = new(T.CarryOn, Carrying(Ended(Driven("Taken") with { State = "stopped" }, Goes(Own)), CourseCarryOn.StopReleased)),
        ["K-12"] = new(T.CarryOn, Carrying(Ended(Driven("Taken") with { State = "stood-down", StoodDown = true }, Goes(Own)), CourseCarryOn.BroughtForward)),
        ["K-13"] = new(T.CarryOn, Carrying(Ended(Driven("Open") with { State = "failed" }, Goes(Own)), CourseCarryOn.CutOff)),
        ["K-14"] = new(T.CarryOn, Carrying(Pressed(Ended(Driven("Taken") with { State = "stopped" }, Goes(Own)), CoursePressKind.Stop), CourseCarryOn.CutOff)),
        ["K-15"] = new(T.CarryOn, Carrying(Pressed(Ended(Driven("Taken") with { State = "stopped" }, Goes(Own)), CoursePressKind.Pause), CourseCarryOn.CutOff)),

        // ——— N: past the transient bound.
        ["N-01"] = new(T.TransientPastBound, Exhausted(Ended(Driven("Taken") with { State = "failed" }, Resumes))),
        ["N-02"] = new(T.WordsKept, Exhausted(Ended(Driven("Taken", P(afterEnd: true)) with { State = "failed" }, Resumes))),
        ["N-03"] = new(T.TransientPastBound, Exhausted(Ended(Driven("Open") with { State = "failed" }, Resumes))),
        ["N-04"] = new(T.TransientPastBound, Cut(Pressed(Exhausted(Ended(Driven("Taken") with { State = "failed" }, Resumes)), CoursePressKind.TryAgain), until: null, ended: Seen.AddHours(-1))),
        ["N-05"] = new(T.TransientPastBound, Exhausted(Ended(Driven("Taken") with { State = "failed" }, Resumes))),
        ["N-06"] = new(T.TransientPastBound, Cut(Pressed(Exhausted(Ended(Driven("Open") with { State = "failed" }, Resumes)), CoursePressKind.TryAgain), until: null, ended: Seen.AddHours(-1))),
        ["N-07"] = new(T.WordsKept, Exhausted(Ended(Driven("Taken", P()) with { State = "failed" }, Resumes))),
        ["N-08"] = new(T.WordsKept, Exhausted(Ended(Driven("Taken", P(), P("w2", afterEnd: true)) with { State = "failed" }, Resumes))),

        // ——— G: the curve.
        ["G-01"] = new(T.TurnEnd, Ruled(Live(Chat(), at: CourseBoundary.TurnRunning), switches: true, curve: "account-2")),
        ["G-02"] = new(T.TurnEnd, Ruled(Live(Chat()), switches: true, curve: "account-2")),
        ["G-03"] = new(T.TurnEnd, Ruled(Live(Chat()), switches: true)),
        ["G-04"] = new(T.TurnEnd, Ruled(Live(Chat()), curve: "account-2")),
        ["G-05"] = new(T.TurnEnd, Ruled(Live(Chat()), switches: true)),
        ["G-06"] = new(T.TurnEnd, Ruled(Live(Chat()), switches: true)),
        ["G-07"] = new(T.TurnEnd, Ruled(Live(Chat()), switches: true)),
        ["G-08"] = new(T.TurnEnd, Ruled(Live(Chat()), switches: true, curve: "account-2")),
        ["G-09"] = new(T.TurnEnd, Ruled(Live(Chat()) with { Boundary = CourseBoundary.Reserved }, switches: true, curve: "account-2")),
        ["G-10"] = new(T.TurnEnd, Ruled(Live(Chat()), switches: true, curve: "account-2")),
        ["G-11"] = new(T.TurnEnd, Ruled(Live(Help()), switches: true, curve: "account-2")),

        // ——— H: a conversation's hand-over, with its consent before an agent's ask.
        ["H-01"] = new(T.HandOver, Pressed(Live(Chat(P())), CoursePressKind.HandOver, "codex-acp", "work", CourseFromWhy.Limit, CourseFromBy.Person)),
        ["H-02"] = new(T.HandOver, Pressed(Live(Chat(P()) with { Teammate = true }), CoursePressKind.HandOver, "codex-acp")),
        ["H-03"] = new(T.HandOver, Pressed(Parked(Chat(P()) with { Intake = true }), CoursePressKind.HandOver, "codex-acp")),
        ["H-04"] = new(T.HandOver, Pressed(Live(Chat(P())), CoursePressKind.HandOver, "fake", Own)),
        ["H-05"] = new(T.HandOver, Pressed(Live(Chat(P()) with { Tree = null }), CoursePressKind.HandOver, "codex-acp")),
        ["H-06"] = new(T.HandOver, Pressed(Ended(Chat(P()) with { SupersededBy = "m0v3d000" }), CoursePressKind.HandOver, "codex-acp")),
        ["H-07"] = new(T.HandOver, Pressed(Live(Chat(P())) with { Boundary = CourseBoundary.Reserved }, CoursePressKind.HandOver, "fake")),
        ["H-08"] = new(T.HandOver, Pressed(Live(Help(P())), CoursePressKind.HandOver, "codex-acp")),
        ["H-09"] = new(T.HandOver, Pressed(Live(Chat(P())), CoursePressKind.HandOver, "fake", "account-2", CourseFromWhy.Press, CourseFromBy.Person)),
        ["H-10"] = new(T.HandOver, Pressed(Live(Chat(P())), CoursePressKind.HandOver, "other", null, CourseFromWhy.Press, CourseFromBy.Person)),
        ["H-11"] = new(T.HandOver, Ruled(Pressed(Live(Chat(P())), CoursePressKind.HandOver, "fake", "account-2", CourseFromWhy.Agent, CourseFromBy.Agent), admits: true)),
        ["H-12"] = new(T.HandOver, Pressed(Live(Chat(P())), CoursePressKind.HandOver, "fake", "account-2", CourseFromWhy.Curve, CourseFromBy.Daoris)),
        ["H-13"] = new(T.HandOver, Pressed(Live(Chat(P()), picked: true), CoursePressKind.HandOver, "other", null, CourseFromWhy.Press, CourseFromBy.Person)),
        ["H-14"] = new(T.HandOver, Ruled(Pressed(Live(Chat(P()), at: CourseBoundary.TurnRunning), CoursePressKind.HandOver, "codex-acp", why: CourseFromWhy.Agent, by: CourseFromBy.Agent), asks: true)),
        ["H-15"] = new(T.HandOver, Ruled(Pressed(Live(Chat(P()), at: CourseBoundary.TurnRunning), CoursePressKind.HandOver, "codex-acp", why: CourseFromWhy.Agent, by: CourseFromBy.Agent))),
        ["H-16"] = new(T.HandOver, Ruled(Pressed(Live(Chat(P()), at: CourseBoundary.TurnRunning), CoursePressKind.HandOver, "codex-acp", why: CourseFromWhy.Agent, by: CourseFromBy.Agent), admits: true)),
        ["H-17"] = new(T.HandOver, Ruled(Pressed(Live(Chat(P()), at: CourseBoundary.TurnRunning), CoursePressKind.HandOver, "codex-acp", why: CourseFromWhy.Agent, by: CourseFromBy.Agent), admits: true)),

        // ——— I: a quest's session's hand-over, with its consent before an agent's ask.
        ["I-01"] = new(T.HandOver, Pressed(Live(Driven("Taken", P()), at: CourseBoundary.TurnRunning), CoursePressKind.HandOver, "codex-acp", "work", CourseFromWhy.Limit, CourseFromBy.Person)),
        ["I-02"] = new(T.HandOver, Pressed(Live(Driven("Taken", P())), CoursePressKind.HandOver, "fake", Own)),
        ["I-03"] = new(T.HandOver, Pressed(Ended(Driven("Done", P())), CoursePressKind.HandOver, "codex-acp")),
        ["I-04"] = new(T.HandOver, Pressed(Ended(Driven("Taken", P()) with { State = "stood-down", StoodDown = true }), CoursePressKind.HandOver, "codex-acp")),
        ["I-05"] = new(T.HandOver, Pressed(Ended(Driven("Taken", P()) with { SupersededBy = "qu3st000" }), CoursePressKind.HandOver, "codex-acp")),
        ["I-06"] = new(T.HandOver, Pressed(Parked(Driven("Taken", P())), CoursePressKind.HandOver, "codex-acp")),
        ["I-07"] = new(T.HandOver, Pressed(Parked(Driven("Taken", P()) with { AgentGone = true }), CoursePressKind.HandOver, "codex-acp")),
        ["I-08"] = new(T.HandOver, Pressed(Live(Driven("Open", P()), at: CourseBoundary.TurnRunning), CoursePressKind.HandOver, "codex-acp")),
        ["I-09"] = new(T.HandOver, Pressed(Live(Driven("Taken", P())) with { Boundary = CourseBoundary.Reserved }, CoursePressKind.HandOver, "fake")),
        ["I-10"] = new(T.HandOver, Ruled(Pressed(Live(Driven("Taken", P()), at: CourseBoundary.TurnRunning), CoursePressKind.HandOver, "codex-acp", why: CourseFromWhy.Agent, by: CourseFromBy.Agent), admits: true)),
        ["I-11"] = new(T.HandOver, Ruled(Pressed(Parked(Driven("Taken", P())), CoursePressKind.HandOver, "codex-acp", why: CourseFromWhy.Agent, by: CourseFromBy.Agent), admits: true)),
        ["I-12"] = new(T.HandOver, Ruled(Pressed(Parked(Driven("Done", P())), CoursePressKind.HandOver, "codex-acp", why: CourseFromWhy.Agent, by: CourseFromBy.Agent), asks: true)),
        ["I-13"] = new(T.HandOver, Ruled(Pressed(Live(Driven("Taken", P()), at: CourseBoundary.TurnRunning), CoursePressKind.HandOver, "codex-acp", why: CourseFromWhy.Agent, by: CourseFromBy.Agent), admits: true)),
        ["I-14"] = new(T.HandOver, Ruled(Pressed(Live(Driven("Taken", P()), at: CourseBoundary.TurnRunning), CoursePressKind.HandOver, "codex-acp", why: CourseFromWhy.Agent, by: CourseFromBy.Agent), asks: true)),
        ["I-15"] = new(T.HandOver, Ruled(Pressed(Live(Driven("Taken", P()), at: CourseBoundary.TurnRunning), CoursePressKind.HandOver, "codex-acp", why: CourseFromWhy.Agent, by: CourseFromBy.Agent))),
        ["I-16"] = new(T.HandOver, Ruled(Live(Driven("Taken", P()), at: CourseBoundary.TurnRunning), admits: true)),
        ["I-17"] = new(T.HandOver, Ruled(Pressed(Live(Driven("Taken", P()), at: CourseBoundary.TurnRunning), CoursePressKind.HandOver, "dsh", why: CourseFromWhy.Agent, by: CourseFromBy.Agent), admits: true)),
        ["I-18"] = new(T.HandOver, Ruled(Live(Driven("Taken", P()), at: CourseBoundary.TurnRunning), asks: true)),
        ["I-19"] = new(T.HandOver, Ruled(Pressed(Live(Driven("Taken", P()), at: CourseBoundary.TurnRunning), CoursePressKind.HandOver, "dsh", why: CourseFromWhy.Limit, by: CourseFromBy.Person), asks: true)),
        ["I-20"] = new(T.HandOver, Ruled(Pressed(Live(Driven("Taken", P()), at: CourseBoundary.TurnRunning), CoursePressKind.HandOver, "codex-acp", why: CourseFromWhy.Agent, by: CourseFromBy.Agent), admits: true)),
        ["I-21"] = new(T.HandOver, Pressed(Live(Driven("Taken", P()), at: CourseBoundary.TurnRunning), CoursePressKind.HandOver, "stub")),
        ["I-22"] = new(T.HandOver, Pressed(Live(Driven("Taken", P()), at: CourseBoundary.TurnRunning), CoursePressKind.HandOver, "codex-acp")),
        ["I-23"] = new(T.HandOver, Pressed(Live(Driven("Done", P()), at: CourseBoundary.TurnRunning), CoursePressKind.HandOver, "codex-acp")),

        // ——— C: go on in a new session; start from these words.
        ["C-01"] = new(T.GoOnNew, Ended(Driven("Taken", P(), P("w2")), Goes("account-2"))),
        ["C-02"] = new(T.GoOnNew, Parked(Driven("Taken", P()), Goes("account-2"))),
        ["C-03"] = new(T.GoOnNew, Ended(Driven("Taken", P()) with { Teammate = true }, Goes("account-2"))),
        ["C-04"] = new(T.GoOnNew, Parked(Chat(P()) with { Intake = true }, Goes("account-2"))),
        ["C-05"] = new(T.GoOnNew, Ended(Driven("Taken", P()) with { State = "stood-down", StoodDown = true }, Goes("account-2"))),
        ["C-06"] = new(T.GoOnNew, Ended(Driven("Taken", P()) with { State = "failed", SupersededBy = "l4t3r000" }, Goes("account-2"))),
        ["C-07"] = new(T.GoOnNew, Ended(Chat(P()) with { SupersededBy = "m0v3d000" }, Goes("account-2"))),
        ["C-08"] = new(T.GoOnNew, Ended(Driven("Taken"), Goes("account-2"))),
        ["C-09"] = new(T.GoOnNew, Live(Driven("Taken", P()), Goes("account-2"), CourseBoundary.TurnRunning)),
        ["C-10"] = new(T.GoOnNew, Ended(Chat(P()), Goes("account-2"))),
        ["C-11"] = new(T.GoOnNew, Ended(Driven("Done", P()), Goes("account-2"))),
        ["C-12"] = new(T.GoOnNew, Ended(Driven("Taken", P()), Resumes)),
        ["C-13"] = new(T.StartFrom, Pressed(Ended(Driven("Done", P())), CoursePressKind.StartFrom, intent: "i-1")),
        ["C-14"] = new(T.StartFrom, Pressed(Ended(Chat(P())), CoursePressKind.StartFrom, intent: "i-1")),
        ["C-15"] = new(T.StartFrom, Pressed(Ended(Chat(P(), Helped())), CoursePressKind.StartFrom, intent: "i-1")),
        ["C-16"] = new(T.StartFrom, Pressed(Ended(Driven("Done", P()) with { Teammate = true }), CoursePressKind.StartFrom, intent: "i-1")),
        ["C-17"] = new(T.StartFrom, Pressed(Ended(Help(P())), CoursePressKind.StartFrom, intent: "i-1")),
        ["C-18"] = new(T.StartFrom, Pressed(Ended(Driven("Taken", P()) with { State = "stood-down", StoodDown = true }), CoursePressKind.StartFrom, intent: "i-1")),
        ["C-19"] = new(T.StartFrom, Pressed(Live(Driven("Taken", P()), at: CourseBoundary.TurnRunning), CoursePressKind.StartFrom, intent: "i-1")),
        ["C-20"] = new(T.StartFrom, Pressed(Parked(Driven("Taken", P())), CoursePressKind.StartFrom, intent: "i-1")),
        ["C-21"] = new(T.StartFrom, Pressed(Live(Chat(P()), at: CourseBoundary.TurnRunning), CoursePressKind.StartFrom, intent: "i-1")),
        ["C-22"] = new(T.StartFrom, Pressed(Ended(Driven("Done")), CoursePressKind.StartFrom, intent: "i-1")),
        ["C-23"] = new(T.StartFrom, Pressed(Ended(Driven("Taken", P()) with { State = "failed" }), CoursePressKind.StartFrom, intent: "i-1")),
        ["C-24"] = new(T.StartFrom, Pressed(Ended(Driven("Open", P())), CoursePressKind.StartFrom, intent: "i-1")),
        ["C-25"] = new(T.StartFrom, Pressed(Ended(Chat(P()) with { SupersededBy = "m0v3d000" }), CoursePressKind.StartFrom, intent: "i-1")),
        ["C-26"] = new(T.StartFrom, Pressed(Parked(Chat(P()) with { Intake = true }), CoursePressKind.StartFrom, intent: "i-1")),
        ["C-27"] = new(T.StartFrom, Pressed(new CourseFacts(new CourseHead("new", Conversation: true, Waiting: [P()]), CourseBoundary.ProcessExited), CoursePressKind.StartFrom, intent: "i-1")),
        ["C-28"] = new(T.StartFrom, Pressed(new CourseFacts(new CourseHead("new", Conversation: true, Waiting: [P()]), CourseBoundary.ProcessExited), CoursePressKind.StartFrom, intent: "i-1")),
        ["C-29"] = new(T.StartFrom, Pressed(new CourseFacts(new CourseHead("new", Conversation: true, Waiting: [P()]), CourseBoundary.ProcessExited), CoursePressKind.StartFrom, intent: "i-2")),

        // ——— AA: the answers-alone hold, as course rows.
        ["AA-01"] = new(T.WordsKept, Alone(Ended(Chat(Helped()), Resumes), 2)),
        ["AA-02"] = new(T.WordsKept, Alone(Ended(Chat(Helped()), Resumes), 1)),
        ["AA-03"] = new(T.WordsKept, Alone(Ended(Chat(Helped()), Resumes), 2)),
        ["AA-04"] = new(T.WordsKept, Alone(Ended(Chat(Helped()), Resumes), 0)),
        ["AA-05"] = new(T.WordsKept, Alone(Ended(Chat(Helped()) with { Teammate = true }, Resumes), 2)),
        ["AA-06"] = new(T.WordsKept, Alone(Ended(Chat(Helped(), P()), Resumes), 2)),
        ["AA-07"] = new(T.WordsKept, Alone(Ended(Driven("Taken", Helped()), Resumes), 2)),
        ["AA-08"] = new(T.WordsKept, Alone(Ended(Chat(Helped()), Resumes), 2, released: true)),
        ["AA-09"] = new(T.WordsKept, Alone(Ended(Chat(Helped()), Resumes), 0, kept: true)),
        ["AA-10"] = new(T.WordsKept, Alone(Ended(Chat(Helped(), P()), Resumes), 0, kept: true)),
        ["AA-11"] = new(T.WordsKept, Alone(Ended(Chat(Helped()), Resumes), 2)),
        ["AA-12"] = new(T.WordsKept, Alone(Ended(Chat(Helped()), Resumes), 1)),
        ["AA-13"] = new(T.WordsKept, Alone(Ended(Chat(Helped()), Resumes), 0)),
        ["AA-14"] = new(T.WordsKept, Alone(Ended(Chat(Helped(), P()), Resumes), 2)),
        ["AA-15"] = new(T.WordsKept, Alone(Ended(Chat(Helped(), Rebased()), Resumes), 2)),
        ["AA-16"] = new(T.WordsKept, Alone(Ended(Chat(), Resumes), 2)),
        ["AA-17"] = new(T.WordsKept, Alone(Ended(Driven("Taken", Supervised()), Resumes), 2)),
        ["AA-18"] = new(T.Answer, Alone(Parked(Driven("Taken", Supervised()), Resumes), 2)),

        // ——— S: a move record standing for the line, as the design has it (§3.1, §3.3); no path writes one yet.
        ["S-01"] = new(T.WordsKept, Ended(Chat(P()), Resumes) with { Standing = new CourseStanding(CourseMovePhase.Delivering, OldPart: true) }),
        ["S-02"] = new(T.LimitEnd, Live(Chat(P()) with { Limited = true }, Goes("account-2")) with { Standing = new CourseStanding(CourseMovePhase.Held, Destination: true) }),
        ["S-03"] = new(T.WordsKept, Ended(Chat(P()), Resumes) with { Standing = new CourseStanding(CourseMovePhase.Delivered, OldPart: true) }),
    };

    // ——— The shaped outcome: what an answer says, as the table keeps it.

    internal static string Shape(LineCourse course) => course switch
    {
        LineCourse.Resume resume => $"Resume words={Said(resume.Words)}",
        LineCourse.Move move =>
            $"Move when={move.When} where={Where(move.Where)} how={move.How} why={move.Why} by={move.By} words={Said(move.Words)}",
        LineCourse.Start start => $"Start key={start.Key} words={Said(start.Words)}",
        LineCourse.Wait wait =>
            $"Wait until=+{(wait.Until - Seen).TotalHours.ToString("0.##", CultureInfo.InvariantCulture)}h why={wait.Why} accounts={Listed(wait.Accounts)} doors={Listed(wait.Out)}",
        LineCourse.Hold hold => $"Hold why={hold.Why} doors={Listed(hold.Doors)} repair={hold.Repair} code={hold.Code ?? "-"}",
        LineCourse.Never never => $"Never why={never.Why}",
        _ => throw new InvalidOperationException($"an answer the course does not give: {course}"),
    };

    private static string Said(CourseWords words) =>
        (words.Ids.Count == 0 ? "-" : string.Join(",", words.Ids)) + (words.Daoris == CourseDaorisWord.None ? "" : $"+{words.Daoris}:{words.Key}");

    private static string Where(CourseWhere where) =>
        $"{where.Source}{(where.Agent is { } agent ? "@" + agent : "")}{(where.Account is { } account ? ":" + account : "")}"
        + $"({where.Role ?? "-"}/{where.Choice ?? "-"}){(where.Kept ? "!kept" : "")}";

    private static string Listed<TItem>(IEnumerable<TItem> items) => items.Any() ? string.Join(",", items) : "-";

    // ——— The table.

    private sealed record Row(string Id, string Group, string Source, string Outcome);

    private sealed record Difference(string Name, IReadOnlyList<string> Rows, IReadOnlyList<string> Adopts, string Title);

    private sealed record Table(
        IReadOnlyList<Row> Rows, IReadOnlyList<string> Groups, IReadOnlyList<Difference> Differences,
        IReadOnlyDictionary<string, string> LeftOut, IReadOnlyDictionary<string, int> Suites, IReadOnlyDictionary<string, string> Reasons);

    private static Table Read()
    {
        var rows = new List<Row>();
        var groups = new List<string>();
        var differences = new List<Difference>();
        var leftOut = new Dictionary<string, string>(StringComparer.Ordinal);
        var suites = new Dictionary<string, int>(StringComparer.Ordinal);
        var reasons = new Dictionary<string, string>(StringComparer.Ordinal);
        string? group = null;
        foreach (var raw in File.ReadAllLines(TablePath))
        {
            var line = raw.TrimEnd();
            if (line.Length == 0) continue;
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                group = line[3..].Split(' ')[0];
                groups.Add(group);
                continue;
            }

            if (line.StartsWith("#", StringComparison.Ordinal)) continue;
            switch (group)
            {
                case "Differences":
                {
                    var parts = line.Split(' ', 4);
                    differences.Add(new Difference(parts[0], parts[2].Split(','), parts[1].Split(','), parts[3]));
                    break;
                }
                case "Reasons":
                    reasons[line[..1]] = line[2..];
                    break;
                case "Left":
                {
                    var parts = line.Split(' ');
                    Assert.True(parts is ["out", _, _], $"a left-out line reads `out Suite.Method why`: {line}");
                    Assert.True(leftOut.TryAdd(parts[1], parts[2]), $"left out twice: {parts[1]}");
                    break;
                }
                case "Suites":
                {
                    var parts = line.Split(' ');
                    suites[parts[1]] = int.Parse(parts[2], CultureInfo.InvariantCulture);
                    break;
                }
                default:
                {
                    var arrow = line.IndexOf(" => ", StringComparison.Ordinal);
                    Assert.True(arrow > 0 && group is not null, $"a row reads `id source => outcome` under its path's heading: {line}");
                    var head = line[..arrow].Split(' ');
                    rows.Add(new Row(head[0], group!, head[1], line[(arrow + 4)..]));
                    break;
                }
            }
        }

        return new Table(rows, groups, differences, leftOut, suites, reasons);
    }

    // ——— The proofs.

    /// <summary>
    /// Every row of the table answers as its path answers today: its case's facts and trigger through <see cref="CourseDecision.Decide"/>, the
    /// answer shaped as the table keeps it. Every case has its row and every row its case.
    /// </summary>
    [Fact]
    public void Every_row_answers_as_its_path_answers_today()
    {
        var table = Read();
        var cases = Cases();
        Assert.Equal(table.Rows.Select(row => row.Id).Order(StringComparer.Ordinal), cases.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(table.Rows.Count, table.Rows.Select(row => row.Id).Distinct(StringComparer.Ordinal).Count());

        var failures = new List<string>();
        foreach (var row in table.Rows)
        {
            var each = cases[row.Id];
            string answered;
            try
            {
                answered = Shape(CourseDecision.Decide(each.Facts, each.Trigger));
            }
            catch (Exception error) when (error is not Xunit.Sdk.XunitException)
            {
                answered = $"threw {error.GetType().Name}";
            }

            if (answered != row.Outcome) failures.Add($"{row.Id} {row.Source}\n    today  {row.Outcome}\n    course {answered}");
        }

        Assert.True(failures.Count == 0, $"{failures.Count} of {table.Rows.Count} rows answer otherwise:\n" + string.Join("\n", failures.Take(40)));
    }

    /// <summary>
    /// The rows go by path in the design's order, each row under its own path's heading, and each automatic hand-over's door has its
    /// consent case: the curve's switch, and an agent's ask under <c>ask</c> and <c>off</c> on a conversation and on a quest's session.
    /// </summary>
    [Fact]
    public void The_rows_go_by_path_in_the_design_s_order_with_a_consent_case_per_door()
    {
        var table = Read();
        Assert.Equal(["LA", "B", "E", "J", "D", "A", "F", "K", "N", "G", "H", "I", "C", "AA", "S", "Differences", "Reasons", "Left", "Suites"], table.Groups);
        Assert.All(table.Rows, row => Assert.StartsWith(row.Group + "-", row.Id, StringComparison.Ordinal));

        bool Says(string group, string outcome) => table.Rows.Any(row => row.Group == group && row.Outcome.StartsWith(outcome, StringComparison.Ordinal));
        Assert.True(Says("G", "Hold why=SwitchOff"), "the curve's door has no case of its switch refusing it");
        foreach (var door in new[] { "H", "I" })
        {
            Assert.True(Says(door, "Hold why=ConsentRefused doors=LetAskGo,NotNow"), $"{door}: no agent's ask the rule puts to the person");
            Assert.True(Says(door, "Hold why=ConsentRefused doors=- repair=None code=off"), $"{door}: no agent's ask the rule refuses");
            Assert.True(table.Rows.Any(row => row.Group == door && row.Outcome.Contains("why=Agent by=Agent", StringComparison.Ordinal)), $"{door}: no agent's ask the rule admits");
        }
    }

    /// <summary>
    /// Every row names the case it was ported from, and that case is there: a suite's test method (a theory's case after a colon), a row of
    /// the account rule's own table, or, for a reading no suite pins today, the source line the path answers at (named in D212's note).
    /// The standing move's rows are the design's, since no course reads a move record yet.
    /// </summary>
    [Fact]
    public void Every_row_is_ported_from_a_case_that_is_there()
    {
        var table = Read();
        var accountRows = File.ReadAllLines(Path.Combine(Tests, "Agents", "line-account-cases.txt"))
            .Where(line => line.StartsWith("row-", StringComparison.Ordinal)).Select(line => line[..line.IndexOf(' ')]).ToHashSet(StringComparer.Ordinal);
        var driver = Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver");
        var missing = new List<string>();
        foreach (var row in table.Rows)
        {
            var source = row.Source;
            if (source.StartsWith("line-account-cases:", StringComparison.Ordinal))
            {
                if (!accountRows.Contains(source["line-account-cases:".Length..])) missing.Add($"{row.Id}: {source}");
            }
            else if (source.StartsWith("code:", StringComparison.Ordinal))
            {
                var at = source["code:".Length..].Split(':');
                var file = Path.Combine(driver, at[0].Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(file) || File.ReadAllLines(file).Length < int.Parse(at[1], CultureInfo.InvariantCulture)) missing.Add($"{row.Id}: {source}");
            }
            else if (source.StartsWith("design:", StringComparison.Ordinal))
            {
                if (row.Group != "S") missing.Add($"{row.Id}: only the standing move's rows are the design's");
            }
            else
            {
                var (suite, method) = SuiteAndMethod(source);
                if (!Methods(suite).Contains(method)) missing.Add($"{row.Id}: {source}");
            }
        }

        Assert.True(missing.Count == 0, "rows ported from a case that is not there: " + string.Join("; ", missing));
    }

    /// <summary>
    /// Each path's suites, counted: every case (<c>[Fact]</c> or <c>[Theory]</c>) of each is a row's source or left out with its why, and the
    /// table's count of each suite is the suite's, so a case added to a path's suite is placed in the table as it lands.
    /// </summary>
    [Fact]
    public void Every_case_of_the_paths_suites_is_ported_or_left_out_with_why()
    {
        var table = Read();
        var ported = table.Rows.Where(row => !row.Source.Contains(':', StringComparison.Ordinal) || char.IsUpper(row.Source[0]))
            .Select(row => row.Source.Split(':')[0]).ToHashSet(StringComparer.Ordinal);
        var failures = new List<string>();
        foreach (var (suite, count) in table.Suites)
        {
            var methods = Methods(suite);
            if (methods.Count != count) failures.Add($"{suite}: the table counts {count} cases, the suite has {methods.Count}");
            foreach (var method in methods)
            {
                var key = $"{suite}.{method}";
                var left = table.LeftOut.ContainsKey(key);
                if (ported.Contains(key) == left) failures.Add($"{key}: {(left ? "both ported and left out" : "neither ported nor left out")}");
            }
        }

        foreach (var (key, why) in table.LeftOut)
        {
            if (!table.Reasons.ContainsKey(why)) failures.Add($"{key}: left out for {why}, which is no reason the table names");
            if (!table.Suites.ContainsKey(key[..key.IndexOf('.')])) failures.Add($"{key}: a suite the table does not count");
        }

        foreach (var row in table.Rows.Where(row => char.IsUpper(row.Source[0])))
        {
            if (!table.Suites.ContainsKey(SuiteAndMethod(row.Source).Suite) && !row.Source.StartsWith("PlannerTests.", StringComparison.Ordinal))
            {
                failures.Add($"{row.Id}: its suite {row.Source} is not counted");
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>
    /// The ten places where two paths answer the same facts two ways today, each held as its rows: they answer otherwise, the decision telling
    /// them apart by the trigger or a fact and reproducing both, and each names the row that adopts it.
    /// </summary>
    [Fact]
    public void Each_difference_is_rows_answering_alike_facts_two_ways_for_the_row_that_adopts_it()
    {
        var table = Read();
        var rows = table.Rows.ToDictionary(row => row.Id, StringComparer.Ordinal);
        var cases = Cases();
        Assert.Equal(10, table.Differences.Count);
        var failures = new List<string>();
        foreach (var difference in table.Differences)
        {
            Assert.True(difference.Rows.Count >= 2, $"{difference.Name}: a difference is two rows at least");
            Assert.All(difference.Rows, id => Assert.True(rows.ContainsKey(id) && cases.ContainsKey(id), $"{difference.Name}: no row {id}"));
            Assert.NotEmpty(difference.Adopts);
            Assert.All(difference.Adopts, adopts => Assert.Contains(adopts, new[] { "h", "i" }));

            // The decision itself tells the rows apart and gives each its path's answer: both are kept, neither is chosen.
            var decided = difference.Rows.Select(id => Shape(CourseDecision.Decide(cases[id].Facts, cases[id].Trigger))).ToList();
            if (decided.Distinct(StringComparer.Ordinal).Count() < 2) failures.Add($"{difference.Name} ({difference.Title}): its rows answer alike, {decided[0]}");
            foreach (var (id, answered) in difference.Rows.Zip(decided))
            {
                if (answered != rows[id].Outcome) failures.Add($"{difference.Name}: {id} answers {answered}, where its path answers {rows[id].Outcome}");
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>
    /// Every answer carries the preconditions it was read on (§3.1, X): the record's revision, the ids of the words waiting, the quest's
    /// status and a standing move's phase, and a wait the cool-off it waits for, so the executor asks again where one moved.
    /// </summary>
    [Fact]
    public void Every_answer_carries_the_preconditions_it_was_read_on()
    {
        foreach (var (id, each) in Cases())
        {
            var answer = CourseDecision.Decide(each.Facts, each.Trigger);
            var needs = answer switch
            {
                LineCourse.Resume resume => resume.Needs,
                LineCourse.Move move => move.Needs,
                LineCourse.Start start => start.Needs,
                LineCourse.Wait wait => wait.Needs,
                LineCourse.Hold hold => hold.Needs,
                _ => null,
            };
            if (needs is null) continue;

            var head = each.Facts.Head;
            Assert.True(needs.Revision == head.Revision, id);
            Assert.True(needs.WaitingIds!.SequenceEqual(head.Words.Select(word => word.Id)), id);
            Assert.True(needs.QuestStatus == head.QuestStatus, id);
            Assert.True(needs.MovePhase == each.Facts.Standing?.Phase, id);
            if (answer is LineCourse.Wait waits) Assert.True(needs.Cooling?.Until == waits.Until, id);
        }
    }

    /// <summary>
    /// A workflow's send-back (path L) is no decision of its own: an ended conversation's go-on from it answers as path B's words kept. And
    /// SWITCHCTX1's carry (path M) has no code, so no case answers a carried move.
    /// </summary>
    [Fact]
    public void A_send_back_answers_as_an_ended_chat_s_go_on_and_no_case_answers_a_carry()
    {
        foreach (var (id, each) in Cases().Where(pair => pair.Key.StartsWith("B-", StringComparison.Ordinal) && pair.Value.Trigger == T.WordsKept))
        {
            Assert.True(
                Shape(CourseDecision.Decide(each.Facts, T.SendBack)) == Shape(CourseDecision.Decide(each.Facts, T.WordsKept)),
                $"{id}: a send-back answers otherwise than the words kept");
        }

        Assert.All(Cases(), pair => Assert.False(CourseDecision.Decide(pair.Value.Facts, pair.Value.Trigger) is LineCourse.Move { How: CourseHow.Carry }, pair.Key));
    }

    /// <summary>
    /// The decision reads the account rule's answer once, as the executor handed it, and never walks: it names no walk, no roster and no
    /// call of the rule (§3.2, the account step), and holds no I/O (its folder's rule, <c>StructureTests</c>).
    /// </summary>
    [Fact]
    public void The_decision_reads_the_account_answer_and_never_walks()
    {
        var code = CourseCensusTests.WithoutComments(File.ReadAllText(Path.Combine(
            WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver", "Course", "Domain", "LineCourse.Decide.cs")));
        Assert.DoesNotMatch(@"\bLineAccount\.Decide|\bDecideAsync\b|\bLineAsk\b|\bHarnessRoster\b|\bFunc<|\bTask\b", code);
        Assert.Matches(@"\.Account\?*\.Answer\b", code);
    }

    // ——— A suite's cases, read from its source.

    private static (string Suite, string Method) SuiteAndMethod(string source)
    {
        var name = source.Split(':')[0];
        var dot = name.IndexOf('.');
        return (name[..dot], name[(dot + 1)..]);
    }

    private static readonly Dictionary<string, IReadOnlyList<string>> Known = new(StringComparer.Ordinal);

    /// <summary>
    /// The test methods of a suite of the driver's tests: each method a <c>[Fact]</c> or <c>[Theory]</c> stands on. Every suite the table
    /// counts is the driver's, so the read stays in this project, whose changes reach the driver's gate (MOD9's reach rule).
    /// </summary>
    private static IReadOnlyList<string> Methods(string suite)
    {
        lock (Known)
        {
            if (Known.TryGetValue(suite, out var known)) return known;
            var file = Directory.EnumerateFiles(Tests, suite + ".cs", SearchOption.AllDirectories)
                .FirstOrDefault(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar));
            IReadOnlyList<string> methods = file is null
                ? []
                : [.. Regex.Matches(File.ReadAllText(file), @"\[(?:Fact|Theory)\b[^\]]*\](?:\s*\[[^\]]*\])*\s*public\s+(?:async\s+Task|void)\s+(\w+)\(")
                    .Select(match => match.Groups[1].Value)];
            Known[suite] = methods;
            return methods;
        }
    }
}
