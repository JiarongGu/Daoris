namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The fixture's one git runner (TESTGIT1), against the run that held the merge's real-process half for 1 h 33 m (FIX-LOG
/// 2026-10-04): git writing more to stderr than a pipe holds before its stdout ends.
/// </summary>
/// <remarks>
/// Fixtures live under the repository's gitignored `_fixtures/`, never OS temp.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class GitFixtureTests : IDisposable
{
    private readonly string _scratch;

    public GitFixtureTests()
    {
        _scratch = Path.Combine(RepoRoot(), "_fixtures", "git-fixture", Guid.NewGuid().ToString("N")[..8]);
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
    /// `add -A` over two hundred new files whose line endings convert: git writes a warning per file to stderr, several
    /// times what filled the pipe at sixty, before its stdout ends. A runner that read stdout to its end before stderr
    /// waited here forever; this one answers inside the bound, with every warning and every file added.
    /// </summary>
    [Fact]
    public async Task Add_over_two_hundred_files_whose_line_endings_convert_answers_with_every_warning()
    {
        const int count = 200;
        var root = Path.Combine(_scratch, "engine");
        Directory.CreateDirectory(root);
        await GitFixture.GitAsync(root, "init", "--quiet");
        // The fixture's own config, so the person's git settings can neither quiet the warnings nor make them a refusal.
        await GitFixture.GitAsync(root, "config", "core.autocrlf", "true");
        await GitFixture.GitAsync(root, "config", "core.safecrlf", "warn");
        for (var i = 0; i < count; i++)
        {
            await File.WriteAllTextAsync(Path.Combine(root, $"file-{i:D3}.txt"), "one line\n");
        }

        var added = await GitFixture.RunAsync(root, "add", "-A").WaitAsync(TimeSpan.FromSeconds(60));

        Assert.True(added.ExitCode == 0, added.Stderr);
        // At least a line a file, whatever git's version or language words it as.
        Assert.True(added.Stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length >= count, added.Stderr);
        var staged = await GitFixture.GitAsync(root, "ls-files");
        Assert.Equal(count, staged.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")) &&
               !File.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? AppContext.BaseDirectory;
    }
}
