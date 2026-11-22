namespace Daoris.Driver;

/// <summary>The service's in-process half, beside the library it answers from (the platform below cannot name it).</summary>
internal static class GitServiceReads
{
    /// <summary>
    /// The library's answer to a read, with no git started and no place taken; null where git must answer it, and where the
    /// library did not answer within <paramref name="bound"/>, so git answers it under its own deadline.
    /// </summary>
    internal static async Task<GitLibraryAnswer?> ReadInProcessAsync(
        this GitService service, string folder, IReadOnlyList<string> arguments, TimeSpan bound, CancellationToken ct)
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
