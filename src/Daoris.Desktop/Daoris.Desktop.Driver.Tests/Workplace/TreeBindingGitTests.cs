using Daoris.Driver;
using Xunit.Abstractions;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// 🔴 GITWORKTREECONFIG1 (D51's note): a session's tree git binds to another checkout is never removed, reset or written through.
/// With worktree-specific configuration on (<c>extensions.worktreeConfig</c>), a <c>core.worktree</c> in the repository's shared
/// configuration applies to every linked tree, so git reports the main checkout as each one's working root (the 2026-10-27 incident).
/// Each door that writes through a session's tree is driven here over such a tree: it refuses with the sentence that names what binds it
/// and what to do, and the checkout's files, index and HEAD, the session's branch and the configuration are as they were. The same value
/// in the checkout's own worktree configuration binds nothing, and the tree goes.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TreeBindingGitTests(ITestOutputHelper output) : BranchTidyingFixture
{
    private static readonly Func<CancellationToken, Task<IReadOnlySet<string>>> NoneRunning =
        _ => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());

    /// <summary>The person's removal, forced: <c>--force</c> discards the tree's work, never what git would reach through it.</summary>
    [Fact]
    public async Task A_forced_removal_of_a_bound_tree_is_refused_and_changes_nothing()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await TreeWithWorkAsync(trees, root);
        await BindAsync(root, tree.Path);
        var before = await CheckoutAsync(root, tree.Branch);

        var removal = await trees.RemoveAsync(tree.Path, force: true);

        Assert.False(removal.Removed, removal.Message);
        Refused(removal.Message, tree.Path);
        Assert.StartsWith($"the tree at {tree.Path} stays: ", removal.Message, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(tree.Path, ".git")));
        Assert.Equal(before, await CheckoutAsync(root, tree.Branch));
    }

    /// <summary>
    /// The person's press of the clean-up (D88) over a landed tree: its row is judged kept, what git says its tree holds being the
    /// checkout's, and says why, before any ref is kept or anything goes.
    /// </summary>
    [Fact]
    public async Task The_clean_ups_press_keeps_a_bound_tree_and_changes_nothing()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await LandedTreeAsync(trees, root);
        await BindAsync(root, tree.Path);
        var before = await CheckoutAsync(root, tree.Branch);

        var pressed = await trees.SweepAsync([("engine", "default", root)], new HashSet<string>());

        var result = pressed.Single(each => each.Item.Branch == tree.Branch);
        Assert.False(result.Removed, result.Message);
        Assert.True(result.Item.Unread, result.Item.Detail);
        Refused(result.Item.Detail ?? "", tree.Path);
        Assert.True(File.Exists(Path.Combine(tree.Path, ".git")));
        Assert.Equal(before, await CheckoutAsync(root, tree.Branch));
        Assert.Empty(await GitAsync(root, "for-each-ref", "refs/daoris/"));
    }

    /// <summary>The press of Accept on the merge rule: nothing is merged, judged or written from a tree git reads as the checkout.</summary>
    [Fact]
    public async Task A_landing_from_a_bound_tree_is_refused_and_changes_nothing()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await TreeWithWorkAsync(trees, root);
        await BindAsync(root, tree.Path);
        var before = await CheckoutAsync(root, tree.Branch);

        var landed = await trees.LandAsync(tree.Path, new LandingSubject("s1", "0fda18", "Fix the gap"), inUse: NoneRunning);

        Assert.False(landed.Landed, landed.Message);
        Refused(landed.Message, tree.Path);
        Assert.Equal(before, await CheckoutAsync(root, tree.Branch));
    }

    /// <summary>Bring up to date (REVIEWBASE2-i) where the line moved: no switch, rebase or reset runs in a tree git reads as the checkout.</summary>
    [Fact]
    public async Task Bringing_a_bound_tree_up_to_date_is_refused_and_changes_nothing()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await TreeWithWorkAsync(trees, root);
        await CommitAsync(root, "line.txt", "the line moved\n", "the line moved");
        await BindAsync(root, tree.Path);
        var before = await CheckoutAsync(root, tree.Branch);

        var update = await trees.UpdateTreeAsync(tree.Path, _ => Task.FromResult(false));

        Assert.False(update.Done, update.Message);
        Refused(update.Message, tree.Path);
        Assert.Equal(before, await CheckoutAsync(root, tree.Branch));
    }

    /// <summary>A resume in a tree that stands (TREEGONE1): no session goes on where what its agent writes, git writes in the checkout.</summary>
    [Fact]
    public async Task A_start_does_not_go_on_in_a_bound_tree()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await TreeWithWorkAsync(trees, root);
        await BindAsync(root, tree.Path);
        var before = await CheckoutAsync(root, tree.Branch);

        var standing = await trees.EnsureAsync(root, tree.Path);

        Assert.Equal(TreeStand.Unmade, standing.Stand);
        Assert.StartsWith("It does not start: ", standing.Said!.Note, StringComparison.Ordinal);
        Refused(standing.Said.Note, tree.Path);
        Assert.Equal(before, await CheckoutAsync(root, tree.Branch));
    }

    /// <summary>An open undone (OPENCUT1) where the tree it made is bound: the tree and its branch are left, for the look to say.</summary>
    [Fact]
    public async Task An_undone_opening_leaves_a_bound_tree_and_its_branch()
    {
        var root = await RepositoryAsync("engine");
        var tree = await new SessionTrees(_home).OpenAsync(root, "engine", "default");
        await BindAsync(root, tree.Path);
        var before = await CheckoutAsync(root, tree.Branch);

        await TreeOpenings.UndoAsync(root, tree.Path, tree.Branch, ours: true);

        Assert.True(File.Exists(Path.Combine(tree.Path, ".git")));
        Assert.Equal(before, await CheckoutAsync(root, tree.Branch));
    }

    /// <summary>
    /// An opening under the shared configuration (the last door of GITWORKTREECONFIG1): the tree it just made is let go of through the
    /// opening's own undo, no folder and no branch remain, the checkout is as it was, and the refusal says what binds it.
    /// </summary>
    [Fact]
    public async Task An_opening_under_a_shared_core_worktree_is_refused_and_leaves_nothing()
    {
        var root = await RepositoryAsync("engine");
        await GitAsync(root, "config", "extensions.worktreeConfig", "true");
        await GitAsync(root, "config", "core.worktree", root.Replace('\\', '/'));
        var trees = new SessionTrees(_home);
        var before = await CheckoutAsync(root, null);
        var branches =await GitAsync(root, "branch", "--list");

        var error = await Assert.ThrowsAsync<DriverException>(() => trees.OpenAsync(root, "engine", "default"));

        Assert.Contains($"as the working root of the tree at ", error.Message, StringComparison.Ordinal);
        Assert.Contains("`core.worktree", error.Message, StringComparison.Ordinal);
        Assert.Contains("/.git/config", error.Message.Replace('\\', '/'), StringComparison.Ordinal);
        Assert.Contains("own worktree configuration, or unset it, then ask again", error.Message, StringComparison.Ordinal);
        Assert.Equal(before, await CheckoutAsync(root, null));
        Assert.Equal(branches, await GitAsync(root, "branch", "--list"));
        Assert.Single((await GitAsync(root, "worktree", "list", "--porcelain")).Split('\n'), line => line.StartsWith("worktree ", StringComparison.Ordinal));
        var made = Path.Combine(trees.TreesRoot, "default", "engine");
        Assert.False(Directory.Exists(made) && Directory.EnumerateDirectories(made).Any());
    }

    /// <summary>The same value in the checkout's own worktree configuration binds no linked tree: the opening goes as before.</summary>
    [Fact]
    public async Task An_opening_under_the_valid_configuration_opens()
    {
        var root = await RepositoryAsync("engine");
        await GitAsync(root, "config", "extensions.worktreeConfig", "true");
        await GitAsync(root, "config", "--worktree", "core.worktree", root.Replace('\\', '/'));

        var tree = await new SessionTrees(_home).OpenAsync(root, "engine", "default");

        Assert.True(File.Exists(Path.Combine(tree.Path, ".git")));
    }

    /// <summary>
    /// What a replay's own tree rests on (<see cref="BranchReplays"/>): git's own removal of a tree it binds elsewhere names the tree by its
    /// record and deletes that folder alone, so Daoris's tree made a moment ago for a replay is let go of so, unused. A session's tree
    /// still stays: what Daoris reads of it before a removal is the checkout's.
    /// </summary>
    [Fact]
    public async Task Gits_own_removal_of_a_bound_tree_takes_that_folder_alone()
    {
        var root = await RepositoryAsync("engine");
        var aside = Path.Combine(_scratch, "r-0aside00");
        await GitAsync(root, "worktree", "add", "--quiet", "--detach", aside, "main");
        await BindAsync(root, aside);
        var before = await CheckoutAsync(root, null);

        var removed = await RunAsync(root, "worktree", "remove", "--force", "--force", aside);

        Assert.True(removed.ExitCode == 0, removed.Stderr);
        Assert.False(Directory.Exists(aside));
        Assert.Equal(before, await CheckoutAsync(root, null));
    }

    /// <summary>
    /// The configuration that restored the trees on 2026-10-27: the same <c>core.worktree</c> in the checkout's own worktree configuration
    /// binds no linked tree, so nothing is refused, and a forced removal takes the tree and its branch as before.
    /// </summary>
    [Fact]
    public async Task The_same_value_in_the_checkouts_own_worktree_configuration_binds_nothing()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await TreeWithWorkAsync(trees, root);
        await GitAsync(root, "config", "extensions.worktreeConfig", "true");
        await GitAsync(root, "config", "--worktree", "core.worktree", root.Replace('\\', '/'));
        var answer = (await GitAsync(tree.Path, "rev-parse", "--show-toplevel")).Trim();
        output.WriteLine($"{(await GitAsync(root, "--version")).Trim()}: the tree's working root, the value in the checkout's own config.worktree: {answer}");
        Assert.True(TreeGit.SamePath(answer, tree.Path), $"git answered {answer} for the tree at {tree.Path}");
        var before = await CheckoutAsync(root, null);

        var removal = await trees.RemoveAsync(tree.Path, force: true);

        Assert.True(removal.Removed, removal.Message);
        Assert.False(Directory.Exists(tree.Path));
        Assert.Equal("", (await GitAsync(root, "branch", "--list", tree.Branch)).Trim());
        Assert.Equal(before, await CheckoutAsync(root, null));
    }

    /// <summary>
    /// The incident's configuration, written as something other than Daoris wrote it: worktree-specific configuration on, and the
    /// checkout's root as <c>core.worktree</c> in the shared configuration. What git then answers for the tree is printed, and must be the
    /// checkout's root, or the class proves nothing.
    /// </summary>
    private async Task BindAsync(string root, string tree)
    {
        await GitAsync(root, "config", "extensions.worktreeConfig", "true");
        await GitAsync(root, "config", "core.worktree", root.Replace('\\', '/'));
        var answer = (await GitAsync(tree, "rev-parse", "--show-toplevel")).Trim();
        output.WriteLine($"{(await GitAsync(root, "--version")).Trim()}: the tree's working root, a shared core.worktree: {answer}");
        Assert.True(TreeGit.SamePath(answer, root), $"git answered {answer} for the tree at {tree}, not the checkout {root}");
    }

    /// <summary>The refusal's sentence: what git reports, what binds it there and where, that nothing was done, and what to do.</summary>
    private static void Refused(string message, string tree)
    {
        Assert.Contains($"as the working root of the tree at {Path.GetFullPath(tree)}", message, StringComparison.Ordinal);
        Assert.Contains("`core.worktree", message, StringComparison.Ordinal);
        Assert.Contains("/.git/config", message.Replace('\\', '/'), StringComparison.Ordinal);
        Assert.Contains("set by something other than Daoris", message, StringComparison.Ordinal);
        Assert.Contains("nothing was removed, reset or written through it", message, StringComparison.Ordinal);
        Assert.Contains("own worktree configuration, or unset it, then ask again", message, StringComparison.Ordinal);
    }

    /// <summary>
    /// What a write through the tree would change of the checkout: its files and what they hold, its index, its status, its HEAD, the
    /// session's branch (where <paramref name="branch"/> is named) and the <c>core.worktree</c> git reads there.
    /// </summary>
    private static async Task<string> CheckoutAsync(string root, string? branch)
    {
        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/'))
            .Where(file => !file.StartsWith(".git/", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(file => $"{file} = {File.ReadAllText(Path.Combine(root, file))}")
            .ToList();
        files.Add("index: " + await GitAsync(root, "ls-files", "--stage"));
        files.Add("status: " + await GitAsync(root, "status", "--porcelain", "--ignored"));
        files.Add("HEAD: " + await GitAsync(root, "rev-parse", "HEAD") + await GitAsync(root, "symbolic-ref", "-q", "HEAD"));
        if (branch is not null) files.Add("branch: " + await GitAsync(root, "rev-parse", "--verify", "--quiet", $"refs/heads/{branch}"));
        files.Add("core.worktree: " + await GitAsync(root, "config", "--get", "core.worktree"));
        return string.Join('\n', files);
    }
}
