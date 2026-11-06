namespace Daoris.Driver;

/// <summary>
/// The sessions whose landings carried a verification's batch (the pipeline design §4.5): every landing on a line is a request its integration
/// settled, so the batch's are the settled requests into the run's line whose frozen tip the batch holds. Each is named by the session its
/// request records, else by the sessions of its repository whose tree its branch (<c>daoris/&lt;tree&gt;</c>) is.
/// </summary>
internal static class VerifyBatchSessions
{
    /// <summary>How many of the repository's newest closed requests are read for a batch.</summary>
    private const int Looked = 200;

    /// <summary>The settled requests into <paramref name="run"/>'s line, newest first; none for a person's run.</summary>
    public static IReadOnlyList<IntegrationRequest> Settled(string home, VerifyRun run) =>
        run.Line is not { } line
            ? []
            : [.. new IntegrationStore(home).Closed(run.Repository, Looked)
                .Where(each => each.ClosedWhy == IntegrationRequestCloses.Settled && string.Equals(each.Line, line, StringComparison.Ordinal))];

    /// <summary>
    /// The sessions of <paramref name="settled"/> whose tip is among <paramref name="batch"/>'s commits, in the order they landed, each once.
    /// </summary>
    public static IReadOnlyList<string> Of(IReadOnlyList<IntegrationRequest> settled, IReadOnlySet<string> batch, IReadOnlyList<SessionRecord> sessions) =>
    [
        .. settled.Where(each => batch.Contains(each.Tip)).OrderBy(each => each.ClosedAt ?? each.At)
            .SelectMany(each => each.Session is { } session ? [session] : Trees(each, sessions))
            .Distinct(StringComparer.Ordinal),
    ];

    private const string Prefix = "daoris/";

    /// <summary>The sessions whose tree a request's branch is, oldest first.</summary>
    private static IEnumerable<string> Trees(IntegrationRequest request, IReadOnlyList<SessionRecord> sessions) =>
        request.Branch.StartsWith(Prefix, StringComparison.Ordinal)
            ? sessions.Where(session => string.Equals(session.Repository, request.Repository, StringComparison.OrdinalIgnoreCase)
                                        && session.Tree is { Length: > 0 } tree
                                        && string.Equals(Path.GetFileName(tree.TrimEnd('/', '\\')), request.Branch[Prefix.Length..], StringComparison.Ordinal))
                .OrderBy(session => session.Created).Select(session => session.Id)
            : [];
}
