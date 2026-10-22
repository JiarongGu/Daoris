using static Daoris.Knowledge.HelpProposalFile;

namespace Daoris.Knowledge;

/// <summary>
/// The <c>agent</c> kind's writer (HELP6): an agent's Update, or a pin to one exact version, and how its accounts are used, ordered and
/// offered again (TOOL4l-a). Since POOL1b (D197 point 6, the account-pool design §5.3) its accounts are one set: a <c>use</c> or an
/// <c>order</c> is written with <c>set: true</c>, the mark the driver applies only, and names no workspace; HELP10's <c>default</c> left.
/// </summary>
internal sealed class HelpAgentProposalWriter(HelpProposalFile file)
{
    /// <summary>The doors this writes, the driver's <c>HelpAgentProposals.Doors</c> exactly (TOOL4l-b; the kinds test holds both ways).</summary>
    public static readonly IReadOnlyList<string> Doors = ["update", "pin", "use", "order", "ready"];

    /// <summary>
    /// Write one agent proposal (HELP6): <c>update</c> to its newest release, or <c>pin</c> to one exact
    /// version — the Agents screen's two presses, which the driver judges by that door's roster — or <c>use</c>, <c>order</c> or
    /// <c>ready</c> on the agent's one set.
    /// </summary>
    /// <param name="account">For <c>ready</c>, the account whose cool-off ends (or <c>own</c>), written as <c>account</c>.</param>
    /// <param name="use">For <c>use</c> (TOOL4l-a): <c>goal</c> or <c>order</c>, the set's way of choosing.</param>
    /// <param name="keep">For <c>use</c>: the account kept; <paramref name="noKeep"/> keeps none, and neither leaves it as it is.</param>
    /// <param name="early">For <c>use</c>, the old name (D165 §6): switch before the limit, or not; the driver reads it on.</param>
    /// <param name="near">For <c>use</c>, the old name: the person's curve's five-hour window flat at this percent.</param>
    /// <param name="accounts">For <c>order</c>: the accounts put first, in order; the rest follow in theirs.</param>
    /// <param name="clear">For <c>order</c>: the order goes back to the accounts' ids.</param>
    /// <param name="switched">For <c>use</c> (SWITCH1d): <c>smart</c>, <c>curve</c> or <c>off</c>, when the scope switches accounts.</param>
    /// <param name="curve">For <c>use</c>: the person's points, one window each, spelled as the terminal's <c>--curve</c>
    /// (<c>weekly=0:30,50:70,100:100</c>); the driver judges the curve's rules.</param>
    /// <param name="noCurve">For <c>use</c>: the person's curve taken out, written as a curve of null.</param>
    public (string? Id, string Message) Propose(
        string action, string agent, string? version, string why, string? session, DateTimeOffset at,
        string? account = null,
        string? use = null, string? keep = null, bool noKeep = false, bool? early = null, int? near = null,
        IReadOnlyList<string>? accounts = null, bool clear = false,
        string? switched = null, IReadOnlyList<string>? curve = null, bool noCurve = false)
    {
        var door = action.Trim().ToLowerInvariant();
        var named = Blank(agent);
        var release = Blank(version);
        var whose = Blank(account);
        var way = Blank(use);
        var kept = Blank(keep);
        var list = accounts?.Select(Blank).ToList() ?? [];
        var when = Blank(switched);
        var windows = curve?.Where(each => !string.IsNullOrWhiteSpace(each)).Select(each => each.Trim()).ToList() ?? [];
        var points = windows.Select(CurveWindow).ToList();
        if (string.IsNullOrWhiteSpace(why)) return NoReason;
        var usesAny = way is not null || kept is not null || noKeep || early is not null || near is not null
            || when is not null || windows.Count > 0 || noCurve;
        // POOL1b (D197 point 6): `default` left; the order says where starts begin, and the set serves every workspace.
        var refused = door == "default"
            ? "`default` is retired: every account of Daoris's serves every workspace (D197), and the order says where starts begin — "
              + "propose `order` with the account first."
            : !Doors.Contains(door)
            ? "an agent's change is `update`, `pin`, `use`, `order` or `ready`: update moves it to its newest release, "
              + "pin to one exact version, use sets how its accounts are used, order puts accounts first, "
              + "and ready ends an account's cool-off."
            : named is null ? "the change names the agent it is for, as `daoris agent` spells it."
            : door == "pin" && release is null ? "`pin` names the version: one exact release, such as 2.1.300."
            : door != "pin" && release is not null ? $"`{door}` names no version — only `pin` names one."
            : door == "ready" && whose is null ? "`ready` names the account whose cool-off ends, or `own` for the tool's own sign-in."
            : door != "ready" && whose is not null ? $"`{door}` names no account — only `ready` names one."
            : door != "use" && usesAny ? $"`{door}` takes no use, keep, switch or curve — only `use` does."
            : door != "order" && (list.Count > 0 || clear) ? $"`{door}` takes no accounts list or clear — only `order` does."
            : door == "use" && !usesAny
                ? "`use` names what changes: `use` (goal or order), `keep` (an account, or none), `switch` (smart, curve or off) or "
                  + "`curve` (your own points, a window each)."
            : door == "use" && way is not (null or "goal" or "order") ? $"a way to use accounts is `goal` or `order`, not `{way}`."
            : door == "use" && kept is not null && noKeep ? "`use` can keep one account or none, not both."
            : door == "use" && near is not null and (< 50 or > 99) ? $"near is a whole percent from 50 to 99, not {near}."
            : door == "use" && when is not (null or "smart" or "curve" or "off") ? $"when to switch accounts is `smart`, `curve` or `off`, not `{when}`."
            : door == "use" && windows.Count > 0 && noCurve ? "`use` sets a curve or no curve, not both."
            : door == "use" && points.FindIndex(each => each is null) is var bad and >= 0
                ? $"`{windows[bad]}` is not a window's curve: a curve is `<window>=<elapsed>:<used>,…`, a window each, such as "
                  + "`weekly=0:30,50:70,100:100`."
            : door == "order" && (list.Count > 0) == clear
                ? (clear ? "`order` puts accounts first or returns to the ids' order, never both." : "`order` names the accounts to put first, in order, or clears it back to the ids' order.")
            : door == "order" && list.Any(each => each is null) ? "an account of the list is not blank."
            : Word(named, "an agent") ?? (release is null ? null : Word(release, "a version"))
              ?? (whose is null ? null : Word(whose, "an account"))
              ?? (way is null ? null : Word(way, "a way to use accounts")) ?? (kept is null ? null : Word(kept, "an account"))
              ?? list.Select(each => Word(each!, "an account")).FirstOrDefault(each => each is not null);
        if (refused is not null) return (null, $"{Capital(refused)} Nothing was proposed.");

        return file.Write(writer =>
        {
            writer.WriteString("kind", "agent");
            writer.WriteString("door", door);
            writer.WriteString("target", named);
            // The set serves every workspace (POOL1b), so a proposal names none.
            writer.WriteNull("workspace");
            // A pin's version: what the change sets.
            Nullable(writer, "value", release);
            // POOL1b (§5.3): a use or an order is written for the one set, and the driver applies only one so marked.
            if (door is "use" or "order") writer.WriteBoolean("set", true);
            // The use, order and ready doors' own fields (TOOL4l-a): a field left out is no change, and keep none is a null.
            if (door == "ready") writer.WriteString("account", whose);
            if (way is not null) writer.WriteString("use", way);
            if (kept is not null) writer.WriteString("keep", kept);
            else if (noKeep) writer.WriteNull("keep");
            if (early is { } flag) writer.WriteBoolean("early", flag);
            if (near is { } percent) writer.WriteNumber("near", percent);
            // SWITCH1d: the switch, and the person's curve as `harnesses.json` keeps it, or null for none.
            if (when is not null) writer.WriteString("switch", when);
            if (noCurve) writer.WriteNull("curve");
            else if (points.Count > 0)
            {
                writer.WriteStartObject("curve");
                foreach (var (window, set) in points.Select(each => each!.Value))
                {
                    writer.WriteStartArray(window);
                    foreach (var (elapsed, used) in set)
                    {
                        writer.WriteStartArray();
                        writer.WriteNumberValue(elapsed);
                        writer.WriteNumberValue(used);
                        writer.WriteEndArray();
                    }

                    writer.WriteEndArray();
                }

                writer.WriteEndObject();
            }
            if (list.Count > 0)
            {
                writer.WriteStartArray("accounts");
                foreach (var each in list) writer.WriteStringValue(each);
                writer.WriteEndArray();
            }

            if (clear) writer.WriteBoolean("clear", true);
            writer.WriteNull("sentence");
        }, why, session, at);
    }

    /// <summary>
    /// One window of a curve as the terminal's <c>--curve</c> spells it, <c>&lt;window&gt;=&lt;elapsed&gt;:&lt;used&gt;,…</c>, each
    /// number whole; null where it is not so spelled. The curve's rules (its windows, ranges and order) are the driver's to judge.
    /// </summary>
    private static (string Window, IReadOnlyList<(long Elapsed, long Used)> Points)? CurveWindow(string spelled)
    {
        var at = spelled.IndexOf('=');
        if (at <= 0) return null;
        var points = new List<(long, long)>();
        foreach (var point in spelled[(at + 1)..].Split(','))
        {
            var parts = point.Split(':');
            if (parts.Length != 2
                || !long.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out var elapsed)
                || !long.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out var used))
            {
                return null;
            }

            points.Add((elapsed, used));
        }

        return (spelled[..at].Trim(), points);
    }
}
