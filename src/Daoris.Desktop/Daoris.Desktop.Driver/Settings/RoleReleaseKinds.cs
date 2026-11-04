namespace Daoris.Driver;

/// <summary>The kinds of a role's release: a stop's (§3.1 row 8) and, with STEWARDROLE1f, a transient hold's (row 11).</summary>
public static class RoleReleaseKinds
{
    public const string Stop = "stop";
    public const string Transient = "transient";
}
