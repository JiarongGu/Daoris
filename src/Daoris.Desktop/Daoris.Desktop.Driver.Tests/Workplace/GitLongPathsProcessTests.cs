using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TREELONG1, over real git: a tracked file that fits under its repository's checkout passes Windows' 260 characters in a session's
/// tree, which sits deeper. The session's own git, through the environment its start is handed, reads the tree clean; a git handed
/// nothing reads the same committed file as deleted, which an agent's <c>git add -A</c> would commit.
/// </summary>
/// <remarks>
/// Alone: it sets the process's <c>GIT_CONFIG_NOSYSTEM</c> and <c>GIT_CONFIG_GLOBAL</c>, so the person's own configuration, which
/// may already say <c>core.longpaths</c>, is kept from the read and from both gits. Windows alone: elsewhere a path has no such bound.
/// </remarks>
[Trait(Category.Name, Category.Process)]
[Collection(Alone.Name)]
public sealed class GitLongPathsProcessTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-treelong-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly Dictionary<string, string?> _was = [];

    private string Home => Path.Combine(_root, "data");

    private string Repo => Path.Combine(_root, "engine");

    /// <summary>The session's tree, deeper than the checkout by its folder's name, as a tree under the trees home is.</summary>
    private string Tree => Path.Combine(_root, "trees", new string('t', 48));

    public GitLongPathsProcessTests()
    {
        Directory.CreateDirectory(Home);
        // Git for Windows' own system file says core.fscache, under which a path git cannot open reads as a deleted file; without
        // it the same git says "Filename too long" and lists nothing.
        var global = Path.Combine(_root, "global");
        File.WriteAllText(global, "[core]\n\tfscache = true\n");
        Set("GIT_CONFIG_NOSYSTEM", "1");
        Set("GIT_CONFIG_GLOBAL", global);
    }

    private void Set(string name, string value)
    {
        if (!_was.ContainsKey(name)) _was[name] = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
    }

    public void Dispose()
    {
        foreach (var (name, value) in _was) Environment.SetEnvironmentVariable(name, value);
        try { GitSeed.Remove(_root); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private async Task Git(string root, params string[] arguments)
    {
        var (code, _, error) = await WorkingTree.GitAsync(root, arguments, CancellationToken.None);
        Assert.True(code == 0, error);
    }

    /// <summary>A checkout with a tracked file at about 240 characters, and a session's tree of it where that file passes 260.</summary>
    private async Task<string> DeepTreeAsync()
    {
        await GitSeed.PlainAsync(Repo, "engine");
        var folder = Repo;
        while (folder.Length < 200) folder = Path.Combine(folder, new string('d', 30));
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, new string('f', Math.Max(1, 240 - folder.Length - 1)) + ".txt");
        File.WriteAllText(file, "committed\n");
        Assert.True(file.Length < 260, file);
        await Git(Repo, "add", "-A");
        await Git(Repo, "commit", "-qm", "a deep file");
        Directory.CreateDirectory(Path.GetDirectoryName(Tree)!);
        await Git(Repo, "worktree", "add", "-q", "-b", "session", Tree);
        Assert.True(Path.Combine(Tree, Path.GetRelativePath(Repo, file)).Length > 260);
        return Tree;
    }

    private static async Task<string> StatusAsync(ProcessStartInfo info)
    {
        using var process = Process.Start(info) ?? throw new InvalidOperationException("git did not start");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var bound = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await process.WaitForExitAsync(bound.Token);
        Assert.True(process.ExitCode == 0, await stderr);
        return await stdout + await stderr;
    }

    private static readonly string[] Status = ["git", "--no-optional-locks", "status", "--porcelain"];

    [Fact]
    public async Task A_sessions_git_reads_a_tree_past_260_characters_clean()
    {
        if (!OperatingSystem.IsWindows()) return; // a path past 260 characters is bounded on Windows alone
        var tree = await DeepTreeAsync();
        var target = new SessionTarget("q1", "Title", "Body", "Asker", "engine", tree, "http://localhost:5177") { Home = Home };

        var handed = new StubAdapter().Prepare(target, Status);
        var bare = new ProcessStartInfo("git")
        {
            WorkingDirectory = tree,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in Status.Skip(1)) bare.ArgumentList.Add(argument);

        Assert.Equal("", (await StatusAsync(handed)).Trim());
        Assert.Contains("\n D ", "\n" + await StatusAsync(bare));
    }

    /// <summary>
    /// The start hands the setting whatever the person's configuration says, as Daoris's own git calls always pass it (D109): reading
    /// that configuration started a git at every start, which a help start must never (HelpStartReadTests). It is handed once.
    /// </summary>
    [Fact]
    public async Task A_person_whose_configuration_says_otherwise_is_handed_it_once_and_the_start_reads_no_configuration()
    {
        await GitSeed.PlainAsync(Repo, "engine");
        var target = new SessionTarget("q1", "Title", "Body", "Asker", "engine", Repo, "http://localhost:5177") { Home = Home };

        await Git(Repo, "config", "core.longpaths", "false");

        Assert.False(await GitLongPaths.NeededAsync(Repo, CancellationToken.None));
        var info = new StubAdapter().Prepare(target, Status);
        var count = info.Environment.TryGetValue("GIT_CONFIG_COUNT", out var held) && int.TryParse(held, out var number) ? number : 0;
        Assert.Single(Enumerable.Range(0, count), index => info.Environment[$"GIT_CONFIG_KEY_{index}"] == GitLongPaths.Key);
    }
}
