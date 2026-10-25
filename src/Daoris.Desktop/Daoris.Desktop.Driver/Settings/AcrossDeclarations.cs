namespace Daoris.Driver;

/// <summary>
/// What <c>driver.json</c>'s <c>writeAcross</c> may hold (READ1, D107): the settings' own reading of a declared relationship, which
/// the permissions' <see cref="AcrossRules.Problem"/> answers through, so the file reads, the verb refuses and a session's reach
/// resolves one rule.
/// </summary>
public static class AcrossDeclarations
{
    /// <summary>Why a relationship from one repository to another cannot be declared, or null when it can.</summary>
    /// <remarks>The CLI's <c>writeAcrossProblem</c> says the same two sentences.</remarks>
    public static string? Problem(string repository, string to) =>
        string.IsNullOrWhiteSpace(to)
            ? "a relationship names the repository it may write into."
            : string.Equals(repository.Trim(), to.Trim(), StringComparison.OrdinalIgnoreCase)
                ? $"`{repository.Trim()}` writes in its own tree already — a relationship names another repository."
                : null;
}
