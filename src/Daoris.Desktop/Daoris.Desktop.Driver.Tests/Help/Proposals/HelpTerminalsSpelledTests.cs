using Daoris.Driver;

namespace Daoris.Driver.Tests;

public sealed class HelpTerminalsSpelledTests : HelpProposalsFixture
{
    private static readonly HelpMachineFacts Spelled = Machine with
    {
        Workspaces = ["default", "work", "my team", "R&D", "our lab"],
    };

    private static HelpPlan Plan(HelpProposal proposal) => HelpProposals.Plan(proposal, DriverConfig.Empty, Spelled);

    [Theory]
    [InlineData("my team", "daoris workflow import <file> && daoris workflow use docs-to-pr --workspace \"my team\"")]
    [InlineData("R&D", "daoris workflow import <file> && daoris workflow use docs-to-pr --workspace <workspace>")]
    public void A_workflow_s_choice_spells_its_workspace(string workspace, string terminal)
    {
        var graph = System.Text.Json.JsonDocument.Parse(WorkflowDefaults.Of("merge-after-accept")!.GraphJson);
        var plan = Plan(new HelpProposal("p8", "workflow", "new", null, null, null, null, "why", "h1", "proposed")
        {
            Workflow = "docs-to-pr", Scope = workspace, Choose = true, Graph = graph.RootElement.Clone(),
        });

        Assert.Null(plan.Refusal);
        Assert.Equal(terminal, plan.Terminal);
    }
}
