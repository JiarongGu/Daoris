using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>The terminal twin a card shows spells a workspace for any shell (<see cref="ShellWord"/>).</summary>
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

    [Theory]
    [InlineData("my team", "daoris ask --workspace \"my team\" \"fix the stall\"")]
    [InlineData("R&D", "daoris ask --workspace <workspace> \"fix the stall\"")]
    public void An_ask_s_terminal_spells_its_workspace(string workspace, string terminal)
    {
        var plan = Plan(new HelpProposal("p2", "ask", "ask", null, workspace, null, "fix the stall", "why", "h1", "proposed"));

        Assert.Null(plan.Refusal);
        Assert.Equal(terminal, plan.Terminal);
    }
}
