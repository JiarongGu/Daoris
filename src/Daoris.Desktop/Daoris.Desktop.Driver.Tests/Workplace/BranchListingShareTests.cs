using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The look's stages read a repository's trees at once (the tidy over every tree, two owed opinions, the nodes' reads), and a
/// branch of the person's that moves (an agent's worktree branch commits) makes each tree's walk read its repository's branches newest
/// first again. That listing is the repository's whichever tree asks: a fan-out of walks over one repository starts it once for the refs
/// as they stand, not once per tree, so the stages do not queue for the repository's two places behind copies of one read.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class BranchListingShareTests : IDisposable
{
    private const int Trees = 4;

    private static readonly string Listing = string.Join(' ', BranchesByDate.Ask);

    private readonly string _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "branch-listing-share", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() => GitSeed.Discard(_scratch);

    [Fact]
    public async Task Walks_of_one_repositorys_trees_at_once_list_its_branches_once_after_a_branch_moves()
    {
        var home = Path.Combine(_scratch, "daoris-home");
        var root = Path.Combine(_scratch, "reports");
        Directory.CreateDirectory(home);
        Directory.CreateDirectory(root);
        await GitAsync(root, "init", "--quiet", "-b", "main");
        await CommitAsync(root, "README.md", "# reports", "first");
        var sessions = new SessionTrees(home);
        var trees = new List<string>();
        for (var each = 0; each < Trees; each++)
        {
            var tree = (await sessions.OpenAsync(root, "reports", $"work{each}")).Path;
            await CommitAsync(tree, $"work{each}.ts", $"the work of {each}", $"the work of s{each}");
            trees.Add(tree);
        }

        // Each tree's work read once, as a look before the move reads it.
        await Task.WhenAll(trees.Select(tree => new SessionTrees(home).WorkAsync(tree)));

        // An agent's branch of the person's moves: a counted ref, so every walk reads the listing again.
        await GitAsync(root, "branch", "agent-side", "main");
        await GitAsync(root, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "--allow-empty", "-q", "-m", "side");
        await GitAsync(root, "update-ref", "refs/heads/agent-side", "HEAD");
        await GitAsync(root, "reset", "-q", "--hard", "HEAD~1");

        var started = new List<string>();
        GitFlow.Started.Value = arguments =>
        {
            lock (started) started.Add(string.Join(' ', arguments));
        };
        IReadOnlyList<TreeWork?> walked;
        try
        {
            walked = await Task.WhenAll(trees.Select(tree => new SessionTrees(home).WorkAsync(tree)));
        }
        finally
        {
            GitFlow.Started.Value = null;
        }

        Assert.All(walked, work => Assert.Equal(1, work?.Commits));
        Assert.True(started.Count(start => start == Listing) == 1, $"{Trees} walks at once listed the branches {started.Count(start => start == Listing)} times:\n  {string.Join("\n  ", started)}");
    }

    private static async Task CommitAsync(string tree, string file, string content, string message)
    {
        await File.WriteAllTextAsync(Path.Combine(tree, file), content + "\n");
        await GitAsync(tree, "add", "-A");
        await GitAsync(tree, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-q", "-m", message);
    }

    private static async Task<string> GitAsync(string cwd, params string[] arguments)
    {
        var info = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(TestGit.Past(info))!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await Task.WhenAll(stdout, stderr);
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, $"git {string.Join(' ', arguments)}: {await stderr}");
        return await stdout;
    }
}
