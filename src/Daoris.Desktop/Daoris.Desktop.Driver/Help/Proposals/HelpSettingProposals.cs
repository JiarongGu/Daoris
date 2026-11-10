using System.Globalization;
using static Daoris.Driver.HelpProposals;

namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's <c>setting</c> proposal: one of the <c>daoris place</c>, <c>settings</c>, <c>quest</c> and <c>git</c> verbs the room's doors
/// table names — its door the verb, its target the repository, its workspace one for a whole workspace, its
/// value what it is set to — judged by the config's own edits, which throw the route's refusal.
/// </summary>
/// <remarks>
/// <para>The names the routes do not check, and a helper can invent, are checked here first: a registered
/// repository, a workspace, an agent. Applied as the <c>SET_*</c> routes make the same edit.</para>
///
/// <para>Every verb but <c>list</c>, which changes nothing: reading and writing across, as
/// <c>SET_READ_ACROSS</c> and <c>SET_WRITE_ACROSS</c> make them, and <c>cap</c> and <c>adapter</c>, which a
/// terminal sets as well. <c>retry</c> too, its target a quest the loop's last tick parked
/// (<see cref="HelpMachineFacts.Parked"/>), applied as <c>RETRY_QUEST</c> makes the same edit.</para>
///
/// <para>The doors with a plan of their own are their own types: <see cref="HelpAcrossSetting"/>, <see cref="HelpRetrySetting"/>,
/// <see cref="HelpGitUseSetting"/> and the rule doors in <see cref="HelpRuleSettings"/>; the plan here keeps the simple doors.</para>
/// </remarks>
internal sealed class HelpSettingProposals : IHelpProposalKind
{
    public string Kind => "setting";

    // It proposes, and the person applies.
    public string Tool => "setting_propose";

    /// <summary>The `daoris place`, `settings`, `quest` and `git` verbs it takes, in the order the service's twin lists them (<c>HelpSettingProposalWriter.Doors</c>).</summary>
    public IReadOnlyList<string> Doors { get; } =
    [
        "drive", "undrive", "hold", "resume", "trees", "line", "across", "standing", "language", "review", "opinion",
        "helpers", "commits", "retry", "notify", "cap", "cooloff", "quiet", "keep", "git-use",
    ];

    /// <summary>
    /// Each door's target, which its Apply marks the owner's: the quest for <c>retry</c>; for every other
    /// door its repository, or its workspace where the plan is the workspace's (a line, a landing, a language). A machine's door
    /// (each of <see cref="MachineSettings.All"/>) names neither, and marks nothing.
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

        // A retry's target is a quest, not a repository, so it is judged before the registry is asked.
        if (proposal.Door == "retry") return HelpRetrySetting.Plan(target, config, facts);

        // A git identity chosen for a remote or a host is the identities file's own write, not an edit of the driver's file.
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

        // A workspace in a command is spelled for any shell; a repository's name is not.
        var scope = workspace is { Length: > 0 } ? $"--workspace {ShellWord.Of(workspace, ShellWord.Workspace)}" : target ?? "";
        var whose = workspace is { Length: > 0 } ? $"workspace `{workspace}`" : $"`{target}`";
        (string Describe, string Terminal, Func<DriverConfig, DriverConfig> Edit) planned;
        switch (proposal.Door)
        {
            case "drive":
                planned = ($"Drive `{target}`: a quest for it starts a session on this machine.", $"daoris place drive {target}", c => c.WithDrivable(target!, true));
                break;
            case "undrive":
                planned = ($"Stop driving `{target}`.", $"daoris place undrive {target}", c => c.WithDrivable(target!, false));
                break;
            case "hold":
                planned = ($"Hold `{target}`: nothing new starts there until it is resumed.", $"daoris place hold {target}", c => c.WithHold(target!, true));
                break;
            case "resume":
                planned = ($"Resume `{target}`.", $"daoris place resume {target}", c => c.WithHold(target!, false));
                break;
            case "trees":
                planned = value == "on"
                    ? ($"Sessions in `{target}` open their own tree.", $"daoris place trees {target} on", c => c.WithTrees(target!, true))
                    : ($"Sessions in `{target}` work in its checkout.", $"daoris place trees {target} off", c => c.WithTrees(target!, false));
                break;
            case "line":
            {
                var branch = value == "--clear" ? null : value;
                planned = (branch is null ? $"Clear {whose}'s line." : $"Set {whose}'s line to `{branch}`.",
                    $"daoris place line {scope} {value}",
                    c => workspace is { Length: > 0 } ? c.WithWorkspaceLine(workspace, branch) : c.WithLine(target!, branch));
                break;
            }
            case "across":
            {
                var (refused, across) = HelpAcrossSetting.Plan(target, workspace, value, facts);
                if (refused is not null) return Refused(refused, "", "");
                planned = across;
                break;
            }
            case "standing":
            {
                // The person's words for one repository, kept on this machine as `SET_STANDING` and
                // `daoris place standing` keep them; `--clear` takes the answer back.
                var says = value == "--clear" ? null : value;
                var at = DateTimeOffset.UtcNow;
                planned = (says is null
                        ? $"Clear `{target}`'s standing answer: its sessions are handed none."
                        : $"Keep a standing answer for `{target}`, handed to every session in `{target}` beneath its quest: \"{says}\"",
                    $"daoris place standing {target} {(says is null ? "--clear" : $"\"{says.Replace("\"", "\\\"", StringComparison.Ordinal)}\"")}",
                    c => c.WithStanding(target!, says, at));
                break;
            }
            case "language":
            {
                // The work's session language, for a repository or a workspace, judged by the closed
                // table as `SET_LANGUAGE` and `daoris place language` judge it. Its service writer
                // checks only the shape; the registry and the workspace are judged here.
                var circle = workspace is { Length: > 0 };
                if (circle == target is { Length: > 0 })
                {
                    return Refused("a session language is set for a repository or a workspace — name exactly one.", "", "");
                }

                var code = value == "--clear" ? null : SessionLanguages.Code(value);
                if (value != "--clear" && code is null)
                {
                    return Refused(value.Length == 0
                        ? "`language` names a language, `en` or `zh`, or `--clear` — e.g. `daoris place language engine zh`."
                        : SessionLanguages.Refusal(value), "", "");
                }

                var language = code is null ? null : SessionLanguages.NameOf(code);
                planned = (language is null
                        ? circle
                            ? $"Clear workspace `{workspace}`'s session language: each repository there keeps its own, else none."
                            : $"Clear `{target}`'s session language: it takes its workspace's again, else none."
                        : (circle
                            ? $"Sessions in each repository of workspace `{workspace}` that sets none of its own write to you in {language}"
                            : $"Sessions in `{target}` write to you in {language}")
                          + ": a question to you, a closing note, a decline's reason, their last words. The window's own language "
                          + "stays yours, in Settings → Appearance.",
                    $"daoris place language {scope} {code ?? "--clear"}",
                    c => circle ? c.WithWorkspaceLanguage(workspace!, code) : c.WithLanguage(target!, code));
                break;
            }
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
            case "notify" when value is "on" or "off":
                planned = (value == "on" ? "Say so when a session parks, or ends without being asked." : "Stop saying so when a session parks.",
                    $"daoris settings notify {value}", c => c.WithNotify(value == "on"));
                break;
            // The dial only a terminal set before, judged as `daoris settings cap` takes it.
            case "cap" when int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var cap) && cap >= 1:
                planned = (cap == 1 ? "Run one session at a time on this machine, across every repository."
                        : $"Run at most {cap} sessions at once on this machine, across every repository.",
                    $"daoris settings cap {cap}", c => c.WithCap(cap));
                break;
            // How long an account cools when its limit names no time, as `daoris settings cooloff` takes it;
            // a zero would start a spent account again at every look, so less than a minute falls to the refusal below.
            case "cooloff" when int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var cool) && cool >= 1:
                planned = ($"An account whose limit names no time cools for {cool} minute{(cool == 1 ? "" : "s")} before it is offered again.",
                    $"daoris settings cooloff {cool}", c => c.WithCoolOff(cool));
                break;
            // How long a session may be quiet before Daoris looks in, judged as `daoris settings quiet` takes it.
            case "quiet" when long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var quiet) && QuietSetting.Takes(quiet):
                planned = ($"Look in on a session that says nothing and runs nothing for {quiet} minutes; none is ever stopped for running long.",
                    $"daoris settings quiet {quiet}", c => c.WithQuiet((int)quiet));
                break;
            // One kind's keep rule, its words `<kind> <rule>` as `daoris settings keep`
            // takes them, judged by the one judgement the terminal and SET_KEEP make, so a value out of its bounds is refused here.
            case "keep":
            {
                var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (words.Length != 2) return Refused("`keep` takes a kind and its rule: `set-aside <days>|whole|slim|default`, `downloads <days>|never|default`, `logs <days>|default`, `history <days>|never|default` or `floor <n>GB|<n>%|off|default`.", "", "");
                KeepSet set;
                DriverConfig after;
                try
                {
                    set = KeepRules.Asked(words[0], words[1]);
                    after = KeepRules.Apply(config, set);
                }
                catch (DriverException refused)
                {
                    return Refused(refused.Message, "", "");
                }

                var said = KeepRules.Said(set.Kind, after.Keep);
                planned = ($"{char.ToUpperInvariant(said[0])}{said[1..]}.", $"daoris settings keep {set.Kind} {words[1]}", c => KeepRules.Apply(c, set));
                break;
            }
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
