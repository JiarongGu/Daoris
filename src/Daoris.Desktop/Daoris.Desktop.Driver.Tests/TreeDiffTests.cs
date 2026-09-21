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
