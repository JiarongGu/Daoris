namespace Daoris.Knowledge;

/// <summary>The <c>agent</c> kind's writer (HELP6): an agent's Update, or a pin to one exact version.</summary>
public sealed partial class HelpProposalBox
{
    /// <summary>
    /// Write one agent proposal (HELP6): <c>update</c> to its newest release, or <c>pin</c> to one exact
    /// version — the Agents screen's two presses, which the driver judges by that door's roster.
    /// </summary>
    public (string? Id, string Message) ProposeAgent(string action, string agent, string? version, string why, string? session, DateTimeOffset at)
    {
        var door = action.Trim().ToLowerInvariant();
        var named = Blank(agent);
        var release = Blank(version);
        if (string.IsNullOrWhiteSpace(why)) return NoReason;
        var refused = door is not ("update" or "pin")
            ? "an agent's change is `update` or `pin`: update moves it to its newest release, and pin to one exact version."
            : named is null ? "the change names the agent it is for, as `daoris agent` spells it."
            : door == "pin" && release is null ? "`pin` names the version: one exact release, such as 2.1.300."
            : door == "update" && release is not null ? "`update` names no version — it moves to the newest release, and `pin` names one."
            : Word(named, "an agent") ?? (release is null ? null : Word(release, "a version"));
        if (refused is not null) return (null, $"{Capital(refused)} Nothing was proposed.");

        return Write(writer =>
        {
            writer.WriteString("kind", "agent");
            writer.WriteString("door", door);
            writer.WriteString("target", named);
            writer.WriteNull("workspace");
            Nullable(writer, "value", release);
            writer.WriteNull("sentence");
        }, why, session, at);
    }
}
