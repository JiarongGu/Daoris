using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>The system file a git names is kept for the process only once the git has answered.</summary>
public sealed class GitSystemFilesTests
{
    private static readonly string Named = Path.GetFullPath(Path.Combine("_system", "gitconfig"));

    [Fact]
    public async Task A_start_that_did_not_run_is_asked_again_and_an_answer_is_kept()
    {
        var answers = new Queue<(int, string)>([(-1, ""), (0, Named + "\n")]);
        var asked = 0;
        var files = new GitSystemFiles((_, _) =>
        {
            asked++;
            return Task.FromResult(answers.Dequeue());
        });

        await files.LearnAsync("git", "folder", CancellationToken.None);
        Assert.Null(files.Of("git"));

        await files.LearnAsync("git", "folder", CancellationToken.None);
        await files.LearnAsync("git", "folder", CancellationToken.None);
        Assert.Equal((Named, 2), (files.Of("git"), asked));
    }

    [Fact]
    public async Task A_git_that_answers_it_names_none_is_kept_as_none()
    {
        var files = new GitSystemFiles((_, _) => Task.FromResult((0, "\n")));

        await files.LearnAsync("git", "folder", CancellationToken.None);

        Assert.Equal("", files.Of("git"));
    }
}
