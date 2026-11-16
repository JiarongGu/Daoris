namespace Daoris.Driver;

/// <summary>
/// What a kind's workflow asks less than the one its work would follow without the kind, which decides whether the kind's paths hold
/// the work (<see cref="WorkflowGate.Judge"/>): the person's part (<see cref="Involvement.Lowers"/>), then the checks
/// (<see cref="InvolvementRoutes.LowersVerification"/>: a check dropped or its gates narrowed, a node no longer held on one). A kind
/// whose workflow runs fewer of the declaration's gates would land a path outside the kind's on checks that never reached it, as one
/// that drops the person's look would land it unseen, so either holds the paths. A table, never a reading.
/// </summary>
public static class KindLowering
{
    /// <summary>
    /// How the kind's workflow, <paramref name="steps"/> of its bound <paramref name="chosen"/> graph, asks less than
    /// <paramref name="otherwise"/>, each said as what it does; empty where it lowers nothing. Where the bound graph is not read here
    /// (<see cref="WorkflowProcess.Graph"/> null), the person's part is compared alone.
    /// </summary>
    public static IReadOnlyList<string> Of(IReadOnlyList<NamedStep> steps, NodeGraph? chosen, NodeGraph otherwise) =>
    [
        .. Involvement.Lowers(steps, Involvement.StepsOf(otherwise)),
        .. chosen is null ? [] : InvolvementRoutes.LowersVerification(chosen, otherwise),
    ];
}
