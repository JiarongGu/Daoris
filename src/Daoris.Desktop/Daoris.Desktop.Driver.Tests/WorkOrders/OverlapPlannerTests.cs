using Daoris.Desktop.Driver.Tests;

namespace Daoris.Driver.Tests;

/// <summary>
/// At a start the planner compares the quest's declared paths with every earlier unit of
/// its repository, declared or already changed on its branch. Under the rule's <c>overlap: wait</c> the later start is held until the
/// earlier unit's integration is settled; under <c>warn</c> it starts, and the overlap is said.
/// </summary>
public sealed class OverlapPlannerTests
{
    private static QuestView Quest(string id, string status, params string[] paths) =>
        new(id, "Asker", "Game", $"Ask {id}", "Here is why.", status) { Paths = paths };

    private static readonly IsolateStep Waits = IsolateStep.Trees with { OverlapWaits = true };

    private static readonly IsolateStep Warns = IsolateStep.Trees;

    private static readonly DriverConfig Config = DriverConfig.Empty with { Drivable = ["Game"], Cap = 4 };

    private static Consideration Plan(QuestView[] quests, IsolateStep step, Snapshot? with = null)
    {
        var snapshot = (with ?? new Snapshot([], [], [])) with { Quests = quests, Repositories = [new RepoView("Game", true, "D:/fam/Game")] };
        return Planner.Plan(snapshot.Apart(step, "Game"), Config, false).Single(c => c.Quest.Id == "q2");
    }

    [Fact]
    public void An_overlapping_start_under_wait_is_held_and_says_the_earlier_quest_and_the_paths_both_meet()
    {
        var held = Plan([Quest("q1", "Taken", "src/frame/**"), Quest("q2", "Open", "src/frame/cap.cs", "docs/cap.md")], Waits);

        Assert.Equal(StartVerdict.RepositoryBusy, held.Verdict);
        Assert.Equal(SittingFact.Overlaps, held.Said!.Code);
        Assert.Equal("q1", held.Said.Quest);
        Assert.Equal(["src/frame/cap.cs"], held.Said.Paths);
        Assert.StartsWith("overlaps `#q1`: `src/frame/cap.cs`", held.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Under_warn_the_overlapping_start_starts_and_carries_the_overlap_to_say()
    {
        var started = Plan([Quest("q1", "Taken", "src/frame/**"), Quest("q2", "Open", "src/frame/cap.cs")], Warns);

        Assert.Equal(StartVerdict.Start, started.Verdict);
        Assert.Equal("q1", started.Overlaps!.Quest);
        Assert.Equal(["src/frame/cap.cs"], started.Overlaps.Paths);
    }

    /// <summary>
    /// The earlier unit's own declaration names only its docs, and its branch already changed the frame: what it declared cannot release
    /// the hold its changes make on a later start that declares the frame.
    /// </summary>
    [Fact]
    public void An_earlier_units_declaration_cannot_release_the_hold_its_changed_paths_make()
    {
        var snapshot = new Snapshot([], [], [])
        {
            Changed = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase) { ["q1"] = ["src/frame/cap.cs"] },
        };

        var held = Plan([Quest("q1", "Taken", "docs/**"), Quest("q2", "Open", "src/frame/**")], Waits, snapshot);

        Assert.Equal(SittingFact.Overlaps, held.Said!.Code);
        Assert.Equal(["src/frame/**"], held.Said.Paths);
    }

    [Fact]
    public void A_carry_on_of_an_open_quest_goes_back_into_its_tree_under_wait_and_a_first_start_still_waits()
    {
        QuestView[] quests = [Quest("q1", "Taken", "src/frame/**"), Quest("q2", "Open", "src/frame/cap.cs")];
        var limited = new Snapshot([], [], [])
        {
            LastRun = new Dictionary<string, PriorSession>(StringComparer.OrdinalIgnoreCase)
            {
                ["q2"] = new("s2", "D:/trees/s2", "failed") { Limit = true },
            },
        };

        Assert.Equal(StartVerdict.Start, Plan(quests, Waits, limited).Verdict);
        Assert.Equal(StartVerdict.RepositoryBusy, Plan(quests, Waits).Verdict);
    }

    [Fact]
    public void A_done_unit_still_integrating_holds_and_a_settled_one_holds_nothing()
    {
        QuestView[] quests = [Quest("q2", "Open", "src/frame/cap.cs")];
        var integrating = new Snapshot([], [], [])
        {
            Integrating = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "q1" },
            IntegratingQuests = [Quest("q1", "Done", "src/frame/**")],
        };

        Assert.Equal(StartVerdict.RepositoryBusy, Plan(quests, Waits, integrating).Verdict);
        Assert.Equal(StartVerdict.Start, Plan(quests, Waits).Verdict);
    }

    [Fact]
    public void Two_open_starts_at_one_look_meeting_on_a_path_start_one_and_hold_the_later()
    {
        var plan = Planner.Plan(
            new Snapshot([Quest("q1", "Open", "src/frame/**"), Quest("q2", "Open", "src/frame/cap.cs")], [new RepoView("Game", true, "D:/fam/Game")], []).Apart(Waits, "Game"),
            Config, false);

        Assert.Equal(StartVerdict.Start, plan.Single(c => c.Quest.Id == "q1").Verdict);
        Assert.Equal(SittingFact.Overlaps, plan.Single(c => c.Quest.Id == "q2").Said!.Code);
    }

    [Fact]
    public void A_start_declaring_no_paths_meets_nothing()
    {
        Assert.Equal(StartVerdict.Start, Plan([Quest("q1", "Taken", "src/**"), Quest("q2", "Open")], Waits).Verdict);
    }

    [Fact]
    public void A_glob_of_root_files_meets_no_folders_glob_and_one_that_crosses_folders_does()
    {
        Assert.Empty(PathOverlap.Meet(["*.md"], ["docs/**"], []));
        Assert.Empty(PathOverlap.Meet(["docs/**"], ["*.md"], []));
        Assert.Equal(["**/*.md"], PathOverlap.Meet(["**/*.md"], ["docs/**"], []));
        Assert.Equal(["*.md"], PathOverlap.Meet(["*.md"], ["README.*"], []));
    }
}
