using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// INTEGRATE1n part n1 (the integration design §17.4, §17.6): the <c>integrate</c> kind's subject and its title, the two places a kind is named
/// that no table of the graph rules holds. Its authority, options and graph rules are held by <see cref="NodeLifecycleTests"/> and the shared
/// table, its drawing by <see cref="NodeGraphHomesTests"/>, its lowering by <see cref="WorkflowGateTests"/>.
/// </summary>
public sealed class IntegrateNodeKindTests
{
    private static readonly NodeDeclaration Integrate = new("integrate", NodeKinds.Integrate, 1, NodeReach.Waited);

    /// <summary>
    /// §17.4, <i>its subject</i>: the landed branch at the tip <c>handed</c> recorded, as every node after <c>handed</c> binds the landing's tip
    /// from its record (D190's third round, point 2): the landing's subject wherever the graph wires it, and the nodes after <c>handed</c> in
    /// Current's drawing, <c>integrate</c> among them.
    /// </summary>
    [Fact]
    public void An_integrates_subject_is_the_landing_at_the_tip_handed_recorded()
    {
        Assert.Equal(RunSubject.Landing, RunSubjects.Of(Integrate, new HashSet<string>()));
        Assert.Equal(RunSubject.Landing, RunSubjects.Of(Integrate, new HashSet<string> { "integrate" }));

        var current = NodeGraphHomes.Landing(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}") { Integrate = new LandingIntegrate() });
        var after = RunSubjects.AfterHanded(current);
        Assert.Equal(["integrate", "line"], after.Order(StringComparer.Ordinal));
        Assert.All(current.Nodes.Where(node => node.Kind != NodeKinds.Handed), node => Assert.Equal(RunSubject.Landing, RunSubjects.Of(node, after)));
        Assert.Equal(RunSubject.Branch, RunSubjects.Of(current.Nodes.Single(node => node.Kind == NodeKinds.Handed), after));
    }

    /// <summary>§17.4: the run's view names the step, "The integration", where the driver says a node's title in the terminal.</summary>
    [Fact]
    public void The_run_names_the_step_the_integration()
    {
        var step = new WorkflowStep("integrate", NodeKinds.Integrate, WorkflowParticipation.Automatic, WorkflowExecutor.Daoris, null,
            new WorkflowSource("workflow", WorkflowLevels.Repository), [], WorkflowRuntime.Built, null);

        Assert.Equal("The integration", WorkflowRunWords.Title(step));
    }
}
