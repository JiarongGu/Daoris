namespace Daoris.Desktop;

// The payloads whose judge reads the form exact, each its route's whole payload: the role door's change, and the forms the role's acts
// and the steward's desk hold to (every field named once, nothing else, and for the acts a proof over the bytes).

/// <summary>A role's change as <c>SET_ROLE</c> takes it.</summary>
public sealed record SetRolePress(WholePayload Press);

/// <summary>Several role changes as <c>SET_ROLES</c> takes them, its <c>changes</c>.</summary>
public sealed record SetRolesPress(WholePayload Press);

/// <summary>The supervisor's decision or its leaving a subject to the owner, as the judge reads either form.</summary>
public sealed record SupervisorDecisionPress(WholePayload Form);

/// <summary>A role's act, <c>{ role, act, generation, form, proof }</c>.</summary>
public sealed record RoleActPress(WholePayload Form);

/// <summary>A request at the steward's desk, <c>{ act, target, why }</c>.</summary>
public sealed record DeskRequestPress(WholePayload Form);
