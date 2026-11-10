using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// The terminal twin a card shows, and every command a refusal names, spells an account
/// and a workspace for any shell, as the CLI's hints and the page's twins do (<see cref="ShellWord"/>): in double quotes where
/// a space needs them, and a placeholder where no spelling holds. The words around a command name them as they are.
/// </summary>
public sealed class HelpTerminalsSpelledTests : HelpProposalsFixture
{
    private static readonly HelpMachineFacts Spelled = Machine with
    {
        Workspaces = ["default", "work", "my team", "R&D", "our lab"],
    };

    private static HelpPlan Plan(HelpProposal proposal) => HelpProposals.Plan(proposal, DriverConfig.Empty, Spelled);

    [Theory]
    [InlineData("line", "my team", "main", "daoris place line --workspace \"my team\" main")]
    [InlineData("language", "R&D", "zh", "daoris place language --workspace <workspace> zh")]
    [InlineData("across", "my team", "read off", "daoris place across --workspace \"my team\" read off")]
    public void A_workspace_setting_s_terminal_spells_its_workspace(string door, string workspace, string value, string terminal)
    {
        var plan = Plan(Setting(door, workspace: workspace, value: value));

        Assert.Null(plan.Refusal);
        Assert.Equal(terminal, plan.Terminal);
    }

    /// <summary>A workflow card's choice for a workspace spells the workspace for any shell; the workflow's id is an id.</summary>
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
