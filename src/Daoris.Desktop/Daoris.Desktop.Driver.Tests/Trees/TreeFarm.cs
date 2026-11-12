using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Many session trees on one repository for a test that reads them and opens none: one tree opened by the product's own
/// <c>OpenAsync</c>, the rest made from it by copying its folder and its linked-worktree entry, with a branch ref and a record
/// each. Opening one is about ten git starts and a hook stage (4.8 s on this machine), so 145 opened in turn cost twelve minutes
/// where the copies cost seconds; each copy is a tree git reads as it reads any other, so what a test proves of its reading stands.
/// </summary>
internal static class TreeFarm
{
    /// <summary>
    /// <paramref name="count"/> trees under <paramref name="trees"/>' root for <paramref name="repository"/>, the first opened for real and the others its copies, each
    /// clean and on a branch of its own at the first one's commit.
    /// </summary>
    public static async Task<IReadOnlyList<string>> OpenAsync(
        SessionTrees trees, string home, string root, string repository, string workspace, int count)
    {
        var first = await trees.OpenAsync(root, repository, workspace).ConfigureAwait(false);
        var made = new List<string> { first.Path };
        if (count < 2) return made;

        var head = (await GitAsync(first.Path, "rev-parse", "HEAD").ConfigureAwait(false)).Trim();
        var admin = AdminOf(first.Path);
        var heads = Path.Combine(root, ".git", "refs", "heads", "daoris");
        var now = DateTimeOffset.UtcNow;
        for (var i = 1; i < count; i++)
        {
            var name = $"s-{Guid.NewGuid().ToString("N")[..8]}";
            var path = Path.Combine(trees.TreesRoot, workspace, repository, name);
            var entry = Path.Combine(Path.GetDirectoryName(admin)!, name);
            GitSeed.Copy(first.Path, path);
            GitSeed.Copy(admin, entry);
            Rewrite(Path.Combine(path, ".git"), $"gitdir: {entry.Replace('\\', '/')}\n");
            Rewrite(Path.Combine(entry, "gitdir"), $"{Path.Combine(path, ".git").Replace('\\', '/')}\n");
            Rewrite(Path.Combine(entry, "HEAD"), $"ref: refs/heads/daoris/{name}\n");
            Rewrite(Path.Combine(heads, name), head + "\n");
            trees.Grown.Record(new GrownBranch(repository, workspace, $"daoris/{name}", "main", head, null, now)
            {
                Unit = GrownBranch.NewUnit(),
            });
            TreeFilesRecord.Mark(home, path);
            made.Add(path);
        }

        return made;
    }

    /// <summary>A file written whole; a copy keeps its source's read-only mark, which is cleared first.</summary>
    private static void Rewrite(string file, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        if (File.Exists(file)) File.SetAttributes(file, FileAttributes.Normal);
        File.WriteAllText(file, text);
    }

    /// <summary>The linked-worktree entry a tree's <c>.git</c> file names.</summary>
    private static string AdminOf(string tree)
    {
        var text = File.ReadAllText(Path.Combine(tree, ".git")).Trim();
        return Path.GetFullPath(text["gitdir:".Length..].Trim());
    }
}
