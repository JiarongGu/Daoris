using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The git half of <see cref="LayoutReader"/> (LAYOUT7, D117 §6.5): a repository's line read as git objects, which the pure
/// half (<see cref="LayoutReader.Read"/>) makes facts of; the set-up press and the tests call it by this name.
/// </summary>
public static class LayoutGit
{
    /// <summary>How much of one text git is asked for: a manifest, a lock or an instruction file is far smaller.</summary>
    private const int TextLimit = 512 * 1024;

    /// <summary>
    /// The facts of <paramref name="line"/> in the checkout at <paramref name="root"/>, read as git objects: the
    /// line's commit, its tree, the texts <see cref="LayoutReader.Wanted"/> names, and the checkout's <c>core.symlinks</c>. The
    /// working files and the index are never asked, so what the person has in flight there is neither read nor touched.
    /// </summary>
    /// <returns>The facts, or why the line could not be read: no repository there, or a line that names no commit.</returns>
    public static async Task<LineReading> ReadAsync(string root, string line, CancellationToken ct = default)
    {
        // 🔴 git walks UP (FIX-LOG): a folder that is not a repository's top would answer for the one above it.
        if (!await WorkingTree.IsTopLevelAsync(root, ct).ConfigureAwait(false))
        {
            return new(null, "its checkout here is not a git repository");
        }

        if (!BranchName.IsValid(line)) return new(null, $"its line `{line}` is not a branch name git would take");

        var (found, sha, _) = await WorkingTree.GitAsync(
            root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{line}^{{commit}}"], ct).ConfigureAwait(false);
        var commit = sha.Trim();
        if (found != 0 || !WorkingTree.IsCommitId(commit)) return new(null, $"its line `{line}` names no commit here");

        var (listed, output, problem) = await WorkingTree.GitAsync(root, ["ls-tree", "-r", "-z", commit], ct).ConfigureAwait(false);
        if (listed != 0) return new(null, $"git could not list `{line}` at `{TextBounds.ShortCommit(commit, 7)}`: {problem.Trim()}");

        var tree = LineEntry.Parse(output);
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in LayoutReader.Wanted(tree))
        {
            var entry = tree.First(item => item.Path == path);
            if (!WorkingTree.IsCommitId(entry.Object)) continue;
            if (await WorkingTree.GitBytesAsync(root, ["cat-file", "blob", entry.Object], TextLimit, ct).ConfigureAwait(false) is { } read)
            {
                texts[path] = Encoding.UTF8.GetString(read.Bytes, 0, read.Count);
            }
        }

        // Unset answers 1: git's own default then, which is links. `--type=bool` reads `yes`, `on` and `1` as git does.
        var (set, symlinks, _) = await WorkingTree.GitAsync(root, ["config", "--type=bool", "--get", "core.symlinks"], ct).ConfigureAwait(false);
        return new(LayoutReader.Read(line, commit, tree, texts, set == 0 ? symlinks.Trim() == "true" : null), null);
    }
}
