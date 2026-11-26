using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A session whose turn ended on its check goes on at the first look after the verdict, and a resume into its own existing tree
/// queues behind no other start of its repository: where the look read the repository as working in its one place, a head planned
/// as a start at every look kept the repository's turn on the install, and a session whose verdict waited stood behind it for five
/// hours. Such a resume carries an isolation, so the run goes on in the tree its record holds and not in the repository's root.
/// </summary>
public sealed class StartWakePlanTests
{
    private const string Root = "D:/fam/Game";

    private static QuestView Quest(string id, string status = "Taken") => new(id, "Asker", "Game", $"Ask {id}", "Here is why.", status);

    /// <summary>The check's verdict as Daoris says it to the session that asked, kept under the check's key.</summary>
    private static IReadOnlyList<SaidWordView> Verdict(string check) =>
        [new SaidWordView("w-" + check, "Daoris ran, as you asked, `npm run verify` in your tree and it reached.", DateTimeOffset.MinValue, [], false)
        {
            Key = $"check/{check}", From = DaorisWords.From,
        }];

    private static PriorSession Record(string session, string tree) =>
        new(session, tree, "awaiting-step", null, "Game") { Said = Verdict(session) };

    private static PriorSession GoOn(string session, string tree) =>
        new(session, tree, "completed", "ended on its step.") { Said = PersonWords.Of("Go on.") };

    private static SessionView Waiting(string session, string quest, string tree) =>
        new(session, "Game", "awaiting-step") { Kind = "driven", Quest = quest, Tree = tree };

    private static Snapshot Snap(bool apart, QuestView[] quests, SessionView[] active, params PriorSession[] records) =>
        new Snapshot(quests, [new RepoView("Game", true, Root)], active)
        {
            LastRun = records.Zip(quests).ToDictionary(pair => pair.Second.Id, pair => pair.First, StringComparer.OrdinalIgnoreCase),
        }.Apart(apart, "Game");

    private static DriverConfig Config(int cap = 4) => DriverConfig.Empty with { Drivable = ["Game"], Cap = cap };

    [Fact]
    public void A_verdict_the_record_keeps_is_a_word_waiting_on_it_for_the_planner()
    {
        var record = Record("s1", "D:/trees/s1");

        Assert.True(record.WordsWaiting);
        Assert.Equal("check/s1", Assert.Single(record.Waiting).Key);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Two_sessions_waiting_on_their_checks_resume_in_their_own_trees_at_the_same_look(bool apart)
    {
        var plan = Planner.Plan(
            Snap(
                apart,
                [Quest("q1"), Quest("q2")],
                [Waiting("s1", "q1", "D:/trees/s1"), Waiting("s2", "q2", "D:/trees/s2")],
                Record("s1", "D:/trees/s1"), Record("s2", "D:/trees/s2")),
            Config());

        Assert.Equal([StartVerdict.Start, StartVerdict.Start], plan.Select(each => each.Verdict));
        Assert.All(plan, each => Assert.True(each.GoesOn));
        // Read as working in its one place, the repository still sends the run back into the tree its record holds.
        Assert.All(plan, each => Assert.NotNull(each.Isolate));
    }

    /// <summary>The head that held the install's queue: it goes on in the repository's own place, and the verdict's word still starts its session.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_head_going_on_in_the_repository_s_place_holds_no_turn_against_a_resume_in_its_own_tree(bool apart)
    {
        var plan = Planner.Plan(
            Snap(apart, [Quest("head"), Quest("q2")], [Waiting("s2", "q2", "D:/trees/s2")], GoOn("s0", Root), Record("s2", "D:/trees/s2")),
            Config());

        Assert.Equal(StartVerdict.Start, plan.Single(each => each.Quest.Id == "q2").Verdict);
        Assert.NotNull(plan.Single(each => each.Quest.Id == "q2").Isolate);
    }

    /// <summary>A resume in its own tree takes none of the repository's place: a first start there is not queued behind it.</summary>
    [Fact]
    public void A_resume_in_its_own_tree_takes_no_turn_of_the_repository_from_a_start_in_its_place()
    {
        var plan = Planner.Plan(
            Snap(false, [Quest("q1"), Quest("q2", "Open")], [Waiting("s1", "q1", "D:/trees/s1")], Record("s1", "D:/trees/s1")),
            Config());

        Assert.Equal([StartVerdict.Start, StartVerdict.Start], plan.Select(each => each.Verdict));
        Assert.Null(plan[1].Isolate);
    }

    /// <summary>What still holds: the one tree a resume goes back into has one session, whatever the look.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_resume_still_waits_for_another_session_active_in_its_tree(bool apart)
    {
        var plan = Planner.Plan(
            Snap(
                apart,
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
                false,
                [Quest("q1"), Quest("q2")],
                [Waiting("s1", "q1", "D:/trees/s1"), Waiting("s2", "q2", "D:/trees/s2")],
                Record("s1", "D:/trees/s1"), Record("s2", "D:/trees/s2")),
            Config(cap: 1));

        Assert.Equal([StartVerdict.Start, StartVerdict.AtCapacity], plan.Select(each => each.Verdict));
    }

    /// <summary>A resume in the repository's own place works in the one tree every start there shares, so it still goes one at a time.</summary>
    [Fact]
    public void A_resume_in_the_repository_s_place_still_queues_behind_the_start_ahead_of_it()
    {
        var plan = Planner.Plan(Snap(false, [Quest("q1"), Quest("q2")], [], GoOn("s1", Root), GoOn("s2", Root)), Config());

        Assert.Equal([StartVerdict.Start, StartVerdict.RepositoryBusy], plan.Select(each => each.Verdict));
        Assert.Equal(SittingFact.Queued, plan[1].Said?.Code);
        Assert.All(plan, each => Assert.Null(each.Isolate));
    }
}
