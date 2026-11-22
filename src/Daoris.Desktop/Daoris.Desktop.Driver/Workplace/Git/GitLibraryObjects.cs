using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// <c>cat-file --batch</c> of blobs, answered from the object store as git writes it: a line a full id or a full id and a path, each answered
/// <c>ID blob SIZE</c>, the raw bytes and a line feed, or <c>QUESTION missing</c> where the id's tree lacks the path; cut at the asker's limit
/// as the git reader cuts it (<see cref="GitRuns.GitBytesAsync(string, IReadOnlyList{string}, int, CancellationToken)"/>).
/// </summary>
/// <remarks>
/// Only what is exact: every line must be answerable, else git answers the whole question. A tree, a commit, a tag, a missing object (git may
/// fetch a promised one), a path with an operator part and a name that is no full id are git's. The bytes are the stored ones, so no
/// filter, attribute or line-ending setting changes them, as for git's own <c>cat-file</c>.
/// </remarks>
internal static partial class GitLibraryObjects
{
    [GeneratedRegex(@"^([0-9a-f]{40})(?::(.+))?$", RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex Question();

    /// <summary>The service's question: the library's answer to <paramref name="input"/> as a <c>--batch</c> read of at most <paramref name="limit"/> bytes, or a null where git answers.</summary>
    internal static Func<GitLibraryCall, byte[]?>? Asked(IReadOnlyList<string> arguments, string? input, int limit)
    {
        if (!GitObjectReads.IsBatch(arguments) || string.IsNullOrEmpty(input) || limit <= 0 || input[^1] != '\n') return null;
        var lines = input[..^1].Split('\n');
        var asked = new List<(string Line, string Id, string? Path)>();
        foreach (var line in lines)
        {
            if (Question().Match(line) is not { Success: true } match || match.Groups[2] is { Success: true } path && !GitLibraryIds.Plain(path.Value)) return null;
            asked.Add((line, match.Groups[1].Value, match.Groups[2].Success ? match.Groups[2].Value : null));
        }

        return call => Answer(call, asked, limit);
    }

    private static byte[]? Answer(GitLibraryCall call, List<(string Line, string Id, string? Path)> asked, int limit)
    {
        var written = new MemoryStream();
        foreach (var (line, id, path) in asked)
        {
            var room = limit - written.Length;
            if (GitLibraryRevisions.BlobAt(call, id, path, room) is not { } blob) return null;
            if (blob.Id.Length == 0)
            {
                written.Write(Encoding.UTF8.GetBytes(line + " missing\n"));
            }
            else
            {
                written.Write(Encoding.UTF8.GetBytes($"{blob.Id} blob {blob.Size}\n"));
                written.Write(blob.Bytes);
                written.WriteByte((byte)'\n');
            }

            if (written.Length >= limit) break;
        }

        return written.Length > limit ? written.GetBuffer().AsSpan(0, limit).ToArray() : written.ToArray();
    }
}
