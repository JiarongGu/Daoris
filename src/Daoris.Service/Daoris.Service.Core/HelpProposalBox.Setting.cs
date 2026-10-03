using System.Globalization;

namespace Daoris.Knowledge;

/// <summary>A setting Ask Daoris proposes (HELP1c, D89): one of the driver's doors, spelled as the CLI's verbs are.</summary>
/// <param name="Door">One of <see cref="HelpProposalBox.Doors"/>: `drive`, `undrive`, `hold`, `resume`, `trees`, `line`, `landing`, `across`, `intake`, `helper`, `strikes`, `retry`, `timeout`, `notify`, `cap` or `adapter`.</param>
/// <param name="Target">The repository, for the doors that take one; for `retry`, the quest its failed sessions parked or the person's stop holds.</param>
/// <param name="Workspace">The workspace, for a line, a landing or reading across set for a whole workspace.</param>
/// <param name="Value">What it is set to, as the CLI takes it: `on`, a branch, `branch &lt;pattern&gt; --tidy`, `read off`, `write-to &lt;other&gt;`, an agent…</param>
public sealed record SettingChange(string Door, string? Target, string? Workspace, string? Value);

/// <summary>The <c>setting</c> kind's writer (HELP1c): one of the driver's doors, shape-checked as the CLI's verbs take it.</summary>
public sealed partial class HelpProposalBox
{
    /// <summary>
    /// The doors a setting may name, as the CLI's verbs spell them, in the order the driver's <c>HelpSettingProposals</c>
    /// lists them — every <c>daoris driver</c> verb but <c>list</c> (HELP9, D110; <c>retry</c> since HELP10; <c>standing</c>
    /// since KNOWUSE1b).
    /// </summary>
    public static readonly IReadOnlyList<string> Doors =
    [
        "drive", "undrive", "hold", "resume", "trees", "line", "landing", "across", "standing", "intake", "helper", "strikes",
        "retry", "timeout", "notify", "cap", "adapter",
    ];

    /// <summary>The most characters a standing answer holds — the driver's <c>DriverConfig.StandingLimit</c>, a deliberate copy.</summary>
    private const int StandingLimit = 2_000;

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
                return value is "merge" or "--clear" or "merge --tidy" || (value?.StartsWith("branch ", StringComparison.Ordinal) ?? false)
                    ? null
                    : "a landing is `merge`, `branch <pattern>` (with `--tidy` to remove the tree once landed, and `--plugin <id>` for an installed plugin that pushes it and opens the pull request), or `--clear`.";
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
            case "cap":
                return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var cap) && cap >= 1
                    ? null
                    : "`cap` is a whole number, 1 or more: how many sessions run at once.";
            case "adapter":
                return string.IsNullOrWhiteSpace(value) || Word(value, "an agent") is not null
                    ? "`adapter` is set to an agent, one word, as `daoris agent list` names it."
                    : null;
            default: // notify
                return value is "on" or "off" ? null : "`notify` is set `on` or `off`.";
        }
    }

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
    public (string? Id, string Message) ProposeSetting(SettingChange change, string why, string? session, DateTimeOffset at)
    {
        var normal = change with { Door = change.Door.Trim().ToLowerInvariant(), Value = change.Value?.Trim() };
        if (string.IsNullOrWhiteSpace(why)) return (null, "A proposal needs its reason: what the person asked, and what the change would do. Nothing was proposed.");
        if (Refusal(normal) is { } refused) return (null, $"{Capital(refused)} Nothing was proposed.");
        return Write(writer =>
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
