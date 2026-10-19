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

    /// <summary>The `daoris driver` verbs it takes, in the order the service's twin lists them (<c>HelpSettingProposalWriter.Doors</c>).</summary>
    /// <remarks>
    /// HELP9 added <c>across</c> (D107), and <c>cap</c> and <c>adapter</c>, which only a terminal set before; HELP10
    /// <c>retry</c>, once the facts carried the parked quests; LANG1c2 <c>language</c>, once the service's writer listed it;
    /// REVIEWENV1a <c>review</c>, with the service's writer in the same change; XAGENT1a <c>opinion</c>, the same way; TOOL4l-b <c>cooloff</c>, the same way;
    /// DOORS1a <c>helpers</c> and <c>commits</c>, the same way (D164's amendment point 5).
    /// </remarks>
    public IReadOnlyList<string> Doors { get; } =
    [
        "drive", "undrive", "hold", "resume", "trees", "line", "landing", "across", "standing", "language", "review", "opinion",
        "helpers", "commits", "intake", "helper", "strikes", "retry", "timeout", "notify", "cap", "adapter", "cooloff",
    ];

    /// <summary>
    /// ASKSCOPE1c: its target is a repository, save a retry's, which is a quest; <c>across … write-to &lt;other&gt;</c> names a
    /// second repository in its value.
    /// </summary>
    public HelpNames Named(HelpProposal proposal, HelpMachineFacts facts)
    {
        var words = proposal.Door == "across" ? (proposal.Value ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries) : [];
        var other = words is ["write-to", var to, ..] ? to : null;
        return new([proposal.Workspace], [proposal.Door == "retry" ? null : proposal.Target, other, proposal.Repository]);
    }

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

        // ACCTQUOTE1b: a workspace in a command is spelled for any shell; a repository's name is not (D125's ACCTQUOTE1 note).
        var scope = workspace is { Length: > 0 } ? $"--workspace {ShellWord.Of(workspace, ShellWord.Workspace)}" : target ?? "";
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
                var (refused, landing) = Landing(target, workspace, value, config, facts, whose, scope);
                if (refused is not null) return Refused(refused, "", "");
                planned = landing;
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
                // table as `SET_LANGUAGE` and `daoris driver language` judge it. A door since LANG1c2, whose service writer
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
            case "review":
            {
                var (refused, review) = Review(target, workspace, value, config, facts);
                if (refused is not null) return Refused(refused, "", "");
                planned = review;
                break;
            }
            case "opinion":
            {
                var (refused, opinion) = Opinion(target, workspace, value, config, facts);
                if (refused is not null) return Refused(refused, "", "");
                planned = opinion;
                break;
            }
            case "helpers":
            {
                var (refused, helpers) = Helpers(target, workspace, value, config);
                if (refused is not null) return Refused(refused, "", "");
                planned = helpers;
                break;
            }
            case "commits":
            {
                var (refused, commits) = Commits(target, workspace, value, config);
                if (refused is not null) return Refused(refused, "", "");
                planned = commits;
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
            // TOOL4l-b (D125 §6): how long an account cools when its limit names no time, as `daoris driver cooloff` takes it;
            // a zero would start a spent account again at every look, so less than a minute falls to the refusal below.
            case "cooloff" when int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var cool) && cool >= 1:
                planned = ($"An account whose limit names no time cools for {cool} minute{(cool == 1 ? "" : "s")} before it is offered again.",
                    $"daoris driver cooloff {cool}", c => c.WithCoolOff(cool));
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
    /// loop's last look parked by its strikes, marked forgiven at its failures as that look counted them, as
    /// <c>RETRY_QUEST</c> marks it (RETRY1b); or one the person's stop holds (SESSUX1b, D126 §3.4), released from the
    /// session the look named, as <c>RETRY_QUEST</c> releases it. Either is said in the terminal's words.
    /// </summary>
    /// <remarks>
    /// The quest must be on one of the look's two lists, since a helper can invent an id, and forgiving a quest that is not
    /// parked lets it run past its strikes (D110). The mark is the look's count; the limit said is the one standing when
    /// the person applies, as the route reads it.
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
        if (facts.Parked.FirstOrDefault(parked => string.Equals(parked.Quest, quest, StringComparison.OrdinalIgnoreCase))
            is not { } parkedQuest)
        {
            static string Listed(IEnumerable<string> ids) => ids.Any() ? Names(ids.Select(id => $"#{id}")) : "none";
            return new HelpPlan(
                $"`#{quest}` is neither parked nor held by your stop on this machine — only such a quest starts again, as its "
                + $"page's *Try again* does; at the driver's last look it parked {Listed(facts.Parked.Select(each => each.Quest))} "
                + $"and held {Listed(facts.Held.Select(each => each.Quest))}.",
                "", terminal, null);
        }

        // RETRY_QUEST's own mark (RETRY1b): the quest's failures as the last look counted them, not the limit, which left a
        // quest parked a second time parked. A park carrying no count is marked at the least count the planner parks at.
        int Mark(DriverConfig at) => parkedQuest.Failures ?? at.ForgivenAt(quest) + at.Strikes;
        var strikes = config.Strikes;
        return new HelpPlan(null,
            $"Quest `#{quest}` may be started again. Counting from {Mark(config)} failure(s) — what already happened is still in "
            + $"the records, and {(strikes > 0 ? strikes.ToString(CultureInfo.InvariantCulture) : "no")} more failure(s) will park "
            + "it again.",
            terminal,
            // Marked where it stands rather than erased, so the records still read true.
            c => c.WithForgiven(quest, Mark(c)));
    }

    /// <summary>
    /// <c>landing</c>, as <c>daoris driver landing</c> reads its words (HELP8): the form, a branch's pattern, and <c>--tidy</c>,
    /// <c>--plugin &lt;id&gt;</c>, <c>--auto-accept</c> or <c>--no-auto-accept</c> (LAND2a), <c>--key &lt;shape&gt;</c> or
    /// <c>--no-key</c> (PROCESS1b-i) and <c>--squash</c> or <c>--no-squash</c> (PROCESS1b-ii) in any order, a quoted word kept
    /// whole; each refused as the terminal refuses it, and the rule's shape by its edit, in <see cref="LandingRules.Problem(LandingRule)"/>'s words.
    /// </summary>
    /// <remarks>
    /// DOORS1a (D164's amendment point 2): the rule replaces the one set there whole, as the terminal's does, but for its key and
    /// its squash, which a card naming neither keeps where the new rule can hold them (a branch, and a key only for a pattern
    /// saying <c>{ticket}</c>): a card rebuilding the rule without them dropped what the terminal set. They are kept from what
    /// stands when the person applies, as every rule's edit reads it then, and the card says the rule it leaves.
    /// </remarks>
    private static (string? Refusal, (string Describe, string Terminal, Func<DriverConfig, DriverConfig> Edit) Planned) Landing(
        string? target, string? workspace, string value, DriverConfig config, HelpMachineFacts facts, string whose, string scope)
    {
        if (Words(value) is not { } words) return ("`landing`'s words close every quote they open.", default);
        var tidy = words.Remove("--tidy");
        var auto = words.Remove("--auto-accept");
        var byHand = words.Remove("--no-auto-accept");
        var squash = words.Remove("--squash");
        var unsquashed = words.Remove("--no-squash");
        var keyless = words.Remove("--no-key");

        // Refused in the order `daoris driver landing` refuses them.
        var (key, keyMissing) = Valued(words, "--key");
        if (keyMissing) return ($"`--key` needs the shape of a ticket's key — `--key <shape>`, e.g. `--key \"{LandingRules.DefaultKey}\"`.", default);
        if (key is not null && keyless) return ("a rule names its ticket's key or reads the default — say `--key <shape>` or `--no-key`, not both.", default);
        var (plugin, pluginMissing) = Valued(words, "--plugin");
        if (pluginMissing) return ("`--plugin` needs the id of an installed plugin — `--plugin <id>`; `daoris plugin list` shows what there is.", default);
        if (auto && byHand) return ("a rule accepts automatically or waits for your Accept — say `--auto-accept` or `--no-auto-accept`, not both.", default);
        if (squash && unsquashed)
        {
            return ("a rule squashes the work to one commit or keeps the session's commits — say `--squash` or `--no-squash`, not both.", default);
        }

        var bare = string.Join(' ', words);
        LandingRule? rule = bare switch
        {
            "--clear" => null,
            LandingForm.Merge => new LandingRule(LandingForm.Merge, null, tidy, plugin, auto, key, squash),
            _ when bare.StartsWith("branch ", StringComparison.Ordinal) =>
                new LandingRule(LandingForm.Branch, bare["branch ".Length..].Trim(), tidy, plugin, auto, key, squash),
            _ => new LandingRule(bare, null, tidy, plugin, auto, key, squash),
        };

        var circle = workspace is { Length: > 0 };
        LandingRule? Kept(DriverConfig at)
        {
            if (rule is not { Form: LandingForm.Branch }
                || !(circle ? at.WorkspaceLandings : at.Landings).TryGetValue((circle ? workspace : target)!, out var standing))
            {
                return rule;
            }

            var ticketed = (rule.Pattern ?? "").Contains("{ticket}", StringComparison.Ordinal);
            return rule with { Key = key ?? (keyless || !ticketed ? null : standing.Key), Squash = squash || (!unsquashed && standing.Squash) };
        }

        var leaves = Kept(config);
        // The plugin must land work on THIS machine, asked as the screen's route asks it (D100),
        // once the rule's own shape is sound, so a merge naming one is told why in that shape's words.
        if (leaves is { Plugin: { } named } && LandingRules.Problem(leaves) is null
            && LandingRules.PluginProblem(named, facts.Plugins) is { } unready)
        {
            return (unready, default);
        }

        var after = (plugin is null ? "" : $", plugin `{plugin}` pushes it and opens the pull request")
            + (tidy ? ", its tree removed once landed" : "");
        // The person's standing say-so for a push with no press is said on the card they apply (D145 point 5).
        var accepting = auto ? $" It is accepted automatically: {LandingRules.AutoAcceptSays(plugin)}"
            : byHand && leaves?.Form == LandingForm.Branch ? " Done work there waits for your Accept." : "";
        var lands = leaves is null ? $"Clear {whose}'s landing rule."
            : leaves.Form == LandingForm.Branch ? $"Land {whose}'s accepted work on a branch `{leaves.Pattern}`{after}.{accepting}"
            : $"Land {whose}'s accepted work merged into its line{after}.";
        // What finds the branch's ticket and what Accept makes, said of the rule it leaves, as the terminal says them.
        if (leaves is { Form: LandingForm.Branch, Pattern: { } pattern } && pattern.Contains("{ticket}", StringComparison.Ordinal))
        {
            lands += " " + TicketSays(leaves.Key);
        }

        if (leaves is { Squash: true }) lands += " " + Squashed;
        else if (leaves is { Form: LandingForm.Branch } && unsquashed) lands += " The branch holds the session's own commits.";
        return (null, (lands, $"daoris driver landing {scope} {value}",
            c => circle ? c.WithWorkspaceLanding(workspace!, Kept(c)) : c.WithLanding(target!, Kept(c))));
    }

    /// <summary>A flag's value taken out of the words, as the terminal reads it: none where the flag is absent, missing where no value follows it.</summary>
    private static (string? Value, bool Missing) Valued(List<string> words, string flag)
    {
        var at = words.IndexOf(flag);
        if (at < 0) return (null, false);
        if (at + 1 >= words.Count || words[at + 1].StartsWith("--", StringComparison.Ordinal)) return (null, true);

        var value = words[at + 1];
        words.RemoveRange(at, 2);
        return (value, false);
    }

    /// <summary>What finds a branch's ticket (PROCESS1b-i), in the words of the CLI's <c>ticketSays</c>.</summary>
    private static string TicketSays(string? key) =>
        $"`{{ticket}}` is the first match of `{key ?? LandingRules.DefaultKey}` in the title, then the request, then its links, as written"
        + $"{(key is null ? " (the default shape)" : "")}; where none is found, the session's id stands in.";

    /// <summary>What Accept makes under a rule that squashes (PROCESS1b-ii), in <c>daoris driver landing</c>'s words.</summary>
    private const string Squashed =
        "Squashed: Accept makes the pull request's branch one commit from the session's work — the quest's title, then each of the "
        + "session's commits' subjects — made by git as the repository's own `user.name` and `user.email`; where git has none there, "
        + "Accept is refused and says how to set them. A tree on a branch of its own lands as it stands.";

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

                var scope = circle ? $"--workspace {ShellWord.Of(workspace!, ShellWord.Workspace)}" : target;
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

    /// <summary>What a <c>review</c> proposal's words may say, in the refusal that names the shape.</summary>
    private const string ReviewUsage =
        "`review` is `<environment> --kind local|deployed --procedure <path> [--address <url>] [--run \"<command>\"] "
        + "[--required|--not-required]`, `none`, `--drop <environment>`, `--required|--not-required` or `--clear` — e.g. "
        + "`daoris driver review storefront dev --kind local --procedure README.md --address http://localhost:4200 --required`.";

    /// <summary>
    /// <c>review</c> (REVIEWENV1a, D154 point 2, the review-environment design §1.7), as <c>daoris driver review</c> reads its
    /// words: an environment added or replaced with its parts, <c>none</c>, <c>--drop</c>, <c>--required|--not-required</c>
    /// or <c>--clear</c>, for a repository or a whole workspace, a quoted word kept whole. Judged by the twin's table
    /// (<see cref="ReviewRules.Apply"/>), and an environment's procedure looked for in the checkouts it reaches, as the
    /// screen's route and the terminal look; the card says what the rule lets a step do, and what the landing's gate does with it
    /// (<see cref="ReviewRules.Gate"/>, REVIEWENV1c2).
    /// </summary>
    private static (string? Refusal, (string Describe, string Terminal, Func<DriverConfig, DriverConfig> Edit) Planned) Review(
        string? target, string? workspace, string value, DriverConfig config, HelpMachineFacts facts)
    {
        var named = target is { Length: > 0 };
        var circle = workspace is { Length: > 0 };
        if (named == circle) return ("a review rule is set for a repository or a workspace — name exactly one.", default);
        if (Words(value) is not { } words) return ($"`review`'s words close every quote they open — {ReviewUsage}", default);

        var flags = new Dictionary<string, string>(StringComparer.Ordinal);
        var switches = new HashSet<string>(StringComparer.Ordinal);
        var bare = new List<string>();
        for (var at = 0; at < words.Count; at++)
        {
            var word = words[at];
            if (word is "--kind" or "--procedure" or "--address" or "--run" or "--drop")
            {
                if (at + 1 >= words.Count || words[at + 1].StartsWith("--", StringComparison.Ordinal)) return ($"`{word}` needs its value — {ReviewUsage}", default);
                flags[word] = words[++at];
            }
            else if (word is "--clear" or "--required" or "--not-required") switches.Add(word);
            else if (word.StartsWith("--", StringComparison.Ordinal)) return ($"`{word}` is not a word `review` takes — {ReviewUsage}", default);
            else bare.Add(word);
        }

        if (switches.Contains("--required") && switches.Contains("--not-required"))
        {
            return ("a rule is required or not — say `--required` or `--not-required`, not both.", default);
        }

        if (bare.Count > 1) return ($"`review` names one environment — {ReviewUsage}", default);
        var environment = bare.FirstOrDefault();
        var clear = switches.Contains("--clear");
        var drop = flags.GetValueOrDefault("--drop");
        bool? required = switches.Contains("--required") ? true : switches.Contains("--not-required") ? false : null;
        if (environment is null && !clear && drop is null && required is null) return (ReviewUsage, default);

        var parts = new ReviewSpelled(environment, flags.GetValueOrDefault("--kind"), flags.GetValueOrDefault("--procedure"),
            flags.GetValueOrDefault("--address"), flags.GetValueOrDefault("--run"));
        // `none` is the word for a repository with no review environment, unless it is given an environment's parts.
        var none = environment == "none" && parts is { Kind: null, Procedure: null, Address: null, Run: null };
        var edit = new ReviewEdit
        {
            Repository = circle ? null : target,
            Workspace = circle ? workspace : null,
            Clear = clear,
            Drop = drop,
            None = none,
            Put = !none && environment is not null ? parts : null,
            Required = required,
        };

        DriverConfig after;
        try
        {
            after = ReviewRules.Apply(config, edit);
        }
        catch (DriverException refused)
        {
            return (refused.Message, default);
        }

        // An environment put: its procedure is looked for in each checkout it reaches, as the terminal and the screen look.
        var notes = new List<string>();
        if (edit.Put is { Procedure: { } procedure })
        {
            if (!circle)
            {
                var root = facts.Registered.FirstOrDefault(each => string.Equals(each.Repository, target, StringComparison.OrdinalIgnoreCase)).Root;
                if (root is not { Length: > 0 })
                {
                    notes.Add($"Not checked: `{target}` has no checkout on this machine; a set-up step sits until its tree holds `{procedure}`.");
                }
                else if (!ReviewRules.Holds(root, procedure))
                {
                    return (ReviewRules.NotHeld(target!, procedure), default);
                }
                else
                {
                    notes.Add($"`{procedure}` is in `{target}`'s checkout here.");
                }
            }
            else
            {
                notes.AddRange(ReviewRules.Lacking(facts.Registered, edit, procedure).Select(lacking => ReviewRules.SitsUntil(lacking, procedure)));
            }
        }

        var whose = circle ? $"each repository of workspace `{workspace}` that sets none of its own" : $"`{target}`";
        var whoseRule = circle ? $"workspace `{workspace}`'s" : $"`{target}`'s";
        var head = clear
            ? circle
                ? $"Clear workspace `{workspace}`'s review rule: each repository there keeps its own, else none."
                : $"Clear `{target}`'s review rule: it takes its workspace's again, else none."
            : none ? $"Declare that `{target}` has no review environment, whatever its workspace says."
            : drop is not null ? $"Stop declaring `{drop}` for {whose}."
            : environment is null ? $"Make {whoseRule} review rule {(required == true ? "required" : "not required")}."
            : $"Declare the review environment `{environment}` for {whose}.";
        var map = circle ? after.WorkspaceReviews : after.Reviews;
        List<string> said = map.TryGetValue((circle ? workspace : target)!, out var standing)
            ? [.. ReviewRules.Says(standing), .. ReviewRules.Gate(standing)]
            : [];
        var scope = circle ? $"--workspace {ShellWord.Of(workspace!, ShellWord.Workspace)}" : target!;
        return (null, (string.Join(" ", [head, .. said, .. notes]), $"daoris driver review {scope} {value}", c => ReviewRules.Apply(c, edit)));
    }

    /// <summary>What an <c>opinion</c> proposal's words may say, in the refusal that names the shape.</summary>
    private const string OpinionUsage =
        "`opinion` is `--reviewers <adapter,adapter> [--on landing,steps] [--required|--not-required] [--verify|--no-verify] "
        + "[--minutes <n>] [--recheck|--no-recheck]`, `none` or `--clear` — e.g. "
        + "`daoris driver opinion web-app --reviewers codex-acp --required`.";

    /// <summary>
    /// <c>opinion</c> (XAGENT1a, D155 point 3, the second-agent design §2.5), as <c>daoris driver opinion</c> reads its words:
    /// <c>--reviewers</c> and the switches set over the rule set there, <c>none</c> or <c>--clear</c>, for a repository or a
    /// whole workspace, a quoted word kept whole. Judged by the twin's table (<see cref="OpinionRules.Apply"/>); the card says
    /// what the rule lets a reviewer do, names the reviewers of the working agent's own family over this machine's agents, a
    /// plugin's among them (XAGENT1b2), and says what the gate does with it (<see cref="OpinionRules.Gate"/>, XAGENT1f4).
    /// </summary>
    private static (string? Refusal, (string Describe, string Terminal, Func<DriverConfig, DriverConfig> Edit) Planned) Opinion(
        string? target, string? workspace, string value, DriverConfig config, HelpMachineFacts facts)
    {
        var named = target is { Length: > 0 };
        var circle = workspace is { Length: > 0 };
        if (named == circle) return (OpinionRules.Scope, default);
        if (Words(value) is not { } words) return ($"`opinion`'s words close every quote they open — {OpinionUsage}", default);

        var flags = new Dictionary<string, string>(StringComparer.Ordinal);
        var switches = new HashSet<string>(StringComparer.Ordinal);
        var none = false;
        for (var at = 0; at < words.Count; at++)
        {
            var word = words[at];
            if (word is "--reviewers" or "--on" or "--minutes")
            {
                if (at + 1 >= words.Count || words[at + 1].StartsWith("--", StringComparison.Ordinal)) return ($"`{word}` needs its value — {OpinionUsage}", default);
                flags[word] = words[++at];
            }
            else if (word is "--required" or "--not-required" or "--verify" or "--no-verify" or "--recheck" or "--no-recheck" or "--clear")
            {
                switches.Add(word);
            }
            else if (word == "none") none = true;
            else return ($"`{word}` is not a word `opinion` takes — {OpinionUsage}", default);
        }

        bool? Switch(string on, string off) => switches.Contains(on) ? true : switches.Contains(off) ? false : null;
        foreach (var (on, off, why) in new[]
        {
            ("--required", "--not-required", "a rule is required or not"),
            ("--verify", "--no-verify", "a reviewer may build and run or it may not"),
            ("--recheck", "--no-recheck", "the answers are read again or they are not"),
        })
        {
            if (switches.Contains(on) && switches.Contains(off)) return ($"{why} — say `{on}` or `{off}`, not both.", default);
        }

        static IReadOnlyList<string?>? Listed(string? words) => words?.Split(',').Select(each => (string?)each.Trim()).ToList();
        var minutes = flags.GetValueOrDefault("--minutes");
        var set = new OpinionSet
        {
            Reviewers = Listed(flags.GetValueOrDefault("--reviewers")),
            On = Listed(flags.GetValueOrDefault("--on")),
            Required = Switch("--required", "--not-required"),
            Verify = Switch("--verify", "--no-verify"),
            // A number only as digits, as the terminal reads it: anything else is refused in the rule's own words.
            Minutes = minutes is null ? null
                : minutes.Length > 0 && minutes.All(char.IsAsciiDigit) ? double.Parse(minutes, CultureInfo.InvariantCulture) : double.NaN,
            Recheck = Switch("--recheck", "--no-recheck"),
        };
        var clear = switches.Contains("--clear");
        if (!none && !clear && !set.Names) return (OpinionUsage, default);

        var edit = new OpinionEdit
        {
            Repository = circle ? null : target,
            Workspace = circle ? workspace : null,
            Set = set.Names ? set : null,
            None = none,
            Clear = clear,
        };

        DriverConfig after;
        try
        {
            after = OpinionRules.Apply(config, edit);
        }
        catch (DriverException refused)
        {
            return (refused.Message, default);
        }

        var whose = circle ? $"each repository of workspace `{workspace}` that sets none of its own" : $"`{target}`";
        var head = clear
            ? circle
                ? $"Clear workspace `{workspace}`'s second-opinion rule: each repository there keeps its own, else none."
                : $"Clear `{target}`'s second-opinion rule: it takes its workspace's again, else none."
            : none ? $"Declare that `{target}` has no second opinion, whatever its workspace says."
            : $"Set the second opinion for {whose}.";
        var map = circle ? after.WorkspaceOpinions : after.Opinions;
        // The working agent is the one this machine's work runs on: a reviewer of its family is no independent reading. A
        // plugin's agent is judged by what its plugin declares (XAGENT1b2), as the terminal and the reviewer's choice judge it.
        var adapters = AdapterSet.Built().WithPlugins(facts.Plugins);
        List<string> said = map.TryGetValue((circle ? workspace : target)!, out var standing)
            ? [.. OpinionRules.Says(standing, OpinionRules.SameAgent(standing, config.Adapter, adapters)), .. OpinionRules.Gate(standing)]
            : [];
        var scope = circle ? $"--workspace {ShellWord.Of(workspace!, ShellWord.Workspace)}" : target!;
        return (null, (string.Join(" ", [head, .. said]), $"daoris driver opinion {scope} {value}", c => OpinionRules.Apply(c, edit)));
    }

    /// <summary>What a <c>helpers</c> proposal's words may say, in the refusal that names the shape.</summary>
    private const string HelpersUsage =
        "`helpers` is `--mode ask|on|off` and/or `--agents <adapter,adapter>`, or `--clear` — e.g. "
        + "`daoris driver helpers web-app --mode on --agents codex-acp`.";

    /// <summary>
    /// <c>helpers</c> (DOORS1a; D163 point 3, MIXAGENT2a-i), as <c>daoris driver helpers</c> reads its words: <c>--mode</c> and
    /// <c>--agents</c> set over the rule set there, or <c>--clear</c>, for a repository or a whole workspace, a quoted list kept
    /// whole. Judged by the twin's table (<see cref="HelperRules.Apply"/>); the card says the rule it leaves and what an agent's
    /// ask then does, in the terminal's sentences.
    /// </summary>
    private static (string? Refusal, (string Describe, string Terminal, Func<DriverConfig, DriverConfig> Edit) Planned) Helpers(
        string? target, string? workspace, string value, DriverConfig config)
    {
        var circle = workspace is { Length: > 0 };
        if ((target is { Length: > 0 }) == circle) return (HelperRules.Scope, default);
        if (Words(value) is not { } words) return ($"`helpers`'s words close every quote they open — {HelpersUsage}", default);
        if (RuleWords(words, "helpers", HelpersUsage, ["--mode", "--agents"], ["--clear"], out var flags, out var switches) is { } wrong)
        {
            return (wrong, default);
        }

        var set = new HelperSet
        {
            ModeGiven = flags.ContainsKey("--mode"),
            Mode = flags.GetValueOrDefault("--mode"),
            Agents = flags.GetValueOrDefault("--agents")?.Split(',').Select(each => (string?)each.Trim()).ToList(),
        };
        var clear = switches.Contains("--clear");
        if (!clear && !set.Names) return (HelpersUsage, default);

        var edit = new HelperEdit
        {
            Repository = circle ? null : target,
            Workspace = circle ? workspace : null,
            Set = set.Names ? set : null,
            Clear = clear,
        };
        DriverConfig after;
        try
        {
            after = HelperRules.Apply(config, edit);
        }
        catch (DriverException refused)
        {
            return (refused.Message, default);
        }

        var whose = circle ? $"each repository of workspace `{workspace}` that sets none of its own" : $"`{target}`";
        var describe = (circle ? after.WorkspaceHelpers : after.Helpers).TryGetValue((circle ? workspace : target)!, out var rule)
            ? $"Set the helpers rule for {whose}: {HelperRules.Listed(rule)}. " + rule.Mode switch
            {
                HelperRules.On => "A helper an agent asks for starts at once, within the limits: two at once for a session, one turn each.",
                HelperRules.Off => "A helper is not offered to a session here: only your own press asks one.",
                _ => "Each helper an agent asks for waits for your yes.",
            }
            : circle
                ? $"Clear workspace `{workspace}`'s helpers rule: each repository there keeps its own, else each helper waits for your yes."
                : $"Clear `{target}`'s helpers rule: it takes its workspace's again, else each helper waits for your yes.";
        var scope = circle ? $"--workspace {ShellWord.Of(workspace!, ShellWord.Workspace)}" : target!;
        return (null, (describe, $"daoris driver helpers {scope} {value}", c => HelperRules.Apply(c, edit)));
    }

    /// <summary>What a <c>commits</c> proposal's words may say, in the refusal that names the shape.</summary>
    private const string CommitsUsage =
        "`commits` is `--mode as-you-go|ask|at-end` and/or `--convention \"<your words>\"` (one paragraph, at most 400 characters) or "
        + "`--no-convention`, or `--clear` — e.g. `daoris driver commits web-app --mode ask --convention \"Imperative subject line.\"`.";

    /// <summary>
    /// <c>commits</c> (DOORS1a; D164's amendment points 1 and 5, PROCESS1a), as <c>daoris driver commits</c> reads its words:
    /// <c>--mode</c> and <c>--convention</c> set over the rule set there, <c>--no-convention</c> the empty convention that takes
    /// the standing one away, or <c>--clear</c>, for a repository or a whole workspace, a quoted convention kept whole. Judged by
    /// the twin's table (<see cref="CommitRules.Apply"/>); the card says how work is then committed, in the terminal's sentences,
    /// and quotes the convention as it will be kept, which may be seeded from the repository's own instructions but is the
    /// person's once they apply it (D164's amendment point 5).
    /// </summary>
    private static (string? Refusal, (string Describe, string Terminal, Func<DriverConfig, DriverConfig> Edit) Planned) Commits(
        string? target, string? workspace, string value, DriverConfig config)
    {
        var circle = workspace is { Length: > 0 };
        if ((target is { Length: > 0 }) == circle) return (CommitRules.Scope, default);
        if (Words(value) is not { } words) return ($"`commits`' words close every quote they open — {CommitsUsage}", default);
        if (RuleWords(words, "commits", CommitsUsage, ["--mode", "--convention"], ["--no-convention", "--clear"], out var flags, out var switches)
            is { } wrong)
        {
            return (wrong, default);
        }

        var unwritten = switches.Contains("--no-convention");
        if (unwritten && flags.ContainsKey("--convention"))
        {
            return ("a rule keeps a convention or takes it away — say `--convention \"<your words>\"` or `--no-convention`, not both.", default);
        }

        var set = new CommitSet
        {
            ModeGiven = flags.ContainsKey("--mode"),
            Mode = flags.GetValueOrDefault("--mode"),
            ConventionGiven = unwritten || flags.ContainsKey("--convention"),
            Convention = unwritten ? "" : flags.GetValueOrDefault("--convention"),
        };
        var clear = switches.Contains("--clear");
        if (!clear && !set.Names) return (CommitsUsage, default);

        var edit = new CommitEdit
        {
            Repository = circle ? null : target,
            Workspace = circle ? workspace : null,
            Set = set.Names ? set : null,
            Clear = clear,
        };
        DriverConfig after;
        try
        {
            after = CommitRules.Apply(config, edit);
        }
        catch (DriverException refused)
        {
            return (refused.Message, default);
        }

        var whose = circle ? $"each repository of workspace `{workspace}` that sets none of its own" : $"`{target}`";
        var describe = (circle ? after.WorkspaceCommits : after.Commits).TryGetValue((circle ? workspace : target)!, out var rule)
            ? $"Set the commit rule for {whose}: `{rule.Mode}`. " + rule.Mode switch
            {
                CommitRules.Ask => "Each commit waits for your yes before it is made.",
                CommitRules.AtEnd => "One commit is made when the work is done.",
                _ => "Each piece that passes its checks is committed as the work goes.",
            } + (rule.Convention is { } convention ? $" Commit messages follow this convention: \"{convention}\"" : "")
            : circle
                ? $"Clear workspace `{workspace}`'s commit rule: each repository there keeps its own, else work is committed as it goes."
                : $"Clear `{target}`'s commit rule: it takes its workspace's again, else work is committed as it goes.";
        var scope = circle ? $"--workspace {ShellWord.Of(workspace!, ShellWord.Workspace)}" : target!;
        return (null, (describe, $"daoris driver commits {scope} {value}", c => CommitRules.Apply(c, edit)));
    }

    /// <summary>
    /// A rule door's words read as its terminal verb reads them: each of <paramref name="valued"/> with the word after it, each
    /// of <paramref name="named"/>, and nothing else — refused in a sentence that names the shape.
    /// </summary>
    private static string? RuleWords(List<string> words, string door, string usage, string[] valued, string[] named,
        out Dictionary<string, string> flags, out HashSet<string> switches)
    {
        flags = new Dictionary<string, string>(StringComparer.Ordinal);
        switches = new HashSet<string>(StringComparer.Ordinal);
        for (var at = 0; at < words.Count; at++)
        {
            var word = words[at];
            if (valued.Contains(word))
            {
                if (at + 1 >= words.Count || words[at + 1].StartsWith("--", StringComparison.Ordinal)) return $"`{word}` needs its value — {usage}";
                flags[word] = words[++at];
            }
            else if (named.Contains(word)) switches.Add(word);
            else return $"`{word}` is not a word `{door}` takes — {usage}";
        }

        return null;
    }

    /// <summary>
    /// A proposal's words split as a terminal splits them: at spaces, a double-quoted run kept whole as one word, <c>\"</c>
    /// inside it a quote. Null where a quote is left open.
    /// </summary>
    private static List<string>? Words(string value)
    {
        var words = new List<string>();
        var word = new System.Text.StringBuilder();
        var quoted = false;
        var held = false;
        for (var at = 0; at < value.Length; at++)
        {
            var c = value[at];
            if (quoted)
            {
                if (c == '\\' && at + 1 < value.Length && value[at + 1] == '"')
                {
                    word.Append('"');
                    at++;
                }
                else if (c == '"') quoted = false;
                else word.Append(c);
            }
            else if (c == '"')
            {
                quoted = true;
                held = true;
            }
            else if (char.IsWhiteSpace(c))
            {
                if (word.Length > 0 || held) words.Add(word.ToString());
                word.Clear();
                held = false;
            }
            else word.Append(c);
        }

        if (quoted) return null;
        if (word.Length > 0 || held) words.Add(word.ToString());
        return words;
    }

    public Task<HelpApplied> ApplyAsync(HelpApplying applying, CancellationToken ct)
    {
        applying.Doors.Change(applying.Plan.Apply!);
        return Task.FromResult(applying.Settled(true, $"Applied: `#{applying.Id}` — {applying.Plan.Describe} (`{applying.Plan.Terminal}`)", null));
    }
}

public interface IHelpSettingDoors
{
    /// <summary>An edit to the driver's file, as the `SET_*` routes and `RETRY_QUEST` make it.</summary>
    void Change(Func<DriverConfig, DriverConfig> edit);
}

