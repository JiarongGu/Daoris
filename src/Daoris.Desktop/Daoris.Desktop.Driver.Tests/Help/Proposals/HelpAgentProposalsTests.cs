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
}
