namespace Daoris.Knowledge;

/// <summary>
/// The <c>agent</c> kind's writer (HELP6): an agent's Update, or a pin to one exact version — and since HELP10 the
/// account it runs as by default.
/// </summary>
public sealed partial class HelpProposalBox
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
    /// <param name="early">For <c>use</c>: switch before the limit, or not.</param>
    /// <param name="near">For <c>use</c>: the percent at which an account counts as near its limit.</param>
    /// <param name="accounts">For <c>order</c>: the walk's accounts, in order.</param>
    /// <param name="clear">For <c>order</c>: the workspace goes back to the machine's accounts.</param>
    public (string? Id, string Message) ProposeAgent(
        string action, string agent, string? version, string why, string? session, DateTimeOffset at,
        string? account = null, string? workspace = null,
        string? use = null, string? keep = null, bool noKeep = false, bool? early = null, int? near = null,
        IReadOnlyList<string>? accounts = null, bool clear = false)
    {
        var door = action.Trim().ToLowerInvariant();
        var named = Blank(agent);
        var release = Blank(version);
        var whose = Blank(account);
        var circle = Blank(workspace);
        var way = Blank(use);
        var kept = Blank(keep);
        var list = accounts?.Select(Blank).ToList() ?? [];
        if (string.IsNullOrWhiteSpace(why)) return NoReason;
        var usesAny = way is not null || kept is not null || noKeep || early is not null || near is not null;
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
            : door != "use" && usesAny ? $"`{door}` takes no use, keep, early or near — only `use` does."
            : door != "order" && (list.Count > 0 || clear) ? $"`{door}` takes no accounts list or clear — only `order` does."
            : door == "use" && !usesAny ? "`use` names what changes: `use` (goal or order), `keep` (an account, or none), `early` or `near`."
            : door == "use" && way is not (null or "goal" or "order") ? $"a way to use accounts is `goal` or `order`, not `{way}`."
            : door == "use" && kept is not null && noKeep ? "`use` can keep one account or none, not both."
            : door == "use" && near is not null and (< 50 or > 99) ? $"near is a whole percent from 50 to 99, not {near}."
            : door == "order" && (list.Count > 0) == clear
                ? (clear ? "`order` sets the list or clears a workspace's, never both." : "`order` names the accounts in walk order, or clears a workspace's list.")
            : door == "order" && clear && circle is null ? "clearing a list names the workspace it goes back to the machine's accounts for."
            : door == "order" && list.Any(each => each is null) ? "an account of the list is not blank."
            : Word(named, "an agent") ?? (release is null ? null : Word(release, "a version"))
              ?? (whose is null ? null : Word(whose, "an account")) ?? (circle is null ? null : Word(circle, "a workspace"))
              ?? (way is null ? null : Word(way, "a way to use accounts")) ?? (kept is null ? null : Word(kept, "an account"))
              ?? list.Select(each => Word(each!, "an account")).FirstOrDefault(each => each is not null);
        if (refused is not null) return (null, $"{Capital(refused)} Nothing was proposed.");

        return Write(writer =>
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
}
