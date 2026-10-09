using System.Text.Json;
using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AUTOTIDY1 (D88's note) over real git: the look removes a session branch that holds nothing beyond the line, with its tree,
/// by the clean-up's own path, and keeps every other branch exactly as the press finds it. The sessions are the stand-in
/// ledger's, read through the real client.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class BranchTidyingProcessTests : IDisposable
{
    // A look judges each branch the line holds with several git calls, twice: a dozen took over a minute while other worktrees
    // built (FLAKE1), and 22 seconds alone.
    private static readonly TimeSpan Bound = TimeSpan.FromMinutes(4);

    private readonly string _scratch;
    private readonly string _home;
    private readonly StandInLedger _ledger = new();
    private readonly CancellationTokenSource _closing = new(TimeSpan.FromMinutes(10));
    private DateTimeOffset _now = new(2026, 10, 9, 9, 0, 0, TimeSpan.Zero);

    public BranchTidyingProcessTests()
    {
        _scratch = Path.Combine(RepoRoot(), "_fixtures", "autotidy", Guid.NewGuid().ToString("N")[..8]);
        _home = Path.Combine(_scratch, "daoris-home");
        Directory.CreateDirectory(_home);
    }

    public void Dispose()
    {
        _closing.Cancel();
        _closing.Dispose();
        _ledger.Dispose();
        try
        {
            Directory.Delete(_scratch, recursive: true);
        }
        catch (IOException) { /* a straggling git handle */ }
        catch (UnauthorizedAccessException) { /* read-only pack files under .git */ }
    }

    /// <summary>
    /// 🔴 The row's own case: a session branch whose work a merge took onto the line goes with its tree, and a branch whose tree
    /// was removed by hand goes too, each said once in the report and the machine log. Every other branch stays, as the press
    /// finds it, and says nothing: commits beyond the line, on a branch of the person's, or on the line by content after a
    /// squash; a tree whose branch never moved, which words to its session go on in; and, each on a branch the line took, an
    /// uncommitted change, an untracked file, an ignored file only the tree holds, a build's output the checkout holds too, a
    /// session working in its tree, and one waiting on the person.
    /// </summary>
    [Fact]
    public async Task The_look_removes_an_empty_branch_and_keeps_every_other()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);

        var merged = await LandedTreeAsync(trees, root);
        await GitAsync(root, "branch", "daoris/s-treeless");

        var beyond = await TreeWithWorkAsync(trees, root);
        var featured = await TreeWithWorkAsync(trees, root);
        await GitAsync(root, "branch", "feature/x", featured.Branch);
        var squashed = await TreeWithWorkAsync(trees, root);
        await GitAsync(root, "merge", "--squash", squashed.Branch);
        await GitAsync(root, "commit", "--quiet", "-m", "the work, squashed");
        var unmoved = await trees.OpenAsync(root, "engine", "default");
        var changed = await LandedTreeAsync(trees, root);
        await File.WriteAllTextAsync(Path.Combine(changed.Path, "README.md"), "# engine, edited\n");
        var untracked = await LandedTreeAsync(trees, root);
        await File.WriteAllTextAsync(Path.Combine(untracked.Path, "loose.txt"), "not committed\n");
        var ignored = await LandedTreeAsync(trees, root);
        await File.WriteAllTextAsync(Path.Combine(ignored.Path, "local.db"), "made here alone\n");
        var built = await LandedTreeAsync(trees, root);
        Directory.CreateDirectory(Path.Combine(built.Path, "build"));
        await File.WriteAllTextAsync(Path.Combine(built.Path, "build", "out.txt"), "a build's output\n");
        var running = await LandedTreeAsync(trees, root);
        var waiting = await LandedTreeAsync(trees, root);

        _ledger.Register("engine", root);
        _ledger.Working("s-running", "engine", running.Path);
        _ledger.Publish("q1", "engine");
        _ledger.Park("s-waiting", "q1", waiting.Path);
        using var log = Log();
        var tidying = new BranchTidying(_home);

        var said = await tidying.LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);

        var branches = await GitAsync(root, "branch", "--list", "daoris/*");
        Assert.DoesNotContain(merged.Branch, branches, StringComparison.Ordinal);
        Assert.DoesNotContain("daoris/s-treeless", branches, StringComparison.Ordinal);
        Assert.False(Directory.Exists(merged.Path));
        foreach (var kept in new[] { beyond, featured, squashed, unmoved, changed, untracked, ignored, built, running, waiting })
        {
            Assert.Contains(kept.Branch, branches, StringComparison.Ordinal);
            Assert.True(Directory.Exists(kept.Path), kept.Path);
        }

        Assert.Equal(2, said.Count);
        Assert.Contains($"tidy  engine: removed `{merged.Branch}` with its tree, which held nothing beyond `main`.", said);
        Assert.Contains("tidy  engine: removed `daoris/s-treeless`, which held nothing beyond `main`.", said);
        Assert.Equal(2, Events("branch.tidied").Length);
        Assert.Empty(Events("branch.kept"));
        // The record of where session branches grew from forgets it, as the press's does.
        Assert.DoesNotContain(trees.Grown.All(), entry => entry.Branch == merged.Branch);
        Assert.Contains(trees.Grown.All(), entry => entry.Branch == unmoved.Branch);
    }

    /// <summary>
    /// What git cannot read, and a removal git refuses (a locked tree), keep the branch, and the look says why once: the next
    /// look meets the same and says nothing, in the report or the log.
    /// </summary>
    [Fact]
    public async Task A_branch_git_cannot_read_or_remove_stays_and_is_said_once()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var unreadable = await trees.OpenAsync(root, "engine", "default");
        // Its link to the repository broken: git cannot say what the tree holds. git leaves the link read-only.
        var link = Path.Combine(unreadable.Path, ".git");
        File.SetAttributes(link, FileAttributes.Normal);
        await File.WriteAllTextAsync(link, $"gitdir: {Path.Combine(_scratch, "nowhere")}\n");
        var locked = await LandedTreeAsync(trees, root);
        await GitAsync(root, "worktree", "lock", "--reason", "a person's", locked.Path);
        _ledger.Register("engine", root);
        using var log = Log();
        var tidying = new BranchTidying(_home) { Pace = TimeSpan.Zero };

        var first = await tidying.LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);
        var second = await tidying.LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);

        Assert.Equal(2, first.Count);
        Assert.Contains($"tidy  engine: `{unreadable.Branch}` stays, since git could not say what its tree holds.", first);
        Assert.Contains(first, line => line.StartsWith($"tidy  engine: `{locked.Branch}` stays, since git would not remove its tree", StringComparison.Ordinal));
        Assert.Empty(second);
        var kept = Events("branch.kept");
        Assert.Equal(2, kept.Length);
        Assert.Contains(kept, data => data.GetProperty("branch").GetString() == unreadable.Branch && data.GetProperty("why").GetString() == TidyKept.Unread);
        Assert.Contains(kept, data => data.GetProperty("branch").GetString() == locked.Branch && data.GetProperty("why").GetString() == TidyKept.Refused);
        var branches = await GitAsync(root, "branch", "--list", "daoris/*");
        Assert.Contains(unreadable.Branch, branches, StringComparison.Ordinal);
        Assert.Contains(locked.Branch, branches, StringComparison.Ordinal);
        Assert.True(Directory.Exists(locked.Path));
    }

    /// <summary>
    /// 🔴 A tree a start is choosing, a fresh one or one it resumes in, is held from that choice until its record opens: while a
    /// start holds the repository's trees, the look leaves every branch for another look, says so once, and takes what it may
    /// at the next look once nothing does.
    /// </summary>
    [Fact]
    public async Task A_start_holding_the_repository_leaves_its_branches_for_another_look()
    {
        var root = await RepositoryAsync("engine");
        var landed = await LandedTreeAsync(new SessionTrees(_home), root);
        _ledger.Register("engine", root);
        using var log = Log();
        var tidying = new BranchTidying(_home) { Pace = TimeSpan.Zero };

        IReadOnlyList<string> held;
        using (TreeLock.TryStarting(_home, "default", "engine"))
        {
            held = await tidying.LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);
        }

        Assert.Contains(landed.Branch, await GitAsync(root, "branch", "--list", "daoris/*"), StringComparison.Ordinal);
        Assert.Equal(["tidy  engine: a session was starting in one of `engine`'s trees, so its branches were left as they were for another look."], held);

        var next = await tidying.LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);

        Assert.Equal([$"tidy  engine: removed `{landed.Branch}` with its tree, which held nothing beyond `main`."], next);
        Assert.False(Directory.Exists(landed.Path));
    }

    /// <summary>
    /// A review step in progress (REVIEWENV1d) keeps the tree it shows from, as a session in use keeps its own: a tree the line
    /// took whose build is shown stays, and goes at the look after nothing shows from it.
    /// </summary>
    [Fact]
    public async Task A_tree_a_review_shows_from_stays_while_it_is_shown()
    {
        var root = await RepositoryAsync("engine");
        var landed = await LandedTreeAsync(new SessionTrees(_home), root);
        _ledger.Register("engine", root);
        var tidying = new BranchTidying(_home) { Pace = TimeSpan.Zero };
        IReadOnlyCollection<string> shown = [Path.Combine(landed.Path, "app", "dist")];

        var held = await tidying.LookAsync(_ledger.Client(), null, _closing.Token, _ => Task.FromResult(shown)).WaitAsync(Bound);

        Assert.Empty(held);
        Assert.Contains(landed.Branch, await GitAsync(root, "branch", "--list", "daoris/*"), StringComparison.Ordinal);
        Assert.True(Directory.Exists(landed.Path));

        shown = [];
        var next = await tidying.LookAsync(_ledger.Client(), null, _closing.Token, _ => Task.FromResult(shown)).WaitAsync(Bound);

        Assert.Equal([$"tidy  engine: removed `{landed.Branch}` with its tree, which held nothing beyond `main`."], next);
    }

    /// <summary>
    /// A registered root that is a folder inside a repository is never looked into: git walks up from it, and would answer for
    /// the repository above (FIX-LOG), whose own empty session branch stays.
    /// </summary>
    [Fact]
    public async Task A_root_that_is_not_a_repository_of_its_own_is_never_looked_into()
    {
        var root = await RepositoryAsync("engine");
        await GitAsync(root, "branch", "daoris/s-above");
        var inside = Path.Combine(root, "inside");
        Directory.CreateDirectory(inside);
        _ledger.Register("inside", inside);

        var said = await new BranchTidying(_home).LookAsync(_ledger.Client(), null, _closing.Token).WaitAsync(Bound);

        Assert.Empty(said);
        Assert.Contains("daoris/s-above", await GitAsync(root, "branch", "--list", "daoris/*"), StringComparison.Ordinal);
    }

    /// <summary>The watch's look runs the tidy beside it, and a later look's report says what went.</summary>
    [Fact]
    public async Task The_watch_s_look_removes_an_empty_branch_and_says_so_in_a_report()
    {
        var root = await RepositoryAsync("engine");
        var landed = await LandedTreeAsync(new SessionTrees(_home), root);
        _ledger.Register("engine", root);
        var config = Path.Combine(_home, "driver.json");
        await File.WriteAllTextAsync(config, """{ "drivable": [], "pollSeconds": 1 }""");
        using var log = Log();
        var watch = new DriverWatch(
            _ledger.Client(), config, _home, new SessionProcesses(), sync: null,
            harnesses: new HarnessRoster(AdapterSet.Built(), Path.Combine(_home, "harnesses.json")))
        {
            Log = log,
            Tidying = new BranchTidying(_home) { Pace = TimeSpan.FromMinutes(5) },
        };
        var said = new List<string>();
        var watching = watch.RunAsync(
            (report, _) =>
            {
                lock (said) said.AddRange(report.Events.Where(line => line.StartsWith("tidy", StringComparison.Ordinal)));
                return Task.CompletedTask;
            },
            onError: null, _closing.Token);
        try
        {
            await Poll.Until(() => { lock (said) return said.Count > 0; }, () => "no look said the tidy", Bound);

            lock (said) Assert.Equal([$"tidy  engine: removed `{landed.Branch}` with its tree, which held nothing beyond `main`."], said);
            Assert.DoesNotContain(landed.Branch, await GitAsync(root, "branch", "--list", "daoris/*"), StringComparison.Ordinal);
            Assert.Single(Events("branch.tidied"));
        }
        finally
        {
            await _closing.CancelAsync();
            try { await watching.WaitAsync(Bound); } catch (OperationCanceledException) { }
        }
    }

    private MachineLog Log() => new(_home, "driver", () => _now);

    /// <summary>The data of every line of <paramref name="event"/> the machine log holds.</summary>
    private JsonElement[] Events(string @event)
    {
        var folder = Path.Combine(_home, MachineLog.Folder);
        if (!Directory.Exists(folder)) return [];
        return
        [
            .. Directory.EnumerateFiles(folder, "*.jsonl").SelectMany(StubFile.Lines)
                .Select(line => JsonDocument.Parse(line).RootElement.Clone())
                .Where(line => line.GetProperty("event").GetString() == @event)
                .Select(line => line.GetProperty("data")),
        ];
    }

    private async Task<TreeOpened> TreeWithWorkAsync(SessionTrees trees, string root)
    {
        var tree = await trees.OpenAsync(root, "engine", "default");
        await CommitAsync(tree.Path, $"{Guid.NewGuid():N}.txt", "the session's work", "the work");
        return tree;
    }

    /// <summary>A session tree whose work a merge took onto the line, as a pull request completed with a merge commit leaves one.</summary>
    private async Task<TreeOpened> LandedTreeAsync(SessionTrees trees, string root)
    {
        var tree = await TreeWithWorkAsync(trees, root);
        await GitAsync(root, "merge", "--no-ff", "--no-edit", tree.Branch);
        return tree;
    }

    /// <summary>A repository on <c>main</c> that ignores a database and a build's output, and holds a build's output itself.</summary>
    private async Task<string> RepositoryAsync(string name)
    {
        var root = Path.Combine(_scratch, name);
        Directory.CreateDirectory(root);
        await GitAsync(root, "init", "--quiet", "-b", "main");
        await GitAsync(root, "config", "user.email", "fixture@example.test");
        await GitAsync(root, "config", "user.name", "Fixture");
        await File.WriteAllTextAsync(Path.Combine(root, ".gitignore"), "*.db\nbuild/\n");
        await CommitAsync(root, "README.md", $"# {name}\n", "first");
        Directory.CreateDirectory(Path.Combine(root, "build"));
        await File.WriteAllTextAsync(Path.Combine(root, "build", "out.txt"), "the checkout's own build\n");
        return root;
    }

    private static async Task CommitAsync(string tree, string file, string content, string message)
    {
        await File.WriteAllTextAsync(Path.Combine(tree, file), content);
        await GitAsync(tree, "add", "-A");
        await GitAsync(tree, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "--quiet", "-m", message);
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? AppContext.BaseDirectory;
    }
}
