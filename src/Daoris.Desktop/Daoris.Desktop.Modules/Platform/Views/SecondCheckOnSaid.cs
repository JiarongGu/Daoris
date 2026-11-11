using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// What a role change that has an agent read Ask Daoris's acts means, said on the role door's card: whom each selected turn's acts are
/// sent to, each choice's agent and maker in the order they are tried, and what the readings spend on the person's accounts, by the
/// choices' start budgets. Nothing is said for a change that leaves no agent reading them.
/// </summary>
internal static class SecondCheckOnSaid
{
    public static IEnumerable<PressSaid> Of(RoleRecord set, IReadOnlyList<ActorChoice>? choices, StaffPolicy? policy)
    {
        if (set.Hold is not { Kind: AppointedRoles.Supervisor, Holder: RoleHolders.Agent } hold || !Reads(hold)) yield break;
        var order = choices ?? [];
        if (order.Count == 0) yield break;
        var adapters = ProviderRegistry.Built();
        yield return new PressSaid("second-check-on", ("readers", string.Join(", ", order.Select(choice => AgentFamily.Of(adapters, choice.Agent).Said()))));
        var starts = order.Select(choice => policy?.Of(StaffKey.Of(choice))?.Budget?.Starts).Distinct().ToList();
        yield return starts is [{ } each] ? new PressSaid("second-check-spend", ("starts", each.ToString(System.Globalization.CultureInfo.InvariantCulture))) : new PressSaid("second-check-spend-open");
    }

    private static bool Reads(RoleHold hold) =>
        hold.Acts.Contains(SupervisorKinds.Qualified(SupervisorKinds.AskDaoris, SupervisorKinds.Report), StringComparer.Ordinal);
}
