using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LAYOUT7's Process-half case (D117 §7): the reader against real git. A scratch repository whose <c>CLAUDE.md</c> is a
/// link (mode 120000) committed under <c>core.symlinks=false</c>, read from its LINE: the link is said from its mode,
/// the checkout says it holds links as text, and what is uncommitted, or on another branch, is never read.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class SetupLineProcessTests : IDisposable
{
    private readonly string _scratch = Path.Combine(RepoRoot(), "_fixtures", "setup-line", Guid.NewGuid().ToString("N")[..8]);

    public SetupLineProcessTests() => Directory.CreateDirectory(_scratch);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_scratch, recursive: true);
        }
        catch (IOException) { /* a straggling git handle */ }
        catch (UnauthorizedAccessException) { /* read-only pack files under .git */ }
    }

    [Fact]
    public async Task A_link_committed_on_a_checkout_without_links_is_read_from_the_line()
    {
        var root = Path.Combine(_scratch, "reference");
        Directory.CreateDirectory(root);
        await GitAsync(root, "init", "--quiet", "-b", "main");
        await GitAsync(root, "config", "core.symlinks", "false");
        await File.WriteAllTextAsync(Path.Combine(root, "AGENTS.md"), "# Brief\n\nBuild with make.\n");
        Directory.CreateDirectory(Path.Combine(root, "packages", "api"));
        await File.WriteAllTextAsync(Path.Combine(root, "packages", "api", "AGENTS.md"), "# The api\n");
        await File.WriteAllTextAsync(Path.Combine(root, "target.txt"), "AGENTS.md");
        var blob = (await GitAsync(root, "hash-object", "-w", "target.txt")).Trim();
        File.Delete(Path.Combine(root, "target.txt"));
        await GitAsync(root, "update-index", "--add", "--cacheinfo", $"120000,{blob},CLAUDE.md");
        await GitAsync(root, "add", "AGENTS.md", "packages");
        await GitAsync(root, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "--quiet", "-m", "the reference's shape");

        // In flight in the checkout, and on another branch: neither is the line.
        await GitAsync(root, "checkout", "--quiet", "-b", "elsewhere");
        await File.WriteAllTextAsync(Path.Combine(root, "AGENTS.md"), "# Edited\n\none\ntwo\nthree\nfour\n");
        await File.WriteAllTextAsync(Path.Combine(root, "daoris.json"), "{}");

        var reading = await LayoutReader.ReadAsync(root, "main");

        Assert.Null(reading.Problem);
        var facts = reading.Facts!;
        Assert.Equal("main", facts.Line);
        Assert.True(IsFullCommitId(facts.Commit));
        Assert.Equal(new InstructionFile(InstructionState.Link, Target: "AGENTS.md"), facts.Claude);
        Assert.Equal(new InstructionFile(InstructionState.File, OwnLines: 2), facts.Agents);
        Assert.True(facts.LinksHeldAsText);
        Assert.False(facts.Adopted);
        Assert.Equal(AgentLayout.Own, facts.Layout);
        Assert.Equal(["packages/api"], facts.Rooms);
    }

    [Fact]
    public async Task A_line_that_names_no_commit_and_a_folder_that_is_no_repository_are_said()
    {
        var root = Path.Combine(_scratch, "empty");
        Directory.CreateDirectory(root);
        await GitAsync(root, "init", "--quiet", "-b", "main");

        var noCommit = await LayoutReader.ReadAsync(root, "main");
        Assert.Null(noCommit.Facts);
        Assert.Contains("names no commit here", noCommit.Problem);

        var inside = Path.Combine(root, "sub");
        Directory.CreateDirectory(inside);
        var notTop = await LayoutReader.ReadAsync(inside, "main");
        Assert.Null(notTop.Facts);
        Assert.Contains("not a git repository", notTop.Problem);
    }

    private static bool IsFullCommitId(string text) => text.Length == 40 && text.All(char.IsAsciiHexDigit);

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
        using var process = Process.Start(info)!;
        var stdout = await process.StandardOutput.ReadToEndAsync();
        await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return stdout;
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? AppContext.BaseDirectory;
    }
}
