using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// <c>rev-parse --verify --quiet ID</c>, <c>ID^{commit}</c>, <c>ID^{tree}</c> and <c>ID:path</c> of an object named by its full id, answered
/// from the object store as git says them (<see cref="GitLibraryRevisions.Peel"/>, <see cref="GitLibraryRevisions.EntryAt"/>). No ref, HEAD,
/// index or working tree decides any of these, so no flow memo is needed beneath them.
/// </summary>
/// <remarks>
/// Only what the store holds and says plainly: an object that is missing (git may fetch it, or accept a bare id), a path git would normalise
/// or refuse (<c>.</c>, <c>..</c>, an empty or backslashed part, a trailing slash) and a gitlink are git's. An existing object that has no
/// commit or tree to peel to, and a path the tree lacks, are git's quiet exit 1.
/// </remarks>
internal static partial class GitLibraryIds
{
    private static readonly GitLibraryAnswer Absent = new(1, "");

    [GeneratedRegex(@"^([0-9a-f]{40})(\^\{(commit|tree)\}|:(.+))?$", RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex Named();

    internal static (GitLibraryQuestion, Func<GitLibraryCall, GitLibraryAnswer?>)? Asked(List<string> rest)
    {
        if (rest is not ["--verify", "--quiet" or "-q", var asked] || Named().Match(asked) is not { Success: true } match) return null;
        var id = match.Groups[1].Value;
        if (!match.Groups[4].Success) return (GitLibraryQuestion.Revisions, call => Said(GitLibraryRevisions.Peel(call, id, match.Groups[3].Value)));
        var path = match.Groups[4].Value;
        return Plain(path) ? (GitLibraryQuestion.Revisions, call => Said(GitLibraryRevisions.EntryAt(call, id, path))) : null;
    }

    /// <summary>Whether <paramref name="path"/> is one git reads the same wherever it is asked: no <c>.</c> or <c>..</c> part, no empty one, no backslash.</summary>
    internal static bool Plain(string path) =>
        path.IndexOfAny(['\\', '\0', '\n']) < 0 && path.Split('/').All(part => part.Length > 0 && part is not ("." or ".."));

    private static GitLibraryAnswer? Said(string? id) => id is null ? null : id.Length == 0 ? Absent : new GitLibraryAnswer(0, id + "\n");
}
