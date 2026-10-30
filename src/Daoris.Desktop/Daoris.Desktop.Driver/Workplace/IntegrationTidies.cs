namespace Daoris.Driver;

/// <summary>
/// The landing's own clean-up as the tidy step calls it (INTEGRATE1e part e4b, D215; the integration design §5.2 step 10): the landed branches
/// named in <paramref name="only"/> as <c>repository:branch</c>, and no other, judged and removed by the clean-up's own proof (D102's
/// content-held removal, its recovery ref kept), never forced. The integration run makes no delete of its own.
/// </summary>
internal delegate Task<SweepDone> IntegrationTidy(string repository, string? workspace, string root, IReadOnlySet<string> only, CancellationToken ct);

/// <summary>The landing clean-up's press (<see cref="SessionTrees.CleanAsync"/>) as the tidy step's seam.</summary>
internal static class IntegrationTidies
{
    /// <summary>
    /// The tidy step's seam over <paramref name="trees"/>' clean-up press, named to the branches of the candidate alone: no session is in use
    /// here, since the session's own tree and branch went at <c>handed</c> (LAND3) and the step names no session branch.
    /// </summary>
    public static IntegrationTidy Through(SessionTrees trees) =>
        (repository, workspace, root, only, ct) =>
            trees.CleanAsync([(repository, workspace, (string?)root)], new HashSet<string>(StringComparer.Ordinal), only, ct);
}
