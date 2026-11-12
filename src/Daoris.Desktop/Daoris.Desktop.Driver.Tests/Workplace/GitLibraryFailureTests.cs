using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A library call that fails in any way is git's to answer, and a refusal names its cause.</summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GitLibraryFailureTests : LandedFixture
{
    [Fact]
    public async Task A_call_that_throws_what_the_library_does_not_name_is_the_gits_to_answer()
    {
        var root = await BuildRepositoryAsync("throwing");

        Assert.Null(await GitLibrary.AskAsync<string>(root, GitLibraryQuestion.Refs, _ => throw new NullReferenceException(), CancellationToken.None));
        Assert.Equal(1, GitLibraryJournal.Of(root)!.Reasons[GitLibraryJournal.Failed]);
    }

    [Fact]
    public async Task A_valueless_branch_key_is_read_by_git_and_never_thrown()
    {
        var root = await BuildRepositoryAsync("valueless");
        await File.AppendAllTextAsync(Path.Combine(RefFiles.CommonDirOf(root)!, "config"), "[branch \"main\"]\n\tremote = origin\n\tmerge\n");

        await GitLibraryRefs.ListingAsync(root, CancellationToken.None);
    }

    [Fact]
    public async Task A_configuration_held_open_is_refused_as_a_configuration_not_as_no_repository()
    {
        var root = await BuildRepositoryAsync("config-held");
        using var held = new FileStream(Path.Combine(RefFiles.CommonDirOf(root)!, "config"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        Assert.Equal(GitLibraryAdmission.Configuration, GitLibraryAdmission.RefusalOf(root, GitLibraryQuestion.Names));
    }
}
