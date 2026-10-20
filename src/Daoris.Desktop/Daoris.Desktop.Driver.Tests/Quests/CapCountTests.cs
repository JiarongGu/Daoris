using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// CAPCOUNT1 (D130's note): the cap counts what the driver starts, never a conversation the person holds. On the owner's install two
/// Ask Daoris conversations idle between turns held two of the cap's four slots for hours, so driven quests started two at a time.
/// What the driver starts is a driven quest session, an intake, a helper and a reviewer; a conversation is a record of the chat
/// kind (or Ask Daoris's own place) that none of the driver's own marks names. The list keeps every active session, so a busy tree
/// and a repository's holder read as before: only the count changes.
/// </summary>
public sealed class CapCountTests
{
    private static readonly string[] Repositories = ["A", "B", "C", "D"];

    private static QuestView Quest(string id, string to) => new(id, "Asker", to, $"Ask {id}", "Here is why.", "Open");

    private static RepoView Repo(string name) => new(name, true, $"D:/fam/{name}");

    private static DriverConfig Config(int cap, bool trees = true) =>
        DriverConfig.Empty with { Drivable = Repositories, Cap = cap, Trees = trees ? Repositories : [] };

    private static Snapshot Snapshot(QuestView[] quests, params SessionView[] active) => new(quests, [.. Repositories.Select(Repo)], active);

    /// <summary>An Ask Daoris conversation, idle between turns: the chat kind, in the help room's own place.</summary>
    private static SessionView AskDaoris(string id) => new(id, HelpRoom.Repository, "working") { Kind = "chat" };

    /// <summary>A conversation the person opened in a repository.</summary>
    private static SessionView Chat(string id, string repository) => new(id, repository, "working") { Kind = "chat" };

    /// <summary>An intake: a chat that serves no quest and answers an ask, in a place of its own.</summary>
    private static SessionView Intake(string id, string ask) => new(id, $"ask #{ask}", "working") { Kind = "chat", Ask = ask };

    /// <summary>A helper: a chat in its asker's repository, which answers its asker's session.</summary>
    private static SessionView Helper(string id, string repository, string asker) =>
        new(id, repository, "working") { Kind = "chat", Serves = asker };

    /// <summary>A reviewer: a chat in the candidate's repository, which reads another session's work for its second opinion.</summary>
    private static SessionView Reviewer(string id, string repository, string opinion) =>
        new(id, repository, "working") { Kind = "chat", Opinion = opinion };

    private static QuestView[] Quests() => [Quest("q1", "A"), Quest("q2", "B"), Quest("q3", "C"), Quest("q4", "D")];

    [Fact]
    public void Two_idle_Ask_Daoris_conversations_leave_a_cap_of_four_starting_four_quests()
    {
        var plan = Planner.Plan(Snapshot(Quests(), AskDaoris("h1"), AskDaoris("h2")), Config(cap: 4));

        Assert.Equal([StartVerdict.Start, StartVerdict.Start, StartVerdict.Start, StartVerdict.Start], plan.Select(c => c.Verdict));
    }

    [Fact]
    public void A_conversation_the_person_opened_in_a_repository_takes_no_slot()
    {
        var plan = Planner.Plan(Snapshot([Quest("q1", "B")], Chat("c1", "A")), Config(cap: 1));

        Assert.Equal(StartVerdict.Start, Assert.Single(plan).Verdict);
    }

    /// <summary>The list keeps the conversation: it still holds its repository's root where each session does not open a tree.</summary>
    [Fact]
    public void A_conversation_still_holds_the_repository_it_works_in_though_it_takes_no_slot()
    {
        var plan = Planner.Plan(Snapshot([Quest("q1", "A")], Chat("c1", "A")), Config(cap: 4, trees: false));

        var only = Assert.Single(plan);
        Assert.Equal(StartVerdict.RepositoryBusy, only.Verdict);
        Assert.Contains("`c1`", only.Reason);
    }

    [Fact]
    public void Driven_quest_sessions_take_slots_beside_the_conversations_that_take_none()
    {
        var driven = new SessionView("s1", "A", "working") { Kind = "driven", Quest = "q1", Tree = "D:/trees/s-1" };

        var plan = Planner.Plan(Snapshot(Quests(), driven, AskDaoris("h1"), Chat("c1", "B")), Config(cap: 3));

        // q1's own session is working, so it waits for it; the cap of three leaves two places after the driven one.
        Assert.Equal(
            [StartVerdict.RepositoryBusy, StartVerdict.Start, StartVerdict.Start, StartVerdict.AtCapacity], plan.Select(c => c.Verdict));
    }

    [Fact]
    public void A_running_intake_still_takes_a_slot()
    {
        var plan = Planner.Plan(Snapshot([Quest("q1", "A")], Intake("i1", "a1b2c3")), Config(cap: 1));

        Assert.Equal(StartVerdict.AtCapacity, Assert.Single(plan).Verdict);
    }

    [Fact]
    public void A_running_helper_still_takes_a_slot()
    {
        var plan = Planner.Plan(Snapshot([Quest("q1", "B")], Helper("h1", "A", "s1")), Config(cap: 1));

        Assert.Equal(StartVerdict.AtCapacity, Assert.Single(plan).Verdict);
    }

    [Fact]
    public void A_running_second_opinion_still_takes_a_slot()
    {
        var plan = Planner.Plan(Snapshot([Quest("q1", "B")], Reviewer("r1", "A", "o1")), Config(cap: 1));

        Assert.Equal(StartVerdict.AtCapacity, Assert.Single(plan).Verdict);
    }

    /// <summary>
    /// How a record is told: by its kind and its place, and the marks the driver gives what it starts, never by its words. A record
    /// the host answers no kind for is read as driven work, as it was before, and counts.
    /// </summary>
    [Theory]
    [InlineData("driven", "A", null, null, null, true)]
    [InlineData("", "A", null, null, null, true)]
    [InlineData("chat", "A", null, null, null, false)]
    [InlineData("chat", "daoris:help", null, null, null, false)]
    [InlineData("", "daoris:help", null, null, null, false)]
    [InlineData("chat", "ask #a1b2c3", "a1b2c3", null, null, true)]
    [InlineData("chat", "A", null, "s1", null, true)]
    [InlineData("chat", "A", null, null, "o1", true)]
    [InlineData("chat", "daoris:help", null, "h1", null, true)]
    public void The_driver_tells_what_it_started_from_a_conversation_by_the_records_kind_and_marks(
        string kind, string repository, string? ask, string? serves, string? opinion, bool holds)
    {
        var session = new SessionView("s1", repository, "working") { Kind = kind, Ask = ask, Serves = serves, Opinion = opinion };

        Assert.Equal(holds, ActiveSessions.HoldsSlot(session));
        Assert.Equal(holds ? 1 : 0, Snapshot([], session).SlotsHeld);
        // The same rule over a record's fields, which the Sessions badge and the status bar count by (SESSBADGE1).
        Assert.Equal(holds, ActiveSessions.HoldsSlot(kind, repository, ask, serves, opinion));
    }

    /// <summary>A record whose host answers no kind is driven work over its fields too, as <see cref="SessionView.Kind"/> reads it.</summary>
    [Fact]
    public void A_record_with_no_kind_holds_a_slot_over_its_fields_as_over_its_view() =>
        Assert.True(ActiveSessions.HoldsSlot(kind: null, repository: "A", ask: null, serves: null, opinion: null));

    /// <summary>The count and the list differ: the snapshot keeps every active session and counts those the driver started.</summary>
    [Fact]
    public void The_snapshot_keeps_every_active_session_and_counts_only_what_the_driver_started()
    {
        var snapshot = Snapshot([], AskDaoris("h1"), Chat("c1", "A"), Intake("i1", "a1"), Helper("h2", "B", "s1"), Reviewer("r1", "C", "o1"));

        Assert.Equal(5, snapshot.Active.Count);
        Assert.Equal(3, snapshot.SlotsHeld);
    }

    /// <summary>
    /// What the service lists on loopback is read for the kind and each mark (the same record the prior-session read already takes them
    /// from), so the count is made on the records' own facts end to end.
    /// </summary>
    [Fact]
    public void The_service_s_records_are_read_for_their_kind_and_the_marks_the_driver_counts_by()
    {
        var sessions = ServiceClient.ReadSessions(JsonSerializer.Serialize(new object[]
        {
            new { id = "s1", repository = "A", state = "working", kind = "driven", quest = "q1" },
            new { id = "h1", repository = "daoris:help", state = "working", kind = "chat" },
            new { id = "c1", repository = "B", state = "awaiting-person", kind = "chat" },
            new { id = "i1", repository = "ask #a1", state = "working", kind = "chat", ask = "a1" },
            new { id = "w1", repository = "C", state = "working", kind = "chat", serves = "s1" },
            new { id = "r1", repository = "D", state = "working", kind = "chat", opinion = "o1" },
            new { id = "old", repository = "A", state = "working" },
        }));

        Assert.Equal(["s1", "i1", "w1", "r1", "old"], sessions.Where(ActiveSessions.HoldsSlot).Select(session => session.Id));
        Assert.Equal(["h1", "c1"], sessions.Where(session => !ActiveSessions.HoldsSlot(session)).Select(session => session.Id));
    }
}
