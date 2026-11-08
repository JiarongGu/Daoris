using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The landing supervisor presses a run's steps through each node's own press door and names no node kind: the kit says which of its
/// kinds offer a landing act, which hold a run for the person alone, and which kind takes each act as its own press.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class LandingKindCensusTests
{
    private static readonly string[] Landing =
    [
        "Roles/LandingSteps.cs", "Roles/LandingJudge.cs", "Roles/SupervisorLandings.cs", "Roles/SupervisorLandingDecisions.cs",
        "Roles/SupervisorLandingView.cs", "Workflows/Nodes/SupervisedWords.cs", "Loop/Look/LandingReleaseLook.cs",
    ];

    [Fact]
    public void No_unit_of_the_landing_kind_names_a_node_kind()
    {
        var root = Path.Combine(EveryChildIsHandedTheToolsTests.SourceRoot(), "Daoris.Desktop.Driver");
        var modules = Path.Combine(EveryChildIsHandedTheToolsTests.SourceRoot(), "Daoris.Desktop.Modules", "Bridge", "SupervisorKinds", "LandingKind.cs");
        var found = Landing.Where(file => File.ReadAllText(Path.Combine(root, file)).Contains("NodeKinds.", StringComparison.Ordinal)).ToList();
        if (File.ReadAllText(modules).Contains("NodeKinds.", StringComparison.Ordinal)) found.Add(modules);

        Assert.Empty(found);
    }

    [Fact]
    public void The_acts_the_kit_offers_a_landing_supervisor_are_the_grant_vocabulary_and_each_is_one_kinds_own_press()
    {
        var offered = new List<(string Kind, string Act)>();
        foreach (var kind in NodeKit.Kinds)
        {
            var declared = new NodeDeclaration("n", kind, 1, NodeReach.Pressed) { Outcomes = ["approved"] };
            offered.AddRange(NodeKit.Of(kind)!.LandingActs(declared).Select(act => (kind, act)));
        }

        var vocabulary = SupervisorKinds.ActsOf(SupervisorKinds.Landing).Select(act => act[(SupervisorKinds.Landing.Length + 1)..]).Order().ToList();
        Assert.Equal(vocabulary, offered.Select(each => each.Act).Distinct().Order().ToList());
        foreach (var (kind, act) in offered)
        {
            Assert.True(NodeKit.Of(kind) is IPressed pressed && pressed.Takes.Contains(act), $"{kind} takes no `{act}` as its own press");
            Assert.Equal(act, SupervisorLandingActs.WordOf(act));
        }
    }

    [Fact]
    public void Only_a_go_ahead_holds_a_run_for_the_person_alone_and_a_task_by_hand_offers_no_landing_act()
    {
        Assert.Equal([NodeKinds.GoAhead], NodeKit.Kinds.Where(kind => NodeKit.Of(kind)!.HoldsRun).ToList());
        var task = new NodeDeclaration("t", NodeKinds.Human, 1, NodeReach.Pressed) { Outcomes = ["done"] };
        Assert.Empty(NodeKit.Of(NodeKinds.Human)!.LandingActs(task));
    }
}
