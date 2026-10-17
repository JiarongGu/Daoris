using System.Diagnostics;
using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// A checkout's read never enters what git ignores (KNOWWALK1): git's own answer names the ignored folders without listing
/// what is in them, and a walk of a documents folder skips them before it lists them, so a read over a checkout with a large
/// ignored tree opens nothing under it.
/// </summary>
public sealed class IgnoredPathsTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-ignored-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() => GitCheckouts.Delete(_root);

    private string Write(string relative, string content)
    {
        var file = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
        return file;
    }

    [Fact]
    public void Gits_answer_is_read_as_folders_and_files_each_covering_what_lies_in_it()
    {
        var ignored = IgnoredPaths.Parse("local/\0.claude/worktrees/\0docs/scratch.md\0");

        Assert.Equal(["local/", ".claude/worktrees/", "docs/scratch.md"], ignored.Entries);
        Assert.True(ignored.Covers("local/"));
        Assert.True(ignored.Covers("local/notes/plan.md"));
        Assert.True(ignored.Covers(".claude/worktrees/"));
        Assert.True(ignored.Covers(".claude/worktrees/side/AGENTS.md"));
        Assert.True(ignored.Covers("docs/scratch.md"));
        Assert.False(ignored.Covers(".claude/knowledge/twins.md"));
        Assert.False(ignored.Covers("localised/plan.md"));
        Assert.False(ignored.Covers("docs/scratch.md.txt"));
        Assert.False(ignored.Covers(".claude/"));

        Assert.True(ignored.AnyWithin(".claude"));
        Assert.True(ignored.AnyWithin("docs"));
        Assert.False(ignored.AnyWithin(".claude/knowledge"));
        Assert.False(ignored.AnyWithin("src"));
    }

    [Fact]
    public void Git_names_each_ignored_folder_once_and_nothing_in_it()
    {
        Write(".gitignore", "node_modules/\n_fixtures/\n.claude/worktrees/\n*.log\n");
        Write("docs/design.md", "# Design\n");
        Write("docs/build.log", "built\n");
        // A folder holding nothing git would take is named as ignored itself, so the site has a page of its own.
        Write("docs/site/guide.md", "# The site's guide\n");
        Write("docs/site/node_modules/pkg/README.md", "# A package\n");
        Write("_fixtures/install/app/bin/Daoris.dll", "binary");
        Write(".claude/worktrees/side/AGENTS.md", "# A worktree's brief\n");
        Write(".claude/knowledge/twins.md", "# Twins\n");
        GitCheckouts.Init(_root);

        var ignored = IgnoredPaths.Of(_root);

        Assert.Equal(
            [".claude/worktrees/", "_fixtures/", "docs/build.log", "docs/site/node_modules/"],
            ignored.Entries.Order(StringComparer.Ordinal));
        Assert.False(ignored.Covers("docs/design.md"));
        Assert.False(ignored.Covers(".claude/knowledge/twins.md"));

        // Asked under some folders, it answers for those alone.
        Assert.Equal([".claude/worktrees/"], IgnoredPaths.Of(_root, ".claude").Entries);
    }

    [Fact]
    public void A_folder_that_is_no_git_checkout_ignores_nothing()
    {
        Write(".gitignore", "node_modules/\n");
        Write("docs/site/node_modules/pkg/README.md", "# A package\n");

        Assert.Empty(IgnoredPaths.Of(_root).Entries);
    }

    [Fact]
    public void A_read_of_a_checkouts_documents_opens_nothing_under_a_folder_git_ignores()
    {
        Write(".gitignore", "node_modules/\nscratch.md\n");
        Write("docs/design.md", "# The probe lock\n\n## Why a lock\n\nOne probe at a time.\n");
        // A large ignored tree inside the documents folder: a docs site's packages, each with a README the walk would index.
        for (var package = 0; package < 40; package++)
        {
            Write($"docs/site/node_modules/pkg{package}/README.md", $"# Package {package}\n\n## Zeppelins\n\nNot this repository's.\n");
        }
        var held = Write("docs/site/node_modules/pkg0/README.md", "# Held\n\n## Zeppelins\n\nHeld with no sharing.\n");
        Write("docs/scratch.md", "# Scratch\n\n## Zeppelins\n\nA person's own, ignored.\n");
        GitCheckouts.Init(_root);

        // Held with no sharing at all, as a swap or a build holds a file: a read that opened it would fail here.
        using var hold = new FileStream(held, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var scratch = new FileStream(Path.Combine(_root, "docs", "scratch.md"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var entries = new RepositoryScanner(documents: "docs").Scan(_root);

        Assert.Contains(entries, entry => entry.RelativePath == "docs/design.md");
        Assert.DoesNotContain(entries, entry => entry.RelativePath.Contains("node_modules", StringComparison.Ordinal));
        Assert.DoesNotContain(entries, entry => entry.RelativePath == "docs/scratch.md");
    }
}

/// <summary>A real git checkout for a test: initialised, never committed to, and deleted whatever git made read-only.</summary>
internal static class GitCheckouts
{
    public static void Init(string folder)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = folder,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in new[] { "init", "--quiet" }) start.ArgumentList.Add(argument);
        start.Environment.Remove("GIT_DIR");
        start.Environment.Remove("GIT_WORK_TREE");
        start.Environment.Remove("GIT_INDEX_FILE");
        using var process = Process.Start(start)!;
        var error = process.StandardError.ReadToEndAsync();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"git init failed: {error.Result}");
    }

    public static void Delete(string folder)
    {
        if (!Directory.Exists(folder)) return;
        foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }
        Directory.Delete(folder, recursive: true);
    }
}
