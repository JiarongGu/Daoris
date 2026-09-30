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
    /// <param name="account">For <c>default</c> only: the account, written as the proposal's value.</param>
    /// <param name="workspace">For <c>default</c> only: the one workspace it is the default for; null for the machine.</param>
    public (string? Id, string Message) ProposeAgent(
        string action, string agent, string? version, string why, string? session, DateTimeOffset at,
        string? account = null, string? workspace = null)
    {
        var door = action.Trim().ToLowerInvariant();
        var named = Blank(agent);
        var release = Blank(version);
        var whose = Blank(account);
        var circle = Blank(workspace);
        if (string.IsNullOrWhiteSpace(why)) return NoReason;
        var refused = door is not ("update" or "pin" or "default")
            ? "an agent's change is `update`, `pin` or `default`: update moves it to its newest release, pin to one exact "
              + "version, and default names the account it runs as."
            : named is null ? "the change names the agent it is for, as `daoris agent` spells it."
            : door == "pin" && release is null ? "`pin` names the version: one exact release, such as 2.1.300."
            : door != "pin" && release is not null ? $"`{door}` names no version — only `pin` names one."
            : door == "default" && whose is null ? "`default` names the account the agent runs as, one the room lists under it."
            : door != "default" && (whose is not null || circle is not null)
                ? $"`{door}` names no account or workspace — only `default` does."
            : Word(named, "an agent") ?? (release is null ? null : Word(release, "a version"))
              ?? (whose is null ? null : Word(whose, "an account")) ?? (circle is null ? null : Word(circle, "a workspace"));
        if (refused is not null) return (null, $"{Capital(refused)} Nothing was proposed.");

        return Write(writer =>
        {
            writer.WriteString("kind", "agent");
            writer.WriteString("door", door);
            writer.WriteString("target", named);
            Nullable(writer, "workspace", circle);
            // A pin's version, or a default's account: what the change sets.
            Nullable(writer, "value", release ?? whose);
            writer.WriteNull("sentence");
        }, why, session, at);
    }
}
