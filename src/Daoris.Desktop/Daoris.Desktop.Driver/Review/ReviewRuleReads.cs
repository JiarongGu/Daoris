namespace Daoris.Driver;

/// <summary>
/// The review rule asked of a checkout (REVIEWENV1a, design §1.1): whether it holds a procedure, and which a put would leave
/// without one.
/// </summary>
/// <remarks>
/// <b>Named where its callers always named it</b> (STRUCT3c, D166's note): <c>ReviewRules.Holds</c> and <c>Lacking</c> are static
/// members of a C# 14 extension block on the Domain's <see cref="ReviewRules"/>, whose reading, refusals and sentences are pure;
/// looking in a checkout is Review's Infrastructure's (<see cref="ReviewProcedureFiles"/>). Declared here, another module may name
/// them.
/// </remarks>
public static class ReviewRuleReads
{
    extension(ReviewRules)
    {
        /// <summary>
        /// Whether a checkout holds a procedure (design §1.1): a regular file at that path, reached through folders, with no link
        /// on the way — the CLI's <c>holdsProcedure</c>. Read where the registry says the checkout is, never for meaning.
        /// </summary>
        public static bool Holds(string root, string procedure) =>
            ReviewRules.IsProcedurePath(procedure) && ReviewProcedureFiles.Holds(root, procedure.Split('/'));

        /// <summary>
        /// The repositories of <paramref name="checkouts"/> that a put of <paramref name="procedure"/> reaches and whose checkout
        /// here does not hold it: the one named, or each of the workspace's, matched in any case.
        /// </summary>
        public static IReadOnlyList<string> Lacking(
            IEnumerable<(string Repository, string? Workspace, string? Root)> checkouts, ReviewEdit edit, string procedure) =>
            [.. checkouts
                .Where(each => each.Root is { Length: > 0 })
                .Where(each => edit.Repository is { } repository
                    ? string.Equals(each.Repository, repository.Trim(), StringComparison.OrdinalIgnoreCase)
                    : string.Equals(RemoteTarget.Workspace(each.Workspace), RemoteTarget.Workspace(edit.Workspace), StringComparison.OrdinalIgnoreCase))
                .Where(each => !ReviewRules.Holds(each.Root!, procedure))
                .Select(each => each.Repository)];
    }
}
