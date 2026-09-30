using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>Ask Daoris's <c>account</c> proposal (HELP6): an account's own model and effort, by <c>SET_AGENT_SETTINGS</c>'s rules.</summary>
public sealed class HelpAccountProposalsTests : HelpProposalsFixture
{
    private static HelpProposal Account(string agent, string? account, string? model, string? effort) =>
        Of("account", "settings", agent) with { Account = account, Model = model, Effort = effort };

    [Theory]
    [InlineData("claude-code", "work", "opus", "high", "daoris agent settings claude-code --account work model opus effort high", "model to `opus` and its effort to `high`")]
    [InlineData("claude-code-acp", "work", null, "xhigh", "daoris agent settings claude-code --account work effort xhigh", "`claude-code` account `work`")]
    [InlineData("claude-code", "work", "claude-opus-5", null, "daoris agent settings claude-code --account work model claude-opus-5", "model to `claude-opus-5`")]
    [InlineData("claude-code", "work", "unset", "unset", "daoris agent settings claude-code --account work model unset effort unset", "the tool's own default")]
    public void An_accounts_model_and_effort_are_planned_in_the_tools_own_words(
        string agent, string account, string? model, string? effort, string terminal, string says)
    {
        var plan = HelpProposals.Plan(Account(agent, account, model, effort), DriverConfig.Empty, Machine);

        Assert.Null(plan.Refusal);
        Assert.Equal(terminal, plan.Terminal);
        Assert.Contains(says, plan.Describe);
    }

    [Theory]
    [InlineData("claude-code", "work", null, "max", "one session")]
    [InlineData("claude-code", "work", null, "extreme", "not an effort the tool's settings keep")]
    [InlineData("claude-code", "work", "two words", null, "not a model name")]
    [InlineData("claude-code", "play", "opus", null, "no account `play`")]
    [InlineData("dsh", "work", "deepseek-chat", null, "Daoris offers none")]
    public void An_account_setting_the_route_would_refuse_is_refused(string agent, string account, string? model, string? effort, string says)
    {
        var plan = HelpProposals.Plan(Account(agent, account, model, effort), DriverConfig.Empty, Machine);

        Assert.Contains(says, plan.Refusal);
    }

    [Fact]
    public async Task An_accounts_settings_are_written_through_the_agents_screens_own_route()
    {
        var (set, doors, _) = await ApplyAsync(Account("claude-code-acp", "work", "opus", "unset"));

        Assert.True(set.Applied);
        Assert.Equal(["SET_AGENT_SETTINGS claude-code-acp work opus clear"], doors.Calls);
    }

    /// <summary>An account's fields, read from the file the service's box writes — the twin's shape.</summary>
    [Fact]
    public void An_accounts_fields_are_read_from_the_file()
    {
        var node = new JsonObject
        {
            ["id"] = "k1", ["proposed"] = "2026-09-30T10:00:00.0000000+00:00", ["by"] = new JsonObject { ["session"] = "h1" },
            ["kind"] = "account", ["door"] = "settings", ["target"] = "claude-code", ["workspace"] = null, ["value"] = null,
            ["sentence"] = null, ["account"] = "work", ["model"] = "opus", ["effort"] = "high",
            ["why"] = "the person asked", ["state"] = "proposed", ["note"] = null,
        };
        System.IO.File.WriteAllText(Path.Combine(HelpProposals.FolderOf(_home), "k1.json"), node.ToJsonString());

        var account = HelpProposals.Find(_home, "k1")!;

        Assert.Equal(("work", "opus", "high"), (account.Account, account.Model, account.Effort));
    }
}

public sealed partial class HelpStandInDoors
{
    public AgentSettingsRead SetAgentSettings(string harness, string account, AgentSettingEdit? model, AgentSettingEdit? effort)
    {
        string Said(AgentSettingEdit? edit) => edit is null ? "-" : edit.Value ?? "clear";
        Calls.Add($"SET_AGENT_SETTINGS {harness} {account} {Said(model)} {Said(effort)}");
        return new AgentSettingsRead(model?.Value, effort?.Value, [], null);
    }
}
