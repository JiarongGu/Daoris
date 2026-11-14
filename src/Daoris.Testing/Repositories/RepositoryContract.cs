using Daoris.Ports.Repositories;
using Xunit;

namespace Daoris.Testing;

/// <summary>
/// The cases every <see cref="IGitRepository"/> must pass, written once against the port. A subclass hands the backend and the few acts
/// a test cannot ask of the port (a fresh repository, a file written, a path hidden, a folder gone): the memory repository does them in
/// memory and the real one on disk, so the same case holds both to what git answers.
/// </summary>
public abstract class RepositoryContract
{
    protected abstract IGitRepository Backend { get; }

    /// <summary>A folder holding a new repository on <c>main</c>, with no commit and a committer set.</summary>
    protected abstract Task<string> NewAsync();

    protected abstract Task WriteAsync(string folder, string path, string content);

    protected abstract Task RemoveAsync(string folder, string path);

    protected abstract Task HideAsync(string folder, string path);

    /// <summary>Makes the folder of a working tree gone, as a person deleting it does.</summary>
    protected abstract Task VanishAsync(string folder);

    /// <summary>A folder beside <paramref name="root"/> no working tree stands in yet.</summary>
    protected abstract string Beside(string root, string name);

    private static readonly CancellationToken Ct = CancellationToken.None;

    private async Task<(string Root, string First)> OneAsync()
    {
        var root = await NewAsync();
        await WriteAsync(root, "a.txt", "one\n");
        await WriteAsync(root, "src/b.txt", "two\n");
        Assert.True((await Backend.CommitAllAsync(root, "first", Ct)).Ok);
        return (root, (await Backend.HeadAsync(root, Ct))!);
    }

    [Fact]
    public async Task A_new_repository_has_no_head_and_names_its_branch_and_a_commit_makes_a_head()
    {
        var root = await NewAsync();
        Assert.Null(await Backend.HeadAsync(root, Ct));
        Assert.Equal("main", await Backend.BranchAsync(root, Ct));
        Assert.False((await Backend.CommitAllAsync(root, "nothing", Ct)).Ok);

        await WriteAsync(root, "a.txt", "one\n");
        Assert.True((await Backend.CommitAllAsync(root, "first", Ct)).Ok);

        Assert.Matches("^[0-9a-f]{40}$", await Backend.HeadAsync(root, Ct));
        Assert.False((await Backend.CommitAllAsync(root, "again", Ct)).Ok);
    }

    [Fact]
    public async Task Status_names_each_kind_of_change_and_each_ignored_top_where_asked()
    {
        var (root, _) = await OneAsync();
        await WriteAsync(root, ".gitignore", "*.log\nobj/\n");
        Assert.True((await Backend.CommitAllAsync(root, "ignores", Ct)).Ok);
        await WriteAsync(root, "a.txt", "changed\n");
        await WriteAsync(root, "new.txt", "new\n");
        await WriteAsync(root, "run.log", "x\n");
        await WriteAsync(root, "obj/out.dll", "x\n");

        var plain = await Backend.StatusAsync(root, ignored: false, Ct);
        var all = await Backend.StatusAsync(root, ignored: true, Ct);

        Assert.Equal([("a.txt", " M"), ("new.txt", "??")], plain.OrderBy(each => each.Key, StringComparer.Ordinal).Select(each => (each.Key, each.Value)));
        Assert.Equal([("a.txt", " M"), ("new.txt", "??"), ("obj/", "!!"), ("run.log", "!!")], all.OrderBy(each => each.Key, StringComparer.Ordinal).Select(each => (each.Key, each.Value)));
    }

    [Fact]
    public async Task Ignored_tops_are_listed_under_the_paths_asked()
    {
        var (root, _) = await OneAsync();
        await WriteAsync(root, ".gitignore", "*.log\nobj/\n");
        Assert.True((await Backend.CommitAllAsync(root, "ignores", Ct)).Ok);
        await WriteAsync(root, "run.log", "x\n");
        await WriteAsync(root, "obj/out.dll", "x\n");
        await WriteAsync(root, "src/build.log", "x\n");

        Assert.Equal(["obj/", "run.log", "src/build.log"], await Backend.IgnoredAsync(root, [], Ct));
        Assert.Equal(["obj/", "run.log", "src/build.log"], await Backend.IgnoredTopsAsync(root, Ct));
        Assert.Equal(["obj/"], await Backend.IgnoredAsync(root, ["obj"], Ct));
        Assert.Equal(["src/build.log"], await Backend.IgnoredAsync(root, ["src"], Ct));
    }

    /// <summary>Whether this backend's repository matches ignore patterns without regard to case, as git does on the machine's own disk.</summary>
    protected abstract bool CaseIgnored { get; }

    [Fact]
    public async Task Whether_ignore_patterns_match_without_regard_to_case_is_the_repositorys_own_setting()
    {
        var root = await NewAsync();

        Assert.Equal(CaseIgnored, await Backend.IgnoresCaseAsync(root, Ct));
    }

    [Fact]
    public async Task A_tracked_file_taken_away_is_a_deletion()
    {
        var (root, _) = await OneAsync();
        Assert.Empty(await Backend.StatusAsync(root, ignored: false, Ct));

        await RemoveAsync(root, "a.txt");

        Assert.Equal([("a.txt", " D")], (await Backend.StatusAsync(root, ignored: false, Ct)).Select(each => (each.Key, each.Value)));
    }

    [Fact]
    public async Task A_revision_names_a_commit_by_its_branch_by_head_by_its_parent_and_a_name_that_is_none_names_nothing()
    {
        var (root, first) = await OneAsync();
        await WriteAsync(root, "a.txt", "two\n");
        Assert.True((await Backend.CommitAllAsync(root, "second", Ct)).Ok);
        var second = (await Backend.HeadAsync(root, Ct))!;

        Assert.Equal(second, await Backend.CommitOfAsync(root, "main", Ct));
        Assert.Equal(second, await Backend.CommitOfAsync(root, "refs/heads/main^{commit}", Ct));
        Assert.Equal(first, await Backend.CommitOfAsync(root, "HEAD^", Ct));
        Assert.Equal(first, await Backend.CommitOfAsync(root, first, Ct));
        Assert.Null(await Backend.CommitOfAsync(root, "nothing-by-this-name", Ct));
        Assert.Null(await Backend.CommitOfAsync(root, "-x", Ct));
    }

    [Fact]
    public async Task A_switch_makes_a_branch_goes_to_it_detaches_and_refuses_one_that_is_not_there()
    {
        var (root, first) = await OneAsync();

        Assert.True((await Backend.SwitchAsync(root, new GitSwitch("work", first, Creates: true), Ct)).Ok);
        Assert.Equal("work", await Backend.BranchAsync(root, Ct));
        Assert.False((await Backend.SwitchAsync(root, new GitSwitch("work", first, Creates: true), Ct)).Ok);
        Assert.True((await Backend.SwitchAsync(root, new GitSwitch("main"), Ct)).Ok);
        Assert.True((await Backend.SwitchAsync(root, new GitSwitch(null, first), Ct)).Ok);
        Assert.Null(await Backend.BranchAsync(root, Ct));
        Assert.Equal(first, await Backend.HeadAsync(root, Ct));
        Assert.False((await Backend.SwitchAsync(root, new GitSwitch("not-there"), Ct)).Ok);
        Assert.Null(await Backend.BranchAsync(root, Ct));
    }

    [Fact]
    public async Task A_switch_never_overwrites_work_and_keeps_what_is_untracked()
    {
        var (root, first) = await OneAsync();
        Assert.True((await Backend.SwitchAsync(root, new GitSwitch("work", first, Creates: true), Ct)).Ok);
        await WriteAsync(root, "a.txt", "changed on work\n");
        await WriteAsync(root, "only-on-work.txt", "x\n");
        Assert.True((await Backend.CommitAllAsync(root, "work", Ct)).Ok);
        Assert.True((await Backend.SwitchAsync(root, new GitSwitch("main"), Ct)).Ok);
        await WriteAsync(root, "scratch.txt", "mine\n");

        Assert.True((await Backend.SwitchAsync(root, new GitSwitch("work"), Ct)).Ok);
        Assert.Equal([("scratch.txt", "??")], (await Backend.StatusAsync(root, false, Ct)).Select(each => (each.Key, each.Value)));
        Assert.True((await Backend.SwitchAsync(root, new GitSwitch("main"), Ct)).Ok);
        await WriteAsync(root, "a.txt", "edited by hand\n");

        var refused = await Backend.SwitchAsync(root, new GitSwitch("work"), Ct);

        Assert.False(refused.Ok);
        Assert.False(string.IsNullOrWhiteSpace(refused.Error));
        Assert.Equal("main", await Backend.BranchAsync(root, Ct));
        Assert.Equal(" M", (await Backend.StatusAsync(root, false, Ct))["a.txt"]);
    }

    [Fact]
    public async Task History_answers_the_base_two_commits_share_what_reaches_what_and_how_many_each_has_beyond_the_other()
    {
        var (root, first) = await OneAsync();
        Assert.True((await Backend.SwitchAsync(root, new GitSwitch("work", first, Creates: true), Ct)).Ok);
        await WriteAsync(root, "w1.txt", "1\n");
        Assert.True((await Backend.CommitAllAsync(root, "w1", Ct)).Ok);
        await WriteAsync(root, "w2.txt", "2\n");
        Assert.True((await Backend.CommitAllAsync(root, "w2", Ct)).Ok);
        Assert.True((await Backend.SwitchAsync(root, new GitSwitch("main"), Ct)).Ok);
        await WriteAsync(root, "m1.txt", "1\n");
        Assert.True((await Backend.CommitAllAsync(root, "m1", Ct)).Ok);

        Assert.Equal(first, await Backend.MergeBaseAsync(root, "work", "main", Ct));
        Assert.Null(await Backend.MergeBaseAsync(root, "work", "nothing-by-this-name", Ct));
        Assert.True(await Backend.IsAncestorAsync(root, first, "work", Ct));
        Assert.False(await Backend.IsAncestorAsync(root, "work", first, Ct));
        Assert.False(await Backend.IsAncestorAsync(root, "work", "main", Ct));
        Assert.Null(await Backend.IsAncestorAsync(root, "nothing-by-this-name", "main", Ct));
        Assert.Equal(2, await Backend.CountAsync(root, "work", ["main"], Ct));
        Assert.Equal(1, await Backend.CountAsync(root, "main", ["work"], Ct));
        Assert.Equal(0, await Backend.CountAsync(root, first, ["work", "main"], Ct));
        Assert.Null(await Backend.CountAsync(root, "work", ["nothing-by-this-name"], Ct));
    }

    [Fact]
    public async Task Refs_are_listed_by_name_under_their_prefixes_and_by_the_commit_they_hold()
    {
        var (root, first) = await OneAsync();
        Assert.True((await Backend.SwitchAsync(root, new GitSwitch("daoris/s-1", first, Creates: true), Ct)).Ok);
        await WriteAsync(root, "w.txt", "1\n");
        Assert.True((await Backend.CommitAllAsync(root, "w", Ct)).Ok);
        var tip = (await Backend.HeadAsync(root, Ct))!;
        Assert.True((await Backend.SwitchAsync(root, new GitSwitch("main"), Ct)).Ok);

        var heads = await Backend.RefsAsync(root, ["refs/heads"], null, null, Ct);
        var own = await Backend.RefsAsync(root, ["refs/heads/daoris/"], null, null, Ct);
        var holding = await Backend.RefsAsync(root, ["refs/heads", "refs/tags"], tip, null, Ct);
        var one = await Backend.RefsAsync(root, ["refs/heads"], first, 1, Ct);

        Assert.Equal([("refs/heads/daoris/s-1", tip), ("refs/heads/main", first)], heads!.Select(each => (each.Name, each.Commit)));
        Assert.Equal([("refs/heads/daoris/s-1", tip)], own!.Select(each => (each.Name, each.Commit)));
        Assert.Equal([("refs/heads/daoris/s-1", tip)], holding!.Select(each => (each.Name, each.Commit)));
        Assert.Single(one!);
        Assert.Empty((await Backend.RefsAsync(root, ["refs/remotes"], null, null, Ct))!);
    }

    [Fact]
    public async Task A_ref_moves_where_it_stood_where_the_asker_said_and_no_other()
    {
        var (root, first) = await OneAsync();
        Assert.True((await Backend.SwitchAsync(root, new GitSwitch("work", first, Creates: true), Ct)).Ok);
        await WriteAsync(root, "w.txt", "1\n");
        Assert.True((await Backend.CommitAllAsync(root, "w", Ct)).Ok);
        var tip = (await Backend.HeadAsync(root, Ct))!;
        Assert.True((await Backend.SwitchAsync(root, new GitSwitch(null, first), Ct)).Ok);

        Assert.False((await Backend.UpdateRefAsync(root, "refs/heads/main", tip, tip, Ct)).Ok);
        Assert.Equal(first, await Backend.CommitOfAsync(root, "main", Ct));
        Assert.True((await Backend.UpdateRefAsync(root, "refs/heads/main", tip, first, Ct)).Ok);
        Assert.Equal(tip, await Backend.CommitOfAsync(root, "main", Ct));
        Assert.False((await Backend.UpdateRefAsync(root, "refs/heads/main", "nothing-by-this-name", null, Ct)).Ok);
    }

    [Fact]
    public async Task A_commits_tree_is_listed_whole_or_under_a_folder()
    {
        var (root, first) = await OneAsync();

        var whole = await Backend.EntriesAsync(root, first, [], true, Ct);
        var below = await Backend.EntriesAsync(root, first, ["src"], true, Ct);
        var top = await Backend.EntriesAsync(root, first, [], false, Ct);
        var inside = await Backend.EntriesAsync(root, first, ["src/"], false, Ct);
        var either = await Backend.EntriesAsync(root, first, ["a.txt", "src"], true, Ct);

        Assert.Equal([("100644", "a.txt"), ("100644", "src/b.txt")], whole!.Select(each => (each.Mode, each.Path)));
        Assert.Equal([("100644", "src/b.txt")], below!.Select(each => (each.Mode, each.Path)));
        Assert.Equal([("100644", "a.txt"), ("040000", "src")], top!.Select(each => (each.Mode, each.Path)));
        Assert.Equal([("100644", "src/b.txt")], inside!.Select(each => (each.Mode, each.Path)));
        Assert.Equal(whole!.Select(each => each.Path), either!.Select(each => each.Path));
        Assert.Null(await Backend.EntriesAsync(root, "nothing-by-this-name", [], true, Ct));
        Assert.Equal("two\n", await Backend.TextAtAsync(root, first, "src/b.txt", Ct));
        Assert.Null(await Backend.TextAtAsync(root, first, "src/none.txt", Ct));
    }

    [Fact]
    public async Task A_path_marked_unchanged_is_told()
    {
        var (root, _) = await OneAsync();
        Assert.Empty((await Backend.HiddenAsync(root, Ct))!);

        await HideAsync(root, "a.txt");

        Assert.Equal(["a.txt"], await Backend.HiddenAsync(root, Ct));
    }

    [Fact]
    public async Task Working_trees_are_added_locked_listed_unlocked_removed_and_pruned_once_gone()
    {
        var (root, first) = await OneAsync();
        var tree = Beside(root, "tree-1");
        var listed = (await Backend.WorktreesAsync(root, Ct))!;
        Assert.Equal([(root, "main", false)], listed.Select(each => (each.Path, each.Branch, each.Locked)));

        Assert.True((await Backend.AddWorktreeAsync(root, new GitWorktreeAdd(tree, first, LockReason: "daoris opening"), Ct)).Ok);
        Assert.False((await Backend.AddWorktreeAsync(root, new GitWorktreeAdd(tree, first), Ct)).Ok);

        var both = (await Backend.WorktreesAsync(tree, Ct))!;
        Assert.Equal([root, tree], both.Select(each => each.Path));
        Assert.Equal((null, true, "daoris opening"), (both[1].Branch, both[1].Locked, both[1].LockReason));
        Assert.Equal(first, await Backend.HeadAsync(tree, Ct));
        Assert.False((await Backend.LockAsync(root, tree, "again", Ct)).Ok);
        Assert.True((await Backend.UnlockAsync(root, tree, Ct)).Ok);
        Assert.False((await Backend.UnlockAsync(root, tree, Ct)).Ok);
        Assert.True((await Backend.LockAsync(root, tree, "daoris kept tree", Ct)).Ok);
        Assert.Equal((true, "daoris kept tree"), ((await Backend.WorktreesAsync(root, Ct))![1].Locked, (await Backend.WorktreesAsync(root, Ct))![1].LockReason));
        Assert.False((await Backend.LockAsync(root, root, "main", Ct)).Ok);

        await VanishAsync(tree);
        Assert.True((await Backend.PruneAsync(root, Ct)).Ok);
        Assert.Equal(2, (await Backend.WorktreesAsync(root, Ct))!.Count);
        Assert.True((await Backend.UnlockAsync(root, tree, Ct)).Ok);
        Assert.True((await Backend.PruneAsync(root, Ct)).Ok);
        Assert.Single((await Backend.WorktreesAsync(root, Ct))!);

        var again = Beside(root, "tree-2");
        Assert.True((await Backend.AddWorktreeAsync(root, new GitWorktreeAdd(again, first), Ct)).Ok);
        Assert.True((await Backend.RemoveWorktreeAsync(root, again, Ct)).Ok);
        Assert.Single((await Backend.WorktreesAsync(root, Ct))!);
    }

    [Fact]
    public async Task A_working_tree_is_added_on_a_new_branch_which_cannot_go_while_it_is_out_and_the_root_is_the_top_of_each_folder()
    {
        var (root, first) = await OneAsync();
        var tree = Beside(root, "tree-1");

        Assert.True((await Backend.AddWorktreeAsync(root, new GitWorktreeAdd(tree, first, Branch: "daoris/s-1", LockReason: "opening"), Ct)).Ok);

        Assert.Equal("daoris/s-1", await Backend.BranchAsync(tree, Ct));
        Assert.Equal(first, await Backend.CommitOfAsync(root, "refs/heads/daoris/s-1", Ct));
        Assert.Equal([(tree, "daoris/s-1", true)], (await Backend.WorktreesAsync(root, Ct))!.Skip(1).Select(each => (each.Path, each.Branch, each.Locked)));
        Assert.False((await Backend.AddWorktreeAsync(root, new GitWorktreeAdd(Beside(root, "tree-2"), first, Branch: "daoris/s-1"), Ct)).Ok);
        Assert.Equal(root, await Backend.TopOfAsync(root, Ct));
        Assert.Equal(tree, await Backend.TopOfAsync(tree, Ct));
        Assert.Equal(root, await Backend.TopOfAsync(Path.Combine(root, "src"), Ct));

        Assert.False((await Backend.DeleteBranchAsync(root, "daoris/s-1", Ct)).Ok);
        Assert.True((await Backend.RemoveWorktreeAsync(root, tree, Ct)).Ok);
        Assert.True((await Backend.DeleteBranchAsync(root, "daoris/s-1", Ct)).Ok);
        Assert.Null(await Backend.CommitOfAsync(root, "refs/heads/daoris/s-1", Ct));
        Assert.False((await Backend.DeleteBranchAsync(root, "daoris/s-1", Ct)).Ok);
    }

    [Fact]
    public async Task A_fast_forward_moves_the_branch_on_where_it_is_a_descendant_and_no_work_is_in_the_way_and_a_hard_reset_loses_tracked_changes_only()
    {
        var (root, first) = await OneAsync();
        Assert.True((await Backend.SwitchAsync(root, new GitSwitch("feature", Creates: true), Ct)).Ok);
        await WriteAsync(root, "a.txt", "feature\n");
        await WriteAsync(root, "extra.txt", "more\n");
        Assert.True((await Backend.CommitAllAsync(root, "feature", Ct)).Ok);
        var feature = (await Backend.HeadAsync(root, Ct))!;
        Assert.True((await Backend.SwitchAsync(root, new GitSwitch("main"), Ct)).Ok);

        await WriteAsync(root, "a.txt", "work\n");
        Assert.False((await Backend.FastForwardAsync(root, feature, Ct)).Ok);
        Assert.Equal(first, await Backend.HeadAsync(root, Ct));
        Assert.True((await Backend.ResetHardAsync(root, first, Ct)).Ok);
        Assert.Empty(await Backend.StatusAsync(root, false, Ct));

        await WriteAsync(root, "loose.txt", "x\n");
        Assert.True((await Backend.FastForwardAsync(root, feature, Ct)).Ok);
        Assert.Equal(feature, await Backend.HeadAsync(root, Ct));
        Assert.Equal("feature\n", await Backend.TextAtAsync(root, "HEAD", "a.txt", Ct));
        Assert.Equal([("loose.txt", "??")], (await Backend.StatusAsync(root, false, Ct)).Select(each => (each.Key, each.Value)));
        Assert.True((await Backend.FastForwardAsync(root, feature, Ct)).Ok);

        Assert.True((await Backend.ResetHardAsync(root, first, Ct)).Ok);
        Assert.Equal(first, await Backend.HeadAsync(root, Ct));
        Assert.Equal([("loose.txt", "??")], (await Backend.StatusAsync(root, false, Ct)).Select(each => (each.Key, each.Value)));
        await WriteAsync(root, "b.txt", "diverged\n");
        Assert.True((await Backend.CommitAllAsync(root, "diverge", Ct)).Ok);
        Assert.False((await Backend.FastForwardAsync(root, feature, Ct)).Ok);
        Assert.False((await Backend.ResetHardAsync(root, "refs/heads/none", Ct)).Ok);
    }

    [Fact]
    public async Task The_default_branch_is_main_where_it_stands_else_master_else_none()
    {
        var root = await NewAsync();
        Assert.Null(await Backend.DefaultBranchAsync(root, Ct));
        await WriteAsync(root, "a.txt", "one\n");
        Assert.True((await Backend.CommitAllAsync(root, "first", Ct)).Ok);
        Assert.Equal("main", await Backend.DefaultBranchAsync(root, Ct));

        Assert.True((await Backend.SwitchAsync(root, new GitSwitch("master", Creates: true), Ct)).Ok);
        Assert.True((await Backend.DeleteBranchAsync(root, "main", Ct)).Ok);
        Assert.Equal("master", await Backend.DefaultBranchAsync(root, Ct));

        Assert.True((await Backend.SwitchAsync(root, new GitSwitch("other", Creates: true), Ct)).Ok);
        Assert.True((await Backend.DeleteBranchAsync(root, "master", Ct)).Ok);
        Assert.Null(await Backend.DefaultBranchAsync(root, Ct));
    }

    [Fact]
    public async Task The_paths_two_commits_differ_in_are_named_in_path_order_and_a_commit_that_is_none_names_nothing()
    {
        var (root, first) = await OneAsync();
        await WriteAsync(root, "a.txt", "changed\n");
        await WriteAsync(root, "new/c.txt", "three\n");
        await RemoveAsync(root, "src/b.txt");
        Assert.True((await Backend.CommitAllAsync(root, "second", Ct)).Ok);
        var second = (await Backend.HeadAsync(root, Ct))!;

        Assert.Equal(["a.txt", "new/c.txt", "src/b.txt"], await Backend.ChangedAsync(root, first, second, Ct));
        Assert.Empty((await Backend.ChangedAsync(root, second, second, Ct))!);
        Assert.Null(await Backend.ChangedAsync(root, first, "0000000000000000000000000000000000000001", Ct));
    }

    [Fact]
    public async Task A_search_finds_each_line_holding_a_name_in_the_files_the_pathspecs_name_and_not_those_excluded()
    {
        var root = await NewAsync();
        await WriteAsync(root, "a.txt", "alpha\nbeta needle\n");
        await WriteAsync(root, "src/b.cs", "needle one\nnothing\nneedle two\n");
        await WriteAsync(root, "src/gen/c.cs", "needle generated\n");
        await WriteAsync(root, "doc/d.md", "needle doc\n");
        Assert.True((await Backend.CommitAllAsync(root, "all", Ct)).Ok);
        var head = (await Backend.HeadAsync(root, Ct))!;

        var all = await Backend.GrepAsync(root, head, ["needle"], ["a.txt", "src/", "doc/"], Ct);
        var code = await Backend.GrepAsync(root, head, ["needle"], ["*.cs", ":(exclude)src/gen/"], Ct);

        Assert.Equal([("a.txt", "beta needle"), ("doc/d.md", "needle doc"), ("src/b.cs", "needle one"), ("src/b.cs", "needle two"), ("src/gen/c.cs", "needle generated")], all.Select(each => (each.Path, each.Line)));
        Assert.Equal([("src/b.cs", "needle one"), ("src/b.cs", "needle two")], code.Select(each => (each.Path, each.Line)));
        Assert.Empty(await Backend.GrepAsync(root, head, ["absent"], ["src/"], Ct));
    }

    [Fact]
    public async Task A_ref_transaction_makes_every_act_or_none_and_each_act_holds_the_expectation_it_names()
    {
        var (root, first) = await OneAsync();
        await WriteAsync(root, "a.txt", "changed\n");
        Assert.True((await Backend.CommitAllAsync(root, "second", Ct)).Ok);
        var second = (await Backend.HeadAsync(root, Ct))!;
        GitRefTransaction Make(GitRefAct[] acts, params GitRefCheck[] checks) => new(acts, checks);

        Assert.True((await Backend.TransactAsync(root, Make([new(GitRefActKind.Create, "refs/heads/one", null, first), new(GitRefActKind.Create, "refs/heads/two", null, second)]), Ct)).Done);
        Assert.Equal((first, second), (await Backend.CommitOfAsync(root, "refs/heads/one", Ct), await Backend.CommitOfAsync(root, "refs/heads/two", Ct)));

        var refused = await Backend.TransactAsync(root, Make([new(GitRefActKind.Update, "refs/heads/one", second, first), new(GitRefActKind.Delete, "refs/heads/two", second, null)]), Ct);
        Assert.False(refused.Done);
        Assert.False(refused.Unanswered);
        Assert.Equal((first, second), (await Backend.CommitOfAsync(root, "refs/heads/one", Ct), await Backend.CommitOfAsync(root, "refs/heads/two", Ct)));

        Assert.False((await Backend.TransactAsync(root, Make([new(GitRefActKind.Create, "refs/heads/one", null, second)]), Ct)).Done);
        Assert.False((await Backend.TransactAsync(root, Make([new(GitRefActKind.Delete, "refs/heads/two", second, null)], new GitRefCheck("refs/heads/one", second)), Ct)).Done);
        Assert.Equal(second, await Backend.CommitOfAsync(root, "refs/heads/two", Ct));

        Assert.True((await Backend.TransactAsync(root, Make([new(GitRefActKind.Update, "refs/heads/one", first, second), new(GitRefActKind.Delete, "refs/heads/two", second, null)], new GitRefCheck("refs/heads/absent", null)), Ct)).Done);
        Assert.Equal(second, await Backend.CommitOfAsync(root, "refs/heads/one", Ct));
        Assert.Null(await Backend.CommitOfAsync(root, "refs/heads/two", Ct));
    }

    [Fact]
    public async Task Paths_are_put_back_as_head_holds_them_and_a_hook_that_is_absent_is_done()
    {
        var (root, _) = await OneAsync();
        await WriteAsync(root, "a.txt", "changed\n");
        await WriteAsync(root, "src/b.txt", "changed too\n");
        await WriteAsync(root, "loose.txt", "x\n");
        Assert.Equal(2, (await Backend.StatusAsync(root, false, Ct)).Count(each => each.Value == " M"));

        Assert.True((await Backend.RestoreAsync(root, ["a.txt", "src/b.txt"], Ct)).Ok);

        Assert.Equal([("loose.txt", "??")], (await Backend.StatusAsync(root, false, Ct)).Select(each => (each.Key, each.Value)));
        Assert.False((await Backend.RestoreAsync(root, ["missing.txt"], Ct)).Ok);
        Assert.True((await Backend.RunHookAsync(root, "post-checkout", ["0000000000000000000000000000000000000000", (await Backend.HeadAsync(root, Ct))!, "1"], Ct)).Ok);
    }

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
        var own = await Backend.RevListAsync(root, "HEAD", ["HEAD^1"], Ct);
        string[] expected = [(await Backend.HeadAsync(root, Ct))!, feature];
        Assert.Equal(expected.Order(StringComparer.Ordinal), own!.Order(StringComparer.Ordinal));
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

    private async Task<(string Root, string First, string Tree)> WithTreeAsync(string branch)
    {
        var (root, first) = await OneAsync();
        var tree = Beside(root, "tree");
        Assert.True((await Backend.AddWorktreeAsync(root, new GitWorktreeAdd(tree, first, branch), Ct)).Ok);
        return (root, first, tree);
    }

    [Fact]
    public async Task A_tree_counts_its_uncommitted_paths_with_an_untracked_folder_as_one_and_reads_its_head_and_branch()
    {
        var (root, first) = await OneAsync();
        Assert.Equal(0, await Backend.UncommittedAsync(root, Ct));
        Assert.Equal(new GitTreeStatus(0, first, "main"), await Backend.TreeStatusAsync(root, Ct));

        await WriteAsync(root, "a.txt", "changed\n");
        await WriteAsync(root, "new/one.txt", "1\n");
        await WriteAsync(root, "new/two.txt", "2\n");

        Assert.Equal(2, await Backend.UncommittedAsync(root, Ct));
        Assert.Equal(new GitTreeStatus(2, first, "main"), await Backend.TreeStatusAsync(root, Ct));
    }

    [Fact]
    public async Task A_place_names_the_common_directory_the_working_root_and_the_branch_and_a_detached_tree_names_head()
    {
        var (root, first, tree) = await WithTreeAsync("feature/x");

        var own = await Backend.PlaceAsync(tree, Ct);
        var main = await Backend.PlaceAsync(root, Ct);

        Assert.Equal(0, own.Code);
        Assert.Equal("feature/x", own.Branch.Trim());
        Assert.Equal(Path.GetFullPath(Path.Combine(root, ".git")), Path.GetFullPath(own.CommonDir.Trim()));
        Assert.Equal(Path.GetFullPath(tree), Path.GetFullPath(own.TopLevel.Trim()));
        Assert.Equal("main", main.Branch.Trim());

        Assert.True((await Backend.SwitchAsync(tree, new GitSwitch(null, first), Ct)).Ok);
        Assert.Equal("HEAD", (await Backend.PlaceAsync(tree, Ct)).Branch.Trim());
    }

    [Fact]
    public async Task A_layout_puts_a_linked_trees_git_directory_under_the_common_one_the_main_checkouts_in_it()
    {
        var (root, _, tree) = await WithTreeAsync("feature/x");

        var main = await Backend.LayoutAsync(root, Ct);
        var linked = await Backend.LayoutAsync(tree, Ct);

        Assert.Equal(Path.GetFullPath(Path.Combine(root, ".git")), main!.CommonDir);
        Assert.Equal(main.CommonDir, main.GitDir);
        Assert.Equal(main.CommonDir, linked!.CommonDir);
        Assert.StartsWith(Path.Combine(main.CommonDir, "worktrees") + Path.DirectorySeparatorChar, linked.GitDir, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_tree_is_removed_clean_and_refused_with_changes_or_a_lock_until_forced()
    {
        var (root, _, tree) = await WithTreeAsync("feature/x");
        await WriteAsync(tree, "loose.txt", "x\n");

        Assert.False((await Backend.RemoveTreeAsync(root, tree, force: false, Ct)).Ok);
        Assert.Contains(await Backend.WorktreesAsync(root, Ct) ?? [], each => each.Branch == "feature/x");

        await RemoveAsync(tree, "loose.txt");
        Assert.True((await Backend.LockAsync(root, tree, "held", Ct)).Ok);
        Assert.False((await Backend.RemoveTreeAsync(root, tree, force: false, Ct)).Ok);
        Assert.True((await Backend.UnlockAsync(root, tree, Ct)).Ok);

        Assert.True((await Backend.RemoveTreeAsync(root, tree, force: false, Ct)).Ok);
        Assert.DoesNotContain(await Backend.WorktreesAsync(root, Ct) ?? [], each => each.Branch == "feature/x");
    }

    [Fact]
    public async Task The_commits_no_branch_of_the_persons_holds_are_listed_newest_first_and_daoris_branches_hold_none()
    {
        var (root, first, tree) = await WithTreeAsync("daoris/s-1");
        await WriteAsync(tree, "one.txt", "1\n");
        Assert.True((await Backend.CommitAllAsync(tree, "one", Ct)).Ok);
        await WriteAsync(tree, "two.txt", "2\n");
        Assert.True((await Backend.CommitAllAsync(tree, "two", Ct)).Ok);
        var tip = (await Backend.HeadAsync(tree, Ct))!;

        var lines = (await Backend.UnlandedAsync(tree, "HEAD", null, Ct))!;
        Assert.Equal(2, lines.Count);
        Assert.EndsWith(" two", lines[0]);
        Assert.EndsWith(" one", lines[1]);
        Assert.Empty((await Backend.UnlandedAsync(root, first, null, Ct))!);

        Assert.True((await Backend.UpdateRefAsync(root, "refs/heads/main", tip, first, Ct)).Ok);
        Assert.Empty((await Backend.UnlandedAsync(tree, "HEAD", null, Ct))!);
        Assert.Null(await Backend.UnlandedAsync(tree, "refs/heads/not-there", null, Ct));
    }

    [Fact]
    public async Task A_status_lists_each_changed_path_with_its_state_and_each_untracked_file_by_itself()
    {
        var (root, _) = await OneAsync();
        await WriteAsync(root, "a.txt", "changed\n");
        await WriteAsync(root, "new/c.txt", "new\n");
        await RemoveAsync(root, "src/b.txt");

        var listed = await Backend.StatusEntriesAsync(root, Ct);

        Assert.Equal([("a.txt", " M"), ("new/c.txt", "??"), ("src/b.txt", " D")], listed.OrderBy(each => each.Path, StringComparer.Ordinal).Select(each => (each.Path, each.State)));
        Assert.All(listed, each => Assert.Null(each.From));
    }

    [Fact]
    public async Task Git_answers()
    {
        Assert.True(await Backend.AvailableAsync(Ct));
    }

    [Fact]
    public async Task An_init_makes_a_repository_on_the_line_with_no_commit()
    {
        var made = Beside(await NewAsync(), "made");

        Assert.True((await Backend.InitAsync(made, "trunk", Ct)).Ok);

        Assert.Equal("trunk", await Backend.BranchAsync(made, Ct));
        Assert.Null(await Backend.HeadAsync(made, Ct));
        Assert.Empty((await Backend.RefsAsync(made, ["refs"], null, null, Ct))!);
    }

    [Fact]
    public async Task An_empty_commit_is_one_commit_on_the_line_holding_no_file()
    {
        var made = Beside(await NewAsync(), "made");
        Assert.True((await Backend.InitAsync(made, "trunk", Ct)).Ok);

        Assert.True((await Backend.CommitEmptyAsync(made, "Fixture", "fixture@example.test", "begin", Ct)).Ok);

        var head = (await Backend.HeadAsync(made, Ct))!;
        Assert.Equal(1, await Backend.CountAsync(made, "HEAD", [], Ct));
        Assert.Empty((await Backend.EntriesAsync(made, head, [], true, Ct))!);
        Assert.Equal([head], (await Backend.RefsAsync(made, ["refs"], null, null, Ct))!.Select(each => each.Commit));
        Assert.Equal("refs/heads/trunk", Assert.Single(await Backend.RefsAsync(made, ["refs"], null, null, Ct) ?? []).Name);
    }
}
