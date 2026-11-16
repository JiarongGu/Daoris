namespace Daoris.Driver;

/// <summary>The one check of a supervisor's grants, read by the admission, the judge, the keeping, the releases and the tickets: an act
/// is covered where the supervisor decides its kind (a generic one every kind), its <c>authority</c> lists it, and a spend no grant covers
/// is not its (the census, <see cref="ActSpends"/>); a leave needs none.</summary>
public static class SupervisorGrants
{
    /// <summary>Whether <paramref name="role"/> is a supervisor deciding <paramref name="kind"/>.</summary>
    public static bool Decides(RoleRecord role, string kind) =>
        role.Hold is { Kind: AppointedRoles.Supervisor } hold && (hold.Generic || hold.Decides.Contains(kind, StringComparer.Ordinal));

    /// <summary>Whether <paramref name="role"/>'s grants cover <paramref name="act"/> of <paramref name="kind"/> (the act unqualified).</summary>
    public static bool Covers(RoleRecord role, string kind, string act) => Missing(role, kind, act) is null;

    /// <summary>
    /// The grant <paramref name="role"/> misses for <paramref name="act"/> of <paramref name="kind"/>: the qualified act where it does not decide
    /// the kind or is not granted it, else what the act spends where no grant covers it (that spend stays the owner role's); none where
    /// it is covered. A spend a grant does cover (a re-cast naming the prepaid harness) depends on the use, so its kind asks it with the use.
    /// </summary>
    public static string? Missing(RoleRecord role, string kind, string act)
    {
        var qualified = SupervisorKinds.Qualified(kind, act);
        if (!Decides(role, kind) || !role.Hold!.Acts.Contains(qualified, StringComparer.Ordinal)) return qualified;
        return ActSpends.Of(qualified) is { Covered: null, Spends: not ActSpending.None } ? ActSpends.Missing([role.Hold.Acts], qualified, spends: true) : null;
    }

    /// <summary>The kind and act a holder's decide asks a grant for; null for a leave, which needs none.</summary>
    public static (string Kind, string Act)? Asked(ISupervisorAct act) => act is SupervisorDecideForm decide ? (decide.Kind, decide.Do) : null;
}
