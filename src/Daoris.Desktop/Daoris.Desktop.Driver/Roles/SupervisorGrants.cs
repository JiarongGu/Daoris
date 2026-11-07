namespace Daoris.Driver;

/// <summary>
/// The one check of a supervisor's grants, read by the admission and so by every step that asks it: the judge at a holder's decide, the
/// keeping, the releases and the tickets. A supervisor covers an act where it decides the act's kind (a generic one decides every kind)
/// and its <c>authority</c> lists the act. Leaving a decision to the person needs no grant, so a leave never asks.
/// </summary>
public static class SupervisorGrants
{
    /// <summary>Whether <paramref name="role"/> is a supervisor deciding <paramref name="kind"/>.</summary>
    public static bool Decides(RoleRecord role, string kind) =>
        role.Hold is { Kind: AppointedRoles.Supervisor } hold && (hold.Generic || hold.Decides.Contains(kind, StringComparer.Ordinal));

    /// <summary>Whether <paramref name="role"/>'s grants cover <paramref name="act"/> of <paramref name="kind"/> (the act unqualified).</summary>
    public static bool Covers(RoleRecord role, string kind, string act) =>
        Decides(role, kind) && role.Hold!.Acts.Contains(SupervisorKinds.Qualified(kind, act), StringComparer.Ordinal);

    /// <summary>The kind and act a holder's decide asks a grant for; null for a leave, which needs none.</summary>
    public static (string Kind, string Act)? Asked(ISupervisorAct act) => act is SupervisorDecideForm decide ? (decide.Kind, decide.Do) : null;
}
