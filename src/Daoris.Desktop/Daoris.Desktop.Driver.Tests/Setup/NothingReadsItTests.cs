using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>What the set-up press counts as a repository nothing else reads: one commit on its line, no session.</summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class NothingReadsItTests : IDisposable
{
    private readonly string _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "nothing-reads-it", Guid.NewGuid().ToString("N")[..8]);

    public NothingReadsItTests() => Directory.CreateDirectory(_scratch);

    public void Dispose() => GitSeed.Remove(_scratch);

    private async Task<RepoView> MadeAsync()
    {
        var root = await GitSeed.PlainAsync(Path.Combine(_scratch, "ledger"), "ledger");
        return new RepoView("ledger", false, root, "aurora");
    }

    [Fact]
    public async Task One_commit_and_no_session_is_a_repository_nothing_reads()
    {
        var row = await MadeAsync();

        Assert.True(await NothingReadsIt.JudgeAsync(row, "main", [], default));
    }

    [Fact]
    public async Task A_second_commit_or_a_session_in_it_is_work()
    {
        var row = await MadeAsync();
        Assert.False(await NothingReadsIt.JudgeAsync(row, "main", [new SessionRun("s1", "Ledger")], default));

        await GitFixture.GitAsync(row.Root!, "commit", "--allow-empty", "--quiet", "-m", "second");
        Assert.False(await NothingReadsIt.JudgeAsync(row, "main", [], default));
    }
}
