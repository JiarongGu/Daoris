using System.Globalization;
using static Daoris.Driver.TreeGit;

namespace Daoris.Driver;

/// <summary>Where the session trees stand, read from the layout and from git: the list, how far a tree is ahead of its line, and whether the line holds a commit.</summary>
internal sealed class TreeListings(TreeContext context, TreeSetAside setAsides)
{
    /// <summary>
    /// How many commits a session tree's HEAD holds beyond its repository's line: zero is a done that made no commits,
    /// which lands nothing; null is git unable to say, or no line it can name, and the landing then decides.
    /// </summary>
    public async Task<int?> AheadOfLineAsync(string tree, CancellationToken ct = default)
    {
        var full = Path.GetFullPath(tree);
        if (!context.Holds(full) || !Directory.Exists(full)) return null;
        var (code, commonDir, _) = await WorkingTree.GitAsync(full, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct)
            .ConfigureAwait(false);
        if (code != 0) return null;
        var root = Path.GetDirectoryName(commonDir.Trim())!;
        var (workspace, repository) = context.OwnerOf(full);
        var line = (await context.LineAsync(root, repository, workspace, ct).ConfigureAwait(false)).Branch;
        var against = line is null ? null : await ComparableAsync(root, line, ct).ConfigureAwait(false);
        if (against is null) return null;
        var (countCode, count, _) = await WorkingTree.GitAsync(full, ["rev-list", "--count", $"{against}..HEAD"], ct).ConfigureAwait(false);
        return countCode == 0 && int.TryParse(count.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var ahead) ? ahead : null;
    }

    /// <summary>
    /// Whether the repository's line holds a commit: <c>merge-base --is-ancestor</c> in its checkout against the line as a landing
    /// reads it, so work that landed by any path settles the words that asked its session to rebase. Null where git cannot say: no line it can
    /// name, a commit this checkout does not have, or git failing, which establishes nothing.
    /// </summary>
    public async Task<bool?> LineHoldsAsync(string root, string repository, string workspace, string commit, CancellationToken ct = default)
    {
        if (!Directory.Exists(root)) return null;
        var line = (await context.LineAsync(root, repository, workspace, ct).ConfigureAwait(false)).Branch;
        var against = line is null ? null : await ComparableAsync(root, line, ct).ConfigureAwait(false);
        if (against is null) return null;
        // `--is-ancestor` answers 0 for yes and 1 for no; anything else is git failing to answer.
        var (code, _, _) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", commit, against], ct).ConfigureAwait(false);
        return code switch { 0 => true, 1 => false, _ => null };
    }

    /// <summary>The session trees and folders moved aside of the repositories <paramref name="only"/> holds (<see cref="ListAsync(CancellationToken)"/>).</summary>
    /// <param name="only">
    /// Whether a repository's folder is listed, by its name, in every workspace: another's is never entered, so its
    /// trees cost no git. Every repository's where null.
    /// </param>
    public async Task<IReadOnlyList<SessionTree>> ListAsync(Func<string, bool>? only, CancellationToken ct = default)
    {
        var found = new List<SessionTree>();
        IEnumerable<string> Repositories(string workspace) =>
            Directory.EnumerateDirectories(workspace).Where(folder => only?.Invoke(Path.GetFileName(folder)) ?? true);

        // Both places of the trees, each as found. The folders the look moved aside are no trees: git no
        // longer knows them as such.
        var folders = (
            from root in context.TreesRoots.Where(Directory.Exists)
            from workspace in Directory.EnumerateDirectories(root).Where(folder => Path.GetFileName(folder) != TreeSetAside.SetAsideFolder)
            from repository in Repositories(workspace)
            from tree in Directory.EnumerateDirectories(repository)
            select (workspace, repository, tree)).ToList();
        // A folder with no link to a repository is no tree, and git asked in it would answer for whatever repository holds the home.
        var branches = await TreeHeads.ReadAsync([.. folders.Select(each => each.tree).Where(tree => Path.Exists(Path.Combine(tree, ".git")))], ct)
            .ConfigureAwait(false);
        var grown = context.Grown.All();
        foreach (var (workspace, repository, tree) in folders)
        {
            var (code, branch) = branches.TryGetValue(tree, out var read) ? read : (1, SessionTree.NotATree);
            var named = code == 0 ? branch.Trim() : branch == SessionTree.NotATree ? branch : "(unreadable)";
            // Whose it is and why it stays, from the record the tree's branch is kept in (the run that owns it, what its release kept it for).
            var owner = grown.LastOrDefault(entry => SessionBranches.SameBranch(entry.Repository, entry.Branch, Path.GetFileName(repository), named));
            found.Add(new(tree, Path.GetFileName(workspace), Path.GetFileName(repository), named) { Run = owner?.Run, Reason = owner?.Kept });
        }

        // The folders moved aside at every place of the trees, the old place's included.
        foreach (var aside in setAsides.SetAsideRoots.Where(Directory.Exists))
        foreach (var workspace in SetAsideEligibility.Workspaces(aside))
        foreach (var repository in Repositories(workspace))
        foreach (var folder in Directory.EnumerateDirectories(repository))
        {
            found.Add(new(folder, Path.GetFileName(workspace), Path.GetFileName(repository), SessionTree.SetAside)
            {
                MovedAside = TreeSetAside.MovedAt(Path.GetFileName(folder)),
                // The package folders the slim dropped, from the manifest beside it, which the inventory's line names.
                Slimmed = SlimManifest.Read(SlimManifest.PathFor(folder)).Manifest?.Removed ?? [],
            });
        }

        return found;
    }
}
