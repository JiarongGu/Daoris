using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// GIT1b (D147 §2.3–§2.6, §4): the reads behind a page from real repositories through the git Daoris runs: a graph page
/// by its parents, a commit against its first parent, a file's history across a rename and across pages, a blame and the
/// commit before a change, a compare both ways, and the processes each view starts. <c>GitReadsAskTests</c> holds the
/// calls and the memory with a stand-in; this holds that git answers each format as it is read.
/// </summary>
/// <remarks>
/// Fixtures live under the repository's gitignored <c>_fixtures/</c>, never OS temp. Every commit is dated, so date order
/// is the same on every run. 🔴 <b>Nothing here reaches a network.</b>
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class GitReadsTests : IDisposable
{
    private readonly string _scratch;
    private long _clock = 1_791_000_000;

    public GitReadsTests()
    {
        _scratch = Path.Combine(RepoRoot(), "_fixtures", "git-reads", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_scratch);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_scratch, recursive: true);
        }
        catch (IOException) { /* a straggling git handle; the next run's folder is its own */ }
        catch (UnauthorizedAccessException) { /* read-only pack files under .git */ }
    }

    /// <summary>
    /// The history every test reads: <c>first</c> on main, <c>topic: three</c> on a branch from it, <c>main: b</c> beside it,
    /// the merge, then <c>a.txt</c> renamed to <c>c.txt</c>.
    /// </summary>
    private sealed record History(string Root, string First, string Topic, string MainB, string Merge, string Renamed);

    private async Task<History> HistoryAsync()
    {
        var root = await RepositoryAsync("engine");
        var first = await CommitAsync(root, "first", ("a.txt", "one\ntwo\n"));
        await GitAsync(root, "switch", "-q", "-c", "topic");
        var topic = await CommitAsync(root, "topic: three", ("a.txt", "one\ntwo\nthree\n"));
        await GitAsync(root, "switch", "-q", "main");
        var mainB = await CommitAsync(root, "main: b", ("b.txt", "b\n"));
        await GitWithAsync(root, Dated(), null, "merge", "-q", "--no-ff", "topic", "-m", "Merge topic");
        var merge = await RevAsync(root, "HEAD");
        await GitAsync(root, "mv", "a.txt", "c.txt");
        var renamed = await CommitAsync(root, "rename a to c");
        return new History(root, first, topic, mainB, merge, renamed);
    }

    [Fact]
    public async Task A_graph_page_is_each_commit_in_date_order_with_its_parents()
    {
        var history = await HistoryAsync();
        var reads = new GitReads();

        var page = await reads.GraphAsync(history.Root, [history.Renamed]);

        Assert.Equal(GitReadRefusal.None, page.Refusal);
        Assert.False(page.Value!.More);
        Assert.Equal(
            [history.Renamed, history.Merge, history.MainB, history.Topic, history.First],
            page.Value.Commits.Select(commit => commit.Commit));
        var merge = page.Value.Commits[1];
        Assert.Equal([history.MainB, history.Topic], merge.Parents);
        Assert.Equal(("Merge topic", "Fixture", "fixture@example.test"), (merge.Subject, merge.Author, merge.AuthorEmail));
        // The fourth commit made, a second after the third.
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_791_000_003), merge.CommittedAt);
        Assert.Empty(page.Value.Commits[^1].Parents);
    }

    /// <summary>A branch's page: it and its line since they parted, each commit marked with its side (D147 §2.3).</summary>
    [Fact]
    public async Task A_branch_beside_its_line_since_they_parted_marks_each_side()
    {
        var history = await HistoryAsync();

        var page = await new GitReads().GraphAsync(history.Root, [history.Topic, history.MainB], sinceParted: true);

        Assert.Equal(
            [(history.MainB, GitSide.Right), (history.Topic, GitSide.Left)],
            page.Value!.Commits.Select(commit => (commit.Commit, commit.Side)));
    }

    /// <summary>
    /// A merge's changes are read against its first parent, as git's own diff of the two gives them, and a root commit's
    /// against the empty tree (D147 §2.4).
    /// </summary>
    [Fact]
    public async Task A_commit_is_its_fields_and_its_changes_against_its_first_parent()
    {
        var history = await HistoryAsync();
        var reads = new GitReads();

        var merge = await reads.CommitAsync(history.Root, history.Merge);

        Assert.Equal(GitReadRefusal.None, merge.Refusal);
        var detail = merge.Value!;
        Assert.Equal((history.MainB, true, "Merge topic"), (detail.Against, detail.Merge, detail.Subject));
        Assert.Equal([history.MainB, history.Topic], detail.Parents);
        Assert.Equal(("Fixture", "Fixture"), (detail.Author, detail.Committer));
        var changed = Assert.Single(detail.Changes!.Files);
        Assert.Equal(("a.txt", "modified", 1, 0), (changed.Path, changed.Status, changed.Added, changed.Removed));
        Assert.Equal($"1\t0\ta.txt\n", await GitAsync(history.Root, "diff", "--numstat", history.MainB, history.Merge));
        Assert.Equal([$"git show -s {history.Merge}", $"git diff {history.MainB} {history.Merge}"], merge.Commands);

        var root = await reads.CommitAsync(history.Root, history.First);

        Assert.Null(root.Value!.Against);
        Assert.Equal(("a.txt", "added", 2), (root.Value.Changes!.Files.Single().Path, root.Value.Changes.Files.Single().Status, root.Value.Changes.Files.Single().Added));

        var renamed = await reads.CommitAsync(history.Root, history.Renamed);
        Assert.Equal(("c.txt", "renamed"), (renamed.Value!.Changes!.Files.Single().Path, renamed.Value.Changes.Files.Single().Status));
    }

    /// <summary>🔴 One process per view (REVIEW3, D147 §4): a commit is two starts, and none once its answer is kept.</summary>
    [Fact]
    public async Task A_commit_starts_two_git_processes_and_none_when_read_again()
    {
        var history = await HistoryAsync();
        var starts = 0;
        var reads = new GitReads(new GitReadCache(), (root, arguments, take, ct) =>
        {
            Interlocked.Increment(ref starts);
            return WorkingTree.ReadGitAsync(root, arguments, take, ct);
        });

        await reads.CommitAsync(history.Root, history.Merge);
        Assert.Equal(2, starts);

        var again = await reads.CommitAsync(history.Root, history.Merge);
        Assert.True(again.Remembered);
        Assert.Equal(2, starts);

        await reads.CompareAsync(history.Root, history.Topic, history.MainB);
        await reads.BlameAsync(history.Root, history.Renamed, "c.txt");
        await reads.HistoryAsync(history.Root, history.Renamed, "c.txt");
        await reads.GraphAsync(history.Root, [history.Renamed]);
        Assert.Equal(2 + 2 + 1 + 1 + 1, starts);
    }

    [Fact]
    public async Task A_file_s_history_follows_its_rename_with_the_path_it_had_at_each_commit()
    {
        var history = await HistoryAsync();

        var read = await new GitReads().HistoryAsync(history.Root, history.Renamed, "c.txt");

        Assert.Equal(GitReadRefusal.None, read.Refusal);
        Assert.Equal(
            [(history.Renamed, "renamed", "c.txt", (string?)"a.txt"), (history.Topic, "modified", "a.txt", null), (history.First, "added", "a.txt", null)],
            read.Value!.Changes.Select(change => (change.Commit.Commit, change.Status, change.Path, change.From)));
        Assert.False(read.Value.More);
    }

    /// <summary>
    /// A hundred commits a page, cut from one walk: <c>--follow</c> ignores <c>--skip</c>, so a later page is the same walk
    /// read further, not a walk skipped. 105 commits, made by one <c>fast-import</c>.
    /// </summary>
    [Fact]
    public async Task A_file_s_history_pages_a_hundred_at_a_time()
    {
        var root = await RepositoryAsync("paged");
        var stream = new System.Text.StringBuilder();
        for (var i = 1; i <= 105; i++)
        {
            var message = $"c{i}";
            var content = $"{i}\n";
            stream.Append("commit refs/heads/main\n")
                .Append($"committer Fixture <fixture@example.test> {1_791_000_000 + i} +0000\n")
                .Append($"data {message.Length}\n{message}\n")
                .Append($"M 644 inline docs/中文 名.txt\ndata {System.Text.Encoding.UTF8.GetByteCount(content)}\n{content}\n");
        }

        await GitWithAsync(root, null, stream.ToString(), "fast-import", "--quiet");
        var tip = await RevAsync(root, "main");
        var reads = new GitReads();

        var first = await reads.HistoryAsync(root, tip, "docs/中文 名.txt");
        var second = await reads.HistoryAsync(root, tip, "docs/中文 名.txt", page: 1);

        Assert.Equal((100, true), (first.Value!.Changes.Count, first.Value.More));
        Assert.Equal((5, false), (second.Value!.Changes.Count, second.Value.More));
        Assert.Equal(["c105", "c104"], first.Value.Changes.Take(2).Select(change => change.Commit.Subject));
        Assert.Equal(["c5", "c4", "c3", "c2", "c1"], second.Value.Changes.Select(change => change.Commit.Subject));
        Assert.All(second.Value.Changes, change => Assert.Equal("docs/中文 名.txt", change.Path));

        var blame = await reads.BlameAsync(root, tip, "docs/中文 名.txt");
        Assert.Equal(("105", "docs/中文 名.txt"), (blame.Value!.Runs.Single().Lines.Single().Text, blame.Value.Runs.Single().Path));
    }

    /// <summary>
    /// Each line with the commit that last changed it, across the rename; and <i>Before this change</i>: a run's previous
    /// commit and path, blamed in turn (D147 §2.5).
    /// </summary>
    [Fact]
    public async Task A_blame_names_each_line_s_commit_and_the_commit_before_it()
    {
        var history = await HistoryAsync();
        var reads = new GitReads();

        var blame = await reads.BlameAsync(history.Root, history.Renamed, "c.txt");

        Assert.Equal(GitReadRefusal.None, blame.Refusal);
        Assert.False(blame.Value!.Binary);
        Assert.Null(blame.Value.StoppedAt);
        Assert.Equal(
            [(history.First, 1, "a.txt", new[] { "one", "two" }), (history.Topic, 3, "a.txt", new[] { "three" })],
            blame.Value.Runs.Select(run => (run.Commit, run.Line, run.Path, run.Lines.Select(line => line.Text).ToArray())));
        Assert.Equal("topic: three", blame.Value.Commits[history.Topic].Summary);
        var three = blame.Value.Runs[1];
        Assert.Equal((history.First, "a.txt"), (three.Previous, three.PreviousPath));

        var before = await reads.BlameAsync(history.Root, three.Previous!, three.PreviousPath!);
        Assert.Equal(["one", "two"], before.Value!.Runs.SelectMany(run => run.Lines).Select(line => line.Text));
    }

    /// <summary>D111's test: a binary file has a history and no blame.</summary>
    [Fact]
    public async Task A_binary_file_has_a_history_and_no_blame()
    {
        var root = await RepositoryAsync("binary");
        await File.WriteAllBytesAsync(Path.Combine(root, "logo.bin"), [0x78, 0, 0x79, (byte)'\n', 0, 0xff]);
        var tip = await CommitAsync(root, "a binary");
        var reads = new GitReads();

        var blame = await reads.BlameAsync(root, tip, "logo.bin");
        var history = await reads.HistoryAsync(root, tip, "logo.bin");

        Assert.True(blame.Value!.Binary);
        Assert.Empty(blame.Value.Runs);
        Assert.Equal("added", Assert.Single(history.Value!.Changes).Status);
    }

    /// <summary>A path the commit does not hold is not answered, and nothing is kept for it.</summary>
    [Fact]
    public async Task A_path_the_commit_does_not_hold_is_not_answered()
    {
        var history = await HistoryAsync();
        var reads = new GitReads();

        var blame = await reads.BlameAsync(history.Root, history.Renamed, "a.txt");

        Assert.Equal(GitReadRefusal.NotAnswered, blame.Refusal);
        Assert.Equal(0, reads.Cache.Count);
    }

    /// <summary>
    /// The commits only on each side, and what the right side changed since the two parted, or everything that differs
    /// (D147 §2.6), each as git's own diff of that range has it.
    /// </summary>
    [Fact]
    public async Task A_compare_reads_each_side_and_the_changes_since_they_parted_or_all_of_them()
    {
        var history = await HistoryAsync();
        var reads = new GitReads();

        var parted = await reads.CompareAsync(history.Root, history.Topic, history.MainB);
        var everything = await reads.CompareAsync(history.Root, history.Topic, history.MainB, everything: true);

        Assert.Equal(
            [(history.MainB, GitSide.Right), (history.Topic, GitSide.Left)],
            parted.Value!.Commits.Select(commit => (commit.Commit, commit.Side)));
        Assert.Equal(["b.txt"], parted.Value.Changes!.Files.Select(file => file.Path));
        Assert.Equal(["a.txt", "b.txt"], everything.Value!.Changes!.Files.Select(file => file.Path));
        Assert.Equal(("modified", 0, 1), (everything.Value.Changes.Files[0].Status, everything.Value.Changes.Files[0].Added, everything.Value.Changes.Files[0].Removed));
    }

    [Fact]
    public async Task The_branches_holding_a_commit_and_a_name_s_commit_are_git_s_answers()
    {
        var history = await HistoryAsync();
        var reads = new GitReads();

        var holding = await reads.HoldingAsync(history.Root, history.Topic);
        var resolved = await reads.ResolveAsync(history.Root, "topic");
        var missing = await reads.ResolveAsync(history.Root, "no-such-branch");

        Assert.Equal(["refs/heads/main", "refs/heads/topic"], holding.Value!.Branches);
        Assert.Equal(history.Topic, resolved.Value!.Commit);
        Assert.Equal(GitReadRefusal.NotAnswered, missing.Refusal);
    }

    private async Task<string> RepositoryAsync(string name)
    {
        var root = Path.Combine(_scratch, name);
        Directory.CreateDirectory(root);
        await GitAsync(root, "init", "--quiet", "-b", "main");
        await GitAsync(root, "config", "user.email", "fixture@example.test");
        await GitAsync(root, "config", "user.name", "Fixture");
        await GitAsync(root, "config", "core.autocrlf", "false");
        return root;
    }

    /// <summary>A commit of every change in the tree, dated a second after the last, so date order is the same on every run.</summary>
    private async Task<string> CommitAsync(string root, string message, params (string File, string Content)[] files)
    {
        foreach (var (file, content) in files) await File.WriteAllTextAsync(Path.Combine(root, file), content);
        await GitAsync(root, "add", "-A");
        await GitWithAsync(root, Dated(), null, "commit", "-q", "-m", message);
        return await RevAsync(root, "HEAD");
    }

    private Dictionary<string, string> Dated()
    {
        var at = $"@{_clock++} +0000";
        return new Dictionary<string, string>(StringComparer.Ordinal) { ["GIT_AUTHOR_DATE"] = at, ["GIT_COMMITTER_DATE"] = at };
    }

    private static async Task<string> RevAsync(string root, string revision) => (await GitAsync(root, "rev-parse", revision)).Trim();

    private static Task<string> GitAsync(string cwd, params string[] arguments) => GitWithAsync(cwd, null, null, arguments);

    /// <summary>git through the fixture's one runner (TESTGIT1), its stdout once it has exited cleanly.</summary>
    private static async Task<string> GitWithAsync(string cwd, Dictionary<string, string>? environment, string? input, params string[] arguments)
    {
        var run = await GitFixture.RunWithAsync(cwd, environment, input, arguments);
        Assert.True(run.ExitCode == 0, $"git {string.Join(' ', arguments)} failed: {run.Stderr}");
        return run.Stdout;
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
