using static Daoris.Knowledge.HelpProposalFile;

namespace Daoris.Knowledge;

/// <summary>A setting Ask Daoris proposes: one of the driver's doors, spelled as the CLI's verbs are.</summary>
/// <param name="Door">One of <see cref="HelpSettingProposalWriter.Doors"/>: `review`, `opinion`, `helpers`, `commits`, `retry` or `git-use`.</param>
/// <param name="Target">The repository, for the doors that take one; for `retry`, the quest its failed sessions parked or the person's stop holds.</param>
/// <param name="Workspace">The workspace, for a review rule, a second-opinion rule, a helpers rule or a commit rule set for a whole workspace.</param>
/// <param name="Value">What it is set to, as the CLI takes it: `--mode ask`, `--agents claude-code,codex`, `none`, `--clear`…</param>
public sealed record SettingChange(string Door, string? Target, string? Workspace, string? Value);

/// <summary>The <c>setting</c> kind's writer: one of the driver's doors, shape-checked as the CLI's verbs take it.</summary>
internal sealed class HelpSettingProposalWriter(HelpProposalFile file)
{
    /// <summary>
    /// The doors a setting may name, as the CLI's verbs spell them, in the order the driver's <c>HelpSettingProposals</c> lists them.
    /// </summary>
    public static readonly IReadOnlyList<string> Doors = ["review", "opinion", "helpers", "commits", "retry", "git-use"];

    private static readonly IReadOnlyList<string> ReviewFlags =
        ["--kind", "--procedure", "--address", "--run", "--drop", "--clear", "--required", "--not-required"];

    /// <summary>The flags a second opinion's words may carry, as <c>daoris place opinion</c> takes them.</summary>
    private static readonly IReadOnlyList<string> OpinionFlags =
    [
        "--agents", "--on", "--minutes", "--required", "--not-required", "--verify", "--no-verify", "--recheck", "--no-recheck",
        "--clear",
    ];

    /// <summary>The flags a helpers rule's words may carry, as <c>daoris place helpers</c> takes them.</summary>
    private static readonly IReadOnlyList<string> HelpersFlags = ["--mode", "--agents", "--clear"];

    /// <summary>The flags a commit rule's words may carry, as <c>daoris place commits</c> takes them.</summary>
    private static readonly IReadOnlyList<string> CommitsFlags = ["--mode", "--convention", "--no-convention", "--clear"];

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
            case "retry":
                // The quest by id, as `daoris quest retry <quest>` takes it; whether it is parked, or held by the
                // person's stop, is the driver's to judge, so the box takes either id unchanged.
                return !named || circle || !string.IsNullOrWhiteSpace(value) || Word(change.Target!.Trim(), "a quest") is not null
                    ? "`retry` names the quest its failed sessions parked or the person's stop holds, by id, as the target — and nothing else."
                    : null;
            case "review":
                // Where work is shown to the person before it lands, as `daoris place review` takes
                // its words; the rule itself, the registry and the procedure are the driver's to judge, in the twins' words.
                if (named == circle) return "a review rule is set for a repository or a workspace — name exactly one.";
                // A quoted run, a command or a path with spaces, is one word whatever it holds.
                return string.IsNullOrWhiteSpace(value) || Flags(value).Any(word => !ReviewFlags.Contains(word))
                    ? "a review is `<environment> --kind local|deployed --procedure <path> [--address <url>] [--run \"<command>\"] "
                      + "[--required|--not-required]`, `none`, `--drop <environment>`, `--required|--not-required` or `--clear`."
                    : null;
            case "opinion":
                // Which other agent reads work before it lands, as `daoris place opinion` takes its
                // words; the rule itself, its reviewers and the registry are the driver's to judge, in the twins' words.
                if (named == circle) return "a second-opinion rule is set for a repository or a workspace — name exactly one.";
                return string.IsNullOrWhiteSpace(value) || Flags(value).Any(word => !OpinionFlags.Contains(word))
                    ? "an opinion is `--agents <adapter,adapter> [--on landing,steps] [--required|--not-required] "
                      + "[--verify|--no-verify] [--minutes <n>] [--recheck|--no-recheck]`, `none` or `--clear`."
                    : null;
            case "helpers":
                // What a helper an agent asks for does, as `daoris place helpers` takes its words; the rule,
                // its agents and the registry are the driver's to judge, in the twins' words.
                if (named == circle) return "a helpers rule is set for a repository or a workspace — name exactly one.";
                return string.IsNullOrWhiteSpace(value) || Flags(value).Any(word => !HelpersFlags.Contains(word))
                    ? "`helpers` is `--mode ask|on|off` and/or `--agents <adapter,adapter>`, or `--clear`."
                    : null;
            case "commits":
                // How work is committed, as `daoris place commits` takes its words, a quoted
                // convention one word whatever it holds; the rule and the registry are the driver's to judge.
                if (named == circle) return "a commit rule is set for a repository or a workspace — name exactly one.";
                return string.IsNullOrWhiteSpace(value) || Flags(value).Any(word => !CommitsFlags.Contains(word))
                    ? "`commits` is `--mode as-you-go|ask|at-end` and/or `--convention \"<words>\"` or `--no-convention`, or `--clear`."
                    : null;
            default: // git-use
            {
                // A repository's remote or a workspace's host pointed at an identity, as `daoris git use`
                // takes it; whether the identity exists, its host, and never `own`, are the driver's to judge.
                if (named == circle) return "a git identity is chosen for a repository's remote or a workspace's host — name exactly one.";
                var words = (value ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
                return words.Length > 0 && words.All(word => word != "--machine")
                    ? null
                    : "`git-use` is an identity's label or `--clear`: for a repository `[--remote <name>] <identity>|--clear`, for a workspace `--host <host> <identity>|--clear`.";
            }
        }
    }

    /// <summary>The flags in a door's words, a quoted run, a command or a list with spaces, being one word whatever it holds.</summary>
    private static IEnumerable<string> Flags(string value) =>
        System.Text.RegularExpressions.Regex.Replace(value, "\"(?:[^\"\\\\]|\\\\.)*\"", "quoted")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word.StartsWith("--", StringComparison.Ordinal));

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
