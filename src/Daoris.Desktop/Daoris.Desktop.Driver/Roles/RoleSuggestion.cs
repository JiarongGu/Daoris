namespace Daoris.Driver;

/// <summary>A role Daoris suggests adding: its record and its choices, applied only by the person's press through the role door.</summary>
public sealed record SuggestedRole(RoleRecord Role, IReadOnlyList<ActorChoice> Choices);

/// <summary>
/// The roles Daoris suggests (FOUNDATION1y-i; D205 point 7's card, a dated table): the <i>Designer</i>, taking design and decision work on
/// Claude Code at <c>opus</c> · high, the reference's <b>build-judgement</b> row (<c>docs/model-roles.md</c>), offered while no role takes
/// design, since such work starts on Build's order until one does (§2.7). Its words on the card say it can spend more. A census holds the
/// table to the reference.
/// </summary>
public static class RoleSuggestion
{
    /// <summary>The day the table was read from the reference.</summary>
    public const string Dated = "2026-10-31";

    /// <summary>Claude Code's agent, as the machine names it.</summary>
    public const string ClaudeCode = "claude-code";

    /// <summary>The Designer: design and decision work, machine-wide, on Claude Code · opus · high.</summary>
    public static SuggestedRole Designer { get; } = new(
        new RoleRecord("designer", "Designer", "设计师", RoleRecord.Always, [RoleWork.Design], RoleScope.Everywhere),
        [new ActorChoice(ClaudeCode, "opus", "high")]);

    /// <summary>What is suggested for <paramref name="catalogue"/>: the Designer while no role takes design and its id is free.</summary>
    public static IReadOnlyList<SuggestedRole> For(RoleCatalog catalogue) =>
        catalogue.Slots.Any(slot => slot.Id == Designer.Role.Id || slot.Role?.Takes.Contains(RoleWork.Design, StringComparer.Ordinal) == true)
            ? []
            : [Designer];
}
