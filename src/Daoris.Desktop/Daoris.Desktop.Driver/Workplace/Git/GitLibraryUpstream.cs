namespace Daoris.Driver;

/// <summary>
/// A branch's upstream as <c>&lt;branch&gt;@{upstream}</c> resolves it, from the branch's configuration with no git started, in the two forms
/// the driver asks: <c>rev-parse --abbrev-ref --symbolic-full-name</c> (the upstream's short name) and <c>rev-parse --verify --quiet
/// ...^{commit}</c> (its commit). Answered where the branch is here and either has no upstream configured at all (git's words for it
/// are said as git says them) or its upstream's ref is here; a branch that is not, an upstream whose ref is gone and any configuration
/// <see cref="GitLibraryRefs"/> does not map as git does are git's.
/// </summary>
internal static class GitLibraryUpstream
{
    private const string Suffix = "@{upstream}";
    private const string Peel = "^{commit}";

    /// <summary>The library's question for <paramref name="rest"/>, the arguments after <c>rev-parse</c>; null where it is git's.</summary>
    internal static (GitLibraryQuestion, Func<GitLibraryCall, GitLibraryAnswer?>)? Asked(List<string> rest)
    {
        if (rest is ["--abbrev-ref", "--symbolic-full-name", var shortened] && Branch(shortened, "") is { } one) return (GitLibraryQuestion.Refs, call => Short(call, one));
        if (rest is ["--verify", "--quiet" or "-q", var verified] && verified.EndsWith(Peel, StringComparison.Ordinal) && Branch(verified, Peel) is { } other)
        {
            return (GitLibraryQuestion.Refs, call => Commit(call, other));
        }

        return null;
    }

    private static string? Branch(string asked, string tail)
    {
        if (!asked.EndsWith(Suffix + tail, StringComparison.Ordinal)) return null;
        var name = asked[..^(Suffix.Length + tail.Length)];
        return name.Length > 0 && name.All(letter => char.IsAsciiLetterOrDigit(letter) || letter is '-' or '_' or '/' or '.')
               && !name.Contains("..", StringComparison.Ordinal) && !name.Contains("//", StringComparison.Ordinal) && !name.StartsWith('-')
               && !name.StartsWith('/') && !name.EndsWith('/') && !name.EndsWith('.') && !name.EndsWith(".lock", StringComparison.Ordinal)
            ? name
            : null;
    }

    private static GitLibraryAnswer? Short(GitLibraryCall call, string branch) => Held(call, branch) switch
    {
        (true, null) => new GitLibraryAnswer(128, "", $"fatal: no upstream configured for branch '{branch}'\n"),
        (true, { } full) => new GitLibraryAnswer(0, GitLibraryRefs.Shorten(call, full) + "\n"),
        _ => null,
    };

    private static GitLibraryAnswer? Commit(GitLibraryCall call, string branch)
    {
        if (Held(call, branch) is not (true, var full)) return null;
        if (full is null) return new GitLibraryAnswer(1, "");
        return GitLibraryRevisions.CommitOf(call, full) is { } commit ? new GitLibraryAnswer(0, commit.Sha + "\n") : null;
    }

    /// <summary>Whether the library may say, and the upstream's full name where one is configured and its ref is here; null with none configured.</summary>
    private static (bool Said, string? Full) Held(GitLibraryCall call, string branch)
    {
        if (call.Repository.Refs["refs/heads/" + branch] is null || GitLibraryRefs.Branching(call) is not { } config) return (false, null);
        if (!config.ContainsKey($"branch.{branch}.merge") && !config.ContainsKey($"branch.{branch}.remote")) return (true, null);
        if (!GitLibraryRefs.Upstream(config, branch, out var full) || full is null) return (false, null);
        return call.Repository.Refs[full]?.ResolveToDirectReference() is null ? (false, null) : (true, full);
    }
}
