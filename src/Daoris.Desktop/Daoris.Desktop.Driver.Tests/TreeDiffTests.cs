using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The session's landed work, as a diff (SURF6, design §5). Driven against REAL git for the same
/// reason the session-tree tests are: the answers are git's, and a mock that agrees with a guess
/// about `--name-status` proves only that the guess is self-consistent.
/// </summary>
/// <remarks>
/// Fixtures live under the repository's gitignored `_fixtures/`, never OS temp.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class TreeDiffTests : IDisposable
{
    private readonly string _scratch;

    public TreeDiffTests()
    {
        _scratch = Path.Combine(RepoRoot(), "_fixtures", "tree-diff", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_scratch);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_scratch, recursive: true);
        }
        catch (IOException) { /* a straggling git handle; the next run's GUID never collides */ }
        catch (UnauthorizedAccessException) { /* read-only pack files under .git */ }
    }

    [Fact]
    public async Task It_names_every_file_that_changed_since_the_base_with_its_patch()
    {
        var root = await MakeRepositoryAsync();
        var before = (await GitAsync(root, "rev-parse", "HEAD")).Trim();

        await File.WriteAllTextAsync(Path.Combine(root, "added.txt"), "new\n");
        await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "# changed\n");
        File.Delete(Path.Combine(root, "gone.txt"));
        await GitAsync(root, "add", "-A");
        await GitAsync(root, "commit", "-m", "the session's work");

        var diff = await WorkingTree.DiffAsync(root, before);

        Assert.NotNull(diff);
        Assert.Equal(before, diff!.Base);
        Assert.Null(diff.Truncated);

        var byPath = diff.Files.ToDictionary(file => file.Path.Replace('\\', '/'));
        Assert.Equal("added", byPath["added.txt"].Status);
        Assert.Equal("modified", byPath["README.md"].Status);
        Assert.Equal("deleted", byPath["gone.txt"].Status);

        // The patch is git's own, verbatim — the surface renders it, it does not re-derive it.
        Assert.Contains("+new", byPath["added.txt"].Patch);
        Assert.Equal(1, byPath["added.txt"].Added);
        Assert.Equal(0, byPath["added.txt"].Removed);
    }

    /// <summary>
    /// A session that committed nothing changed nothing, and that is an ANSWER — an empty list, not
    /// a null. Null means git could not say, which reads very differently to a person.
    /// </summary>
    [Fact]
    public async Task A_session_that_landed_nothing_reports_no_files_rather_than_nothing_at_all()
    {
        var root = await MakeRepositoryAsync();
        var before = (await GitAsync(root, "rev-parse", "HEAD")).Trim();

        var diff = await WorkingTree.DiffAsync(root, before);

        Assert.NotNull(diff);
        Assert.Empty(diff!.Files);
        Assert.Null(diff.Truncated);
    }

    /// <summary>
    /// 🔴 <b>git WALKS UP.</b> Run it in a directory that is not a repository and it answers for
    /// whatever repository encloses it — so an unguarded diff of a session tree that has been
    /// deleted, or of a checkout that was never a repository, returns the PARENT project's commits
    /// presented as that session's work. Not hypothetical: the example family the desktop drives are
    /// plain directories inside this repository, and before the guard existed a review of a session
    /// in one of them showed Daoris's own last commit.
    /// </summary>
    [Fact]
    public async Task It_says_nothing_it_cannot_know_rather_than_answering_for_the_repository_above()
    {
        var root = await MakeRepositoryAsync();

        // A base this repository has never heard of, and no base at all. Both are "git cannot say",
        // which a surface must report as such — an empty diff would read as "it changed nothing".
        Assert.Null(await WorkingTree.DiffAsync(root, "0123456789abcdef0123456789abcdef01234567"));
        Assert.Null(await WorkingTree.DiffAsync(root, ""));

        // A plain directory INSIDE a repository — the shape that made this a real defect rather than
        // a tidy one. `_scratch` sits under this repository's `_fixtures/`, so git has an answer here
        // and it is the wrong one.
        var notARepository = Path.Combine(_scratch, "bare");
        Directory.CreateDirectory(notARepository);
        Assert.Null(await WorkingTree.DiffAsync(notARepository, "HEAD"));

        // And a SUBDIRECTORY of a real repository, which git would happily narrow the diff to.
        var inside = Path.Combine(root, "src");
        Directory.CreateDirectory(inside);
        Assert.Null(await WorkingTree.DiffAsync(inside, (await GitAsync(root, "rev-parse", "HEAD")).Trim()));
    }

    /// <summary>
    /// The bound is stated, never hidden (design §5) — and the FILE LIST survives it. A person must
    /// always learn that a file changed, even when the surface will not show them how.
    /// </summary>
    [Fact]
    public async Task The_bound_drops_patches_and_says_so_while_every_file_still_appears()
    {
        var root = await MakeRepositoryAsync();
        var before = (await GitAsync(root, "rev-parse", "HEAD")).Trim();

        // Each file alone is under the per-file cap; together they overrun the whole budget, which is
        // the case the budget exists for — one session touching many large files.
        var slab = string.Concat(Enumerable.Repeat("a line of perfectly ordinary source text\n", 1_800));
        for (var i = 0; i < 12; i++)
        {
            await File.WriteAllTextAsync(Path.Combine(root, $"big{i}.txt"), slab);
        }
        await GitAsync(root, "add", "-A");
        await GitAsync(root, "commit", "-m", "a great deal of work");

        var diff = await WorkingTree.DiffAsync(root, before);

        Assert.NotNull(diff);
        Assert.Equal(12, diff!.Files.Count);
        Assert.NotNull(diff.Truncated);
        Assert.Contains("git diff", diff.Truncated);
        // Some patches are there and some are not — that is what "bounded" means here.
        Assert.Contains(diff.Files, file => file.Patch is not null);
        Assert.Contains(diff.Files, file => file.Patch is null);
        // And every file is still named, with its counts, whether or not its patch survived.
        Assert.All(diff.Files, file => Assert.NotNull(file.Added));
    }

    /// <summary>A binary file has no line counts, and "not counted" is not "counted nothing".</summary>
    [Fact]
    public async Task A_binary_file_reports_no_counts_rather_than_zero()
    {
        var root = await MakeRepositoryAsync();
        var before = (await GitAsync(root, "rev-parse", "HEAD")).Trim();

        await File.WriteAllBytesAsync(
            Path.Combine(root, "logo.bin"), [0, 1, 2, 3, 0, 255, 254, 0, 7, 9]);
        await GitAsync(root, "add", "-A");
        await GitAsync(root, "commit", "-m", "a binary");

        var diff = await WorkingTree.DiffAsync(root, before);

        var binary = Assert.Single(diff!.Files, file => file.Path.EndsWith("logo.bin", StringComparison.Ordinal));
        Assert.Null(binary.Added);
        Assert.Null(binary.Removed);
    }

    /// <summary>
    /// 🔴 REVIEW3: a review starts two git processes, whether the session changed three files or sixty — one asks whether the
    /// root is a repository's top, one reads the whole range. The review used to start one more per file, and one git start
    /// on a large repository costs about a second. Counted at the seam the review reads git through, around real git.
    /// </summary>
    [Fact]
    public async Task A_review_starts_two_git_processes_however_many_files_changed()
    {
        var root = await MakeRepositoryAsync();
        var before = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        for (var i = 0; i < 3; i++) await File.WriteAllTextAsync(Path.Combine(root, $"few{i}.txt"), $"few {i}\n");
        await GitAsync(root, "add", "-A");
        await GitAsync(root, "commit", "-m", "a little work");
        var middle = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        for (var i = 0; i < 60; i++) await File.WriteAllTextAsync(Path.Combine(root, $"many{i}.txt"), $"many {i}\n");
        await GitAsync(root, "add", "-A");
        await GitAsync(root, "commit", "-m", "a lot of work");
        var tip = (await GitAsync(root, "rev-parse", "HEAD")).Trim();

        var (few, fewStarts) = await CountedAsync(git => WorkingTree.DiffBetweenAsync(root, before, middle, git, CancellationToken.None));
        var (many, manyStarts) = await CountedAsync(git => WorkingTree.DiffBetweenAsync(root, middle, tip, git, CancellationToken.None));
        var (tree, treeStarts) = await CountedAsync(git => WorkingTree.DiffAsync(root, before, git, CancellationToken.None));

        Assert.Equal((3, 60, 63), (few!.Files.Count, many!.Files.Count, tree!.Files.Count));
        Assert.Equal((2, 2, 2), (fewStarts, manyStarts, treeStarts));
        Assert.All(tree.Files, file => Assert.Contains("@@", file.Patch));
    }

    /// <summary>
    /// The whole range's answer, split, is each file's own read (REVIEW3): every file that is not a rename carries exactly
    /// the patch `git diff -M &lt;range&gt; -- &lt;path&gt;` gives it alone — a modification, an addition, a deletion, a
    /// binary file, a mode-only change, a symlink become a file (both of git's halves), a path with a space and one outside
    /// ASCII. A rename now carries git's rename: a read of its new path alone could not see where it came from, so it showed
    /// the whole file as added and found no counts for it.
    /// </summary>
    [Fact]
    public async Task Each_file_s_patch_is_the_one_its_own_read_gives_and_a_rename_is_git_s_rename()
    {
        var root = await MakeRepositoryAsync();
        await File.WriteAllTextAsync(Path.Combine(root, "moved.txt"), string.Concat(Enumerable.Range(1, 40).Select(i => $"line {i}\n")));
        await File.WriteAllTextAsync(Path.Combine(root, "run.sh"), "echo hi\n");
        await File.WriteAllTextAsync(Path.Combine(root, "with space.txt"), "a\n");
        await File.WriteAllTextAsync(Path.Combine(root, "中文.txt"), "b\n");
        await File.WriteAllTextAsync(Path.Combine(root, "target.txt"), "# a link's target\n");
        await GitAsync(root, "add", "-A");
        // A symlink through the index, since a Windows checkout may not be able to make one on disk.
        var target = (await GitAsync(root, "hash-object", "-w", "target.txt")).Trim();
        await GitAsync(root, "update-index", "--add", "--cacheinfo", $"120000,{target},link");
        await GitAsync(root, "commit", "-m", "the shapes, before");
        var before = (await GitAsync(root, "rev-parse", "HEAD")).Trim();

        await File.WriteAllTextAsync(Path.Combine(root, "added.txt"), "new\n");
        await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "# changed\n");
        File.Delete(Path.Combine(root, "gone.txt"));
        File.Move(Path.Combine(root, "moved.txt"), Path.Combine(root, "renamed.txt"));
        await File.WriteAllTextAsync(
            Path.Combine(root, "renamed.txt"),
            string.Concat(Enumerable.Range(1, 40).Select(i => i == 7 ? "line seven\n" : $"line {i}\n")));
        await File.WriteAllTextAsync(Path.Combine(root, "with space.txt"), "a\nb\n");
        await File.WriteAllTextAsync(Path.Combine(root, "中文.txt"), "b\nc\n");
        await File.WriteAllBytesAsync(Path.Combine(root, "logo.bin"), [0, 1, 2, 3, 0, 255, 254, 0, 7, 9]);
        await File.WriteAllTextAsync(Path.Combine(root, "link"), "now a file\n");
        await GitAsync(root, "add", "-A");
        var file = (await GitAsync(root, "hash-object", "-w", "link")).Trim();
        await GitAsync(root, "update-index", "--cacheinfo", $"100644,{file},link");
        await GitAsync(root, "update-index", "--chmod=+x", "run.sh");
        await GitAsync(root, "commit", "-m", "the shapes, after");

        var diff = await WorkingTree.DiffAsync(root, before);

        Assert.NotNull(diff);
        var byPath = diff!.Files.ToDictionary(changed => changed.Path);
        Assert.Equal(
            new[] { "README.md", "added.txt", "gone.txt", "link", "logo.bin", "renamed.txt", "run.sh", "with space.txt", "中文.txt" },
            diff.Files.Select(changed => changed.Path).Order(StringComparer.Ordinal));
        foreach (var changed in diff.Files.Where(changed => changed.Status != "renamed"))
        {
            var alone = await GitTextAsync(root, "-c", "core.quotePath=false", "diff", "--no-color", "-M", $"{before}..HEAD", "--", changed.Path);
            Assert.Equal(alone, changed.Patch);
        }

        Assert.Equal("modified", byPath["link"].Status);
        Assert.Equal(2, byPath["link"].Patch!.Split("diff --git a/link b/link\n").Length - 1);
        Assert.Contains("Binary files /dev/null and b/logo.bin differ", byPath["logo.bin"].Patch);
        Assert.Equal("diff --git a/run.sh b/run.sh\nold mode 100644\nnew mode 100755\n", byPath["run.sh"].Patch);
        Assert.Contains("+++ b/with space.txt", byPath["with space.txt"].Patch);
        Assert.Equal((1, 0), (byPath["中文.txt"].Added!.Value, byPath["中文.txt"].Removed!.Value));

        var renamed = byPath["renamed.txt"];
        Assert.Equal("renamed", renamed.Status);
        Assert.Equal((1, 1), (renamed.Added!.Value, renamed.Removed!.Value));
        Assert.Contains("rename from moved.txt\nrename to renamed.txt\n", renamed.Patch);
        Assert.Contains("-line 7\n+line seven\n", renamed.Patch);
    }

    /// <summary>
    /// A person's own settings do not change what the review reads (REVIEW3): no prefixes, colour always, octal paths and
    /// a diff program that fails would each change the answer's shape, or the line each patch is matched to its file by.
    /// </summary>
    [Fact]
    public async Task A_person_s_diff_settings_do_not_change_what_the_review_reads()
    {
        var root = await MakeRepositoryAsync();
        var before = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "# changed\n");
        await File.WriteAllTextAsync(Path.Combine(root, "中文.txt"), "b\n");
        await GitAsync(root, "add", "-A");
        await GitAsync(root, "commit", "-m", "the session's work");
        foreach (var (name, value) in new[]
                 {
                     ("diff.noprefix", "true"), ("color.ui", "always"), ("core.quotePath", "true"), ("diff.external", "false"),
                 })
        {
            await GitAsync(root, "config", name, value);
        }

        var diff = await WorkingTree.DiffAsync(root, before);

        Assert.NotNull(diff);
        Assert.Equal(
            new WorkingTree.DiffFile[]
            {
                new("README.md", "modified", 1, 1,
                    // The blobs' ids are their contents' (`# engine`, `# changed`, `b`, each with its newline).
                    "diff --git a/README.md b/README.md\nindex 8239e59..df97349 100644\n--- a/README.md\n+++ b/README.md\n"
                    + "@@ -1 +1 @@\n-# engine\n+# changed\n"),
                new("中文.txt", "added", 1, 0,
                    "diff --git a/中文.txt b/中文.txt\nnew file mode 100644\nindex 0000000..6178079\n--- /dev/null\n"
                    + "+++ b/中文.txt\n@@ -0,0 +1 @@\n+b\n"),
            },
            diff!.Files);
    }

    /// <summary>A review read through a seam that counts each git start, around real git.</summary>
    private static async Task<(WorkingTree.TreeDiff? Diff, int Starts)> CountedAsync(
        Func<WorkingTree.GitRead, Task<WorkingTree.TreeDiff?>> review)
    {
        var starts = 0;
        var diff = await review((root, arguments, take, ct) =>
        {
            Interlocked.Increment(ref starts);
            return WorkingTree.ReadGitAsync(root, arguments, take, ct);
        });
        return (diff, starts);
    }

    private async Task<string> MakeRepositoryAsync()
    {
        var root = Path.Combine(_scratch, "engine");
        Directory.CreateDirectory(root);
        await GitAsync(root, "init", "--quiet");
        await GitAsync(root, "config", "user.email", "fixture@example.test");
        await GitAsync(root, "config", "user.name", "Fixture");
        await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "# engine\n");
        await File.WriteAllTextAsync(Path.Combine(root, "gone.txt"), "delete me\n");
        await GitAsync(root, "add", ".");
        await GitAsync(root, "commit", "-m", "first");
        return root;
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
        using var process = Process.Start(info)!;
        var stdout = await process.StandardOutput.ReadToEndAsync();
        await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return stdout;
    }

    /// <summary>git's answer read as the UTF-8 it is, for a patch that names a path outside ASCII.</summary>
    private static async Task<string> GitTextAsync(string cwd, params string[] arguments)
    {
        var info = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
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
