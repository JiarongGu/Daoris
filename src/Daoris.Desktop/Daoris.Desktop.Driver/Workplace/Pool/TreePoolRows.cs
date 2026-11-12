using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>The pool's four tables read into their records: the columns each query names, and the mapping of a row.</summary>
internal static class TreePoolRows
{
    internal const string KeptColumns = "workspace, repository, number, registration, path, line, tip, state, said, since, bytes, toolchain";

    internal const string ClaimColumns =
        "workspace, repository, number, kind, holder, line, branch, start, expect, opens, job, record, phase, claim_run, claim_generation, at, expect_branch, expect_line";

    internal const string NamesLine =
        $"(line = $line OR (expect_line = $line AND phase IN ('{TreeClaimPhases.Undoing}', '{TreeClaimPhases.UndoWaiting}')))";

    internal const string VisitColumns = "workspace, repository, number, kind, visitor, run, at";

    internal const string EndColumns = "workspace, repository, line, branch, session, number, kind, tip, read, \"left\", toolchain, at";

    internal static KeptTree Kept(DbDataReader row) =>
        new(row.GetString(0), row.GetString(1), row.GetInt32(2), row.GetString(3), row.GetString(4), row.GetString(5), row.GetString(6),
            row.GetString(7), row.GetString(8), Moment(row, 9), row.GetInt64(10), row.GetString(11));

    internal static TreeClaim Claim(DbDataReader row) =>
        new(row.GetString(0), row.GetString(1), row.GetInt32(2), row.GetString(3), row.GetString(4), row.GetString(5), row.GetString(6),
            row.GetString(7), row.GetString(8), row.GetString(9), row.GetString(10), row.GetString(11), row.GetString(12), row.GetString(13),
            row.GetInt64(14), Moment(row, 15))
        {
            ExpectBranch = row.GetString(16),
            ExpectLine = row.GetString(17),
        };

    internal static SlotVisit Visit(DbDataReader row) =>
        new(row.GetString(0), row.GetString(1), row.GetInt32(2), row.GetString(3), row.GetString(4), row.GetString(5), Moment(row, 6));

    internal static LineEnd End(DbDataReader row) =>
        new(row.GetString(0), row.GetString(1), row.GetString(2), row.GetString(3), row.GetString(4), row.GetInt32(5), row.GetString(6),
            row.GetString(7), Moment(row, 8), row.GetString(9), row.GetString(10), Moment(row, 11));

    private static DateTimeOffset Moment(DbDataReader row, int at) =>
        Moments.Read(row.GetString(at)) ?? throw new DesktopStoreException("a pool record's moment does not read as one");
}
