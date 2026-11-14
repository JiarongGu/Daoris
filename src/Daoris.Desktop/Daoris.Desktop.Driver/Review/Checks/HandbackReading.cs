namespace Daoris.Driver;

/// <summary>
/// A hand-back as a check step reads it from git, never from the session's note: the commit judged, the merge base with the line, how
/// many commits the line moved past it, the paths touched since, and the commits without a maker's trailer. A line moved past the
/// work, or a commit with no maker's trailer, is a fault only the session can put right, and the step's verdict.
/// </summary>
internal sealed record HandbackReading(string Tip, string? Base, int Behind, IReadOnlyList<string> Touched, IReadOnlyList<UnsignedCommit> Unsigned)
{
    /// <summary>The name the step's failed verdict gives the reading, where a gate would be named.</summary>
    internal const string Gate = "hand-back";

    /// <summary>The line the reading is made against, named in <see cref="Words"/>.</summary>
    internal string Line { get; init; } = "the line";

    public bool Faulty => Behind > 0 || Unsigned.Count > 0;

    /// <summary>The reading in words, for the session: what Daoris read and what it holds against the work.</summary>
    public string Words()
    {
        var lines = new List<string>
        {
            $"Daoris read this hand-back from git, not from the note: based on {Base ?? "no common commit"}"
            + (Behind > 0 ? $", {Behind} {(Behind == 1 ? "commit" : "commits")} behind `{Line}`" : ", up to date with the line")
            + $"; {Touched.Count} {(Touched.Count == 1 ? "path" : "paths")} touched.",
        };
        if (Behind > 0) lines.Add($"The line moved past this work: bring the branch up to date with `{Line}` (rebase it onto `{Line}`) and commit again.");
        if (CommitAttribution.Words(Unsigned) is { } unsigned) lines.Add(unsigned);
        return string.Join('\n', lines);
    }

    /// <summary>The reading of <paramref name="commit"/> in <paramref name="folder"/> against <paramref name="line"/>, or null where git could not say.</summary>
    internal static async Task<HandbackReading?> ReadAsync(string folder, string line, string commit, CancellationToken ct)
    {
        var repository = MachinePorts.Repository;
        var mergeBase = await repository.MergeBaseAsync(folder, commit, line, ct).ConfigureAwait(false);
        if (mergeBase is null) return null;
        var behind = await repository.CountAsync(folder, line, [commit], ct).ConfigureAwait(false);
        var touched = await repository.ChangedAsync(folder, mergeBase, commit, ct).ConfigureAwait(false);
        var unsigned = await CommitAttribution.UnsignedAsync(folder, mergeBase, commit, ct).ConfigureAwait(false);
        return touched is null || unsigned is null ? null : new HandbackReading(commit, mergeBase, behind ?? 0, touched, unsigned) { Line = line };
    }

    /// <summary>The line a tree's work goes back to: the remote's head where it names one, else a local <c>main</c> or <c>master</c>; null where none is there.</summary>
    internal static async Task<string?> LineAsync(string folder, CancellationToken ct)
    {
        if (await OneAsync(folder, ["symbolic-ref", "--short", "refs/remotes/origin/HEAD"], ct).ConfigureAwait(false) is { } remote) return remote;
        foreach (var name in new[] { "main", "master" })
        {
            if (await OneAsync(folder, ["rev-parse", "--verify", "--quiet", name], ct).ConfigureAwait(false) is not null) return name;
        }

        return null;
    }

    private static async Task<string?> OneAsync(string folder, string[] arguments, CancellationToken ct)
    {
        var (code, stdout, _) = await WorkingTree.GitAsync(folder, ["--no-optional-locks", .. arguments], ct).ConfigureAwait(false);
        return code == 0 && stdout.Trim() is { Length: > 0 } read ? read : null;
    }
}
