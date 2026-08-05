namespace Daoris.Devkit;

/// <summary>
/// The `devkit` field in the declaration, enforced.
/// </summary>
/// <remarks>
/// <para>A pin that nothing checks is a comment. This one was parsed and never read, under a doc
/// comment claiming "the launcher enforces it" — describing a launcher that does not exist. The field
/// is the whole reason gates can be *declared* rather than copied: it says which toolkit's semantics
/// this repository's declaration was written against.</para>
///
/// <para>Checked <b>before</b> the gates, not alongside them. If the toolkit is the wrong one, the gate
/// results are not trustworthy, and reporting a mismatch after printing seven confident lines is
/// backwards.</para>
/// </remarks>
public static class VersionPin
{
    /// <summary>Throws when the declaration pins a devkit this binary is not.</summary>
    /// <remarks>
    /// An unpinned declaration is allowed and silent. Pinning is a choice a repository makes when it
    /// starts caring, and demanding it from the first run would be ceremony — the same reasoning that
    /// lets the manifest default its harness.
    /// </remarks>
    public static void Require(string? declared, string running)
    {
        if (string.IsNullOrWhiteSpace(declared)) return;
        if (string.Equals(declared.Trim(), running, StringComparison.OrdinalIgnoreCase)) return;

        throw new DevkitException(
            $"this repository pins devkit {declared.Trim()} and this binary is {running}.\n"
            + "Gates are declared against a toolkit's semantics, so a different one may read the same "
            + "declaration differently — which is the failure pinning exists to prevent.\n"
            + $"Install {declared.Trim()}, or update the 'devkit' field once you have checked that the "
            + "declaration still means what it did.");
    }
}
