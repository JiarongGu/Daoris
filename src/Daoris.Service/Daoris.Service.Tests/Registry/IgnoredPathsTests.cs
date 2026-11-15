using System.Diagnostics;
using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// A checkout's read never enters what git ignores: git's own answer names the ignored folders without listing
/// what is in them, and a walk of a documents folder skips them before it lists them, so a read over a checkout with a large
/// ignored tree opens nothing under it. What git itself answers over a real checkout is <see cref="IgnoredPathsGitTests"/>.
/// </summary>
public sealed class IgnoredPathsTests() : ScratchFolder("daoris-ignored-")
{
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
    public void A_folder_that_is_no_git_checkout_ignores_nothing()
    {
        Write(".gitignore", "node_modules/\n");
        Write("docs/site/node_modules/pkg/README.md", "# A package\n");

        Assert.Empty(IgnoredPaths.Of(_root).Entries);
    }

    [Fact]
    public void The_start_names_the_real_git_behind_the_launcher_with_its_variables()
    {
        if (!OperatingSystem.IsWindows()) return;
        // An install laid as Git for Windows lays one, empty files, its launcher's folder first on PATH.
        var top = Path.Combine(_root, "Git");
        Write("Git/cmd/git.exe", "");
        var real = Write("Git/mingw64/bin/git.exe", "");
        Directory.CreateDirectory(Path.Combine(top, "usr", "bin"));
        var path = Path.Combine(top, "cmd") + @";C:\a";

        var start = IgnoredPaths.Start(_root, ["docs"], path);

        Assert.Equal(real, start.FileName);
        Assert.Equal("MINGW64", start.Environment["MSYSTEM"]);
        Assert.StartsWith($@"{Path.Combine(top, "mingw64", "bin")};{Path.Combine(top, "usr", "bin")};", start.Environment["PATH"]);
        Assert.EndsWith(";" + path, start.Environment["PATH"]);
        Assert.Equal(["-C", _root, "ls-files", "-z", "--others", "--ignored", "--exclude-standard", "--directory", "--", "docs"], start.ArgumentList);
        Assert.Equal(Path.GetDirectoryName(_root), start.Environment["GIT_CEILING_DIRECTORIES"]);
    }

    [Fact]
    public void The_start_names_git_bare_where_PATH_finds_no_launcher()
    {
        Write("other/git.exe", "");

        var start = IgnoredPaths.Start(_root, [], Path.Combine(_root, "other"));

        Assert.Equal("git", start.FileName);
        Assert.Equal(Environment.GetEnvironmentVariable("MSYSTEM"), start.Environment.TryGetValue("MSYSTEM", out var msystem) ? msystem : null);
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
