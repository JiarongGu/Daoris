using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// ACCTQUOTE1b (D125's ACCTQUOTE1 note): the terminal twin a card shows, and every command a refusal names, spells an account
/// and a workspace for any shell, as the CLI's hints and the page's twins do (<see cref="ShellWord"/>): in double quotes where
/// a space needs them, and a placeholder where no spelling holds. The words around a command name them as they are.
/// </summary>
public sealed class HelpTerminalsSpelledTests : HelpProposalsFixture
{
    private static IReadOnlyDictionary<string, IReadOnlyList<string>> Lists(string agent, params string[] accounts) =>
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase) { [agent] = accounts };

    /// <summary>
    /// A machine whose accounts need spelling: the set's order <c>work, my acct, R&amp;D</c> (POOL1b, D197). Before POOL1b it held
    /// the workspaces' own lists, whose names a <c>default</c>'s and a <c>use</c>'s <c>--workspace</c> spelled; neither names one now.
    /// </summary>
    private static readonly HelpMachineFacts Spelled = Machine with
    {
        Workspaces = ["default", "work", "my team", "R&D", "our lab"],
        Doors =
        [
            new HelpDoorFacts("claude-code")
            {
                Present = true, Updates = "tool", Channel = "claude-code-releases", Owner = "claude-code",
                Accounts = ["work", "my acct", "R&D"], SettingsKnown = true, Product = "Claude Code",
            },
        ],
        Wiring = new HarnessSettings { Rotation = Lists("claude-code", "work", "my acct", "R&D") },
    };

    private static HelpPlan Plan(HelpProposal proposal) => HelpProposals.Plan(proposal, DriverConfig.Empty, Spelled);

    private static HelpProposal Agent(string door, string? account, string? workspace, UseChange? use = null) =>
        new("p7", "agent", door, "claude-code", workspace, account, null, "the person asked", "h1", "proposed") { AccountUse = use, ForSet = true };

    /// <summary>A stored <c>default</c>'s refusal names the account <c>order</c> would put first, spelled for any shell (POOL1b, §5.3).</summary>
    [Theory]
    [InlineData("my acct", "`daoris agent profile order claude-code \"my acct\"` puts one first")]
    [InlineData("R&D", "`daoris agent profile order claude-code <account>` puts one first")]
    public void A_default_s_refusal_spells_the_account_order_would_put_first(string account, string door)
    {
        Assert.Contains(door, Plan(Agent("default", account, null)).Refusal);
    }

    [Theory]
    [InlineData("my acct", "daoris agent profile use claude-code --keep \"my acct\"")]
    [InlineData("R&D", "daoris agent profile use claude-code --keep <account>")]
    public void A_use_s_terminal_spells_its_kept_account(string keep, string terminal)
    {
        var plan = Plan(Agent("use", null, null, new UseChange(Keep: keep)));

        Assert.Null(plan.Refusal);
        Assert.Equal(terminal, plan.Terminal);
    }

    [Fact]
    public void An_order_s_terminal_spells_its_accounts()
    {
        var plan = Plan(Agent("order", null, null) with { AccountOrder = new AccountOrder(["my acct", "R&D"], false) });

        Assert.Null(plan.Refusal);
        Assert.Equal("daoris agent profile order claude-code \"my acct\" <account>", plan.Terminal);
    }

    [Theory]
    [InlineData("my acct", "daoris agent settings claude-code --account \"my acct\" model opus")]
    [InlineData("R&D", "daoris agent settings claude-code --account <account> model opus")]
    public void An_account_s_settings_terminal_spells_its_account(string account, string terminal)
    {
        var plan = Plan(Of("account", "settings", "claude-code") with { Account = account, Model = "opus" });

        Assert.Null(plan.Refusal);
        Assert.Equal(terminal, plan.Terminal);
    }

    [Theory]
    [InlineData("line", "my team", "main", "daoris driver line --workspace \"my team\" main")]
    [InlineData("landing", "my team", "merge", "daoris driver landing --workspace \"my team\" merge")]
    [InlineData("language", "R&D", "zh", "daoris driver language --workspace <workspace> zh")]
    [InlineData("across", "my team", "read off", "daoris driver across --workspace \"my team\" read off")]
    public void A_workspace_setting_s_terminal_spells_its_workspace(string door, string workspace, string value, string terminal)
    {
        var plan = Plan(Setting(door, workspace: workspace, value: value));

        Assert.Null(plan.Refusal);
        Assert.Equal(terminal, plan.Terminal);
    }

    /// <summary>WORKFLOW1h-b: a workflow card's choice for a workspace spells the workspace for any shell; the workflow's id is an id.</summary>
    [Theory]
    [InlineData("my team", "daoris driver workflow import <file> && daoris driver workflow use docs-to-pr --workspace \"my team\"")]
    [InlineData("R&D", "daoris driver workflow import <file> && daoris driver workflow use docs-to-pr --workspace <workspace>")]
    public void A_workflow_s_choice_spells_its_workspace(string workspace, string terminal)
    {
        var steps = System.Text.Json.JsonDocument.Parse("""[{"id":"work","kind":"work"},{"id":"land","kind":"landing","form":"merge","accept":"you"}]""");
        var plan = Plan(new HelpProposal("p8", "workflow", "new", null, null, null, null, "why", "h1", "proposed")
        {
            Workflow = "docs-to-pr", Scope = workspace, Choose = true, Steps = steps.RootElement.Clone(),
        });

        Assert.Null(plan.Refusal);
        Assert.Equal(terminal, plan.Terminal);
    }

    [Theory]
    [InlineData("my team", "daoris-driver ask --workspace \"my team\" \"fix the stall\"")]
    [InlineData("R&D", "daoris-driver ask --workspace <workspace> \"fix the stall\"")]
    public void An_ask_s_terminal_spells_its_workspace(string workspace, string terminal)
    {
        var plan = Plan(new HelpProposal("p2", "ask", "ask", null, workspace, null, "fix the stall", "why", "h1", "proposed"));

        Assert.Null(plan.Refusal);
        Assert.Equal(terminal, plan.Terminal);
    }
}
