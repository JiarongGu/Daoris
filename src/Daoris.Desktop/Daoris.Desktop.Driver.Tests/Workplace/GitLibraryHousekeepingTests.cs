using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitLibraryParity;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A question asked while git rewrites a repository's packs, multi-pack index or commit graph is git's, so the library never maps a
/// file git is about to rename over (Windows refuses that rename however the file is shared); a marker a crashed run left stops nothing.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GitLibraryHousekeepingTests : LandedFixture
{
    [Theory]
    [InlineData("gc.pid")]
    [InlineData("objects/pack/.tmp-1234-pack-abc.pack")]
    [InlineData("objects/pack/multi-pack-index.lock")]
    [InlineData("objects/info/commit-graph.lock")]
    [InlineData("objects/info/commit-graphs/commit-graph-chain.lock")]
    public async Task A_marker_of_a_rewrite_under_way_sends_every_question_to_git_until_it_goes(string marker)
    {
        var root = await BuildRepositoryAsync("housekeeping");
        var file = Path.Combine(RefFiles.CommonDirOf(root)!, marker.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        Assert.Null(GitLibraryAdmission.RefusalOf(root, GitLibraryQuestion.Refs));

        await File.WriteAllTextAsync(file, "x");
        foreach (var question in Enum.GetValues<GitLibraryQuestion>())
        {
            Assert.Equal(GitLibraryAdmission.Housekeeping, GitLibraryAdmission.RefusalOf(root, question));
        }

        Assert.Null(await GitLibrary.AskAsync(root, GitLibraryQuestion.Refs, _ => "never", CancellationToken.None));
        Assert.Equal(1, GitLibraryJournal.Of(root)!.Reasons[GitLibraryAdmission.Housekeeping]);

        File.Delete(file);
        Assert.Null(GitLibraryAdmission.RefusalOf(root, GitLibraryQuestion.Refs));
    }

    [Fact]
    public async Task A_question_that_waited_for_a_slot_is_admitted_once_it_has_one()
    {
        var root = await BuildRepositoryAsync("housekeeping-queued");
        using var release = new ManualResetEventSlim();
        var holders = Enumerable.Range(0, GitLibrary.AtOnce)
            .Select(_ => GitLibrary.AskAsync(root, GitLibraryQuestion.Refs, _ => { release.Wait(); return "held"; }, CancellationToken.None))
            .ToList();
        while (GitLibrary.Open < GitLibrary.AtOnce) await Task.Delay(10);

        var queued = GitLibrary.AskAsync(root, GitLibraryQuestion.Refs, _ => "never", CancellationToken.None);
        await Task.Delay(200);
        await File.WriteAllTextAsync(Path.Combine(RefFiles.CommonDirOf(root)!, "gc.pid"), "x");
        release.Set();

        Assert.Null(await queued.WaitAsync(Poll.Patience));
        await Task.WhenAll(holders).WaitAsync(Poll.Patience);
    }

    [GitLegFact]
    public async Task A_marker_a_crashed_run_left_for_a_day_stops_no_question()
    {
        var root = await BuildRepositoryAsync("housekeeping-stale");
        var file = Path.Combine(RefFiles.CommonDirOf(root)!, "gc.pid");
        await File.WriteAllTextAsync(file, "x");
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddDays(-1));

        Assert.Null(GitLibraryAdmission.RefusalOf(root, GitLibraryQuestion.Refs));
    }

    [GitLegFact]
    public async Task A_marker_outside_the_files_git_rewrites_stops_no_question()
    {
        var root = await BuildRepositoryAsync("housekeeping-other");
        var pack = Path.Combine(RefFiles.CommonDirOf(root)!, "objects", "pack");
        Directory.CreateDirectory(pack);
        await File.WriteAllTextAsync(Path.Combine(pack, "tmp_pack_fetching"), "x");
        await File.WriteAllTextAsync(Path.Combine(pack, "pack-abc.keep"), "x");

        Assert.Null(GitLibraryAdmission.RefusalOf(root, GitLibraryQuestion.Refs));
    }
}
