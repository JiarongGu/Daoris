using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitLibraryParity;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The <c>git.read.formats</c> probe: a repository's object and ref formats are read once for its configuration's stamp,
/// and a format the library cannot read is admitted to git's binary alone, so the library is never opened on it.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GitLibraryFormatsTests : LandedFixture
{
    [GitLegFact]
    public async Task A_default_repository_is_sha1_and_files_and_is_read_once_until_its_configuration_moves()
    {
        var root = await BuildRepositoryAsync("formats-default");
        var common = RefFiles.CommonDirOf(root)!;
        var before = GitLibraryFormats.Reads;

        var format = GitLibraryFormats.Of(common);
        Assert.Equal(new GitLibraryFormat("sha1", "files"), format);
        Assert.Null(GitLibraryAdmission.RefusalOf(root, GitLibraryQuestion.Walks));
        Assert.Equal(format, GitLibraryFormats.Of(common));
        Assert.Equal(before + 1, GitLibraryFormats.Reads);

        await GitAsync(root, "config", "daoris.moved", "yes");
        Assert.Equal(format, GitLibraryFormats.Of(common));
        Assert.Equal(before + 2, GitLibraryFormats.Reads);
    }

    [GitLegFact]
    public async Task A_sha256_repository_is_git_s_for_every_question_and_the_library_never_opens_it()
    {
        var root = Path.Combine(Scratch, "formats-sha256");
        Directory.CreateDirectory(root);
        Assert.Equal(0, (await RunAsync(root, null, null, "init", "--quiet", "--object-format=sha256", "-b", "main")).Code);
        await CommitAsync(root, "a.txt", "a\n", "first");

        Assert.Equal(new GitLibraryFormat("sha256", "files"), GitLibraryFormats.Of(RefFiles.CommonDirOf(root)!));
        foreach (var question in Enum.GetValues<GitLibraryQuestion>())
        {
            Assert.Equal(GitLibraryAdmission.Sha256, GitLibraryAdmission.RefusalOf(root, question));
        }

        var opened = false;
        Assert.Null(await GitLibrary.AskAsync(root, GitLibraryQuestion.Walks, _ =>
        {
            opened = true;
            return "never";
        }, CancellationToken.None));
        Assert.False(opened);
        Assert.Equal(1, GitLibraryJournal.Of(root)!.Reasons[GitLibraryAdmission.Sha256]);
    }

    [GitLegFact]
    public async Task A_repository_whose_configuration_names_reftable_is_git_s_though_it_holds_no_reftable_folder_yet()
    {
        var root = await BuildRepositoryAsync("formats-reftable");
        await GitAsync(root, "config", "core.repositoryformatversion", "1");
        await GitAsync(root, "config", "extensions.refstorage", "reftable");

        Assert.Equal(new GitLibraryFormat("sha1", "reftable"), GitLibraryFormats.Of(RefFiles.CommonDirOf(root)!));
        Assert.Equal(GitLibraryAdmission.Reftable, GitLibraryAdmission.RefusalOf(root, GitLibraryQuestion.Refs));
    }

    [GitLegFact]
    public async Task A_format_the_probe_does_not_know_is_refused_by_its_own_word()
    {
        var root = await BuildRepositoryAsync("formats-unknown");
        await GitAsync(root, "config", "core.repositoryformatversion", "1");
        await GitAsync(root, "config", "extensions.objectformat", "sha3");

        Assert.Equal(GitLibraryAdmission.ObjectFormat, GitLibraryAdmission.RefusalOf(root, GitLibraryQuestion.Walks));
    }
}
