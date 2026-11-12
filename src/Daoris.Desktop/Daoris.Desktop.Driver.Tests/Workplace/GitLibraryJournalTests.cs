using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The library's journal: every refusal its admission makes and every read that fell back to git's binary is kept with its
/// reason beside the repository it served, and every answer it gave is counted, so the Git page lists what each repository read
/// through the library and through git.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GitLibraryJournalTests : LandedFixture
{
    [Fact]
    public async Task An_admitted_answer_is_counted_for_its_repository_and_asks_git_nothing()
    {
        var root = await BuildRepositoryAsync("journal-answered");

        Assert.NotNull(await GitLibrary.AskAsync(root, GitLibraryQuestion.Walks, call => call.Repository.Head.CanonicalName, CancellationToken.None));

        var row = GitLibraryJournal.Of(root);
        Assert.NotNull(row);
        Assert.Equal(1, row.Library);
        Assert.Equal(0, row.Git);
        Assert.Empty(row.Reasons);
    }

    [Fact]
    public async Task A_refusal_is_kept_with_its_reason_and_a_call_the_library_declined_with_its_own()
    {
        var root = await BuildRepositoryAsync("journal-refused");
        Directory.CreateDirectory(Path.Combine(root, ".git", "reftable"));

        Assert.Null(await GitLibrary.AskAsync(root, GitLibraryQuestion.Refs, _ => "never", CancellationToken.None));
        Assert.Null(await GitLibrary.AskAsync(root, GitLibraryQuestion.Walks, _ => "never", CancellationToken.None));
        Directory.Delete(Path.Combine(root, ".git", "reftable"));
        Assert.Null(await GitLibrary.AskAsync(root, GitLibraryQuestion.Walks, _ => (string?)null, CancellationToken.None));

        var row = GitLibraryJournal.Of(root)!;
        Assert.Equal(3, row.Git);
        Assert.Equal(0, row.Library);
        Assert.Equal(2, row.Reasons[GitLibraryAdmission.Reftable]);
        Assert.Equal(1, row.Reasons[GitLibraryJournal.Unanswered]);
    }

    [Fact]
    public async Task A_call_that_failed_is_kept_as_failed_and_the_journal_names_the_folder_never_its_path()
    {
        var root = await BuildRepositoryAsync("journal-failed");

        Assert.Null(await GitLibrary.AskAsync(root, GitLibraryQuestion.Walks,
            new Func<GitLibraryCall, string?>(_ => throw new IOException("a pack went away")), CancellationToken.None));

        var row = GitLibraryJournal.Of(root)!;
        Assert.Equal(1, row.Reasons[GitLibraryJournal.Failed]);
        Assert.Equal("journal-failed", row.Repository);
        Assert.DoesNotContain(Scratch, row.Repository, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task What_it_kept_is_read_again_by_the_next_process()
    {
        var home = Path.Combine(Scratch, "journal-home");
        var root = await BuildRepositoryAsync("journal-kept");
        Directory.CreateDirectory(Path.Combine(root, ".git", "reftable"));
        var before = ProgramHome.Named;
        ProgramHome.Named = home;
        try
        {
            Assert.Null(await GitLibrary.AskAsync(root, GitLibraryQuestion.Refs, _ => "never", CancellationToken.None));

            Assert.Equal(1, GitLibraryJournal.Loaded(home).Single(row => row.Repository == "journal-kept").Reasons[GitLibraryAdmission.Reftable]);
        }
        finally
        {
            ProgramHome.Named = before;
        }
    }

    [Fact]
    public void The_git_page_lists_each_repository_by_name_with_its_counts_and_reasons_and_no_path()
    {
        var folder = Path.Combine(Scratch, "listed-engine");
        GitLibraryJournal.Answered(folder);
        GitLibraryJournal.Answered(folder);
        GitLibraryJournal.Fell(folder, GitLibraryAdmission.Reftable);
        GitLibraryJournal.Fell(folder, GitLibraryJournal.Unanswered);
        GitLibraryJournal.Fell(folder, GitLibraryAdmission.Reftable);

        var listed = JsonSerializer.SerializeToElement(GitLibraryJournal.Page(), new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var row = listed.EnumerateArray().Single(each => each.GetProperty("repository").GetString() == "listed-engine");
        Assert.Equal((2, 3), (row.GetProperty("library").GetInt32(), row.GetProperty("git").GetInt32()));
        Assert.Equal(
            [("reftable", 2), ("unanswered", 1)],
            row.GetProperty("reasons").EnumerateArray()
                .Select(each => (each.GetProperty("reason").GetString()!, each.GetProperty("count").GetInt32())).ToArray());
        Assert.DoesNotContain(Scratch, row.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }
}
