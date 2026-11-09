using System.Text;

namespace Daoris.Driver;

/// <summary>
/// Every object Daoris reads, its bytes, text, type or presence, as one start of <c>git cat-file --batch</c> in the asker's folder asked
/// one question, read by <see cref="GitObjectAnswer"/>. A blob missing in a repository with a promisor is asked once more as a start
/// allowed to fetch (<see cref="GitLazyFetch.Fetching"/>); a type or a presence is what this repository holds, and is never fetched.
/// </summary>
/// <remarks>
/// A commit's fields are not read here: its parents, author and message are the format read git's history gives, which applies a
/// shallow boundary, grafts and <c>.mailmap</c> as no stored object's bytes do.
/// </remarks>
internal static class GitObjectReads
{
    /// <summary>The most of a file read whole: past it, its text is none rather than a part of it.</summary>
    internal const int Whole = 64 * 1024 * 1024;

    /// <summary>A small object's room, so a commit's or a tag's answer is read to git's own end rather than ended.</summary>
    private const int Small = 4 * 1024;

    /// <summary>
    /// The object <paramref name="name"/> with up to <paramref name="limit"/> of its bytes; missing where git holds none after the one
    /// remote start; null where git failed or gave no answer it gives.
    /// </summary>
    internal static async Task<(GitObjectAnswer Answer, byte[] Bytes)?> ReadAsync(string root, GitObjectName name, int limit, CancellationToken ct)
    {
        var read = await NamedAsync(root, name, limit, ct).ConfigureAwait(false);
        if (read is { Answer.Missing: true } && GitLazyFetch.Promisor(root))
        {
            // A promised object its remote does not give ends git (`could not fetch ... from promisor remote`): still missing.
            using (GitLazyFetch.Fetching()) read = await NamedAsync(root, name, limit, ct).ConfigureAwait(false) ?? read;
        }

        return read;
    }

    /// <summary>The object's header alone: its type and size, or missing; null where git failed. Never fetched.</summary>
    internal static async Task<GitObjectAnswer?> InfoAsync(string root, GitObjectName name, CancellationToken ct) =>
        (await NamedAsync(root, name, Small, ct).ConfigureAwait(false))?.Answer;

    /// <summary>The blob's first <paramref name="limit"/> bytes as text; null where it is missing, not a blob, or git failed.</summary>
    internal static async Task<string?> TextAsync(string root, GitObjectName name, int limit, CancellationToken ct) =>
        await ReadAsync(root, name, limit, ct).ConfigureAwait(false) is ({ Missing: false, Type: "blob" } answer, var bytes) ? Text(bytes, answer) : null;

    /// <summary>The blob's whole text; null where it is missing, not a blob, larger than <see cref="Whole"/>, or git failed.</summary>
    internal static async Task<string?> TextAsync(string root, GitObjectName name, CancellationToken ct) =>
        await ReadAsync(root, name, Whole, ct).ConfigureAwait(false) is ({ Missing: false, Type: "blob" } answer, var bytes) && answer.Count == answer.Size
            ? Text(bytes, answer)
            : null;

    /// <summary>
    /// The objects <paramref name="names"/> ask, each answered whole up to <paramref name="limit"/>, by one start asked a line each: the
    /// store's listing of a commit, its tree and each folder on a path's way. Null where git failed, a name is not one line, or an answer
    /// came short of its size.
    /// </summary>
    internal static async Task<IReadOnlyList<(GitObjectAnswer Answer, byte[] Bytes)>?> BatchAsync(
        string root, IReadOnlyList<GitObjectName> names, int limit, CancellationToken ct)
    {
        if (names.Count == 0 || names.Any(name => !name.Framed)) return null;
        var room = names.Sum(name => (long)GitObjectAnswer.HeaderRoom(name.Question) + Math.Max(0, limit) + 1);
        var read = await WorkingTree.GitBytesAsync(root, ["cat-file", "--batch"], (int)Math.Min(room, Whole), string.Concat(names.Select(name => name.Question + "\n")), ct)
            .ConfigureAwait(false);
        if (read is not { } bytes) return null;

        var answers = new List<(GitObjectAnswer, byte[])>(names.Count);
        var at = 0;
        foreach (var _ in names)
        {
            if (GitObjectAnswer.Parse(bytes.Bytes, bytes.Count, at) is not { } answer || answer.Count < answer.Size) return null;
            answers.Add((answer, bytes.Bytes));
            // Git writes a line feed after each object's bytes.
            at = answer.Start + (answer.Missing ? 0 : answer.Count + 1);
        }

        return answers;
    }

    /// <summary>
    /// An answer's bytes as text, read as git's text output is read: UTF-8, a leading byte-order mark taken as the mark it is.
    /// </summary>
    internal static string Text(byte[] bytes, GitObjectAnswer answer)
    {
        using var reader = new StreamReader(new MemoryStream(bytes, answer.Start, answer.Count), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static async Task<(GitObjectAnswer Answer, byte[] Bytes)?> NamedAsync(string root, GitObjectName name, int limit, CancellationToken ct)
    {
        if (name.Framed) return await AskAsync(root, name.Question, limit, ct).ConfigureAwait(false);
        if (name.Path is null || !GitObjectName.Line(name.Name)) return null;
        // The path is an argument, never a line of the question: git resolves it to the id asked.
        var (code, id, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"{name.Name}:{name.Path}"], ct).ConfigureAwait(false);
        if (code == 1) return (new GitObjectAnswer(true, "", 0, 0, 0), []);
        return code == 0 && GitObjectName.Line(id.Trim()) ? await AskAsync(root, id.Trim(), limit, ct).ConfigureAwait(false) : null;
    }

    private static async Task<(GitObjectAnswer Answer, byte[] Bytes)?> AskAsync(string root, string question, int limit, CancellationToken ct)
    {
        var read = await WorkingTree.GitBytesAsync(root, ["cat-file", "--batch"], GitObjectAnswer.HeaderRoom(question) + Math.Max(0, limit), question + "\n", ct)
            .ConfigureAwait(false);
        // The header's room, unused by a short header, is not more of the object than the asker's limit.
        return read is { } bytes && GitObjectAnswer.Parse(bytes.Bytes, bytes.Count) is { } answer
            ? (answer with { Count = Math.Min(answer.Count, Math.Max(0, limit)) }, bytes.Bytes)
            : null;
    }
}
