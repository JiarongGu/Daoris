namespace Daoris.Driver;

/// <summary>
/// An appointed role's staffing: a supervisor an agent holds walks an order of one to four choices, kept as its own row of
/// <c>role_staffing</c> as an added role's is, and judged by the one judge of an order's shape; any other holder walks no order. Its
/// policy is a working role's, but for the works a choice may take, since a supervisor takes no work.
/// </summary>
public static class AppointedStaffing
{
    /// <summary>Whether <paramref name="hold"/> walks an order: a supervisor held by an agent.</summary>
    public static bool Ordered(RoleHold? hold) => hold is { Kind: AppointedRoles.Supervisor, Holder: RoleHolders.Agent };

    /// <summary>
    /// The order a set of <paramref name="role"/> writes as its row: the choices named, else none for a supervisor an agent did not hold
    /// before (refused), else what it has (null); null for an appointed role no agent holds, whose row a move off the agent deletes.
    /// </summary>
    public static IReadOnlyList<ActorChoice>? Staffed(RoleCatalog catalogue, RoleRecord role, IReadOnlyList<ActorChoice>? choices) =>
        !Ordered(role.Hold) ? null : choices ?? (Ordered(Before(catalogue, role.Id)) ? null : []);

    /// <summary>Whether a set of <paramref name="role"/> deletes its row: it moves the holder off the agent.</summary>
    public static bool Unordered(RoleCatalog catalogue, RoleRecord role) => !Ordered(role.Hold) && Ordered(Before(catalogue, role.Id));

    /// <summary>
    /// Whether <paramref name="choices"/> may go with <paramref name="role"/>'s set, null where they may: the steward takes none
    /// (<c>appointed-not-configurable</c>), a supervisor held by its rules or a terminal walks no order, and one an agent holds needs one to
    /// four choices of an order's shape (<c>choices</c>).
    /// </summary>
    public static RoleRefusal? Judge(RoleCatalog catalogue, RoleRecord role, IReadOnlyList<ActorChoice>? choices)
    {
        var hold = role.Hold!;
        if (hold.Kind != AppointedRoles.Supervisor)
        {
            return choices is null ? null : new("appointed-not-configurable", $"`{role.Id}` is the {hold.Kind}: its holder holds it, never a staffing of choices.");
        }

        if (!Ordered(hold))
        {
            return choices is null ? null : new("choices", $"`{role.Id}` is held by {(hold.Holder == RoleHolders.Rules ? "your rules" : "a terminal")}, which walks no choices: an agent holder takes them.");
        }

        if (Staffed(catalogue, role, choices) is not { } order) return null;
        if (order.Count == 0) return new("choices", $"`{role.Id}` is held by an agent: give it one to {ActorKey.Most} choices of agent, model and effort.");
        return ActorKey.JudgeOrder(role.Id, order) is { } said ? new("choices", said) : null;
    }

    /// <summary>
    /// The refusal of <paramref name="policy"/> for an appointed <paramref name="role"/>, or null where the policy door may judge it as a
    /// working role's: only a supervisor an agent holds keeps one, and none of its choices names works.
    /// </summary>
    public static RoleRefusal? JudgePolicy(RoleRecord role, StaffPolicy? policy)
    {
        if (!Ordered(role.Hold)) return new(StaffPolicy.Code, $"`{role.Id}` is an appointed role no agent holds: a policy goes with an agent's choices.");
        return policy?.Choices.Any(entry => entry.Takes is not null) == true
            ? new(StaffPolicy.Code, $"`{role.Id}` is a supervisor, which takes no work: its choices name no works.")
            : null;
    }

    private static RoleHold? Before(RoleCatalog catalogue, string id) => catalogue.Slots.FirstOrDefault(slot => slot.Id == id)?.Role?.Hold;
}
