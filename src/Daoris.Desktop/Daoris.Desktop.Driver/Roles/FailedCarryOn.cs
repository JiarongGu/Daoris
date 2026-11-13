namespace Daoris.Driver;

/// <summary>
/// Whether a continuation goes on with the agent its record ran on. A record that failed is staffed by its role as it stands: it keeps
/// that agent only while the role still lists it, so a part that failed for want of its agent's tools is not carried on there again
/// once the role is staffed with another. A record that did not fail keeps its agent, whose conversation it is.
/// </summary>
internal static class FailedCarryOn
{
    /// <param name="role">The role the continuation is read under, as <see cref="StartPlan.ContinuedOn"/> names it.</param>
    /// <exception cref="ActorUnreadableException">The role's stored entry does not read: the continuation holds, saying so.</exception>
    public static bool Keeps(PriorSession record, string agent, string role, DriverConfig config)
    {
        if (!string.Equals(record.State, "failed", StringComparison.OrdinalIgnoreCase)) return true;

        // A role read from the machine's keys has no stored order, so nothing says another agent staffs it.
        var order = ActorChoices.Effective(role, config);
        return order.Count == 0 || order.Any(entry => string.Equals(entry.Agent, agent, StringComparison.OrdinalIgnoreCase));
    }
}
