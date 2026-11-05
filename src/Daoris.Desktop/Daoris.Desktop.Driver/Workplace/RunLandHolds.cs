namespace Daoris.Driver;

/// <summary>
/// What a landing door answers where no run's <c>land</c> performs the work (D228: a run's land is the one way to land, and no door lands
/// in place): the gate's own refusal where a part of it holds, else why the work's process lands nothing, refused as
/// <see cref="LandingCode.Refused"/>.
/// </summary>
public static class RunLandHolds
{
    /// <summary>The refusal for work the runtime does not land: never a landing.</summary>
    public static TreeLanding Of(IWorkplaceGate gate) =>
        gate.Refusal ?? new TreeLanding(false, $"{Why(gate.Process)} Nothing was landed.") { Refusal = LandingCode.Refused };

    /// <summary>Why nothing lands the work, as its process says it: unread, held with its problem, or bound to a graph with no landing.</summary>
    public static string Why(IWorkplaceProcess? process) => process switch
    {
        { Unread: { } unread } => Sentence($"whether a workflow governs this work is not known: {unread}"),
        { Problem: { } problem } => Sentence(problem),
        { Named: true } named => $"It follows {named.Name}, and only a run's `land` lands its work.",
        _ => "No workflow's run governs this work, so nothing lands it: Set up… chooses one.",
    };

    private static string Sentence(string text)
    {
        var trimmed = text.Trim();
        var opened = trimmed.Length == 0 ? trimmed : char.ToUpperInvariant(trimmed[0]) + trimmed[1..];
        return opened.EndsWith('.') ? opened : opened + ".";
    }
}
