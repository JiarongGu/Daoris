namespace Daoris.Driver;

/// <summary>
/// Where a folder's working tree and repository are, and what its own configuration says of the case of file names, answered in git's
/// words from the folder's own files: <c>rev-parse --show-toplevel</c>, the one <c>rev-parse</c> that asks the common directory, the
/// top and the branch (<see cref="GitReadOnce.Layout"/>), and <c>config</c> of <c>core.ignorecase</c> as a boolean. A folder that is
/// not a working tree's own top (a subfolder, a bare repository), a <c>core.worktree</c>, an unborn or an ambiguous head, a
/// <c>GIT_DIR</c> or <c>GIT_WORK_TREE</c> in the process, and an <c>ignorecase</c> the repository's own configuration does not hold
/// are git's.
/// </summary>
internal static class GitLibraryLayout
{
    private static readonly string[] Variables = ["GIT_DIR", "GIT_WORK_TREE", "GIT_COMMON_DIR", "GIT_CEILING_DIRECTORIES"];

    private static readonly string[] Shortenings = ["refs/{0}", "refs/tags/{0}", "refs/heads/{0}", "refs/remotes/{0}", "refs/remotes/{0}/HEAD"];

    /// <summary>The library's question for <paramref name="rest"/>, the arguments after <c>rev-parse</c>; null where it is git's.</summary>
    internal static (GitLibraryQuestion, Func<GitLibraryCall, GitLibraryAnswer?>)? RevParse(List<string> rest)
    {
        if (rest.SequenceEqual(["--show-toplevel"])) return (GitLibraryQuestion.Layout, call => Top(call) is { } top ? new GitLibraryAnswer(0, top + "\n") : null);
        if (rest.SequenceEqual(["--show-toplevel", "--abbrev-ref", "HEAD"]))
        {
            return (GitLibraryQuestion.Layout, call => Top(call) is { } top && Branch(call) is { } branch ? new GitLibraryAnswer(0, top + "\n" + branch + "\n") : null);
        }

        if (!rest.SequenceEqual(GitReadOnce.Layout.Skip(1))) return null;
        return (GitLibraryQuestion.Layout, call =>
            Top(call) is { } top && GitLibrarySpelling.Real(call.CommonDir) is { } common && Branch(call) is { } branch
                ? new GitLibraryAnswer(0, string.Join('\n', common, top, branch) + "\n")
                : null);
    }

    /// <summary>The library's question for <paramref name="rest"/>, the arguments after <c>config</c>; null where it is git's.</summary>
    internal static (GitLibraryQuestion, Func<GitLibraryCall, GitLibraryAnswer?>)? Config(List<string> rest)
    {
        if (!rest.SequenceEqual(["--bool", "core.ignorecase"]) && !rest.SequenceEqual(["--type=bool", "--get", "core.ignorecase"])) return null;
        return (GitLibraryQuestion.Layout, call => IgnoreCase(call) is { } value ? new GitLibraryAnswer(0, (value ? "true" : "false") + "\n") : null);
    }

    /// <summary><c>core.ignorecase</c> as the repository's own configuration holds it; null where it holds none, or the files say more than a name.</summary>
    internal static bool? IgnoreCase(GitLibraryCall call)
    {
        if (!Plain(call)) return null;
        var found = Keys(call, "ignorecase");
        return found.Count == 0 ? null : Truth(found[^1]);
    }

    /// <summary>The folder's top, as git prints it; null where the folder is not the top of a working tree git would name so.</summary>
    internal static string? Top(GitLibraryCall call)
    {
        if (!Plain(call) || call.Repository.Info.IsBare || call.Repository.Info.WorkingDirectory is not { } work) return null;
        return GitLibrarySpelling.Real(work);
    }

    private static string? Branch(GitLibraryCall call)
    {
        var repository = call.Repository;
        if (repository.Info.IsHeadUnborn) return null;
        if (repository.Info.IsHeadDetached) return "HEAD";
        var head = repository.Head.CanonicalName;
        if (!head.StartsWith("refs/heads/", StringComparison.Ordinal)) return null;
        var name = head["refs/heads/".Length..];
        // --abbrev-ref says "heads/x" where another ref would make "x" ambiguous.
        return Shortenings.Any(rule => rule != "refs/heads/{0}" && repository.Refs[string.Format(rule, name)] is not null) ? null : name;
    }

    internal static bool Plain(GitLibraryCall call) =>
        Variables.All(name => string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name))) && !Includes(call) && !Keys(call, "worktree").Any()
        && !Keys(call, "bare").Any(Truth);

    // A file another file includes may say any of it, so the repository's configuration is git's to read.
    private static bool Includes(GitLibraryCall call) =>
        new[] { Path.Combine(call.CommonDir, "config"), Path.Combine(call.GitDir, "config.worktree") }
            .Any(file => File.Exists(file) && File.ReadLines(file).Any(line => line.TrimStart().StartsWith("[include", StringComparison.OrdinalIgnoreCase)));

    private static bool Truth(string value) => value.Trim().ToLowerInvariant() is "true" or "yes" or "on" or "1" or "";

    /// <summary>The values the repository's own files give <c>core.&lt;key&gt;</c>, in the order git reads them.</summary>
    private static List<string> Keys(GitLibraryCall call, string key)
    {
        var found = new List<string>();
        foreach (var file in new[] { Path.Combine(call.CommonDir, "config"), Path.Combine(call.GitDir, "config.worktree") })
        {
            if (!File.Exists(file)) continue;
            var inCore = false;
            foreach (var raw in File.ReadLines(file))
            {
                var line = raw.Trim();
                if (line.StartsWith('['))
                {
                    inCore = line.Equals("[core]", StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (inCore && line.Split('=', 2) is [var name, var value] && name.Trim().Equals(key, StringComparison.OrdinalIgnoreCase)) found.Add(value.Trim());
            }
        }

        return found;
    }

}
