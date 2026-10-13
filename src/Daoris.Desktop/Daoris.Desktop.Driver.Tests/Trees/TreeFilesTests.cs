using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The files a person may `@` in a conversation (CONV4d): what the session's tree holds, as git
/// answers it. Driven against REAL git, as the diff is, because the answer is git's.
/// </summary>
/// <remarks>
/// Fixtures live under the repository's gitignored `_fixtures/`, never OS temp.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class TreeFilesTests : IDisposable
{
    private readonly string _scratch;

    public TreeFilesTests()
    {
        _scratch = Path.Combine(RepoRoot(), "_fixtures", "tree-files", Guid.NewGuid().ToString("N")[..8]);
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

    /// <summary>
    /// What is there to mention: every file git tracks and every new one it does not ignore — a file
    /// the agent wrote a minute ago is as mentionable as one from last year. What git ignores is not
    /// offered, and neither is a tracked file deleted from the tree, which no harness could expand.
    /// </summary>
    [Fact]
    public async Task It_offers_what_the_tree_holds_and_nothing_it_ignores_or_lost()
    {
        var root = await MakeRepositoryAsync();
        await File.WriteAllTextAsync(Path.Combine(root, ".gitignore"), "build/\n");
        Directory.CreateDirectory(Path.Combine(root, "build"));
        await File.WriteAllTextAsync(Path.Combine(root, "build", "out.bin"), "ignored\n");
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        await File.WriteAllTextAsync(Path.Combine(root, "docs", "new note.md"), "untracked\n");
        File.Delete(Path.Combine(root, "gone.txt"));

        var files = await WorkingTree.FilesAsync(root);

        Assert.NotNull(files);
        Assert.Equal([".gitignore", "README.md", "docs/new note.md", "src/engine.cs"], files!.Files);
        Assert.Equal(0, files.Unlisted);
    }

    /// <summary>
    /// A name outside ASCII arrives as its name. git quotes one as octal escapes by default, and a
    /// completion offering `"\347\254\224\350\256\260.md"` offers a file nobody has.
    /// </summary>
    [Fact]
    public async Task A_name_outside_ascii_arrives_as_itself()
    {
        var root = await MakeRepositoryAsync();
        await File.WriteAllTextAsync(Path.Combine(root, "笔记.md"), "saffron\n");

        var files = await WorkingTree.FilesAsync(root);

        Assert.Contains("笔记.md", files!.Files);
    }

    /// <summary>
    /// 🔴 git WALKS UP (FIX-LOG 2026-09-22): in a plain directory inside a repository it answers for
    /// the repository above, and from a subdirectory it answers for the whole. Either would offer
    /// files that are not in the session's tree, so both are "git cannot say".
    /// </summary>
    [Fact]
    public async Task It_answers_nothing_for_a_tree_that_is_not_a_repository_of_its_own()
    {
        var root = await MakeRepositoryAsync();

        var notARepository = Path.Combine(_scratch, "bare");
        Directory.CreateDirectory(notARepository);
        Assert.Null(await WorkingTree.FilesAsync(notARepository));

        Assert.Null(await WorkingTree.FilesAsync(Path.Combine(root, "src")));
        Assert.Null(await WorkingTree.FilesAsync(Path.Combine(_scratch, "never-made")));
    }

    /// <summary>
    /// The bound is stated, never hidden (design §5): what it left out is counted, so the page can
    /// say that a path it does not offer can still be typed.
    /// </summary>
    [Fact]
    public async Task The_bound_keeps_the_first_files_and_counts_the_rest()
    {
        var root = await MakeRepositoryAsync();

        var files = await WorkingTree.FilesAsync(root, limit: 2);

        Assert.Equal(["README.md", "gone.txt"], files!.Files);
        Assert.Equal(1, files.Unlisted);
    }

    private async Task<string> MakeRepositoryAsync()
    {
        var root = Path.Combine(_scratch, "engine");
        Directory.CreateDirectory(Path.Combine(root, "src"));
        await GitAsync(root, "init", "--quiet");
        await GitAsync(root, "config", "user.email", "fixture@example.test");
        await GitAsync(root, "config", "user.name", "Fixture");
        await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "# engine\n");
        await File.WriteAllTextAsync(Path.Combine(root, "gone.txt"), "delete me\n");
        await File.WriteAllTextAsync(Path.Combine(root, "src", "engine.cs"), "class Engine {}\n");
        await GitAsync(root, "add", ".");
        await GitAsync(root, "commit", "-m", "first");
        return root;
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
