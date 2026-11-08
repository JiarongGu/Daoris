namespace GitLibraryBench;

/// <summary>
/// A repository laid out as Daoris's trees are: a clone under the scratch folder, its line, and linked worktrees on session branches,
/// the first of them a session ahead of its base with uncommitted work.
/// </summary>
internal sealed record Layout(string Root, string Line, string Base, IReadOnlyList<string> Trees)
{
    public string Review => Trees[0];

    public static string RootOf(string scratch, string name) => Path.GetFullPath(Path.Combine(scratch, name));

    /// <summary>The layout <see cref="Fixtures.SetUpAsync"/> made, read back from the repository.</summary>
    public static Layout Read(string scratch, string name)
    {
        var root = RootOf(scratch, name);
        var file = Path.Combine(root, ".git", "bench-layout");
        if (!File.Exists(file)) throw new InvalidOperationException($"no layout in {root}: run setup first");
        var lines = File.ReadAllLines(file);
        return new Layout(root, lines[0], lines[1], lines[2..]);
    }
}

internal static class Fixtures
{
    public const string SessionPrefix = "daoris/bench-";

    /// <summary>
    /// Clone <paramref name="source"/> (a path or a public address) as <paramref name="name"/>, or reuse a clone already there, and lay
    /// out <paramref name="trees"/> linked worktrees: tree <c>i</c> on <c>daoris/bench-i</c> at the line's tip less <c>20 + 5i</c> commits,
    /// its base 20 commits before that, and the first with five changed files, one deleted and ten untracked.
    /// </summary>
    public static async Task<Layout> SetUpAsync(GitBinary git, string scratch, string name, string source, string line, int trees)
    {
        var root = Layout.RootOf(scratch, name);
        if (!Directory.Exists(Path.Combine(root, ".git")))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(root)!);
            var from = Directory.Exists(source) ? Path.GetFullPath(source) : source;
            await MustAsync(git, Path.GetDirectoryName(root)!, ["clone", "--no-hardlinks", "-q", from, root]);
        }

        await MustAsync(git, root, ["config", "core.longpaths", "true"]);
        var tip = (await MustAsync(git, root, ["rev-parse", $"refs/remotes/origin/{line}^{{commit}}"])).Trim();
        await MustAsync(git, root, ["checkout", "-q", "-B", line, tip]);
        var made = new List<string>();
        for (var index = 0; index < trees; index++)
        {
            var path = Path.Combine($"{root}.trees", $"t{index}");
            var at = (await MustAsync(git, root, ["rev-parse", $"{tip}~{20 + 5 * index}"])).Trim();
            if (!Directory.Exists(path)) await MustAsync(git, root, ["worktree", "add", "-q", "-B", $"{SessionPrefix}{index}", path, at]);
            made.Add(Path.GetFullPath(path));
        }

        var review = made[0];
        var baseCommit = (await MustAsync(git, review, ["rev-parse", "HEAD~20"])).Trim();
        await DirtyAsync(git, review);
        File.WriteAllLines(Path.Combine(root, ".git", "bench-layout"), [$"refs/heads/{line}", baseCommit, .. made]);
        return new Layout(root, $"refs/heads/{line}", baseCommit, made);
    }

    private static async Task DirtyAsync(GitBinary git, string tree)
    {
        if (Directory.Exists(Path.Combine(tree, "bench-untracked"))) return;
        var tracked = (await MustAsync(git, tree, ["ls-files", "-z", "--", "*.md", "*.ts", "*.cs"]))
            .Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Where(path => File.Exists(Path.Combine(tree, path)))
            .Take(6)
            .ToArray();
        foreach (var path in tracked.Take(5)) File.AppendAllText(Path.Combine(tree, path), "\nA line the session added.\n");
        if (tracked.Length > 5) File.Delete(Path.Combine(tree, tracked[5]));
        var untracked = Directory.CreateDirectory(Path.Combine(tree, "bench-untracked"));
        for (var index = 0; index < 10; index++)
        {
            File.WriteAllText(Path.Combine(untracked.FullName, $"note-{index}.txt"), string.Join('\n', Enumerable.Range(0, 40).Select(n => $"line {n} of note {index}")) + "\n");
        }
    }

    /// <summary>Delete a fixture folder whole; git writes its objects read-only, which a plain recursive delete refuses on Windows.</summary>
    public static void Delete(string folder)
    {
        if (!Directory.Exists(folder)) return;
        foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(folder, recursive: true);
    }

    public static async Task<string> MustAsync(GitBinary git, string folder, IReadOnlyList<string> arguments)
    {
        var (code, output, _) = await git.RunAsync(folder, arguments);
        if (code != 0) throw new InvalidOperationException($"git {string.Join(' ', arguments)} in {folder} exited {code}");
        return output;
    }
}
