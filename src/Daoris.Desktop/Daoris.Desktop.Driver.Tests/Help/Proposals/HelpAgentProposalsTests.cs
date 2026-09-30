using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>Ask Daoris's <c>agent</c> proposal (HELP6): an Update or a pin, where the Agents screen offers it.</summary>
public sealed class HelpAgentProposalsTests : HelpProposalsFixture
{
    [Theory]
    [InlineData("update", "claude-code", null, "daoris agent update claude-code", "its own updater")]
    [InlineData("update", "claude-code-acp", null, "daoris agent update claude-code-acp", "from 0.84.0 to the newest release")]
    [InlineData("pin", "claude-code", "2.1.300", "daoris agent pin claude-code 2.1.300", "Pin `claude-code` to 2.1.300")]
    [InlineData("pin", "claude-code-acp", "0.85.1", "daoris agent pin claude-code-acp 0.85.1", "to 0.85.1")]
    public void An_agent_is_updated_or_pinned_where_the_agents_screen_offers_it(
        string action, string agent, string? version, string terminal, string says)
    {
        var plan = HelpProposals.Plan(Of("agent", action, agent, version), DriverConfig.Empty, Machine);

        Assert.Null(plan.Refusal);
        Assert.Equal(terminal, plan.Terminal);
        Assert.Contains(says, plan.Describe);
    }

    /// <summary>What the Agents screen would not offer, or its route would refuse, is refused in the route's words.</summary>
    [Theory]
    [InlineData("update", "dsh", null, "offers no Update")]
    [InlineData("update", "codex-acp", null, "not installed")]
    [InlineData("update", "gpt-agent", null, "no agent `gpt-agent`")]
    [InlineData("pin", "dsh", "1.0.0", "no package or release channel")]
    [InlineData("pin", "claude-code", "latest", "never a pointer such as latest")]
    [InlineData("pin", "claude-code", "2.0.1", "before its release manifests were signed")]
    [InlineData("pin", "claude-code-acp", "latest", "one exact release")]
    public void An_agent_action_the_route_would_refuse_is_refused(string action, string agent, string? version, string says)
    {
        var plan = HelpProposals.Plan(Of("agent", action, agent, version), DriverConfig.Empty, Machine);

        Assert.Contains(says, plan.Refusal);
        Assert.Null(plan.Apply);
    }

    [Fact]
    public async Task An_agent_update_or_pin_starts_the_agents_screens_own_action_and_its_end_is_said()
    {
        var (update, doors, later) = await ApplyAsync(Of("agent", "update", "claude-code-acp"));
        var (_, pinned, _) = await ApplyAsync(Of("agent", "pin", "claude-code", "2.1.300") with { Id = "p8" });

        Assert.True(update.Applied);
        Assert.Equal(["HARNESS_ACTION claude-code-acp update"], doors.Calls);
        Assert.Equal(["HARNESS_ACTION claude-code pin 2.1.300"], pinned.Calls);
        Assert.Contains("Started", update.Told);
        Assert.Equal("applied", HelpProposals.Find(_home, "p6")!.State);
        // What the Apply did goes into the conversation first…
        Assert.Equal([update.Told], later);

        // …and the action's end after it, however it ended.
        doors.Ended!(0, null);
        doors.Ended!(1, "the pointer did not answer");
        Assert.Contains("finished", later[1]);
        Assert.Contains("the pointer did not answer", later[2]);
    }

    private static HelpProposal Default(string agent, string? account, string? workspace = null) =>
        new("p7", "agent", "default", agent, workspace, account, null, "the person asked", "h1", "proposed");

    /// <summary>
    /// HELP10: an account made the default, for the machine or a workspace, as the Agents screen's *Make default* and
    /// *use for a workspace* make it — a door's default is its owner's (AGT7), so the command names the owner.
    /// </summary>
    [Theory]
    [InlineData("claude-code", "work", null, "daoris agent profile default claude-code work", "This machine runs `claude-code` as `work` by default.")]
    [InlineData("claude-code-acp", "work", "work", "daoris agent profile default claude-code work --workspace work",
        "Sessions in `work` run `claude-code` as `work`.")]
    public void An_account_is_made_the_default_for_the_machine_or_a_workspace(
        string agent, string account, string? workspace, string terminal, string says)
    {
        var plan = HelpProposals.Plan(Default(agent, account, workspace), DriverConfig.Empty, Machine);

        Assert.Null(plan.Refusal);
        Assert.Equal(terminal, plan.Terminal);
        Assert.Contains(says, plan.Describe);
    }

    /// <summary>HELP10: an account or a workspace a helper can invent is refused, as `daoris agent profile default` refuses it.</summary>
    [Theory]
    [InlineData("claude-code", "play", null, "`claude-code` has no account `play` on this machine — accounts that exist: work")]
    [InlineData("dsh", "work", null, "`dsh` has no account `work` on this machine — accounts that exist: (none)")]
    [InlineData("claude-code", null, null, "names the account")]
    [InlineData("claude-code", "work", "elsewhere", "there is no workspace `elsewhere` on this machine")]
    [InlineData("gpt-agent", "work", null, "no agent `gpt-agent`")]
    public void A_default_the_route_would_refuse_is_refused(string agent, string? account, string? workspace, string says)
    {
        var plan = HelpProposals.Plan(Default(agent, account, workspace), DriverConfig.Empty, Machine);

        Assert.Contains(says, plan.Refusal);
    }

    [Fact]
    public async Task A_default_is_made_through_the_agents_screens_own_action()
    {
        var (applied, doors, later) = await ApplyAsync(Default("claude-code-acp", "work", "work"));

        Assert.True(applied.Applied);
        Assert.Equal(["HARNESS_ACTION claude-code-acp profile-default work work"], doors.Calls);
        Assert.Contains("Applied", applied.Told);
        Assert.Equal([applied.Told], later);
        Assert.Equal("applied", HelpProposals.Find(_home, "p7")!.State);
    }

    [Fact]
    public async Task A_default_the_door_refuses_is_settled_refused_in_its_words()
    {
        var (applied, _, _) = await ApplyAsync(Default("claude-code", "work"), new HelpStandInDoors { DefaultRefusal = "Daoris manages no toolchain for `claude-code`." });

        Assert.False(applied.Applied);
        Assert.Contains("Daoris manages no toolchain", applied.Told);
        Assert.Equal("refused", HelpProposals.Find(_home, "p7")!.State);
    }

    /// <summary>A pin already installed ends before its start is answered: its end is still said second.</summary>
    [Fact]
    public async Task An_action_that_ends_at_once_is_said_after_what_the_Apply_did()
    {
        var (applied, _, later) = await ApplyAsync(Of("agent", "pin", "claude-code", "2.1.300"), new HelpStandInDoors { EndsAtOnce = true });

        Assert.Equal(2, later.Count);
        Assert.Equal(applied.Told, later[0]);
        Assert.Contains("finished", later[1]);
    }
}

public sealed partial class HelpStandInDoors
{
    public Action<int, string?>? Ended { get; private set; }

    /// <summary>An action that ends before its start is answered: a pin already installed does.</summary>
    public bool EndsAtOnce { get; init; }

    public Task StartAgentActionAsync(string harness, string action, string? version, Action<int, string?> ended, CancellationToken ct)
    {
        Calls.Add($"HARNESS_ACTION {harness} {action} {version}".TrimEnd());
        Ended = ended;
        if (EndsAtOnce) ended(0, null);
        return Task.CompletedTask;
    }

    /// <summary>What the default's door refuses with, as `HARNESS_ACTION` would; null to make it.</summary>
    public string? DefaultRefusal { get; init; }

    public Task SetDefaultAccountAsync(string harness, string account, string? workspace, CancellationToken ct)
    {
        if (DefaultRefusal is { } refused) throw new DriverException(refused);
        Calls.Add($"HARNESS_ACTION {harness} profile-default {account} {workspace}".TrimEnd());
        return Task.CompletedTask;
    }
}
