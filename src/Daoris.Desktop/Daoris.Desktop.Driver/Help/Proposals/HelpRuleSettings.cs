using System.Globalization;
using static Daoris.Driver.HelpSettingWords;

namespace Daoris.Driver;

/// <summary>
/// The rule doors of the <c>setting</c> proposal: <c>review</c>, <c>opinion</c>, <c>helpers</c> and <c>commits</c>, each a rule set
/// over a repository or a whole workspace, judged by the twin's table of the same name and said in the terminal's sentences.
/// </summary>
internal static class HelpRuleSettings
{
    /// <summary>What a <c>review</c> proposal's words may say, in the refusal that names the shape.</summary>
    private const string ReviewUsage =
        "`review` is `<environment> --kind local|deployed --procedure <path> [--address <url>] [--run \"<command>\"] "
        + "[--required|--not-required]`, `none`, `--drop <environment>`, `--required|--not-required` or `--clear` — e.g. "
        + "`daoris place review storefront dev --kind local --procedure README.md --address http://localhost:4200 --required`.";

    /// <summary>What an <c>opinion</c> proposal's words may say, in the refusal that names the shape.</summary>
    private const string OpinionUsage =
        "`opinion` is `--agents <adapter,adapter> [--on landing,steps] [--required|--not-required] [--verify|--no-verify] "
        + "[--minutes <n>] [--recheck|--no-recheck]`, `none` or `--clear` — e.g. "
        + "`daoris place opinion web-app --agents codex-acp --required`.";

    /// <summary>What a <c>helpers</c> proposal's words may say, in the refusal that names the shape.</summary>
    private const string HelpersUsage =
        "`helpers` is `--mode ask|on|off` and/or `--agents <adapter,adapter>`, or `--clear` — e.g. "
        + "`daoris place helpers web-app --mode on --agents codex-acp`.";

    /// <summary>What a <c>commits</c> proposal's words may say, in the refusal that names the shape.</summary>
    private const string CommitsUsage =
        "`commits` is `--mode as-you-go|ask|at-end` and/or `--convention \"<your words>\"` (one paragraph, at most 400 characters) or "
        + "`--no-convention`, or `--clear` — e.g. `daoris place commits web-app --mode ask --convention \"Imperative subject line.\"`.";

    /// <summary>
    /// <c>review</c>, as <c>daoris place review</c> reads its
    /// words: an environment added or replaced with its parts, <c>none</c>, <c>--drop</c>, <c>--required|--not-required</c>
    /// or <c>--clear</c>, for a repository or a whole workspace, a quoted word kept whole. Judged by the twin's table
    /// (<see cref="ReviewRules.Apply"/>), and an environment's procedure looked for in the checkouts it reaches, as the
    /// screen's route and the terminal look; the card says what the rule lets a step do, and what the landing's gate does with it
    /// (<see cref="ReviewRules.Gate"/>).
    /// </summary>
    public static (string? Refusal, (string Describe, string Terminal, Func<DriverConfig, DriverConfig> Edit) Planned) Review(
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
        return (null, (string.Join(" ", [head, .. said, .. notes]), $"daoris place review {scope} {value}", c => ReviewRules.Apply(c, edit)));
    }

    /// <summary>
    /// <c>opinion</c>, as <c>daoris place opinion</c> reads its words:
    /// <c>--agents</c> and the switches set over the rule set there, <c>none</c> or <c>--clear</c>, for a repository or a
    /// whole workspace, a quoted word kept whole. Judged by the twin's table (<see cref="OpinionRules.Apply"/>); the card says
    /// what the rule lets a reviewer do, names the reviewers of the working agent's own family over this machine's agents, a
    /// plugin's among them, and says what the gate does with it (<see cref="OpinionRules.Gate"/>).
    /// </summary>
    public static (string? Refusal, (string Describe, string Terminal, Func<DriverConfig, DriverConfig> Edit) Planned) Opinion(
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
            if (word is "--agents" or "--on" or "--minutes")
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
            Reviewers = Listed(flags.GetValueOrDefault("--agents")),
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
        // plugin's agent is judged by what its plugin declares, as the terminal and the reviewer's choice judge it.
        var adapters = ProviderRegistry.Built().WithPlugins(facts.Plugins);
        List<string> said = map.TryGetValue((circle ? workspace : target)!, out var standing)
            ? [.. OpinionRules.Says(standing, OpinionRules.SameAgent(standing, ActorChoices.BuildAgent(config), adapters)), .. OpinionRules.Gate(standing)]
            : [];
        var scope = circle ? $"--workspace {ShellWord.Of(workspace!, ShellWord.Workspace)}" : target!;
        return (null, (string.Join(" ", [head, .. said]), $"daoris place opinion {scope} {value}", c => OpinionRules.Apply(c, edit)));
    }

    /// <summary>
    /// <c>helpers</c>, as <c>daoris place helpers</c> reads its words: <c>--mode</c> and
    /// <c>--agents</c> set over the rule set there, or <c>--clear</c>, for a repository or a whole workspace, a quoted list kept
    /// whole. Judged by the twin's table (<see cref="HelperRules.Apply"/>); the card says the rule it leaves and what an agent's
    /// ask then does, in the terminal's sentences.
    /// </summary>
    public static (string? Refusal, (string Describe, string Terminal, Func<DriverConfig, DriverConfig> Edit) Planned) Helpers(
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
            ? $"Set the helpers rule for {whose}: {HelperRules.Listed(rule)}. " + HelperRules.Says(rule.Mode)
            : circle
                ? $"Clear workspace `{workspace}`'s helpers rule: each repository there keeps its own, else each helper waits for your yes."
                : $"Clear `{target}`'s helpers rule: it takes its workspace's again, else each helper waits for your yes.";
        var scope = circle ? $"--workspace {ShellWord.Of(workspace!, ShellWord.Workspace)}" : target!;
        return (null, (describe, $"daoris place helpers {scope} {value}", c => HelperRules.Apply(c, edit)));
    }

    /// <summary>
    /// <c>commits</c>, as <c>daoris place commits</c> reads its words:
    /// <c>--mode</c> and <c>--convention</c> set over the rule set there, <c>--no-convention</c> the empty convention that takes
    /// the standing one away, or <c>--clear</c>, for a repository or a whole workspace, a quoted convention kept whole. Judged by
    /// the twin's table (<see cref="CommitRules.Apply"/>); the card says how work is then committed, in the terminal's sentences,
    /// and quotes the convention as it will be kept, which may be seeded from the repository's own instructions but is the
    /// person's once they apply it.
    /// </summary>
    public static (string? Refusal, (string Describe, string Terminal, Func<DriverConfig, DriverConfig> Edit) Planned) Commits(
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
            ? $"Set the commit rule for {whose}: `{rule.Mode}`. " + CommitRules.Says(rule.Mode) + (rule.Convention is { } convention ? $" Commit messages follow this convention: \"{convention}\"" : "")
            : circle
                ? $"Clear workspace `{workspace}`'s commit rule: each repository there keeps its own, else work is committed as it goes."
                : $"Clear `{target}`'s commit rule: it takes its workspace's again, else work is committed as it goes.";
        var scope = circle ? $"--workspace {ShellWord.Of(workspace!, ShellWord.Workspace)}" : target!;
        return (null, (describe, $"daoris place commits {scope} {value}", c => CommitRules.Apply(c, edit)));
    }
}
