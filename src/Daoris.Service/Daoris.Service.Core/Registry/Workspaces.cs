namespace Daoris.Knowledge;

/// <summary>
/// The workspace name, and the two rules every layer must agree on: what silence means, and when two
/// names are the same name.
/// </summary>
/// <remarks>
/// <para><b>The workspace is the unit of sharing</b> (D48). Everything that crosses repositories —
/// search, convergence, the registry, quests, session records — is scoped to one, and the boundary is
/// drawn where a person drew it rather than where a folder happened to be.</para>
///
/// <para><b>Silence is <see cref="Default"/>, everywhere.</b> A machine that never names a workspace
/// runs exactly as it did before workspaces existed — one group, no wiring, no server, no account. That
/// is not a convenience: the local deployment working alone is binding (design §2a), and a scoping
/// rule whose absent case is undefined is how it would stop being true.</para>
///
/// <para><b>Comparison is a person's comparison</b> — trimmed and case-insensitive. Two workspaces
/// differing only in case would be a boundary nobody can see, and an invisible boundary is worse than
/// none: it produces a refusal with no explanation in it.</para>
/// </remarks>
public static class Workspaces
{
    /// <summary>Where a repository lives when nobody has said otherwise.</summary>
    public const string Default = "default";

    /// <summary>A name as it is stored and printed: trimmed, and <see cref="Default"/> when unstated.</summary>
    public static string Normalize(string? name) =>
        string.IsNullOrWhiteSpace(name) ? Default : name.Trim();

    /// <summary>Whether two names — either of which may be unstated — are the same workspace.</summary>
    public static bool Same(string? left, string? right) =>
        string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);
}
