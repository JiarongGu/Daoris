using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A session waiting for its step holds no place under the cap. On the owner's install all ten places were held by sessions
/// whose turn had ended on a held check, a hold or an opinion, which used no tokens, while new quests retried their start for an hour.
/// The cap counts the sessions that run a turn or will within the look; one awaiting its step frees its place until the step answers,
/// and takes one again, ahead of every new start, when it resumes. The list still keeps it: it holds its tree and its quest.
/// </summary>
public sealed class SlotWaitTests
{
    private static readonly string[] Repositories = ["A", "B", "C", "D"];

    private static QuestView Open(string id, string to) => new(id, "Asker", to, $"Ask {id}", "Here is why.", "Open");

    private static QuestView Taken(string id, string to) => new(id, "Asker", to, $"Ask {id}", "Here is why.", "Taken");

    private static DriverConfig Config(int cap) => DriverConfig.Empty with { Drivable = Repositories, Cap = cap };

    private static SessionView Waiting(string id, string repository, string quest) =>
        new(id, repository, "awaiting-step") { Kind = "driven", Quest = quest, Tree = $"D:/trees/{id}" };

    private static SessionView Working(string id, string repository, string quest) =>
        new(id, repository, "working") { Kind = "driven", Quest = quest, Tree = $"D:/trees/{id}" };

    private static Snapshot Snapshot(QuestView[] quests, SessionView[] active, IReadOnlyDictionary<string, PriorSession>? lastRun = null) =>
        (new Snapshot(quests, [.. Repositories.Select(name => new RepoView(name, true, $"D:/fam/{name}"))], active)
        {
            LastRun = lastRun ?? new Dictionary<string, PriorSession>(),
        }).Apart(Repositories);

    /// <summary>A record whose turn ended on its step: the state the service keeps, with no process.</summary>
    private static PriorSession Record(string session, string repository, bool answered) =>
        new(session, $"D:/trees/{session}", "awaiting-step", null, repository)
        {
            Said = answered ? PersonWords.Of("The check passed.") : [],
        };

    [Fact]
    public void With_the_cap_at_two_and_two_sessions_awaiting_steps_a_third_quest_starts()
    {
        var plan = Planner.Plan(
            Snapshot(
                [Taken("q1", "A"), Taken("q2", "B"), Open("q3", "C")],
                [Waiting("s1", "A", "q1"), Waiting("s2", "B", "q2")]),
            Config(cap: 2));

        Assert.Equal(StartVerdict.Start, Assert.Single(plan, c => c.Quest.Id == "q3").Verdict);
    }

    [Fact]
    public void A_session_awaiting_its_step_takes_no_slot_in_the_count_and_stays_in_the_list()
    {
        var snapshot = Snapshot([], [Waiting("s1", "A", "q1"), Working("s2", "B", "q2")]);

        Assert.Equal(2, snapshot.Active.Count);
        Assert.Equal(1, snapshot.SlotsHeld);
        Assert.False(ActiveSessions.HoldsSlot(Waiting("s1", "A", "q1")));
        Assert.True(ActiveSessions.HoldsSlot(Working("s2", "B", "q2")));
        // The same rule over a record's fields as the service answers them.
        Assert.False(ActiveSessions.HoldsSlot("driven", "A", null, null, null, state: "awaiting-step"));
        Assert.True(ActiveSessions.HoldsSlot("driven", "A", null, null, null, state: "working"));
    }

    /// <summary>It holds its tree and its quest all the same: a start of its quest sits, and a tree it holds is not given away.</summary>
    [Fact]
    public void A_session_awaiting_its_step_still_holds_its_quest_and_its_tree()
    {
        var plan = Planner.Plan(Snapshot([Open("q1", "A")], [Waiting("s1", "A", "q1")]), Config(cap: 2));

        var only = Assert.Single(plan);
        Assert.Equal(StartVerdict.RepositoryBusy, only.Verdict);
        Assert.Contains("`s1`", only.Reason);
    }

    /// <summary>The step answers while the places are spent: the session goes on ahead of the quest that began after it.</summary>
    [Fact]
    public void A_session_whose_step_answered_resumes_ahead_of_a_new_start_when_one_place_is_left()
    {
        var lastRun = new Dictionary<string, PriorSession> { ["q1"] = Record("s1", "A", answered: true) };
        var plan = Planner.Plan(
            Snapshot(
                [Open("q3", "C"), Taken("q1", "A"), Taken("q2", "B")],
                [Waiting("s1", "A", "q1"), Waiting("s2", "B", "q2"), Working("s4", "D", "q4")],
                lastRun),
            Config(cap: 2));

        Assert.Equal(StartVerdict.Start, plan.Single(c => c.Quest.Id == "q1").Verdict);
        Assert.Equal(StartVerdict.AtCapacity, plan.Single(c => c.Quest.Id == "q3").Verdict);
    }

    /// <summary>With every place spent it waits for one, as a start does, and no later quest takes it first.</summary>
    [Fact]
    public void A_session_whose_step_answered_takes_a_place_again_where_the_cap_is_spent()
    {
        var lastRun = new Dictionary<string, PriorSession> { ["q1"] = Record("s1", "A", answered: true) };
        var plan = Planner.Plan(
            Snapshot(
                [Open("q3", "C"), Taken("q1", "A")],
                [Waiting("s1", "A", "q1"), Working("s4", "D", "q4"), Working("s5", "D", "q5")],
                lastRun),
            Config(cap: 2));

        Assert.Equal(StartVerdict.AtCapacity, plan.Single(c => c.Quest.Id == "q1").Verdict);
        Assert.Equal(StartVerdict.AtCapacity, plan.Single(c => c.Quest.Id == "q3").Verdict);
    }

    /// <summary>A park the person answered still holds its place among the active: going on, it starts no second session.</summary>
    [Fact]
    public void An_answered_park_still_holds_its_own_slot()
    {
        var parked = new SessionView("s1", "A", "awaiting-person") { Kind = "driven", Quest = "q1", Tree = "D:/trees/s1" };
        var park = new PriorSession("s1", "D:/trees/s1", "awaiting-person", null, "A") { Said = PersonWords.Of("Port 8080.") };

        var plan = Planner.Plan(
            Snapshot([Taken("q1", "A")], [parked], new Dictionary<string, PriorSession> { ["q1"] = park }), Config(cap: 1));

        Assert.Equal(StartVerdict.Start, Assert.Single(plan).Verdict);
    }
}
