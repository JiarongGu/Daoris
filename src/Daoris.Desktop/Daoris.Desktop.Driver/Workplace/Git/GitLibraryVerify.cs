namespace Daoris.Driver;

/// <summary>
/// <c>rev-parse --verify --quiet NAME</c> and <c>NAME^{commit}</c> for <c>HEAD</c> or a full ref name, answered as git does: the id and
/// exit 0, or exit 1 and nothing where no ref answers or the ref's object is not a commit. A short name, an operator, a dangling
/// symbolic ref and an object not here are git's. The failing form that is not quiet says its words on the error stream, so it is git's.
/// </summary>
internal static class GitLibraryVerify
{
    private const string CommitSuffix = "^{commit}";

    private static readonly string[] Rules = ["{0}", "refs/{0}", "refs/tags/{0}", "refs/heads/{0}", "refs/remotes/{0}", "refs/remotes/{0}/HEAD"];

    private static readonly GitLibraryAnswer Absent = new(1, "");

    /// <summary>The library's question for <paramref name="rest"/>, the arguments after <c>rev-parse</c>; null where it is git's.</summary>
    internal static (GitLibraryQuestion, Func<GitLibraryCall, GitLibraryAnswer?>)? Asked(List<string> rest)
    {
        // Not quiet, a failure speaks on the error stream: only the id is the library's.
        if (rest is ["HEAD"] or ["--verify", "HEAD"])
        {
            return (GitLibraryQuestion.Revisions, call => Verified(call, "HEAD", peel: false) is { Code: 0 } found ? found : null);
        }

        if (rest is not ["--verify", "--quiet" or "-q", var asked]) return null;
        var peel = asked.EndsWith(CommitSuffix, StringComparison.Ordinal);
        var name = peel ? asked[..^CommitSuffix.Length] : asked;
        if (!Plain(name)) return null;
        return (GitLibraryQuestion.Revisions, call => Verified(call, name, peel));
    }

    private static bool Plain(string name) =>
        (name == "HEAD" || name.StartsWith("refs/", StringComparison.Ordinal))
        && !name.Contains("..", StringComparison.Ordinal)
        && name.IndexOfAny(['^', '~', ':', '@', '*', '?', '[', '\\', ' ', '{']) < 0;

    private static GitLibraryAnswer? Verified(GitLibraryCall call, string name, bool peel)
    {
        var repository = call.Repository;
        if (name == "HEAD")
        {
            if (repository.Info.IsHeadUnborn) return Absent;
            return repository.Head.Tip is { } tip ? new GitLibraryAnswer(0, tip.Sha + "\n") : null;
        }

        foreach (var rule in Rules)
        {
            if (repository.Refs[string.Format(rule, name)] is { } reference) return Found(call, reference.CanonicalName, peel);
        }

        return Absent;
    }

    private static GitLibraryAnswer? Found(GitLibraryCall call, string name, bool peel)
    {
        if (call.Repository.Refs[name]?.ResolveToDirectReference() is not { } direct) return null;
        if (!peel) return new GitLibraryAnswer(0, direct.TargetIdentifier + "\n");
        if (direct.Target is null) return null;
        return GitLibraryRevisions.CommitOf(call, name) is { } commit ? new GitLibraryAnswer(0, commit.Sha + "\n") : Absent;
    }
}
