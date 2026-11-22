namespace Daoris.Driver;

/// <summary>The service's in-process half, beside the library it answers from (the platform below cannot name it).</summary>
internal static class GitServiceReads
{
    /// <summary>
    /// The answer to a read with no git started and no place taken: a ref's id and the refs' listing from git's files (<see cref="GitRefReads"/>),
    /// else the library's; null where git must answer it, and where the library did not answer within <paramref name="bound"/>, so git
    /// answers it under its own deadline.
    /// </summary>
    internal static async Task<GitLibraryAnswer?> ReadInProcessAsync(
        this GitService service, string folder, IReadOnlyList<string> arguments, TimeSpan bound, CancellationToken ct)
    {
        if (GitRefReads.Asked(folder, arguments) is { } refs)
        {
            if (refs.Answer is { } direct)
            {
                GitAskTally.Shared.RecordMemo(GitAskers.Current, "rev-parse", hit: true);
                return direct;
            }

            var peeled = await LibraryAsync(folder, arguments, bound, ct).ConfigureAwait(false);
            if (peeled is not null) refs.Learn(peeled);
            return peeled;
        }

        if (!GitRefReads.Listed(arguments)) return await LibraryAsync(folder, arguments, bound, ct).ConfigureAwait(false);
        if (GitRefReads.Kept(folder) is { } kept)
        {
            GitAskTally.Shared.RecordMemo(GitAskers.Current, "for-each-ref", hit: true);
            return kept;
        }

        var before = GitRefReads.Stamped(folder);
        var listed = await LibraryAsync(folder, arguments, bound, ct).ConfigureAwait(false);
        if (listed is not null) GitRefReads.Keep(folder, before, listed);
        return listed;
    }

    /// <summary>A <c>cat-file --batch</c> of blobs by full id from the library (<see cref="GitLibraryObjects"/>); null where git answers.</summary>
    internal static async Task<(byte[] Bytes, int Count)?> ReadBytesInProcessAsync(
        this GitService service, string folder, IReadOnlyList<string> arguments, string? input, int limit, TimeSpan bound, CancellationToken ct)
    {
        if (GitLibraryObjects.Asked(arguments, input, limit) is not { } asked) return null;
        using var within = CancellationTokenSource.CreateLinkedTokenSource(ct);
        within.CancelAfter(bound);
        try
        {
            return await GitLibrary.AskAsync(folder, GitLibraryQuestion.Revisions, asked, within.Token).ConfigureAwait(false) is { } bytes ? (bytes, bytes.Length) : null;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && within.IsCancellationRequested)
        {
            return null;
        }
    }

    private static async Task<GitLibraryAnswer?> LibraryAsync(string folder, IReadOnlyList<string> arguments, TimeSpan bound, CancellationToken ct)
    {
        using var within = CancellationTokenSource.CreateLinkedTokenSource(ct);
        within.CancelAfter(bound);
        try
        {
            return await GitLibraryReads.AnswerAsync(folder, arguments, within.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && within.IsCancellationRequested)
        {
            return null;
        }
    }
}
