namespace Daoris.Driver;

/// <summary>
/// A branch name the line may be set to (WSR2): git's own rules for a ref name, the ones a person could
/// break by typing. The CLI's <c>driverconfig.ts</c> holds the same rule and the same test table.
/// </summary>
public static class BranchName
{
    public static bool IsValid(string name) =>
        name.Length is > 0 and <= 200
        && name != "@"
        && !name.StartsWith('-') && !name.StartsWith('/') && !name.EndsWith('/') && !name.EndsWith('.')
        && !name.Contains("..", StringComparison.Ordinal)
        && !name.Contains("//", StringComparison.Ordinal)
        && !name.Contains("@{", StringComparison.Ordinal)
        && !name.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || c is '~' or '^' or ':' or '?' or '*' or '[' or '\\')
        && name.Split('/').All(part => !part.StartsWith('.') && !part.EndsWith(".lock", StringComparison.Ordinal));
}

/// <summary>Where a repository's line came from, for the screen and the sentences that name it.</summary>
public static class LineSource
{
    public const string Repository = "repository";
    public const string Workspace = "workspace";
    public const string Checkout = "checkout";
    public const string None = "none";
}

/// <summary>A repository's line and what said so. <see cref="Branch"/> is null only where nothing names one.</summary>
public sealed record Line(string? Branch, string Source);

/// <summary>One repository's line as the screen shows it: which, in which circle, and what said so.</summary>
public sealed record RepositoryLine(string Repository, string Workspace, string? Branch, string Source);

/// <summary>
/// Which line a repository's work grows from and lands on (WSR2), read from one place by every door:
/// a session tree's start, the merge, a tree's removal, and sync's feed. What the person set for the
/// repository, then for its workspace, then the checkout's own guess (`origin/HEAD`, else `main`, else
/// `master`), and the answer always says which.
/// </summary>
public static class CanonicalLine
{
    /// <remarks>A repository in no workspace is in the `default` one (D48 §2), so its line is found under that name.</remarks>
    public static Line Choose(DriverConfig config, string repository, string? workspace, string? guess)
    {
        if (config.Lines.TryGetValue(repository, out var set)) return new(set, LineSource.Repository);
        if (config.WorkspaceLines.TryGetValue(RemoteTarget.Workspace(workspace), out var shared)) return new(shared, LineSource.Workspace);
        return guess is not null ? new(guess, LineSource.Checkout) : new(null, LineSource.None);
    }

    /// <summary>
    /// Every repository's line, for the screen: git is asked only for one with a checkout here and
    /// nothing set, and one with no checkout here is answered from what is set alone.
    /// </summary>
    public static async Task<IReadOnlyList<RepositoryLine>> OfAsync(
        DriverConfig config, IEnumerable<(string Repository, string? Workspace, string? Root)> repositories,
        CancellationToken ct = default)
    {
        var lines = new List<RepositoryLine>();
        foreach (var (repository, workspace, root) in repositories)
        {
            var line = string.IsNullOrWhiteSpace(root)
                ? Choose(config, repository, workspace, guess: null)
                : await ResolveAsync(root, repository, workspace, config, ct).ConfigureAwait(false);
            lines.Add(new(repository, RemoteTarget.Workspace(workspace), line.Branch, line.Source));
        }

        return lines;
    }

    /// <summary>The line, with the checkout asked only when nothing is set.</summary>
    public static async Task<Line> ResolveAsync(
        string root, string repository, string? workspace, DriverConfig config, CancellationToken ct = default)
    {
        var chosen = Choose(config, repository, workspace, guess: null);
        if (chosen.Branch is not null) return chosen;
        return Choose(config, repository, workspace, await WorkingTree.DefaultBranchAsync(root, ct).ConfigureAwait(false));
    }

    /// <summary>The line, spelled for a person: the branch and what said so.</summary>
    public static string Describe(Line line) => line.Source switch
    {
        LineSource.Repository => $"`{line.Branch}`, the line set for this repository",
        LineSource.Workspace => $"`{line.Branch}`, the line set for its workspace",
        LineSource.Checkout => $"the canonical line `{line.Branch}`",
        _ => "no line: none is set and git names none",
    };
}
