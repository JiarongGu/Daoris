namespace Daoris.Driver;

/// <summary>
/// The work as merged onto its line's tip, which a run's check judges: made from git's objects alone (<c>merge-tree --write-tree</c>,
/// then <c>commit-tree</c>), so the session's branch never moves and nothing is rebased. Work that holds the line's tip is checked as it
/// stands, a clean merge is checked as merged, and a conflict names its paths, which only the session can put right.
/// </summary>
/// <param name="Line">The line's name, as the words say it.</param>
/// <param name="Onto">The line's tip the work is merged onto; null where git could not merge, so the work is checked as it stands.</param>
/// <param name="Work">The work's commit, the one the check's verdict names and the landing lands.</param>
/// <param name="At">The commit the verification tree takes: <paramref name="Work"/> where it holds the line's tip or git could not merge, else the merge; null where they conflict.</param>
/// <param name="Conflicts">The paths the merge could not settle.</param>
internal sealed record LineMerge(string Line, string? Onto, string Work, string? At, IReadOnlyList<string> Conflicts)
{
    private const int Named = 10;

    /// <summary>The merge's words for the session; null where the work holds the line's tip and there is nothing to say.</summary>
    public string? Words()
    {
        if (Onto is not { } onto) return $"Daoris could not merge this work onto `{Line}`, so it is checked as it stands.";
        var at = TextBounds.ShortCommit(onto, 7);
        if (At is null)
        {
            var named = string.Join(", ", Conflicts.Take(Named)) + (Conflicts.Count > Named ? $" and {Conflicts.Count - Named} more" : "");
            return $"The line moved past this work and merging it onto `{Line}` at {at} conflicts in {named}: merge `{Line}` into the branch,"
                + $" resolve {(Conflicts.Count == 1 ? "that path" : "those paths")} and commit again.";
        }

        return At == Work ? null : $"The line moved past this work, so it is checked as merged onto `{Line}` at {at}; its branch is left as it is.";
    }

    /// <summary>
    /// <paramref name="work"/> merged onto <paramref name="line"/>'s tip in <paramref name="folder"/>'s repository: the merge commit is an
    /// object only, its parents the line's tip and the work, written under Daoris's name.
    /// </summary>
    internal static async Task<LineMerge> ReadAsync(string folder, string line, string work, CancellationToken ct)
    {
        var unread = new LineMerge(line, null, work, work, []);
        var (found, tip, _) = await WorkingTree.GitAsync(folder, ["--no-optional-locks", "rev-parse", "--verify", "--quiet", $"{line}^{{commit}}"], ct).ConfigureAwait(false);
        if (found != 0 || tip.Trim() is not { Length: > 0 } onto) return unread;
        var (held, _, _) = await WorkingTree.GitAsync(folder, ["merge-base", "--is-ancestor", onto, work], ct).ConfigureAwait(false);
        if (held == 0) return new LineMerge(line, onto, work, work, []);
        if (held != 1) return unread;

        var (merged, output, _) = await WorkingTree.GitAsync(
            folder, ["-c", "core.quotePath=false", "merge-tree", "--write-tree", "--name-only", "--no-messages", onto, work], ct).ConfigureAwait(false);
        if (Written(merged, output) is not { } written) return unread;
        if (written.Tree is not { } tree) return new LineMerge(line, onto, work, null, written.Conflicts);
        var (made, commit, _) = await WorkingTree.GitAsync(
            folder, ["-c", "user.name=Daoris", "-c", "user.email=daoris@localhost", "commit-tree", "--no-gpg-sign", tree, "-p", onto, "-p", work,
                "-m", $"The work as merged onto {line}, which its check judges"], ct).ConfigureAwait(false);
        return made == 0 && commit.Trim() is { Length: > 0 } at ? new LineMerge(line, onto, work, at, []) : unread;
    }

    /// <summary>What <c>merge-tree --write-tree --name-only --no-messages</c> said: the tree it wrote where the merge is clean, else the paths it could not settle; null where git did not merge.</summary>
    internal static (string? Tree, IReadOnlyList<string> Conflicts)? Written(int code, string output)
    {
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return (code, lines) switch
        {
            (0, [var tree, ..]) => (tree, []),
            (1, [_, .. var paths]) when paths.Length > 0 => (null, [.. paths.Distinct(StringComparer.Ordinal)]),
            _ => null,
        };
    }
}
