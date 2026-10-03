using System.Globalization;
using static Daoris.Driver.HelpProposals;

namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's <c>setting</c> proposal (HELP1c, D89): one of the <c>daoris driver</c> verbs the room's doors
/// table names — its door the verb, its target the repository, its workspace one for a whole workspace, its
/// value what it is set to — judged by the config's own edits, which throw the route's refusal.
/// </summary>
/// <remarks>
/// <para>The names the routes do not check, and a helper can invent, are checked here first: a registered
/// repository, a workspace, an agent. Applied as the <c>SET_*</c> routes make the same edit.</para>
///
/// <para>Since HELP9 (D110), every verb but <c>list</c>, which changes nothing: reading and writing across (D107), as
/// <c>SET_READ_ACROSS</c> and <c>SET_WRITE_ACROSS</c> make them, and <c>cap</c> and <c>adapter</c>, which only a
/// terminal set before. Since HELP10 <c>retry</c> too, its target a quest the loop's last tick parked
/// (<see cref="HelpMachineFacts.Parked"/>), applied as <c>RETRY_QUEST</c> makes the same edit.</para>
/// </remarks>
internal sealed class HelpSettingProposals : IHelpProposalKind
{
    public string Kind => "setting";

    // It proposes, and the person applies (HELP1c, D89).
    public string Tool => "setting_propose";

    /// <summary>The `daoris driver` verbs it takes, in the order the service's twin lists them (<c>HelpProposalBox.Doors</c>).</summary>
    /// <remarks>
    /// HELP9 added <c>across</c> (D107), and <c>cap</c> and <c>adapter</c>, which only a terminal set before; HELP10
    /// <c>retry</c>, once the facts carried the parked quests.
    /// </remarks>
    public IReadOnlyList<string> Doors { get; } =
    [
        "drive", "undrive", "hold", "resume", "trees", "line", "landing", "across", "standing", "intake", "helper", "strikes",
        "retry", "timeout", "notify", "cap", "adapter",
    ];

    public HelpPlan Plan(HelpProposal proposal, DriverConfig config, HelpMachineFacts facts)
    {
        var target = proposal.Target?.Trim();
        var workspace = proposal.Workspace?.Trim();
        var value = proposal.Value?.Trim() ?? "";
        HelpPlan Refused(string why, string describe, string terminal) => new(why, describe, terminal, null);

        // HELP10: a retry's target is a quest, not a repository, so it is judged before the registry is asked.
        if (proposal.Door == "retry") return Retry(target, config, facts);

        // The names the route itself does not check, and a helper can invent: a repository, a circle, an agent.
        if (target is { Length: > 0 } && !facts.Repositories.Contains(target, StringComparer.OrdinalIgnoreCase))
        {
            return Refused($"`{target}` is not registered on this machine — use a repository's name as Repositories lists it.", "", "");
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
                planned = ($"Drive `{target}`: a quest for it starts a session on this machine.", $"daoris driver drive {target}", c => c.WithDrivable(target!, true));
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
            case "across":
            {
                var (refused, across) = Across(target, workspace, value, facts);
                if (refused is not null) return Refused(refused, "", "");
                planned = across;
                break;
            }
            case "standing":
            {
                // KNOWUSE1b (D135 §3): the person's words for one repository, kept on this machine as `SET_STANDING` and
                // `daoris driver standing` keep them; `--clear` takes the answer back.
                var says = value == "--clear" ? null : value;
                var at = DateTimeOffset.UtcNow;
                planned = (says is null
                        ? $"Clear `{target}`'s standing answer: its sessions are handed none."
                        : $"Keep a standing answer for `{target}`, handed to every session in `{target}` beneath its quest: \"{says}\"",
                    $"daoris driver standing {target} {(says is null ? "--clear" : $"\"{says.Replace("\"", "\\\"", StringComparison.Ordinal)}\"")}",
                    c => c.WithStanding(target!, says, at));
                break;
            }
            case "language":
            {
                // LANG1c (D142 point 7): the work's session language, for a repository or a workspace, judged by the closed
                // table as `SET_LANGUAGE` and `daoris driver language` judge it. Not among `Doors` until the service's writer
                // lists it beside them (HelpCoverageTests), so only a proposal file naming it reaches here meanwhile.
                var circle = workspace is { Length: > 0 };
                if (circle == target is { Length: > 0 })
                {
                    return Refused("a session language is set for a repository or a workspace — name exactly one.", "", "");
                }

                var code = value == "--clear" ? null : SessionLanguages.Code(value);
                if (value != "--clear" && code is null)
                {
                    return Refused(value.Length == 0
                        ? "`language` names a language, `en` or `zh`, or `--clear` — e.g. `daoris driver language engine zh`."
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
                    $"daoris driver language {scope} {code ?? "--clear"}",
                    c => circle ? c.WithWorkspaceLanguage(workspace!, code) : c.WithLanguage(target!, code));
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
            // HELP9: the two dials only a terminal set before, judged as `daoris driver cap|adapter` takes them.
            case "cap" when int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var cap) && cap >= 1:
                planned = (cap == 1 ? "Run one session at a time on this machine, across every repository."
                        : $"Run at most {cap} sessions at once on this machine, across every repository.",
                    $"daoris driver cap {cap}", c => c with { Cap = cap });
                break;
            case "adapter":
                // The CLI leaves the name to the driver, which knows its adapters: the same names an intake is judged by.
                if (!facts.Agents.Contains(value, StringComparer.Ordinal))
                {
                    return Refused($"there is no agent `{value}` on this machine — one of {Names(facts.Agents)}.", "", "");
                }

                planned = ($"Start this machine's driven sessions on `{value}`.", $"daoris driver adapter {value}", c => c with { Adapter = value });
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

    /// <summary>
    /// <c>retry</c> (HELP10, D110), as <c>daoris driver retry</c> reads it: a quest by id, <c>#</c> or not, which the
    /// loop's last look parked by its strikes, marked forgiven at the strike limit as <c>RETRY_QUEST</c> marks it; or one
    /// the person's stop holds (SESSUX1b, D126 §3.4), released from the session the look named, as <c>RETRY_QUEST</c>
    /// releases it. Either is said in the terminal's words.
    /// </summary>
    /// <remarks>
    /// The quest must be on one of the look's two lists, since a helper can invent an id, and forgiving a quest that is not
    /// parked lets it run past its strikes (D110). The limit is the one standing when the person applies, as the route
    /// reads it.
    /// </remarks>
    private static HelpPlan Retry(string? target, DriverConfig config, HelpMachineFacts facts)
    {
        var quest = target?.TrimStart('#') ?? "";
        if (quest.Length == 0)
        {
            return new HelpPlan(
                "`retry` names the quest its failed sessions parked or your stop holds, by id — `daoris driver retry <quest>`.",
                "", "", null);
        }

        if (facts.Held.FirstOrDefault(held => string.Equals(held.Quest, quest, StringComparison.OrdinalIgnoreCase)) is { } stopped)
        {
            return new HelpPlan(null,
                $"Quest `#{quest}` is released from your stop of session `{stopped.Session}`: the driver takes it up again at its "
                + "next look, and a later stop holds it again.",
                $"daoris driver retry {quest} --session {stopped.Session}",
                // RETRY_QUEST's own edit for a stop: released from that session, and no mark, since a stop is not a strike.
                c => c.WithReleased(quest, stopped.Session));
        }

        var terminal = $"daoris driver retry {quest}";
        if (!facts.Parked.Any(parked => string.Equals(parked.Quest, quest, StringComparison.OrdinalIgnoreCase)))
        {
            static string Listed(IEnumerable<string> ids) => ids.Any() ? Names(ids.Select(id => $"#{id}")) : "none";
            return new HelpPlan(
                $"`#{quest}` is neither parked nor held by your stop on this machine — only such a quest starts again, as its "
                + $"page's *Try again* does; at the driver's last look it parked {Listed(facts.Parked.Select(each => each.Quest))} "
                + $"and held {Listed(facts.Held.Select(each => each.Quest))}.",
                "", terminal, null);
        }

        var strikes = config.Strikes;
        return new HelpPlan(null,
            $"Quest `#{quest}` may be started again. Counting from {strikes} failure(s) — what already happened is still in the "
            + $"records, and {(strikes > 0 ? strikes.ToString(CultureInfo.InvariantCulture) : "no")} more failure(s) will park it again.",
            terminal,
            // RETRY_QUEST's own edit: marked at the limit rather than erased, so the records still read true.
            c => c.WithForgiven(quest, c.Strikes));
    }

    /// <summary>
    /// <c>across</c> (HELP9, D107), as <c>daoris driver across</c> reads its words: <c>read on|off|--clear</c> for a
    /// repository or a whole workspace, or <c>write-to &lt;other&gt; [--clear]</c> from a repository, <c>--clear</c>
    /// wherever it stands — said as the terminal says it, or refused in its words.
    /// </summary>
    /// <remarks>
    /// A repository naming itself is the route's own refusal, which its edit throws. The other repository of a
    /// write-to is a name a helper can invent, so it is checked here as the target is.
    /// </remarks>
    private static (string? Refusal, (string Describe, string Terminal, Func<DriverConfig, DriverConfig> Edit) Planned) Across(
        string? target, string? workspace, string value, HelpMachineFacts facts)
    {
        var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        var clear = words.Remove("--clear");
        var named = target is { Length: > 0 };
        var circle = workspace is { Length: > 0 };
        switch (words.FirstOrDefault())
        {
            case "read":
            {
                if (named == circle) return ("reading across is set for a repository or a workspace — name exactly one.", default);
                bool? read = words.Count == 2 && !clear ? words[1] switch { "on" => true, "off" => false, _ => null } : null;
                if (clear ? words.Count != 1 : read is null)
                {
                    return ("`across … read` is `read on|off|--clear` — e.g. `daoris driver across engine read off`.", default);
                }

                var scope = circle ? $"--workspace {workspace}" : target;
                var subject = circle ? $"Each checkout in workspace `{workspace}` that sets none of its own" : $"`{target}`'s checkout";
                var describe = read switch
                {
                    true => $"{subject} is read by sessions in its workspace's other repositories and by Ask Daoris: its files, "
                        + "`git status` and the branch list, never a write.",
                    false => $"{subject} is read by no agent outside it: no session in another repository, and not Ask Daoris.",
                    null => circle
                        ? $"Checkouts in workspace `{workspace}` are read across again, unless one says otherwise."
                        : $"`{target}` takes its workspace's reading again, else on.",
                };
                if (circle && read is not null) describe += " A repository with a setting of its own keeps it.";
                return (null, (describe, $"daoris driver across {scope} read {(clear ? "--clear" : words[1])}",
                    c => circle ? c.WithWorkspaceReadAcross(workspace!, read) : c.WithReadAcross(target!, read)));
            }
            case "write-to":
            {
                if (circle || !named)
                {
                    return ("a relationship is declared from one repository — `daoris driver across <repository> write-to <other>`.", default);
                }

                if (words.Count > 2)
                {
                    return ("`across … write-to` names one repository — `write-to <other> [--clear]`.", default);
                }

                var other = words.Count == 2 ? words[1] : "";
                if (other.Length > 0 && !string.Equals(other, target, StringComparison.OrdinalIgnoreCase)
                    && !facts.Repositories.Contains(other, StringComparer.OrdinalIgnoreCase))
                {
                    return ($"`{other}` is not registered on this machine — use a repository's name as Repositories lists it.", default);
                }

                var describe = clear
                    ? $"Sessions in `{target}` no longer write into `{other}`; a change needed there is a quest again."
                    : $"Sessions in `{target}` may also write into `{other}` — its files, and a commit there — where both are in one "
                      + "workspace with a checkout here. Applying it is your standing say-so for writing across, one way: "
                      + $"`{other}` writes nothing into `{target}` unless you declare that too.";
                return (null, (describe, $"daoris driver across {target} write-to {other}{(clear ? " --clear" : "")}",
                    c => c.WithWriteAcross(target!, other, !clear)));
            }
            default:
                return ("`across` sets `read on|off|--clear` or `write-to <other> [--clear]` — e.g. `daoris driver across engine read off`, "
                    + "`daoris driver across plugins write-to engine`.", default);
        }
    }

    public Task<HelpApplied> ApplyAsync(HelpApplying applying, CancellationToken ct)
    {
        applying.Doors.Change(applying.Plan.Apply!);
        return Task.FromResult(applying.Settled(true, $"Applied: `#{applying.Id}` — {applying.Plan.Describe} (`{applying.Plan.Terminal}`)", null));
    }
}

public partial interface IHelpDoors
{
    /// <summary>An edit to the driver's file, as the `SET_*` routes and `RETRY_QUEST` make it.</summary>
    void Change(Func<DriverConfig, DriverConfig> edit);
}

public sealed partial record HelpMachineFacts
{
    /// <summary>
    /// The quests the loop's last tick parked by their failed sessions (HELP10), which a retry must name: the verdict
    /// the quest drawer shows its Retry by. Empty before any tick, and while another loop holds the home (D104).
    /// </summary>
    public IReadOnlyList<ParkedQuest> Parked { get; init; } = [];

    /// <summary>
    /// The quests the loop's last look held by the person's stop (SESSUX1b), each with the session they stopped: a retry of
    /// one releases that stop. Empty before any look, and while another loop holds the home (D104).
    /// </summary>
    public IReadOnlyList<HeldQuest> Held { get; init; } = [];
}
