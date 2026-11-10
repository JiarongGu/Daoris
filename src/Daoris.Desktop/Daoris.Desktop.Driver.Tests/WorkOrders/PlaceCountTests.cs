using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// How many of a place's sessions start at once is its workflow's <c>isolate</c> step's: none, and one writer works in the place; a count,
/// and that many start apart, each carrying the step to its tree, the next sitting until one ends; <c>any</c>, and the cap alone bounds it.
/// </summary>
public sealed class PlaceCountTests
{
    private static QuestView Quest(string id) => new(id, "Asker", "Game", $"Ask {id}", "Here is why.", "Open");

    private static readonly QuestView[] Four = [Quest("q1"), Quest("q2"), Quest("q3"), Quest("q4")];

    private static IReadOnlyList<Consideration> Plan(IsolateStep? apart, int cap) =>
        Planner.Plan(
            new Snapshot(Four, [new RepoView("Game", true, "D:/fam/Game")], [])
            {
                Isolations = apart is null
                    ? new Dictionary<string, IsolateStep>()
                    : new Dictionary<string, IsolateStep>(StringComparer.OrdinalIgnoreCase) { ["game"] = apart },
            },
            DriverConfig.Empty with { Drivable = ["Game"], Cap = cap });

    [Fact]
    public void A_place_whose_workflow_sets_nothing_apart_runs_one_writer_in_place()
    {
        var plan = Plan(null, cap: 5);

        var started = Assert.Single(plan, each => each.Verdict == StartVerdict.Start);
        Assert.Null(started.Isolate);
        Assert.All(plan.Where(each => each != started), each => Assert.Equal(StartVerdict.RepositoryBusy, each.Verdict));
    }

    [Fact]
    public void With_at_once_three_three_start_apart_carrying_the_step_and_the_fourth_sits_on_the_count()
    {
        var step = IsolateStep.Trees with { AtOnce = 3 };

        var plan = Plan(step, cap: 5);

        Assert.Equal(3, plan.Count(each => each.Verdict == StartVerdict.Start && each.Isolate == step));
        var sits = Assert.Single(plan, each => each.Verdict != StartVerdict.Start);
        Assert.Equal((StartVerdict.RepositoryBusy, SittingFact.Apart, 3), (sits.Verdict, sits.Said!.Code, sits.Said.Cap));
    }

    [Fact]
    public void Any_is_bounded_by_the_machines_cap_alone()
    {
        var plan = Plan(IsolateStep.Trees, cap: 2);

        Assert.Equal(2, plan.Count(each => each.Verdict == StartVerdict.Start));
        Assert.All(plan.Where(each => each.Verdict != StartVerdict.Start), each => Assert.Equal(StartVerdict.AtCapacity, each.Verdict));
    }
}
