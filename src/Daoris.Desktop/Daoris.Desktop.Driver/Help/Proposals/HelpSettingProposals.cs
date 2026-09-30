using System.Globalization;
using static Daoris.Driver.HelpProposals;

namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's <c>setting</c> proposal (HELP1c, D89): one of the <c>daoris driver</c> verbs the room's doors
/// table names — its door the verb, its target the repository, its workspace one for a whole workspace, its
/// value what it is set to — judged by the config's own edits, which throw the route's refusal.
/// </summary>
/// <remarks>
/// The names the routes do not check, and a helper can invent, are checked here first: a registered
/// repository, a workspace, an agent. Applied as the <c>SET_*</c> routes make the same edit.
/// </remarks>
internal sealed class HelpSettingProposals : IHelpProposalKind
{
    public string Kind => "setting";

    // It proposes, and the person applies (HELP1c, D89).
    public string Tool => "setting_propose";

    public HelpPlan Plan(HelpProposal proposal, DriverConfig config, HelpMachineFacts facts)
    {
        var target = proposal.Target?.Trim();
        var workspace = proposal.Workspace?.Trim();
        var value = proposal.Value?.Trim() ?? "";
        HelpPlan Refused(string why, string describe, string terminal) => new(why, describe, terminal, null);

        // The names the route itself does not check, and a helper can invent: a repository, a circle, an agent.
        if (target is { Length: > 0 } && !facts.Repositories.Contains(target, StringComparer.OrdinalIgnoreCase))
        {
            return Refused($"`{target}` is not registered on this machine — use a repository's name as Projects lists it.", "", "");
        }

        if (workspace is { Length: > 0 } && !facts.Workspaces.Contains(workspace, StringComparer.OrdinalIgnoreCase))
        {
            return Refused($"there is no workspace `{workspace}` on this machine — one of {Names(facts.Workspaces)}.", "", "");
        }

        var scope = workspace is { Length: > 0 } ? $"--workspace {workspace}" : target ?? "";
        var whose = workspace is { Length: > 0 } ? $"workspace `{workspace}`" : $"`{target}`";
        (string Describe, string Terminal, Func<DriverConfig, DriverConfig> Edit) planned;
        switch (proposal.Door)
        {
            case "drive":
                planned = ($"Drive `{target}`: a quest addressed to it starts a session on this machine.", $"daoris driver drive {target}", c => c.WithDrivable(target!, true));
                break;
            case "undrive":
                planned = ($"Stop driving `{target}`.", $"daoris driver undrive {target}", c => c.WithDrivable(target!, false));
                break;
            case "hold":
                planned = ($"Hold `{target}`: nothing new starts there until it is resumed.", $"daoris driver hold {target}", c => c.WithHold(target!, true));
                break;
            case "resume":
                planned = ($"Resume `{target}`.", $"daoris driver resume {target}", c => c.WithHold(target!, false));
                break;
            case "trees":
                planned = value == "on"
                    ? ($"Sessions in `{target}` open their own tree.", $"daoris driver trees {target} on", c => c.WithTrees(target!, true))
                    : ($"Sessions in `{target}` work in its checkout.", $"daoris driver trees {target} off", c => c.WithTrees(target!, false));
                break;
            case "line":
            {
                var branch = value == "--clear" ? null : value;
                planned = (branch is null ? $"Clear {whose}'s line." : $"Set {whose}'s line to `{branch}`.",
                    $"daoris driver line {scope} {value}",
                    c => workspace is { Length: > 0 } ? c.WithWorkspaceLine(workspace, branch) : c.WithLine(target!, branch));
                break;
            }
            case "landing":
            {
                // As `daoris driver landing` reads its words (HELP8): the form, a branch's pattern, and
                // `--tidy` and `--plugin <id>` in either order. A pattern holds no space, since git takes none.
                var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
                var tidy = words.Remove("--tidy");
                string? plugin = null;
                if (words.IndexOf("--plugin") is var at and >= 0)
                {
                    if (at + 1 >= words.Count || words[at + 1].StartsWith("--", StringComparison.Ordinal))
                    {
                        return Refused("`--plugin` needs the id of an installed plugin — `--plugin <id>`; "
                            + "`daoris plugin list` shows what there is.", "", "");
                    }

                    plugin = words[at + 1];
                    words.RemoveRange(at, 2);
                }

                var bare = string.Join(' ', words);
                LandingRule? rule = bare switch
                {
                    "--clear" => null,
                    "merge" => new LandingRule("merge", null, tidy, plugin),
                    _ when bare.StartsWith("branch ", StringComparison.Ordinal) => new LandingRule("branch", bare["branch ".Length..].Trim(), tidy, plugin),
                    _ => new LandingRule(bare, null, tidy, plugin),
                };

                // The plugin must land work on THIS machine, asked as the screen's route asks it (D100),
                // once the rule's own shape is sound, so a merge naming one is told why in that shape's words.
                if (rule is { Plugin: { } named } && LandingRules.Problem(rule) is null
                    && LandingRules.PluginProblem(named, facts.Plugins) is { } unready)
                {
                    return Refused(unready, "", "");
                }

                var after = (plugin is null ? "" : $", plugin `{plugin}` pushes it and opens the pull request")
                    + (tidy ? ", its tree removed once landed" : "");
                var lands = rule is null ? $"Clear {whose}'s landing rule."
                    : rule.Form == "branch" ? $"Land {whose}'s accepted work on a branch `{rule.Pattern}`{after}."
                    : $"Land {whose}'s accepted work merged into its line{after}.";
                planned = (lands, $"daoris driver landing {scope} {value}",
                    c => workspace is { Length: > 0 } ? c.WithWorkspaceLanding(workspace, rule) : c.WithLanding(target!, rule));
                break;
            }
            case "intake" or "helper":
            {
                var agent = value == "off" ? null : value;
                if (agent is not null && !facts.Agents.Contains(agent, StringComparer.Ordinal))
                {
                    return Refused($"there is no agent `{agent}` on this machine — one of {Names(facts.Agents)}.", "", "");
                }

                var intake = proposal.Door == "intake";
                planned = (intake
                        ? agent is null ? "Stop answering asks with an agent: the declarations alone propose." : $"Answer asks with `{agent}`."
                        : agent is null ? "Turn Ask Daoris's agent off: it offers its starters only." : $"Run Ask Daoris on `{agent}`.",
                    $"daoris driver {proposal.Door} {value}",
                    c => intake ? c.WithIntake(agent) : c.WithHelper(agent));
                break;
            }
            case "strikes" when int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var strikes):
                planned = (strikes == 0 ? "Never park a quest for its failed sessions." : $"Park a quest after {strikes} failed sessions.",
                    $"daoris driver strikes {strikes}", c => c.WithStrikes(strikes));
                break;
            case "timeout" when int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) && minutes >= 1:
                planned = ($"Let one session run up to {minutes} minutes.", $"daoris driver timeout {minutes}", c => c with { TimeoutMinutes = minutes });
                break;
            case "notify" when value is "on" or "off":
                planned = (value == "on" ? "Say so when a session parks, or ends without being asked." : "Stop saying so when a session parks.",
                    $"daoris driver notify {value}", c => c.WithNotify(value == "on"));
                break;
            default:
                return Refused($"`{proposal.Door} {value}` is not a change the driver makes.", "", "");
        }

        // The route's own edits judge the rest — a branch git would not take, a pattern that is not one
        // branch per session — in their own words.
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

    public Task<HelpApplied> ApplyAsync(HelpApplying applying, CancellationToken ct)
    {
        applying.Doors.Change(applying.Plan.Apply!);
        return Task.FromResult(applying.Settled(true, $"Applied: `#{applying.Id}` — {applying.Plan.Describe} (`{applying.Plan.Terminal}`)", null));
    }
}

public partial interface IHelpDoors
{
    /// <summary>An edit to the driver's file, as the `SET_*` routes make it.</summary>
    void Change(Func<DriverConfig, DriverConfig> edit);
}
