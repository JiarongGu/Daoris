namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The worktrees a fixture must never register on the repository the tests run in. A fixture's own repository is a copy of a
/// seed; a git the product starts in a folder that has lost its repository climbs to the checkout's and registers the tree there,
/// a worktree of the real repository that outlives the test. This reads the checkout's registered worktrees and names those that
/// stand under the fixtures folder.
/// </summary>
internal static class CheckoutWorktrees
{
    /// <summary>The registered worktrees of the repository whose git folder is <paramref name="gitDirectory"/> that stand under <paramref name="fixtures"/>.</summary>
    internal static IReadOnlyList<string> Under(string gitDirectory, string fixtures)
    {
        var listed = Path.Combine(CommonDirectory(gitDirectory), "worktrees");
        if (!Directory.Exists(listed)) return [];
        var root = Normal(fixtures) + "/";
        var found = new List<string>();
        foreach (var entry in Directory.EnumerateDirectories(listed))
        {
            var file = Path.Combine(entry, "gitdir");
            if (!File.Exists(file)) continue;
            string path;
            try
            {
                path = Normal(File.ReadAllText(file).Trim());
            }
            catch (IOException)
            {
                continue;
            }

            if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) found.Add(path);
        }

        return found;
    }

    /// <summary>Fails when the checkout the tests run in has a worktree registered under its <c>_fixtures</c> folder.</summary>
    internal static void AssertNoneLeaked()
    {
        var checkout = WorkspaceRoot.Folder;
        var dot = Path.Combine(checkout, ".git");
        var gitDirectory = File.Exists(dot) ? Path.GetFullPath(File.ReadAllText(dot).Trim()["gitdir:".Length..].Trim(), checkout) : dot;
        var leaked = Under(gitDirectory, Path.Combine(checkout, "_fixtures"));
        if (leaked.Count > 0) throw new InvalidOperationException("a test registered a worktree on the repository it runs in: " + string.Join(", ", leaked));
    }

    /// <summary>A linked worktree's git folder names its main one in <c>commondir</c>; a main one is its own.</summary>
    private static string CommonDirectory(string gitDirectory)
    {
        var file = Path.Combine(gitDirectory, "commondir");
        return File.Exists(file) ? Path.GetFullPath(File.ReadAllText(file).Trim(), gitDirectory) : gitDirectory;
    }

    private static string Normal(string path) => Path.GetFullPath(path).Replace('\\', '/').TrimEnd('/');
}
