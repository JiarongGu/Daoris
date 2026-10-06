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
    /// A machine whose accounts and workspaces need spelling: the machine's list <c>work</c>, <c>my team</c>'s
    /// <c>work, my acct</c>, <c>R&amp;D</c>'s <c>work, R&amp;D</c>, and <c>our lab</c> with no list of its own.
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
        Wiring = new HarnessSettings
        {
            Rotation = Lists("claude-code", "work"),
            WorkspaceRotation = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.OrdinalIgnoreCase)
            {
                ["my team"] = Lists("claude-code", "work", "my acct"),
                ["R&D"] = Lists("claude-code", "work", "R&D"),
            },
        },
    };

    private static HelpPlan Plan(HelpProposal proposal) => HelpProposals.Plan(proposal, DriverConfig.Empty, Spelled);

    private static HelpProposal Agent(string door, string? account, string? workspace, UseChange? use = null) =>
        new("p7", "agent", door, "claude-code", workspace, account, null, "the person asked", "h1", "proposed") { AccountUse = use };

    [Theory]
    [InlineData("my acct", "my team", "daoris agent profile default claude-code \"my acct\" --workspace \"my team\"")]
    [InlineData("R&D", "R&D", "daoris agent profile default claude-code <account> --workspace <workspace>")]
    public void A_default_s_terminal_spells_its_account_and_workspace(string account, string workspace, string terminal)
    {
        var plan = Plan(Agent("default", account, workspace));

        Assert.Null(plan.Refusal);
        Assert.Equal(terminal, plan.Terminal);
    }

    [Theory]
    [InlineData("my acct", null, "`daoris agent profile order claude-code work \"my acct\"` adds it")]
    [InlineData("my acct", "R&D", "`daoris agent profile order claude-code work <account> \"my acct\" --workspace <workspace>` adds it")]
    public void A_default_outside_its_list_names_the_order_that_adds_it_spelled(string account, string? workspace, string door)
    {
        Assert.Contains(door, Plan(Agent("default", account, workspace)).Refusal);
    }

    [Theory]
    [InlineData("my acct", "my team", "daoris agent profile use claude-code --keep \"my acct\" --workspace \"my team\"")]
    [InlineData("R&D", "R&D", "daoris agent profile use claude-code --keep <account> --workspace <workspace>")]
    public void A_use_s_terminal_spells_its_kept_account_and_workspace(string keep, string workspace, string terminal)
    {
        var plan = Plan(Agent("use", null, workspace, new UseChange(Keep: keep)));

        Assert.Null(plan.Refusal);
        Assert.Equal(terminal, plan.Terminal);
    }

    [Fact]
    public void A_use_over_no_list_names_the_order_that_makes_one_spelled()
    {
        Assert.Contains(
            "`daoris agent profile order claude-code <account>… --workspace \"our lab\"` gives it one",
            Plan(Agent("use", null, "our lab", new UseChange(Use: "goal"))).Refusal);
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
