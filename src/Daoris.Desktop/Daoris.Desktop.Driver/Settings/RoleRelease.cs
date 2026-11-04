namespace Daoris.Driver;

/// <summary>
/// A role's release of a quest (STEWARDROLE1b, D209 §5.1a): its kind (<see cref="RoleReleaseKinds"/>), the session whose stop or end it
/// releases, the role that wrote it (<c>steward</c>) and its act's id, which the run's memory holds its operation by.
/// </summary>
public sealed record RoleRelease(string Kind, string Session, string By, string Act);
