using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// The grammar of <c>role rules</c> and its twin on Ask Daoris's card: one command writes a place's rules whole, each flag naming the
/// roles it reaches after an <c>@</c> (<c>--set answers-alone=3:hold@build,design</c>), so the card's twin is the one judged write the
/// card was judged as, never a chain of commands judged one by one.
/// </summary>
internal static class SupervisorRulesFlags
{
    public const string Grammar =
        "`role rules --place machine|workspace:<id>|repository:<id> [--set <count>=<at>:<do>[:<minutes>][@<id,…>]]… "
        + "[--off <count>[@<id,…>]]… [--clear <count>[@<id,…>]|all]` (counts: answers-alone, failures, failures-on-choice, tries, quiet, limits, moves; "
        + "`role supervisor [--place <place>]` says them)";

    /// <summary>The stored rules with each flag laid over them in order; one rule per count and roles, never a ladder.</summary>
    public static List<SupervisorRule> Lay(IReadOnlyList<SupervisorRule> stored, IEnumerable<(string Flag, string Value)> steps, string place)
    {
        var rules = stored.ToList();
        foreach (var (flag, value) in steps)
        {
            if (flag == "--clear" && value == "all") { rules.Clear(); continue; }
            var (body, roles) = Split(value);
            var count = flag == "--set" ? body.Split('=', 2)[0] : body;
            rules.RemoveAll(rule => rule.Count == count && Same(rule.Roles, roles));
            if (flag == "--off") rules.Add(new SupervisorRule("", count, SupervisorRules.Windows.GetValueOrDefault(count, ""), 1, SupervisorRules.Off, null, place, roles));
            if (flag != "--set") continue;
            var step = body.Split('=', 2) is [_, var spec] ? spec.Trim() : throw new ChoiceWords($"`--set {value}` names no `<at>:<do>`: {Grammar}.");
            var parts = step.Split(':');
            if (step.Contains(',', StringComparison.Ordinal) || parts.Length is < 2 or > 3
                || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var threshold))
            {
                throw new ChoiceWords($"`{step}` is not one `<at>:<do>[:<minutes>]`: {Grammar}.");
            }

            int? upTo = parts.Length == 3 && int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes)
                ? minutes
                : parts.Length == 3 ? throw new ChoiceWords($"`{parts[2]}` is not whole minutes: {Grammar}.") : null;
            rules.Add(new SupervisorRule("", count, SupervisorRules.Windows.GetValueOrDefault(count, ""), threshold, parts[1], upTo, place, roles));
        }

        return rules;
    }

    /// <summary>The one command that writes <paramref name="press"/>'s rules over whatever the place holds: its place cleared, then each rule.</summary>
    public static string Terminal(SupervisorRulesPress press)
    {
        var said = new System.Text.StringBuilder($"daoris role rules --place {press.Place} --clear all");
        foreach (var rule in press.Rules)
        {
            var roles = rule.Roles is { Count: > 0 } named ? "@" + string.Join(",", named) : "";
            said.Append(rule.Do == SupervisorRules.Off
                ? $" --off {rule.Count}{roles}"
                : $" --set {rule.Count}={rule.At}:{rule.Do}{(rule.UpTo is { } upTo ? $":{upTo}" : "")}{roles}");
        }

        return said.ToString();
    }

    private static (string Body, IReadOnlyList<string>? Roles) Split(string value) =>
        value.Split('@', 2) is [var body, var roles]
            ? (body, [.. roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)])
            : (value, null);

    private static bool Same(IReadOnlyList<string>? held, IReadOnlyList<string>? given) =>
        held is null ? given is null : given is not null && held.Order().SequenceEqual(given.Order());
}
