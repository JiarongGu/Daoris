namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The worktrees a fixture must never register on the repository the tests run in. A fixture's own repository is a copy of a
/// seed; a git the product starts in a folder that has lost its repository climbs to the checkout's and registers the tree there,
/// a worktree of the real repository that outlives the test. This asks the checkout for its worktrees and names those that
/// stand under the fixtures folder.
/// </summary>
internal static class CheckoutWorktrees
{
    /// <summary>The worktrees <paramref name="porcelain"/> (<c>git worktree list --porcelain</c>) lists that stand under <paramref name="fixtures"/>.</summary>
    internal static IReadOnlyList<string> Under(string porcelain, string fixtures)
    {
        var root = Normal(fixtures) + "/";
        return [.. porcelain.Split('\n')
            .Where(line => line.StartsWith("worktree ", StringComparison.Ordinal))
            .Select(line => Normal(line["worktree ".Length..].Trim()))
            .Where(path => path.StartsWith(root, StringComparison.OrdinalIgnoreCase))];
    }

    /// <summary>Fails when the checkout the tests run in has a worktree registered under its <c>_fixtures</c> folder.</summary>
    internal static async Task AssertNoneLeakedAsync()
    {
        var checkout = WorkspaceRoot.Folder;
        var listed = await GitFixture.GitAsync(checkout, "worktree", "list", "--porcelain").ConfigureAwait(false);
        var leaked = Under(listed, Path.Combine(checkout, "_fixtures"));
        if (leaked.Count > 0) throw new InvalidOperationException("a test registered a worktree on the repository it runs in: " + string.Join(", ", leaked));
    }

    private static string Normal(string path) => Path.GetFullPath(path).Replace('\\', '/').TrimEnd('/');
}
