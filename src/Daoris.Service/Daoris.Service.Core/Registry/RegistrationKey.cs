namespace Daoris.Knowledge;

/// <summary>
/// The key a repository's row is written under, so that a repository is one identity whatever its case.
/// </summary>
/// <remarks>
/// The tables are keyed by the name as first written and their primary keys compare exactly, and an existing store keeps the
/// collation it was made with; every read already compares without case. So a write resolves the spelling a row already
/// holds, in the statement that writes, and conflicts with that row instead of making a second.
/// </remarks>
internal static class RegistrationKey
{
    /// <summary>The SQL for the key of <paramref name="table"/>'s row for <c>$repository</c>: the spelling held, or the one given.</summary>
    public static string Held(string table) =>
        $"COALESCE((SELECT repository FROM {table} WHERE repository = $repository COLLATE NOCASE ORDER BY rowid LIMIT 1), $repository)";
}
