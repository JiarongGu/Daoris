using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A session whose turn ended on its check goes on at the first look after the verdict, and a resume into its own existing tree
/// queues behind no other start of its repository: a head planned as a start at every look kept the repository's turn on the
/// install, and a session whose verdict waited stood behind it for five hours. Only a start in the repository's own place shares it.
/// </summary>
public sealed class StartWakePlanTests
{
    private const string Root = "D:/fam/Game";

    private static QuestView Quest(string id, string status = "Taken") => new(id, "Asker", "Game", $"Ask {id}", "Here is why.", status);

    /// <summary>A record whose turn ended on its check, with the verdict's word waiting, in the tree it holds.</summary>
    private static PriorSession Record(string session, string tree) =>
        new(session, tree, "awaiting-step", null, "Game") { Said = PersonWords.Of("Daoris ran, as you asked, `npm run verify` in your tree and it reached.") };

    private static SessionView Waiting(string session, string quest, string tree) =>
        new(session, "Game", "awaiting-step") { Kind = "driven", Quest = quest, Tree = tree };

    private static Snapshot Snap(QuestView[] quests, SessionView[] active, params PriorSession[] records) =>
        new Snapshot(quests, [new RepoView("Game", true, Root)], active)
        {
            LastRun = records.Zip(quests).ToDictionary(pair => pair.Second.Id, pair => pair.First, StringComparer.OrdinalIgnoreCase),
        };

    private static DriverConfig Config(int cap = 4) => DriverConfig.Empty with { Drivable = ["Game"], Cap = cap };

    [Fact]
    public void Two_sessions_waiting_on_their_checks_resume_in_their_own_trees_at_the_same_look()
    {
        var plan = Planner.Plan(
            Snap(
                [Quest("q1"), Quest("q2")],
                [Waiting("s1", "q1", "D:/trees/s1"), Waiting("s2", "q2", "D:/trees/s2")],
                Record("s1", "D:/trees/s1"), Record("s2", "D:/trees/s2")),
            Config());

        Assert.Equal([StartVerdict.Start, StartVerdict.Start], plan.Select(each => each.Verdict));
        Assert.All(plan, each => Assert.True(each.GoesOn));
    }

    /// <summary>The head that held the install's queue: it goes on in the repository's own place, and the verdict's word still starts its session.</summary>
    [Fact]
    public void A_head_going_on_in_the_repository_s_place_holds_no_turn_against_a_resume_in_its_own_tree()
    {
        var plan = Planner.Plan(
            Snap(
                [Quest("head"), Quest("q2")],
                [Waiting("s2", "q2", "D:/trees/s2")],
                new PriorSession("s0", Root, "completed", "ended on its step.") { Said = PersonWords.Of("Go on.") },
                Record("s2", "D:/trees/s2")),
            Config());

        Assert.Equal(StartVerdict.Start, plan.Single(each => each.Quest.Id == "head").Verdict);
        Assert.Equal(StartVerdict.Start, plan.Single(each => each.Quest.Id == "q2").Verdict);
    }

    /// <summary>A resume in its own tree takes none of the repository's place: a first start there is not queued behind it.</summary>
    [Fact]
    public void A_resume_in_its_own_tree_takes_no_turn_of_the_repository_from_a_start_in_its_place()
    {
        var plan = Planner.Plan(
            Snap([Quest("q1"), Quest("q2", "Open")], [Waiting("s1", "q1", "D:/trees/s1")], Record("s1", "D:/trees/s1")),
            Config());

        Assert.Equal([StartVerdict.Start, StartVerdict.Start], plan.Select(each => each.Verdict));
    }

    /// <summary>What still holds: the one tree a resume goes back into has one session, whatever the look.</summary>
    [Fact]
    public void A_resume_still_waits_for_another_session_active_in_its_tree()
    {
        var plan = Planner.Plan(
            Snap(
                [Quest("q1")],
                [Waiting("s1", "q1", "D:/trees/s1"), new SessionView("other", "Game", "working") { Kind = "driven", Quest = "q9", Tree = "D:/trees/s1" }],
                Record("s1", "D:/trees/s1")),
            Config());

        var only = Assert.Single(plan);
        Assert.Equal(StartVerdict.RepositoryBusy, only.Verdict);
        Assert.Equal(SittingFact.Tree, only.Said?.Code);
    }

    /// <summary>And the machine's admission: the cap still counts every resume.</summary>
    [Fact]
    public void Resumes_in_their_own_trees_still_spend_the_cap()
    {
        var plan = Planner.Plan(
            Snap(
                [Quest("q1"), Quest("q2")],
                [Waiting("s1", "q1", "D:/trees/s1"), Waiting("s2", "q2", "D:/trees/s2")],
                Record("s1", "D:/trees/s1"), Record("s2", "D:/trees/s2")),
            Config(cap: 1));

        Assert.Equal([StartVerdict.Start, StartVerdict.AtCapacity], plan.Select(each => each.Verdict));
    }

    /// <summary>A resume in the repository's own place still goes one at a time: it works in the one tree every start there shares.</summary>
    [Fact]
    public void A_resume_in_the_repository_s_place_still_queues_behind_the_start_ahead_of_it()
    {
        var plan = Planner.Plan(
            Snap(
                [Quest("q1"), Quest("q2")],
                [],
                new PriorSession("s1", Root, "completed", "ended on its step.") { Said = PersonWords.Of("Go on.") },
                new PriorSession("s2", Root, "completed", "ended on its step.") { Said = PersonWords.Of("Go on.") }),
            Config());

        Assert.Equal([StartVerdict.Start, StartVerdict.RepositoryBusy], plan.Select(each => each.Verdict));
        Assert.Equal(SittingFact.Queued, plan[1].Said?.Code);
    }
}
