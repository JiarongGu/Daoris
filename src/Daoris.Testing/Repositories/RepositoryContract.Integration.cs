using Daoris.Ports.Repositories;
using Xunit;

namespace Daoris.Testing;

public abstract partial class RepositoryContract
{
    /// <summary>A repository on <c>main</c> at its first commit with <c>feature</c> one commit on it that writes <paramref name="path"/>, main checked out again.</summary>
    private async Task<(string Root, string First, string Feature)> BranchedAsync(string path, string content)
    {
        var (root, first) = await OneAsync();
        Assert.True((await Backend.SwitchAsync(root, new GitSwitch("feature", first, Creates: true), Ct)).Ok);
        await WriteAsync(root, path, content);
        Assert.True((await Backend.CommitAllAsync(root, "on feature", Ct)).Ok);
        var feature = (await Backend.HeadAsync(root, Ct))!;
        Assert.True((await Backend.SwitchAsync(root, new GitSwitch("main"), Ct)).Ok);
        return (root, first, feature);
    }

    [Fact]
    public async Task A_fast_forward_merge_moves_the_branch_and_its_files_and_a_diverged_history_is_refused()
    {
        var (root, _, feature) = await BranchedAsync("c.txt", "three\n");

        var merged = await Backend.MergeAsync(root, new GitMergeAsk(feature, GitMergeKind.FastForwardOnly), Ct);

        Assert.True(merged.Ok, merged.Error);
        Assert.Equal(feature, await Backend.HeadAsync(root, Ct));
        Assert.Equal("three\n", await Backend.TextAtAsync(root, "HEAD", "c.txt", Ct));
        Assert.Empty(await Backend.StatusAsync(root, false, Ct));

        Assert.True((await Backend.SwitchAsync(root, new GitSwitch("other", (await Backend.CommitOfAsync(root, "HEAD^", Ct))!, Creates: true), Ct)).Ok);
        await WriteAsync(root, "d.txt", "four\n");
        Assert.True((await Backend.CommitAllAsync(root, "diverged", Ct)).Ok);
        var refused = await Backend.MergeAsync(root, new GitMergeAsk(feature, GitMergeKind.FastForwardOnly), Ct);

        Assert.False(refused.Ok);
        Assert.False(refused.Pending);
    }

    [Fact]
    public async Task A_merge_commit_holds_both_sides_and_has_the_merged_commit_as_its_second_parent()
    {
        var (root, _, feature) = await BranchedAsync("c.txt", "three\n");
        await WriteAsync(root, "a.txt", "changed on main\n");
        Assert.True((await Backend.CommitAllAsync(root, "on main", Ct)).Ok);
        var main = (await Backend.HeadAsync(root, Ct))!;

        var merged = await Backend.MergeAsync(root, new GitMergeAsk(feature, GitMergeKind.Commit, "Merge branch 'feature'"), Ct);

        Assert.True(merged.Ok, merged.Error);
        Assert.False(merged.Pending);
        Assert.Equal(main, await Backend.CommitOfAsync(root, "HEAD^1", Ct));
        Assert.Equal(feature, await Backend.CommitOfAsync(root, "HEAD^2", Ct));
        Assert.Equal("three\n", await Backend.TextAtAsync(root, "HEAD", "c.txt", Ct));
        Assert.Equal("changed on main\n", await Backend.TextAtAsync(root, "HEAD", "a.txt", Ct));
        Assert.Empty(await Backend.StatusAsync(root, false, Ct));
        Assert.True((await Backend.MergeAsync(root, new GitMergeAsk(feature, GitMergeKind.Commit, "again"), Ct)).Ok);
        Assert.Equal(2, await Backend.CountAsync(root, "HEAD", [main], Ct));
    }

    [Fact]
    public async Task A_staged_merge_waits_for_its_commit_which_makes_the_merge_commit_and_an_abort_puts_it_back()
    {
        var (root, _, feature) = await BranchedAsync("c.txt", "three\n");
        await WriteAsync(root, "a.txt", "changed on main\n");
        Assert.True((await Backend.CommitAllAsync(root, "on main", Ct)).Ok);
        var main = (await Backend.HeadAsync(root, Ct))!;

        var staged = await Backend.MergeAsync(root, new GitMergeAsk(feature, GitMergeKind.Stage), Ct);

        Assert.True(staged.Ok, staged.Error);
        Assert.True(staged.Pending);
        Assert.Equal(main, await Backend.HeadAsync(root, Ct));
        Assert.Empty(await Backend.UnmergedAsync(root, Ct));
        Assert.True((await Backend.MergeAbortAsync(root, Ct)).Ok);
        Assert.Empty(await Backend.StatusAsync(root, false, Ct));
        Assert.Null(await Backend.TextAtAsync(root, "HEAD", "c.txt", Ct));

        Assert.True((await Backend.MergeAsync(root, new GitMergeAsk(feature, GitMergeKind.Stage), Ct)).Ok);
        Assert.True((await Backend.CommitStagedAsync(root, "Merge branch 'feature'", Ct)).Ok);

        Assert.Equal(feature, await Backend.CommitOfAsync(root, "HEAD^2", Ct));
        Assert.Equal(main, await Backend.CommitOfAsync(root, "HEAD^1", Ct));
        Assert.False((await Backend.CommitStagedAsync(root, "nothing staged", Ct)).Ok);
    }

    [Fact]
    public async Task A_merge_whose_sides_changed_one_file_differently_is_left_begun_with_that_path_unmerged_until_abandoned()
    {
        var (root, _, feature) = await BranchedAsync("a.txt", "theirs\n");
        await WriteAsync(root, "a.txt", "ours\n");
        await WriteAsync(root, "b-only.txt", "main only\n");
        Assert.True((await Backend.CommitAllAsync(root, "on main", Ct)).Ok);
        var main = (await Backend.HeadAsync(root, Ct))!;

        var merged = await Backend.MergeAsync(root, new GitMergeAsk(feature, GitMergeKind.Stage), Ct);

        Assert.False(merged.Ok);
        Assert.True(merged.Pending);
        Assert.Equal(["a.txt"], merged.Unmerged);
        Assert.Equal(["a.txt"], await Backend.UnmergedAsync(root, Ct));
        Assert.False((await Backend.CommitStagedAsync(root, "unsettled", Ct)).Ok);
        Assert.False((await Backend.MergeAsync(root, new GitMergeAsk(feature, GitMergeKind.Stage), Ct)).Ok);

        Assert.True((await Backend.MergeAbortAsync(root, Ct)).Ok);

        Assert.Empty(await Backend.UnmergedAsync(root, Ct));
        Assert.Empty(await Backend.StatusAsync(root, false, Ct));
        Assert.Equal(main, await Backend.HeadAsync(root, Ct));
        Assert.True((await Backend.MergeAbortAsync(root, Ct)).Ok);
    }

    [Fact]
    public async Task A_checkout_put_back_at_a_commit_is_detached_there_with_its_merge_abandoned_and_its_loose_files_gone()
    {
        var (root, first, feature) = await BranchedAsync("c.txt", "three\n");
        await WriteAsync(root, "a.txt", "changed on main\n");
        Assert.True((await Backend.CommitAllAsync(root, "on main", Ct)).Ok);
        Assert.True((await Backend.MergeAsync(root, new GitMergeAsk(feature, GitMergeKind.Stage), Ct)).Ok);
        await WriteAsync(root, "loose.txt", "x\n");

        var done = await Backend.PutBackAsync(root, first, Ct);

        Assert.True(done.Ok, done.Error);
        Assert.Null(await Backend.BranchAsync(root, Ct));
        Assert.Equal(first, await Backend.HeadAsync(root, Ct));
        Assert.Empty(await Backend.StatusAsync(root, true, Ct));
        Assert.True((await Backend.MergeAsync(root, new GitMergeAsk(feature, GitMergeKind.Stage), Ct)).Pending);
    }
}
