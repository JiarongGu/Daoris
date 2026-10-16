using static Daoris.Knowledge.HelpProposalFile;

namespace Daoris.Knowledge;

/// <summary>
/// The <c>agent</c> kind's writer (HELP6): an agent's Update, or a pin to one exact version — and since HELP10 the
/// account it runs as by default.
/// </summary>
internal sealed class HelpAgentProposalWriter(HelpProposalFile file)
{
    /// <summary>
    /// Write one agent proposal (HELP6): <c>update</c> to its newest release, or <c>pin</c> to one exact
    /// version — the Agents screen's two presses, which the driver judges by that door's roster. Since HELP10,
    /// <c>default</c>: the account it runs as by default, for the machine or one workspace — the screen's
    /// <i>Make default</i>, and <c>daoris agent profile default</c>.
    /// </summary>
    /// <param name="account">For <c>default</c>, the account, written as the proposal's value; for <c>ready</c>, the account whose cool-off ends (or <c>own</c>), written as <c>account</c>.</param>
    /// <param name="workspace">For <c>default</c>, <c>use</c> and <c>order</c>: the one workspace it is for; null for the machine.</param>
    /// <param name="use">For <c>use</c> (TOOL4l-a): <c>goal</c> or <c>order</c>, the scope's way of choosing.</param>
    /// <param name="keep">For <c>use</c>: the account kept; <paramref name="noKeep"/> keeps none, and neither leaves it as it is.</param>
    /// <param name="early">For <c>use</c>, the old name (D165 §6): switch before the limit, or not; the driver reads it on.</param>
    /// <param name="near">For <c>use</c>, the old name: the person's curve's five-hour window flat at this percent.</param>
    /// <param name="accounts">For <c>order</c>: the walk's accounts, in order.</param>
    /// <param name="clear">For <c>order</c>: the workspace goes back to the machine's accounts.</param>
    /// <param name="switched">For <c>use</c> (SWITCH1d): <c>smart</c>, <c>curve</c> or <c>off</c>, when the scope switches accounts.</param>
    /// <param name="curve">For <c>use</c>: the person's points, one window each, spelled as the terminal's <c>--curve</c>
    /// (<c>weekly=0:30,50:70,100:100</c>); the driver judges the curve's rules.</param>
    /// <param name="noCurve">For <c>use</c>: the person's curve taken out, written as a curve of null.</param>
    public (string? Id, string Message) Propose(
        string action, string agent, string? version, string why, string? session, DateTimeOffset at,
        string? account = null, string? workspace = null,
        string? use = null, string? keep = null, bool noKeep = false, bool? early = null, int? near = null,
        IReadOnlyList<string>? accounts = null, bool clear = false,
        string? switched = null, IReadOnlyList<string>? curve = null, bool noCurve = false)
    {
        var door = action.Trim().ToLowerInvariant();
        var named = Blank(agent);
        var release = Blank(version);
        var whose = Blank(account);
        var circle = Blank(workspace);
        var way = Blank(use);
        var kept = Blank(keep);
        var list = accounts?.Select(Blank).ToList() ?? [];
        var when = Blank(switched);
        var windows = curve?.Where(each => !string.IsNullOrWhiteSpace(each)).Select(each => each.Trim()).ToList() ?? [];
        var points = windows.Select(CurveWindow).ToList();
        if (string.IsNullOrWhiteSpace(why)) return NoReason;
        var usesAny = way is not null || kept is not null || noKeep || early is not null || near is not null
            || when is not null || windows.Count > 0 || noCurve;
        var refused = door is not ("update" or "pin" or "default" or "use" or "order" or "ready")
            ? "an agent's change is `update`, `pin`, `default`, `use`, `order` or `ready`: update moves it to its newest release, "
              + "pin to one exact version, default names the account it runs as, use sets how a list of accounts is used, "
              + "order sets the list, and ready ends an account's cool-off."
            : named is null ? "the change names the agent it is for, as `daoris agent` spells it."
            : door == "pin" && release is null ? "`pin` names the version: one exact release, such as 2.1.300."
            : door != "pin" && release is not null ? $"`{door}` names no version — only `pin` names one."
            : door == "default" && whose is null ? "`default` names the account the agent runs as, one the room lists under it."
            : door == "ready" && whose is null ? "`ready` names the account whose cool-off ends, or `own` for the tool's own sign-in."
            : (door is not ("default" or "ready") && whose is not null) || (door is not ("default" or "use" or "order") && circle is not null)
                ? $"`{door}` names no account or workspace it does not take — `default` and `ready` name an account, `default`, `use` and `order` a workspace."
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
                ? (clear ? "`order` sets the list or clears a workspace's, never both." : "`order` names the accounts in walk order, or clears a workspace's list.")
            : door == "order" && clear && circle is null ? "clearing a list names the workspace it goes back to the machine's accounts for."
            : door == "order" && list.Any(each => each is null) ? "an account of the list is not blank."
            : Word(named, "an agent") ?? (release is null ? null : Word(release, "a version"))
              ?? (whose is null ? null : Word(whose, "an account")) ?? (circle is null ? null : Word(circle, "a workspace"))
              ?? (way is null ? null : Word(way, "a way to use accounts")) ?? (kept is null ? null : Word(kept, "an account"))
              ?? list.Select(each => Word(each!, "an account")).FirstOrDefault(each => each is not null);
        if (refused is not null) return (null, $"{Capital(refused)} Nothing was proposed.");

        return file.Write(writer =>
        {
            writer.WriteString("kind", "agent");
            writer.WriteString("door", door);
            writer.WriteString("target", named);
            Nullable(writer, "workspace", circle);
            // A pin's version, or a default's account: what the change sets.
            Nullable(writer, "value", release ?? (door == "default" ? whose : null));
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
