namespace Daoris.Driver;

/// <summary>
/// The one reader of a role's order for everything but a start's own walk: the role a line is
/// bound to, and its order as the start plan would arrange it now, nothing reserved and no account asked. A caller that wants the stored
/// order reads <see cref="ActorChoices.Effective"/> and is named in <c>OrderReadersCensusTests</c>.
/// </summary>
public static class RoleOrders
{
    /// <summary>
    /// The role a line goes on under: the role its record is bound to while the catalogue holds it, else the role that takes
    /// <paramref name="work"/> at <paramref name="place"/> now, as a removed role's record continues; the work's seeded role where the
    /// catalogue holds the start for a reason of its own (the walk says it).
    /// </summary>
    public static string Bound(string? bound, string work, RoleCatalog catalogue, RolePlace place)
    {
        try
        {
            return bound is { Length: > 0 } ? StartPlan.BoundRole(bound, work, catalogue, place) : catalogue.RoleFor(work, place).Role?.Id ?? StartPlan.RoleOf(work);
        }
        catch (DriverException)
        {
            return StartPlan.RoleOf(work);
        }
    }

    /// <summary>Whether <paramref name="role"/> still lists the choice (agent, model, effort) a hand-over is approved on: it is revalidated, never swapped for another.</summary>
    /// <exception cref="ActorUnreadableException">The role's stored staffing does not read.</exception>
    public static bool Holds(string role, DriverConfig config, string agent, string? model, string? effort) =>
        ActorChoices.Effective(role, config).Any(entry =>
            string.Equals(entry.Agent, agent, StringComparison.OrdinalIgnoreCase) && entry.Model == model && entry.Effort == effort);

    /// <summary>
    /// <paramref name="role"/>'s order as a start of <paramref name="work"/> would walk it: its effective order, arranged by its set policy
    /// (what the policy passes before any account, a choice over its budget or not taking the work, left out). A role under no policy, or with
    /// one choice, is its stored order.
    /// </summary>
    /// <exception cref="ActorUnreadableException">The role's stored staffing does not read.</exception>
    public static IReadOnlyList<ActorEffective> Arranged(
        StaffLedger? staff, string role, string work, DriverConfig config, IReadOnlyList<string>? fence = null, StaffFacts? line = null)
    {
        var order = ActorChoices.Effective(role, config, fence);
        return Arranged(staff, role, work, order, line);
    }

    /// <summary>
    /// <paramref name="order"/> with each agent's own entries in the order <paramref name="arranged"/> gives them, the agents themselves
    /// staying in <paramref name="order"/>'s: a rule that names who runs (a second opinion's other maker, a helper's list, a partner's
    /// named agent) outranks a weight, which only chooses among one agent's alternatives. An entry the arrangement left out stays out.
    /// </summary>
    public static IReadOnlyList<ActorEffective> WithinAgents(IReadOnlyList<ActorEffective> order, IReadOnlyList<ActorEffective> arranged)
    {
        // Nothing arranged: the order stands exactly as it stands, interleaved or not.
        if (arranged.SequenceEqual(order)) return order;
        var agents = order.Select(each => each.Agent).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return [.. agents.SelectMany(agent => arranged.Where(each => string.Equals(each.Agent, agent, StringComparison.OrdinalIgnoreCase)))];
    }

    /// <summary>An order read already (a pick's, a hand-over's), arranged as <see cref="Arranged(StaffLedger?, string, string, DriverConfig, IReadOnlyList{string}?, StaffFacts?)"/> does.</summary>
    public static IReadOnlyList<ActorEffective> Arranged(StaffLedger? staff, string role, string work, IReadOnlyList<ActorEffective> order, StaffFacts? line = null)
    {
        if (staff is null || order.Count < 2) return order;
        try
        {
            return [.. staff.Preview(role, work, order, line).Arrangement.Walk.Select(each => each.Entry)];
        }
        catch (Exception error) when (error is DesktopStoreException or Microsoft.Data.Sqlite.SqliteException or IOException or UnauthorizedAccessException)
        {
            // A store that will not answer arranges nothing: the stored order stands, as with no policy.
            return order;
        }
    }
}
