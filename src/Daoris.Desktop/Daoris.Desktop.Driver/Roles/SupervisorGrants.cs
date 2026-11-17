namespace Daoris.Driver;

public static class SupervisorGrants
{
    /// <summary>Whether <paramref name="role"/> is a supervisor deciding <paramref name="kind"/>.</summary>
    public static bool Decides(RoleRecord role, string kind) =>
        role.Hold is { Kind: AppointedRoles.Supervisor } hold && (hold.Generic || hold.Decides.Contains(kind, StringComparer.Ordinal));

    /// <summary>Whether <paramref name="role"/>'s grants cover <paramref name="act"/> of <paramref name="kind"/> (the act unqualified).</summary>
    public static bool Covers(RoleRecord role, string kind, string act) => Missing(role, kind, act) is null;

    public static string? Missing(RoleRecord role, string kind, string act)
    {
        var qualified = SupervisorKinds.Qualified(kind, act);
        if (!Decides(role, kind) || !role.Hold!.Acts.Contains(qualified, StringComparer.Ordinal)) return qualified;
        return ActSpends.Of(qualified) is { Covered: null, Spends: not ActSpending.None } ? ActSpends.Missing([role.Hold.Acts], qualified, spends: true) : null;
    }

    /// <summary>The kind and act a holder's decide asks a grant for; null for a leave, which needs none.</summary>
    public static (string Kind, string Act)? Asked(ISupervisorAct act) => act is SupervisorDecideForm decide ? (decide.Kind, decide.Do) : null;
}
