using static Daoris.Knowledge.HelpProposalFile;

namespace Daoris.Knowledge;

/// <summary>The <c>agent</c> kind's writer: an agent's Update, or a pin to one exact version.</summary>
internal sealed class HelpAgentProposalWriter(HelpProposalFile file)
{
    /// <summary>The doors this writes, the driver's <c>HelpAgentProposals.Doors</c> exactly (the kinds test holds both ways).</summary>
    public static readonly IReadOnlyList<string> Doors = ["update", "pin"];

    /// <summary>
    /// Write one agent proposal: <c>update</c> to its newest release, or <c>pin</c> to one exact
    /// version — the Agents screen's two presses, which the driver judges by that door's roster.
    /// </summary>
    public (string? Id, string Message) Propose(string action, string agent, string? version, string why, string? session, DateTimeOffset at)
    {
        var door = action.Trim().ToLowerInvariant();
        var named = Blank(agent);
        var release = Blank(version);
        if (string.IsNullOrWhiteSpace(why)) return NoReason;
        var refused = !Doors.Contains(door)
            ? "an agent's change is `update` or `pin`: update moves it to its newest release, pin to one exact version. How its accounts "
              + "are used, ordered and ended early are pressed with `daoris agent account`."
            : named is null ? "the change names the agent it is for, as `daoris agent` spells it."
            : door == "pin" && release is null ? "`pin` names the version: one exact release, such as 2.1.300."
            : door != "pin" && release is not null ? $"`{door}` names no version — only `pin` names one."
            : Word(named, "an agent") ?? (release is null ? null : Word(release, "a version"));
        if (refused is not null) return (null, $"{Capital(refused)} Nothing was proposed.");

        return file.Write(writer =>
        {
            writer.WriteString("kind", "agent");
            writer.WriteString("door", door);
            writer.WriteString("target", named);
            writer.WriteNull("workspace");
            // A pin's version: what the change sets.
            Nullable(writer, "value", release);
            writer.WriteNull("sentence");
        }, why, session, at);
    }
}
