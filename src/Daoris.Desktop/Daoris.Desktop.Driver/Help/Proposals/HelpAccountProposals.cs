using System.Text.Json;
using static Daoris.Driver.HelpProposals;

namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's <c>account</c> proposal (HELP6): an account's own model and effort — its door <c>settings</c>,
/// its target the agent, and its own <c>account</c>, <c>model</c> and <c>effort</c> — judged by
/// <c>SET_AGENT_SETTINGS</c>'s rules: a tool whose settings Daoris knows (D98), one of Daoris's accounts for
/// it, and values the tool reads — its own aliases or a full model id, and an effort its settings keep, never
/// `max`. Applied as <c>SET_AGENT_SETTINGS</c>'s own write.
/// </summary>
internal sealed class HelpAccountProposals : IHelpProposalKind
{
    public string Kind => "account";

    public string Tool => "agent_settings_propose";

    public IReadOnlyList<string> Doors { get; } = ["settings"];

    public HelpProposal Read(HelpProposal proposal, JsonElement file) => proposal with
    {
        Account = Text(file, "account"),
        Model = Text(file, "model"),
        Effort = Text(file, "effort"),
    };

    public HelpPlan Plan(HelpProposal proposal, DriverConfig config, HelpMachineFacts facts)
    {
        var name = proposal.Target?.Trim() ?? "";
        var account = proposal.Account?.Trim() ?? "";
        var model = proposal.Model?.Trim() is { Length: > 0 } m ? m : null;
        var effort = proposal.Effort?.Trim() is { Length: > 0 } e ? e : null;
        if (facts.Doors.FirstOrDefault(each => string.Equals(each.Name, name, StringComparison.Ordinal)) is not { } door)
        {
            return new HelpPlan($"there is no agent `{name}` on this machine — one of {Names(facts.Doors.Select(each => each.Name))}.", "", "", null);
        }

        var owner = door.AccountsOf;
        var terminal = $"daoris agent settings {owner} --account {account}"
            + (model is null ? "" : $" model {model}") + (effort is null ? "" : $" effort {effort}");
        HelpPlan Refused(string why) => new(why, "", terminal, null);

        if (!door.SettingsKnown)
        {
            return Refused($"`{owner}` keeps its settings in files of its own that Daoris does not know the shape of, so Daoris "
                + "offers none — set its model with the tool itself.");
        }

        if (account.Length == 0)
        {
            return Refused($"name the account these settings are for — `{owner}`'s own configuration home is the tool's, and Daoris never touches it.");
        }

        if (!door.Accounts.Contains(account, StringComparer.Ordinal))
        {
            return Refused($"`{owner}` has no account `{account}` on this machine — accounts that exist: "
                + (door.Accounts.Count > 0 ? string.Join(", ", door.Accounts) : "(none)"));
        }

        if (model is null && effort is null) return Refused("the change sets a model, an effort, or both.");
        try
        {
            if (model is not null and not Unset) AgentSettings.JudgeModel(model);
            if (effort is not null and not Unset) AgentSettings.JudgeEffort(effort);
        }
        catch (DriverException refused)
        {
            return Refused(refused.Message);
        }

        static string To(string value) => value == Unset ? "the tool's own default" : $"`{value}`";
        var changes = new List<string>();
        if (model is not null) changes.Add($"model to {To(model)}");
        if (effort is not null) changes.Add($"effort to {To(effort)}");
        return new HelpPlan(null, $"Set `{owner}` account `{account}`'s {string.Join(" and its ", changes)}.", terminal, null);
    }

    public Task<HelpApplied> ApplyAsync(HelpApplying applying, CancellationToken ct)
    {
        var (proposal, plan, id) = (applying.Proposal, applying.Plan, applying.Id);
        static AgentSettingEdit? Edit(string? value) =>
            value?.Trim() is { Length: > 0 } set ? new AgentSettingEdit(set == Unset ? null : set) : null;
        try
        {
            applying.Doors.SetAgentSettings(proposal.Target!.Trim(), proposal.Account!.Trim(), Edit(proposal.Model), Edit(proposal.Effort));
        }
        catch (DriverException error)
        {
            return Task.FromResult(applying.Settled(false, $"Not applied: `#{id}` (`{plan.Terminal}`) — {error.Message}", error.Message));
        }

        return Task.FromResult(applying.Settled(true, $"Applied: `#{id}` — {plan.Describe} (`{plan.Terminal}`)", null));
    }

    /// <summary>The word that returns a key to the tool's own default, as `daoris agent settings` takes it.</summary>
    private const string Unset = "unset";
}

public sealed partial record HelpProposal
{
    /// <summary>An account's name, for an account's settings (HELP6).</summary>
    public string? Account { get; init; }

    /// <summary>The model an account's settings would set — `unset` for the tool's own default — or null to leave it.</summary>
    public string? Model { get; init; }

    /// <summary>The effort an account's settings would set — `unset` for the tool's own default — or null to leave it.</summary>
    public string? Effort { get; init; }
}

public partial interface IHelpDoors
{
    /// <summary><c>SET_AGENT_SETTINGS</c>'s own write to an account's settings file.</summary>
    AgentSettingsRead SetAgentSettings(string harness, string account, AgentSettingEdit? model, AgentSettingEdit? effort);
}
