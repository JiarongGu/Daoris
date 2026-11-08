namespace Daoris.Driver;

/// <summary>
/// An object's bytes as one start of <c>git cat-file --batch</c> in the asker's folder, asked one question and read by
/// <see cref="GitObjectAnswer"/>; where it answers missing in a repository with a promisor, asked once more as a start allowed to
/// fetch (<see cref="GitLazyFetch.Fetching"/>), which waits outside Daoris.
/// </summary>
internal static class GitObjectReads
{
    /// <summary>
    /// The object <paramref name="id"/> with up to <paramref name="limit"/> of its bytes; missing where git holds none after the one
    /// remote start; null where git failed or gave no answer it gives.
    /// </summary>
    internal static async Task<(GitObjectAnswer Answer, byte[] Bytes)?> ReadAsync(string root, string id, int limit, CancellationToken ct)
    {
        var read = await AskAsync(root, id, limit, ct).ConfigureAwait(false);
        if (read is { Answer.Missing: true } && GitLazyFetch.Promisor(root))
        {
            // A promised object its remote does not give ends git (`could not fetch ... from promisor remote`): still missing.
            using (GitLazyFetch.Fetching()) read = await AskAsync(root, id, limit, ct).ConfigureAwait(false) ?? read;
        }

        return read;
    }

    private static async Task<(GitObjectAnswer Answer, byte[] Bytes)?> AskAsync(string root, string id, int limit, CancellationToken ct)
    {
        // A question is one line: an id holds no newline, and nothing else is asked here.
        if (id.Length == 0 || id.Any(char.IsWhiteSpace)) return null;
        var read = await WorkingTree.GitBytesAsync(root, ["cat-file", "--batch"], GitObjectAnswer.HeaderBound + Math.Max(0, limit), id + "\n", ct)
            .ConfigureAwait(false);
        // The header's room, unused by a short header, is not more of the object than the asker's limit.
        return read is { } bytes && GitObjectAnswer.Parse(bytes.Bytes, bytes.Count) is { } answer
            ? (answer with { Count = Math.Min(answer.Count, Math.Max(0, limit)) }, bytes.Bytes)
            : null;
    }
}
