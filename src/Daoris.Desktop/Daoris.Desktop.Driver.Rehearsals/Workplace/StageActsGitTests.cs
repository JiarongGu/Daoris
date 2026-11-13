using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Over real git: stage and unstage by file, hunk and line through the act journal,
/// and Undo. The case that needs the held blob is HEAD = A, index = B, worktree = A, where Unstage loses B and Undo brings it back exactly.
/// Stale bytes or a stale index are refused; so are the switch off, a conversation starting, a session in the checkout, an unreadable ledger,
/// a pull or a promotion holding the lock, an operation in progress and a session's tree.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class StageActsGitTests : TreeSyncFixture
{
    private static readonly string Original = string.Join('\n', Enumerable.Range(1, 30).Select(each => $"line {each}")) + "\n";

    private static readonly IReadOnlyList<TreeHolder> Nobody2 = [];

    private StageActs Acts() => new(new ActJournal(Home, () => StoreSettled.Yes, null, "run-a", StoreProcess.Shell));

    private static StageContext Context(string root, bool allowed = true, Func<CancellationToken, Task<IReadOnlyList<TreeHolder>>>? beside = null) =>
        new("engine", root, () => allowed, beside ?? (_ => Task.FromResult(Nobody2)));

    /// <summary>A checkout whose `lines.txt` is committed as A.</summary>
    private async Task<string> CheckoutAsync()
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, "lines.txt", Original, "lines");
        return root;
    }

    private static string Edited(params (int Line, string Text)[] changes)
    {
        var lines = Original.TrimEnd('\n').Split('\n');
        foreach (var (line, text) in changes) lines[line - 1] = text;
        return string.Join('\n', lines) + "\n";
    }

    private static Task WriteAsync(string root, string file, string text) => File.WriteAllTextAsync(Path.Combine(root, file), text);

    private async Task<string> IndexBlobAsync(string root, string file) => (await GitAsync(root, "rev-parse", ":" + file)).Trim();

    /// <summary>The press for a file's side as the page would send it from the read: every hunk, or the chosen ones.</summary>
    private static async Task<StagePress> PressAsync(string root, string verb, string file, int[]? hunks = null, int[][]? lines = null)
    {
        var side = verb == StageVerbs.Stage ? GitHunks.Unstaged : GitHunks.Staged;
        var read = (await GitHunks.ReadAsync(root, file, side)).Value!;
        var picks = hunks is null ? [] : hunks.Select((at, n) => new HunkPick(read.Hunks[at].Id, lines?[n])).ToList();
        return new StagePress(verb, file, read.Head, read.Index, read.Source, picks);
    }

    [Fact]
    public async Task A_hunk_is_staged_and_only_that_hunk()
    {
        var root = await CheckoutAsync();
        await WriteAsync(root, "lines.txt", Edited((2, "two"), (28, "twenty-eight")));
        var press = await PressAsync(root, StageVerbs.Stage, "lines.txt", [1]);

        var run = await Acts().RunAsync(Context(root), press, CancellationToken.None);

        Assert.False(run.Refused, run.Said);
        Assert.Equal(ActOutcomes.Done, run.Outcome);
        Assert.Equal(Edited((28, "twenty-eight")), await GitAsync(root, "show", ":lines.txt"));
        Assert.Equal(Edited((2, "two"), (28, "twenty-eight")), await File.ReadAllTextAsync(Path.Combine(root, "lines.txt")));
    }

    [Fact]
    public async Task A_line_of_a_hunk_is_staged_and_the_lines_left_stay_unstaged()
    {
        var root = await CheckoutAsync();
        await WriteAsync(root, "lines.txt", Edited((2, "two"), (4, "four")));
        var read = (await GitHunks.ReadAsync(root, "lines.txt", GitHunks.Unstaged)).Value!;
        var hunk = Assert.Single(read.Hunks);
        // One hunk holds both changes: choose the removal and the addition of line 2 and leave line 4's.
        var minus2 = hunk.Lines.ToList().FindIndex(line => line is { Kind: '-', Text: "line 2" });
        var plus2 = hunk.Lines.ToList().FindIndex(line => line is { Kind: '+', Text: "two" });
        var press = new StagePress(StageVerbs.Stage, "lines.txt", read.Head, read.Index, read.Source, [new HunkPick(hunk.Id, [minus2, plus2])]);

        var run = await Acts().RunAsync(Context(root), press, CancellationToken.None);

        Assert.False(run.Refused, run.Said);
        Assert.Equal(Edited((2, "two")), await GitAsync(root, "show", ":lines.txt"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HEAD_A_index_B_worktree_A_is_undone_exactly(bool byHunk)
    {
        var root = await CheckoutAsync();
        var a = await IndexBlobAsync(root, "lines.txt");
        var acts = Acts();
        await WriteAsync(root, "lines.txt", Edited((5, "five")));
        var staged = await acts.RunAsync(Context(root), await PressAsync(root, StageVerbs.Stage, "lines.txt", byHunk ? [0] : null), CancellationToken.None);
        Assert.False(staged.Refused, staged.Said);
        var b = await IndexBlobAsync(root, "lines.txt");
        Assert.NotEqual(a, b);

        // The person puts the file back: HEAD = A, index = B, worktree = A. Unstage drops B from the index, the only place that held it.
        await WriteAsync(root, "lines.txt", Original);
        var unstaged = await acts.RunAsync(Context(root), await PressAsync(root, StageVerbs.Unstage, "lines.txt", byHunk ? [0] : null), CancellationToken.None);
        Assert.False(unstaged.Refused, unstaged.Said);
        Assert.Equal(a, await IndexBlobAsync(root, "lines.txt"));
        Assert.Matches("^[0-9a-f]{40,64}$", (await GitAsync(root, "rev-parse", ActRefs.Of(unstaged.Act!))).Trim());

        var undone = await acts.UndoAsync(Context(root), unstaged.Act!, CancellationToken.None);

        Assert.False(undone.Refused, undone.Said);
        Assert.Equal(ActOutcomes.Done, undone.Outcome);
        Assert.Equal(b, await IndexBlobAsync(root, "lines.txt"));
        Assert.Equal(Edited((5, "five")), await GitAsync(root, "show", ":lines.txt"));
        Assert.Equal(Original, await File.ReadAllTextAsync(Path.Combine(root, "lines.txt")));
    }

    [Fact]
    public async Task An_untracked_file_is_staged_and_a_new_file_unstaged_leaves_it_untracked()
    {
        var root = await CheckoutAsync();
        await WriteAsync(root, "new.txt", "new\n");
        var acts = Acts();

        var staged = await acts.RunAsync(Context(root), await PressAsync(root, StageVerbs.Stage, "new.txt"), CancellationToken.None);
        Assert.False(staged.Refused, staged.Said);
        Assert.Equal("new\n", await GitAsync(root, "show", ":new.txt"));

        var press = await PressAsync(root, StageVerbs.Unstage, "new.txt");
        var unstaged = await acts.RunAsync(Context(root), press, CancellationToken.None);
        Assert.False(unstaged.Refused, unstaged.Said);
        Assert.Contains("?? new.txt", await GitAsync(root, "status", "--porcelain"));
    }

    private static async Task<string> Head(string root) => (await GitAsync(root, "rev-parse", "HEAD")).Trim();

    [Fact]
    public async Task Stale_bytes_are_refused_and_the_index_is_left()
    {
        var root = await CheckoutAsync();
        await WriteAsync(root, "lines.txt", Edited((2, "two")));
        var press = await PressAsync(root, StageVerbs.Stage, "lines.txt", [0]);
        await WriteAsync(root, "lines.txt", Edited((2, "deux")));

        var run = await Acts().RunAsync(Context(root), press, CancellationToken.None);

        Assert.True(run.Refused);
        Assert.Contains("bytes changed", run.Said);
        Assert.Equal(Original, await GitAsync(root, "show", ":lines.txt"));
    }

    [Fact]
    public async Task A_stale_index_entry_is_refused()
    {
        var root = await CheckoutAsync();
        await WriteAsync(root, "lines.txt", Edited((2, "two")));
        var press = await PressAsync(root, StageVerbs.Stage, "lines.txt", [0]);
        await GitAsync(root, "add", "lines.txt");

        var run = await Acts().RunAsync(Context(root), press, CancellationToken.None);

        Assert.True(run.Refused);
        Assert.Contains("index's entry", run.Said);
    }

    [Fact]
    public async Task A_moved_head_is_refused()
    {
        var root = await CheckoutAsync();
        await WriteAsync(root, "lines.txt", Edited((2, "two")));
        var press = await PressAsync(root, StageVerbs.Stage, "lines.txt", [0]);
        await CommitAsync(root, "other.txt", "x\n", "moved");

        var run = await Acts().RunAsync(Context(root), press, CancellationToken.None);

        Assert.True(run.Refused);
        Assert.Contains("HEAD moved", run.Said);
    }

    [Fact]
    public async Task The_switch_off_refuses_and_changes_nothing()
    {
        var root = await CheckoutAsync();
        await WriteAsync(root, "lines.txt", Edited((2, "two")));
        var press = await PressAsync(root, StageVerbs.Stage, "lines.txt");

        var run = await Acts().RunAsync(Context(root, allowed: false), press, CancellationToken.None);

        Assert.True(run.Refused);
        Assert.Contains("Daoris may change this checkout's files", run.Said);
        Assert.Equal(Original, await GitAsync(root, "show", ":lines.txt"));
    }

    [Fact]
    public async Task A_conversation_starting_in_the_checkout_refuses()
    {
        var root = await CheckoutAsync();
        await WriteAsync(root, "lines.txt", Edited((2, "two")));
        var press = await PressAsync(root, StageVerbs.Stage, "lines.txt");

        using var starting = TreeLock.TryStarting(Home, root, StartIn.Fresh);
        var run = await Acts().RunAsync(Context(root), press, CancellationToken.None);

        Assert.NotNull(starting);
        Assert.True(run.Refused);
        Assert.Equal(Original, await GitAsync(root, "show", ":lines.txt"));
    }

    [Theory]
    [InlineData(HeldBy.Updating)]
    [InlineData(HeldBy.Promoting)]
    public async Task A_pull_or_a_promotion_holding_the_lock_refuses(string by)
    {
        var root = await CheckoutAsync();
        await WriteAsync(root, "lines.txt", Edited((2, "two")));
        var press = await PressAsync(root, StageVerbs.Stage, "lines.txt");

        // The pull's or the promotion's hold, taken on a flow of its own: a flow that holds the checkout alone runs its own acts under its hold.
        using var holding = await Task.Run(() => TreeLock.TryWriting(Home, root, "engine", by));
        var run = await Acts().RunAsync(Context(root), press, CancellationToken.None);

        Assert.NotNull(holding);
        Assert.True(run.Refused);
        Assert.Equal(Original, await GitAsync(root, "show", ":lines.txt"));
    }

    [Theory]
    [InlineData("s-running")]
    [InlineData("s-waiting-for-words")]
    public async Task A_session_the_ledger_holds_in_the_checkout_refuses_trees_off(string session)
    {
        var root = await CheckoutAsync();
        await WriteAsync(root, "lines.txt", Edited((2, "two")));
        var press = await PressAsync(root, StageVerbs.Stage, "lines.txt");

        var run = await Acts().RunAsync(Context(root, beside: _ => Task.FromResult<IReadOnlyList<TreeHolder>>([new TreeHolder(root, session) { Leases = true, Runs = true }])), press, CancellationToken.None);

        Assert.True(run.Refused);
        Assert.Contains($"`{session}`", run.Said);
        Assert.Equal(Original, await GitAsync(root, "show", ":lines.txt"));
    }

    [Fact]
    public async Task An_unreadable_ledger_refuses()
    {
        var root = await CheckoutAsync();
        await WriteAsync(root, "lines.txt", Edited((2, "two")));
        var press = await PressAsync(root, StageVerbs.Stage, "lines.txt");

        var unread = await Acts().RunAsync(Context(root, beside: _ => throw new IOException("the service did not answer")), press, CancellationToken.None);
        var none = await Acts().RunAsync(new StageContext("engine", root, () => true, null), press, CancellationToken.None);

        Assert.True(unread.Refused);
        Assert.Contains("ledger could not be read", unread.Said);
        Assert.True(none.Refused);
        Assert.Contains("ledger of sessions was not read", none.Said);
        Assert.Equal(Original, await GitAsync(root, "show", ":lines.txt"));
    }

    [Fact]
    public async Task An_operation_in_progress_and_git_s_own_lock_refuse()
    {
        var root = await CheckoutAsync();
        await WriteAsync(root, "lines.txt", Edited((2, "two")));
        var press = await PressAsync(root, StageVerbs.Stage, "lines.txt");
        var merge = Path.Combine(root, ".git", "MERGE_HEAD");
        await File.WriteAllTextAsync(merge, await Head(root) + "\n");

        var inOperation = await Acts().RunAsync(Context(root), press, CancellationToken.None);
        File.Delete(merge);
        var held = Path.Combine(root, ".git", "index.lock");
        await File.WriteAllTextAsync(held, "");
        var locked = await Acts().RunAsync(Context(root), press, CancellationToken.None);

        Assert.True(inOperation.Refused);
        Assert.Contains("MERGE_HEAD", inOperation.Said);
        Assert.True(locked.Refused);
        Assert.Contains("index.lock", locked.Said);
        Assert.True(File.Exists(held), "Daoris never removes git's lock");
        Assert.Equal(Original, await GitAsync(root, "show", ":lines.txt"));
    }

    [Fact]
    public async Task A_session_s_tree_is_refused_whatever_the_switch_says()
    {
        var root = await CheckoutAsync();
        var tree = Path.Combine(Path.GetDirectoryName(root)!, "engine-tree");
        await GitAsync(root, "worktree", "add", "-b", "tree-branch", tree);
        await WriteAsync(tree, "lines.txt", Edited((2, "two")));
        var press = await PressAsync(tree, StageVerbs.Stage, "lines.txt");

        var run = await Acts().RunAsync(Context(tree), press, CancellationToken.None);

        Assert.True(run.Refused);
        Assert.Contains("session's tree", run.Said);
    }

    [Fact]
    public async Task Undo_names_an_entry_that_changed_since_and_restores_none()
    {
        var root = await CheckoutAsync();
        var acts = Acts();
        await WriteAsync(root, "lines.txt", Edited((5, "five")));
        var staged = await acts.RunAsync(Context(root), await PressAsync(root, StageVerbs.Stage, "lines.txt"), CancellationToken.None);
        Assert.False(staged.Refused, staged.Said);
        await WriteAsync(root, "lines.txt", Edited((5, "five"), (6, "six")));
        await GitAsync(root, "add", "lines.txt");
        var now = await IndexBlobAsync(root, "lines.txt");

        var undone = await acts.UndoAsync(Context(root), staged.Act!, CancellationToken.None);

        Assert.True(undone.Refused);
        Assert.Contains("`lines.txt` changed since act", undone.Said);
        Assert.Equal(now, await IndexBlobAsync(root, "lines.txt"));
    }

    [Fact]
    public async Task Undo_is_refused_with_the_switch_off_and_for_an_act_that_is_no_stage()
    {
        var root = await CheckoutAsync();
        var acts = Acts();
        await WriteAsync(root, "lines.txt", Edited((5, "five")));
        var staged = await acts.RunAsync(Context(root), await PressAsync(root, StageVerbs.Stage, "lines.txt"), CancellationToken.None);

        var off = await acts.UndoAsync(Context(root, allowed: false), staged.Act!, CancellationToken.None);
        var unknown = await acts.UndoAsync(Context(root), "no-such-act", CancellationToken.None);

        Assert.True(off.Refused);
        Assert.True(unknown.Refused);
        Assert.Contains("holds no act", unknown.Said);
    }
}
