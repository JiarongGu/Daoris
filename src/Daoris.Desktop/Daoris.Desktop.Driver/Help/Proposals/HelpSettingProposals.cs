using static Daoris.Driver.HelpProposals;

namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's <c>setting</c> proposal, the doors not yet pressed: a review, a second opinion, a helpers rule, a commit rule, a retry
/// and a git identity — its door the verb, its target the repository, its workspace one for a whole workspace, its
/// value what it is set to — judged by the config's own edits, which throw the route's refusal.
/// </summary>
/// <remarks>
/// <para>The names the routes do not check, and a helper can invent, are checked here first: a registered
/// repository, a workspace, an agent. Applied as the <c>SET_*</c> routes make the same edit. <c>retry</c>'s target is a quest the loop's
/// last tick parked (<see cref="HelpMachineFacts.Parked"/>), applied as <c>RETRY_QUEST</c> makes the same edit.</para>
/// </remarks>
internal sealed class HelpSettingProposals : IHelpProposalKind
{
    public string Kind => "setting";

    public string Tool => "setting_propose";

    /// <summary>The `daoris place`, `settings`, `quest` and `git` verbs it takes, in the order the service's twin lists them (<c>HelpSettingProposalWriter.Doors</c>).</summary>
    public IReadOnlyList<string> Doors { get; } =
    [
        "review", "opinion", "helpers", "commits", "retry", "git-use",
    ];

    /// <summary>
    /// Each door's target, which its Apply marks the owner's: the quest for <c>retry</c>; for every other
    /// door its repository, or its workspace where the plan is the workspace's (a review, a helpers rule, a commit rule).
    /// </summary>
    public HelpOwned Owned(HelpProposal proposal)
    {
        var target = proposal.Target?.Trim();
        if (proposal.Door == "retry")
        {
            return target?.TrimStart('#').Trim() is { Length: > 0 } quest ? new HelpOwned([], [], [quest]) : HelpOwned.Nothing;
        }

        var workspace = proposal.Workspace?.Trim();
        return new HelpOwned(
            target is { Length: > 0 } ? [target] : [],
            workspace is { Length: > 0 } ? [workspace] : [],
            []);
    }

    public HelpPlan Plan(HelpProposal proposal, DriverConfig config, HelpMachineFacts facts)
    {
        var target = proposal.Target?.Trim();
        var workspace = proposal.Workspace?.Trim();
        var value = proposal.Value?.Trim() ?? "";
        HelpPlan Refused(string why, string describe, string terminal) => new(why, describe, terminal, null);

        if (proposal.Door == "retry") return HelpRetrySetting.Plan(target, config, facts);

        if (proposal.Door == "git-use")
        {
            if (target is { Length: > 0 } && !facts.Repositories.Contains(target, StringComparer.OrdinalIgnoreCase))
                return Refused($"`{target}` is not registered on this machine — use a repository's name as Repositories lists it.", "", "");
            if (workspace is { Length: > 0 } && !facts.Workspaces.Contains(workspace, StringComparer.OrdinalIgnoreCase))
                return Refused($"there is no workspace `{workspace}` on this machine — one of {Names(facts.Workspaces)}.", "", "");
            return HelpGitUseSetting.Plan(target, workspace, value, facts);
        }

        // The names the route itself does not check, and a helper can invent: a repository, a circle, an agent.
        if (target is { Length: > 0 } && !facts.Repositories.Contains(target, StringComparer.OrdinalIgnoreCase))
        {
            return Refused($"`{target}` is not registered on this machine — use a repository's name as Repositories lists it.", "", "");
        }

        if (workspace is { Length: > 0 } && !facts.Workspaces.Contains(workspace, StringComparer.OrdinalIgnoreCase))
        {
            return Refused($"there is no workspace `{workspace}` on this machine — one of {Names(facts.Workspaces)}.", "", "");
        }

        (string Describe, string Terminal, Func<DriverConfig, DriverConfig> Edit) planned;
        switch (proposal.Door)
        {
            case "review":
            {
                var (refused, review) = HelpRuleSettings.Review(target, workspace, value, config, facts);
                if (refused is not null) return Refused(refused, "", "");
                planned = review;
                break;
            }
            case "opinion":
            {
                var (refused, opinion) = HelpRuleSettings.Opinion(target, workspace, value, config, facts);
                if (refused is not null) return Refused(refused, "", "");
                planned = opinion;
                break;
            }
            case "helpers":
            {
                var (refused, helpers) = HelpRuleSettings.Helpers(target, workspace, value, config);
                if (refused is not null) return Refused(refused, "", "");
                planned = helpers;
                break;
            }
            case "commits":
            {
                var (refused, commits) = HelpRuleSettings.Commits(target, workspace, value, config);
                if (refused is not null) return Refused(refused, "", "");
                planned = commits;
                break;
            }
            default:
                return Refused($"`{proposal.Door} {value}` is not a change the driver makes.", "", "");
        }

        try
        {
            planned.Edit(config);
        }
        catch (DriverException refused)
        {
            return Refused(refused.Message, planned.Describe, planned.Terminal);
        }

        return new HelpPlan(null, planned.Describe, planned.Terminal, planned.Edit);
    }

    public async Task<HelpApplied> ApplyAsync(HelpApplying applying, CancellationToken ct)
    {
        if (applying.Plan.GitUse is { } use)
        {
            try
            {
                await applying.Doors.UseGitAsync(use, ct).ConfigureAwait(false);
            }
            catch (DriverException refused)
            {
                return applying.Settled(false, $"Not applied: {refused.Message}", null);
            }
        }
        else applying.Doors.Change(applying.Plan.Apply!);
        return applying.Settled(true, $"Applied: `#{applying.Id}` — {applying.Plan.Describe} (`{applying.Plan.Terminal}`)", null);
    }
}
