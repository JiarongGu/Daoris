using System.Globalization;
using static Daoris.Knowledge.HelpProposalFile;

namespace Daoris.Knowledge;

/// <summary>A setting Ask Daoris proposes (HELP1c, D89): one of the driver's doors, spelled as the CLI's verbs are.</summary>
/// <param name="Door">One of <see cref="HelpSettingProposalWriter.Doors"/>: `drive`, `undrive`, `hold`, `resume`, `trees`, `line`, `landing`, `across`, `standing`, `language`, `review`, `opinion`, `helpers`, `commits`, `intake`, `helper`, `strikes`, `retry`, `timeout`, `notify`, `cap`, `adapter`, `cooloff` or `keep`.</param>
/// <param name="Target">The repository, for the doors that take one; for `retry`, the quest its failed sessions parked or the person's stop holds.</param>
/// <param name="Workspace">The workspace, for a line, a landing, a session language, a review rule, a second-opinion rule, a helpers rule, a commit rule or reading across set for a whole workspace.</param>
/// <param name="Value">What it is set to, as the CLI takes it: `on`, a branch, `branch &lt;pattern&gt; --tidy`, `read off`, `write-to &lt;other&gt;`, `zh`, `--mode ask`, an agent, `set-aside 30`…</param>
public sealed record SettingChange(string Door, string? Target, string? Workspace, string? Value);

/// <summary>The <c>setting</c> kind's writer (HELP1c): one of the driver's doors, shape-checked as the CLI's verbs take it.</summary>
internal sealed class HelpSettingProposalWriter(HelpProposalFile file)
{
    /// <summary>
    /// The doors a setting may name, as the CLI's verbs spell them, in the order the driver's <c>HelpSettingProposals</c>
    /// lists them — every <c>daoris driver</c> verb but <c>list</c> (HELP9, D110; <c>retry</c> since HELP10; <c>standing</c>
    /// since KNOWUSE1b; <c>language</c> since LANG1c2; <c>review</c> since REVIEWENV1a; <c>opinion</c> since XAGENT1a; <c>cooloff</c> since TOOL4l-b;
    /// <c>helpers</c> and <c>commits</c> since DOORS1a; <c>keep</c> since KEEP1d).
    /// </summary>
    public static readonly IReadOnlyList<string> Doors =
    [
        "drive", "undrive", "hold", "resume", "trees", "line", "landing", "across", "standing", "language", "review", "opinion",
        "helpers", "commits", "intake", "helper", "strikes", "retry", "timeout", "notify", "cap", "adapter", "cooloff", "keep",
    ];

    /// <summary>The flags a review's words may carry, as <c>daoris driver review</c> takes them (REVIEWENV1a).</summary>
    private static readonly IReadOnlyList<string> ReviewFlags =
        ["--kind", "--procedure", "--address", "--run", "--drop", "--clear", "--required", "--not-required"];

    /// <summary>The flags a second opinion's words may carry, as <c>daoris driver opinion</c> takes them (XAGENT1a).</summary>
    private static readonly IReadOnlyList<string> OpinionFlags =
    [
        "--reviewers", "--on", "--minutes", "--required", "--not-required", "--verify", "--no-verify", "--recheck", "--no-recheck",
        "--clear",
    ];

    /// <summary>The flags a helpers rule's words may carry, as <c>daoris driver helpers</c> takes them (DOORS1a).</summary>
    private static readonly IReadOnlyList<string> HelpersFlags = ["--mode", "--agents", "--clear"];

    /// <summary>The flags a commit rule's words may carry, as <c>daoris driver commits</c> takes them (DOORS1a).</summary>
    private static readonly IReadOnlyList<string> CommitsFlags = ["--mode", "--convention", "--no-convention", "--clear"];

    /// <summary>The most characters a standing answer holds — the driver's <c>DriverConfig.StandingLimit</c>, a deliberate copy.</summary>
    private const int StandingLimit = 2_000;

    /// <summary>
    /// The codes a session language takes (LANG1c2, D142 point 7) — the driver's <c>SessionLanguages.Table</c>, a deliberate
    /// copy, which the driver's <c>HelpSettingProposalsTests</c> holds to its own, code for code.
    /// </summary>
    private static readonly IReadOnlyList<string> Languages = ["en", "zh"];

    /// <summary>Why a setting is no door's shape, or null when it is one.</summary>
    public static string? Refusal(SettingChange change)
    {
        var door = change.Door;
        if (!Doors.Contains(door)) return $"`{door}` is not a door — one of {string.Join(", ", Doors.Select(d => $"`{d}`"))}.";

        var value = change.Value?.Trim();
        var named = !string.IsNullOrWhiteSpace(change.Target);
        var circle = !string.IsNullOrWhiteSpace(change.Workspace);
        switch (door)
        {
            case "drive" or "undrive" or "hold" or "resume":
                return named ? null : $"`{door}` names the repository it is for.";
            case "trees":
                if (!named) return "`trees` names the repository it is for.";
                return value is "on" or "off" ? null : "`trees` is set `on` or `off`.";
            case "line" or "landing":
                if (named == circle) return $"a {door} is set for a repository or a workspace — name exactly one.";
                if (door == "line") return string.IsNullOrWhiteSpace(value) ? "a line is a branch, or `--clear`." : null;
                // LANDSVC1: the sentence names a branch rule's switches as the room and the terminal do, `--auto-accept`
                // (LAND2a) among them, and since DOORS1a the ticket's key and the squash (PROCESS1b-i, -ii); a branch's words
                // past its pattern are the driver's to judge.
                return value is "merge" or "--clear" or "merge --tidy" || (value?.StartsWith("branch ", StringComparison.Ordinal) ?? false)
                    ? null
                    : "a landing is `merge`, `branch <pattern>` (with `--tidy` to remove the tree once landed, `--plugin <id>` for an "
                      + "installed plugin that pushes it and opens the pull request, `--auto-accept` for a quest's done to land it "
                      + "with no press, `--key <shape>` for the shape `{ticket}` is found by, and `--squash` for the pull request's "
                      + "branch as one commit), or `--clear`.";
            case "intake" or "helper":
                return string.IsNullOrWhiteSpace(value) ? $"`{door}` is set to an agent, or `off`." : null;
            case "strikes":
                return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out _) ? null : "`strikes` is a whole number, 0 or more.";
            case "retry":
                // HELP10: the quest by id, as `daoris driver retry <quest>` takes it; whether it is parked, or held by the
                // person's stop (SESSUX1b), is the driver's to judge, so the box takes either id unchanged.
                return !named || circle || !string.IsNullOrWhiteSpace(value) || Word(change.Target!.Trim(), "a quest") is not null
                    ? "`retry` names the quest its failed sessions parked or the person's stop holds, by id, as the target — and nothing else."
                    : null;
            case "timeout":
                return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) && minutes >= 1
                    ? null
                    : "`timeout` is a whole number of minutes, 1 or more.";
            case "across":
                return AcrossShape(named, circle, value);
            case "standing":
                // KNOWUSE1b (D135 §3): one repository's standing answer, in the person's words, as `daoris driver standing` takes it.
                if (!named || circle) return "a standing answer names the repository it holds for, as the target, and no workspace.";
                if (string.IsNullOrWhiteSpace(value)) return "`standing` is set to the person's words, or `--clear`.";
                return value.Length > StandingLimit ? $"a standing answer is at most {StandingLimit} characters of the person's words." : null;
            case "language":
                // LANG1c2 (D142 point 7): the work's session language, as `daoris driver language` takes it — a code of the
                // table in any case, as the driver reads one, or `--clear`; whether the repository or the workspace is one
                // this machine holds is the driver's to judge.
                if (named == circle) return "a session language is set for a repository or a workspace — name exactly one.";
                return value is not null && (value == "--clear" || Languages.Contains(value.ToLowerInvariant()))
                    ? null
                    : $"`language` is set to {string.Join(" or ", Languages.Select(code => $"`{code}`"))}, or `--clear`.";
            case "review":
                // REVIEWENV1a (D154 point 2): where work is shown to the person before it lands, as `daoris driver review` takes
                // its words; the rule itself, the registry and the procedure are the driver's to judge, in the twins' words.
                if (named == circle) return "a review rule is set for a repository or a workspace — name exactly one.";
                // A quoted run, a command or a path with spaces, is one word whatever it holds.
                return string.IsNullOrWhiteSpace(value) || Flags(value).Any(word => !ReviewFlags.Contains(word))
                    ? "a review is `<environment> --kind local|deployed --procedure <path> [--address <url>] [--run \"<command>\"] "
                      + "[--required|--not-required]`, `none`, `--drop <environment>`, `--required|--not-required` or `--clear`."
                    : null;
            case "opinion":
                // XAGENT1a (D155 point 3): which other agent reads work before it lands, as `daoris driver opinion` takes its
                // words; the rule itself, its reviewers and the registry are the driver's to judge, in the twins' words.
                if (named == circle) return "a second-opinion rule is set for a repository or a workspace — name exactly one.";
                return string.IsNullOrWhiteSpace(value) || Flags(value).Any(word => !OpinionFlags.Contains(word))
                    ? "an opinion is `--reviewers <adapter,adapter> [--on landing,steps] [--required|--not-required] "
                      + "[--verify|--no-verify] [--minutes <n>] [--recheck|--no-recheck]`, `none` or `--clear`."
                    : null;
            case "helpers":
                // DOORS1a (D163 point 3): what a helper an agent asks for does, as `daoris driver helpers` takes its words; the rule,
                // its agents and the registry are the driver's to judge, in the twins' words.
                if (named == circle) return "a helpers rule is set for a repository or a workspace — name exactly one.";
                return string.IsNullOrWhiteSpace(value) || Flags(value).Any(word => !HelpersFlags.Contains(word))
                    ? "`helpers` is `--mode ask|on|off` and/or `--agents <adapter,adapter>`, or `--clear`."
                    : null;
            case "commits":
                // DOORS1a (D164's amendment point 1): how work is committed, as `daoris driver commits` takes its words, a quoted
                // convention one word whatever it holds; the rule and the registry are the driver's to judge.
                if (named == circle) return "a commit rule is set for a repository or a workspace — name exactly one.";
                return string.IsNullOrWhiteSpace(value) || Flags(value).Any(word => !CommitsFlags.Contains(word))
                    ? "`commits` is `--mode as-you-go|ask|at-end` and/or `--convention \"<words>\"` or `--no-convention`, or `--clear`."
                    : null;
            case "cap":
                return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var cap) && cap >= 1
                    ? null
                    : "`cap` is a whole number, 1 or more: how many sessions run at once.";
            case "cooloff":
                // TOOL4l-b (D125 §6): the default cool-off, as `daoris driver cooloff` takes it; under a minute would spin.
                return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var cool) && cool >= 1
                    ? null
                    : "`cooloff` is a whole number of minutes, 1 or more: how long an account cools when its limit names no time.";
            case "keep":
            {
                // KEEP1d (D195 point 2): one kind's keep rule, as `daoris driver keep <kind> <rule>` takes it; its bounds are the
                // driver's to judge, which knows them. History's age since KEEP1g, and the free-space floor since KEEP1f, applied by the
                // person's press as every door is.
                var words = (value ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
                return !named && !circle && words.Length == 2 && words[0] is "set-aside" or "downloads" or "logs" or "history" or "floor"
                    ? null
                    : "`keep` names a kind and its rule, for this machine: `set-aside <days>|whole|slim|default`, "
                      + "`downloads <days>|never|default`, `logs <days>|default`, `history <days>|never|default` or `floor <n>GB|<n>%|off|default`.";
            }
            case "adapter":
                return string.IsNullOrWhiteSpace(value) || Word(value, "an agent") is not null
                    ? "`adapter` is set to an agent, one word, as `daoris agent list` names it."
                    : null;
            default: // notify
                return value is "on" or "off" ? null : "`notify` is set `on` or `off`.";
        }
    }

    /// <summary>The flags in a door's words, a quoted run, a command or a list with spaces, being one word whatever it holds.</summary>
    private static IEnumerable<string> Flags(string value) =>
        System.Text.RegularExpressions.Regex.Replace(value, "\"(?:[^\"\\\\]|\\\\.)*\"", "quoted")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word.StartsWith("--", StringComparison.Ordinal));

    /// <summary>
    /// <c>across</c>'s shape (HELP9, D107), as <c>daoris driver across</c> takes its words: <c>read on|off|--clear</c>
    /// for a repository or a workspace, or <c>write-to &lt;other&gt; [--clear]</c> from a repository.
    /// </summary>
    private static string? AcrossShape(bool named, bool circle, string? value)
    {
        var words = (value ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        var clear = words.Remove("--clear");
        switch (words.FirstOrDefault())
        {
            case "read":
            {
                if (named == circle) return "reading across is set for a repository or a workspace — name exactly one.";
                var sound = clear ? words.Count == 1 : words.Count == 2 && words[1] is "on" or "off";
                return sound ? null : "`across … read` is `read on|off|--clear`.";
            }
            case "write-to":
                if (circle || !named) return "a relationship is declared from one repository — name it as the target, and no workspace.";
                return words.Count == 2 ? null : "`across … write-to` names one repository: `write-to <other>`, with `--clear` to take it back.";
            default:
                return "`across` is `read on|off|--clear` or `write-to <other> [--clear]`.";
        }
    }

    /// <summary>Write one setting proposal, or refuse it — answered as the sentence the agent reads.</summary>
    public (string? Id, string Message) Propose(SettingChange change, string why, string? session, DateTimeOffset at)
    {
        var normal = change with { Door = change.Door.Trim().ToLowerInvariant(), Value = change.Value?.Trim() };
        if (string.IsNullOrWhiteSpace(why)) return (null, "A proposal needs its reason: what the person asked, and what the change would do. Nothing was proposed.");
        if (Refusal(normal) is { } refused) return (null, $"{Capital(refused)} Nothing was proposed.");
        return file.Write(writer =>
        {
            writer.WriteString("kind", "setting");
            writer.WriteString("door", normal.Door);
            Nullable(writer, "target", Blank(normal.Target));
            Nullable(writer, "workspace", Blank(normal.Workspace));
            Nullable(writer, "value", Blank(normal.Value));
            writer.WriteNull("sentence");
        }, why, session, at);
    }
}
