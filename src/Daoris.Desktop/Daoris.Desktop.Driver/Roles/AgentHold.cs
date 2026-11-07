namespace Daoris.Driver;

/// <summary>
/// The hold of a supervisor an agent holds. No press draws it: the role's record the person saved is the appointment, so its generation
/// is the role's id and revision, read from the catalogue at every admission and kept nowhere. Any write of the role, from any process,
/// moves the revision, and a decision kept under the old one goes stale at its next step.
/// </summary>
public static class AgentHold
{
    private const string Prefix = "agent:";

    /// <summary>The generation of <paramref name="role"/> at <paramref name="revision"/>: <c>agent:&lt;role&gt;@&lt;revision&gt;</c>.</summary>
    public static string Generation(string role, long revision) =>
        $"{Prefix}{role}@{revision.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

    /// <summary>Whether <paramref name="generation"/> is an agent's, which no terminal's hold ever is.</summary>
    public static bool Is(string? generation) => generation?.StartsWith(Prefix, StringComparison.Ordinal) == true;

    /// <summary>The role <paramref name="generation"/> names, or null where it is no agent's.</summary>
    public static string? RoleOf(string? generation) =>
        Is(generation) && generation!.LastIndexOf('@') is var at and > 0 && at > Prefix.Length ? generation[Prefix.Length..at] : null;
}
