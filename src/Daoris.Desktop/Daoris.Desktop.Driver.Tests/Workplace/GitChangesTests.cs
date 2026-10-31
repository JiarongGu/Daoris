using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// GITAPP2b (D204, the git-writes design §1 and §4): the Changes tab's read. Three fresh observations of a checkout, HEAD to index,
/// index to worktree, and status's untracked and unmerged entries, each started through the counting seam as a question and
/// nothing else. A staged addition whose file was then removed shows in both diffs.
/// </summary>
/// <remarks>Real git in scratch repositories under the repository's <c>_fixtures</c>, with a home of the fixture's own.</remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GitChangesTests : LandedFixture
{
    [Fact]
    public async Task A_file_staged_and_then_removed_is_seen_in_head_to_index_and_in_index_to_worktree()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        await File.WriteAllTextAsync(Path.Combine(root, "gone.txt"), "staged, then removed\n");
        await GitAsync(root, "add", "gone.txt");
        File.Delete(Path.Combine(root, "gone.txt"));

        var answer = await GitChanges.ReadAsync(root);

        var read = Assert.IsType<ChangesRead>(answer.Value);
        var staged = Assert.Single(read.Staged);
        Assert.Equal(("gone.txt", 'A'), (staged.Path, staged.Status));
        Assert.Equal("100644", staged.NewMode);
        Assert.Matches("^[0-9a-f]{40,64}$", staged.NewBlob);
        Assert.Matches("^0+$", staged.OldBlob);
        var unstaged = Assert.Single(read.Unstaged);
        Assert.Equal(("gone.txt", 'D'), (unstaged.Path, unstaged.Status));
        // The index side of the second observation is the staged blob itself.
        Assert.Equal(staged.NewBlob, unstaged.OldBlob);
        Assert.Empty(read.Untracked);
        Assert.Empty(read.Unmerged);
    }

    [Fact]
    public async Task A_modified_file_an_untracked_file_and_a_file_whose_stat_alone_moved_are_told_apart()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        await CommitAsync(root, "kept.txt", "kept", "a file the person keeps");
        await CommitAsync(root, "own.txt", "mine", "a file they change");
        await File.WriteAllTextAsync(Path.Combine(root, "own.txt"), "mine, changed\n");
        await File.WriteAllTextAsync(Path.Combine(root, "fresh.txt"), "new\n");
        File.SetLastWriteTimeUtc(Path.Combine(root, "kept.txt"), DateTime.UtcNow.AddHours(1));
        var index = (await GitAsync(root, "rev-parse", "--path-format=absolute", "--git-path", "index")).Trim();
        var before = File.ReadAllBytes(index);

        var read = Assert.IsType<ChangesRead>((await GitChanges.ReadAsync(root)).Value);

        Assert.Empty(read.Staged);
        Assert.Equal(["own.txt"], read.Unstaged.Select(entry => entry.Path));
        Assert.Equal('M', read.Unstaged[0].Status);
        Assert.Equal(["fresh.txt"], read.Untracked);
        // A read refreshes nothing and takes no lock: the index is the bytes it was.
        Assert.Equal(before, File.ReadAllBytes(index));
        Assert.False(File.Exists(index + ".lock"));
    }

    [Fact]
    public async Task A_conflicted_file_is_unmerged_with_the_mode_and_blob_of_each_stage()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        await CommitAsync(root, "both.txt", "base", "base");
        await GitAsync(root, "checkout", "-q", "-b", "other");
        await CommitAsync(root, "both.txt", "theirs", "theirs");
        await GitAsync(root, "checkout", "-q", "-");
        await CommitAsync(root, "both.txt", "ours", "ours");
        await GitAsync(root, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "merge", "other");

        var read = Assert.IsType<ChangesRead>((await GitChanges.ReadAsync(root)).Value);

        var unmerged = Assert.Single(read.Unmerged);
        Assert.Equal(("both.txt", "UU"), (unmerged.Path, unmerged.Code));
        Assert.Equal(["100644", "100644", "100644"], unmerged.Modes);
        Assert.Equal(3, unmerged.Blobs.Distinct().Count());
    }

    [Fact]
    public async Task The_read_starts_only_questions_through_the_seam_and_none_takes_a_lock()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        await File.WriteAllTextAsync(Path.Combine(root, "gone.txt"), "x\n");
        await GitAsync(root, "add", "gone.txt");

        using var count = GitLaunch.Count();
        var answer = await GitChanges.ReadAsync(root);

        Assert.Null(answer.Problem);
        Assert.NotEmpty(count.Starts);
        Assert.All(count.Starts, start =>
        {
            Assert.True(
                start.Kind is GitKind.Layout or GitKind.IndexDiff or GitKind.WorktreeDiff or GitKind.Status,
                $"{string.Join(' ', start.Arguments)} is no question");
            Assert.Equal(GitRunner.Whole, start.Runner);
            // Every start that looks at the index or the tree leaves its refresh to the person's own git.
            if (start.Kind is not GitKind.Layout) Assert.Contains("--no-optional-locks", start.Arguments);
        });
        Assert.Contains(count.Starts, start => start.Kind == GitKind.IndexDiff);
        Assert.Contains(count.Starts, start => start.Kind == GitKind.WorktreeDiff);
        Assert.Contains(count.Starts, start => start.Kind == GitKind.Status);
    }

    [Fact]
    public async Task A_folder_that_is_not_a_repository_s_top_is_refused_and_asks_nothing_more()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        var inside = Path.Combine(root, "inside");
        Directory.CreateDirectory(inside);

        var answer = await GitChanges.ReadAsync(inside);

        Assert.Equal(GitReadRefusal.NotTop, answer.Refusal);
        Assert.Null(answer.Value);
    }
}
