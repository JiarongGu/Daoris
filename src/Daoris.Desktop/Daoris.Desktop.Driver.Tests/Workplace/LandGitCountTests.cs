using System.Diagnostics;
using Daoris.Driver;
using Xunit.Abstractions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A landing asks git once per fact: asked afresh, one landing, the press's or the look's, starts about 50 git
/// processes and asks some facts four or five times (`rev-parse --show-toplevel`, `symbolic-ref --short -q HEAD`,
/// `rev-parse --verify refs/heads/main^{commit}`, `merge-base`), which on a machine whose antivirus slows every process start
/// costs many seconds of nothing but starts.
/// </summary>
/// <remarks>
/// Every start through the gate is wrapped by <see cref="ProcessStarts.Within"/> while one landing runs; the
/// test setup's own git runs outside it. The same command never repeats within a landing unless a write of the landing's own sits
/// between its two askings (a merge or a ref update changes the answer), which the allowed list names; the count has a ceiling.
/// </remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class LandGitCountTests : IDisposable
{
    private static readonly Func<CancellationToken, Task<IReadOnlySet<string>>> NoneRunning =
        _ => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());

    private readonly string _scratch;
    private readonly string _home;
    private readonly ITestOutputHelper _output;

    public LandGitCountTests(ITestOutputHelper output)
    {
        _output = output;
        _scratch = Path.Combine(RepoRoot(), "_fixtures", "land-git-count", Guid.NewGuid().ToString("N")[..8]);
        _home = Path.Combine(_scratch, "daoris-home");
        Directory.CreateDirectory(_home);
    }

    public void Dispose()
    {
        GitSeed.Discard(_scratch);
    }

    /// <summary>A squash landing: the branch made of one commit.</summary>
    [Fact]
    public async Task A_squash_landing_asks_git_for_each_fact_once()
    {
        var root = await RepositoryAsync("engine");
        var gate = GraphLand.Gate(_home, GraphLand.Branch("feature/{quest}-{slug}", squash: true));
        var trees = new SessionTrees(_home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "first.txt", "the first step", "the first step");
        await CommitAsync(tree.Path, "second.txt", "the second step", "the second step");

        var starts = new List<string>();
        TreeLanding landed;
        using (Counting(starts))
        {
            landed = await trees.LandAsync(tree.Path, new LandingSubject("s1", "q1", "Fix the gap"), inUse: NoneRunning, gate: gate);
        }

        Assert.True(landed.Landed, landed.Message);
        Judge(starts, SquashCeiling);
    }

    /// <summary>A merge landing: the work merged into the line.</summary>
    [Fact]
    public async Task A_merge_landing_asks_git_for_each_fact_once()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "first.txt", "the first step", "the first step");
        await CommitAsync(tree.Path, "second.txt", "the second step", "the second step");

        var starts = new List<string>();
        TreeLanding landed;
        using (Counting(starts))
        {
            landed = await trees.LandAsync(tree.Path, new LandingSubject("s1", "q1", "Fix the gap"), inUse: NoneRunning);
        }

        Assert.True(landed.Landed, landed.Message);
        Judge(starts, MergeCeiling);
    }

    /// <summary>
    /// What another process can move is never kept: the session's head, a branch's tip, and the commits
    /// no branch holds. Each is moved from outside between its asking and its use, and the second asking sees the move.
    /// </summary>
    [Fact]
    public async Task What_another_process_moves_is_asked_of_git_each_time()
    {
        var root = await RepositoryAsync("engine");
        await GitAsync(root, "branch", "side");
        var tipA = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        using var facts = GitFacts.Within();

        // The head: another process commits between the two askings (a replay would be built from the first answer).
        var head = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", "HEAD^{commit}"], default);
        await CommitAsync(root, "b.txt", "b", "b");
        var headNow = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", "HEAD^{commit}"], default);
        Assert.Equal(tipA, head.Stdout.Trim());
        Assert.NotEqual(tipA, headNow.Stdout.Trim());

        // A branch's tip, as the tidy's freshness check reads it.
        var side = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", "refs/heads/side^{commit}"], default);
        await GitAsync(root, "update-ref", "refs/heads/side", headNow.Stdout.Trim());
        var sideNow = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", "refs/heads/side^{commit}"], default);
        Assert.NotEqual(side.Stdout.Trim(), sideNow.Stdout.Trim());

        // What no branch holds: the landing branch is deleted from outside, and the cleanup's proof must see it.
        var held = await WorkingTree.GitAsync(root, ["log", "--oneline", tipA, "--not", "--exclude=daoris/*", "--branches", "--exclude=*/daoris/*", "--remotes"], default);
        await GitAsync(root, "branch", "-D", "side");
        await GitAsync(root, "checkout", "-q", "--detach", "HEAD");
        await GitAsync(root, "branch", "-D", "main");
        var heldNow = await WorkingTree.GitAsync(root, ["log", "--oneline", tipA, "--not", "--exclude=daoris/*", "--branches", "--exclude=*/daoris/*", "--remotes"], default);
        Assert.Equal("", held.Stdout.Trim());
        Assert.NotEqual("", heldNow.Stdout.Trim());
    }

    /// <summary>A table that is closed answers nothing, wherever a child task still holds it, and a child never inherits a live one.</summary>
    [Fact]
    public async Task A_closed_table_is_inert_in_every_flow_that_holds_it()
    {
        var root = await RepositoryAsync("engine");
        var sha = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        string[] ask = ["merge-base", sha, sha];
        var starts = new List<string>();
        Task child;
        using (Counting(starts))
        {
            var scope = GitFacts.Within();
            await WorkingTree.GitAsync(root, ask, default);
            var gate = new TaskCompletionSource();
            child = Task.Run(async () =>
            {
                await gate.Task;
                await WorkingTree.GitAsync(root, ask, default);
            });
            scope.Dispose();
            gate.SetResult();
            await child;
        }

        // The first ask, and the child's after the scope closed: neither is answered from the table.
        Assert.Equal(2, starts.Count);
    }

    /// <summary>
    /// A fact is kept only until a git of the landing's own may move a ref: the branch made changes the answer to the question asked
    /// before it, and a plugin's push (<see cref="GitFacts.Dropped"/>) drops what is held.
    /// </summary>
    [Fact]
    public async Task A_fact_asked_before_a_write_is_asked_again_after_it()
    {
        var root = await RepositoryAsync("engine");
        var starts = new List<string>();
        var sha = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        string[] ask = ["merge-base", sha, sha];
        using (Counting(starts))
        using (GitFacts.Within())
        {
            await WorkingTree.GitAsync(root, ask, default);
            await WorkingTree.GitAsync(root, ask, default);
            Assert.Single(starts);

            await WorkingTree.GitAsync(root, ["branch", "made", "HEAD"], default);
            await WorkingTree.GitAsync(root, ask, default);
            Assert.Equal(3, starts.Count);

            // A write that is not git's: dropped by its caller, as the plugin's push is.
            GitFacts.Dropped();
            await WorkingTree.GitAsync(root, ask, default);
            Assert.Equal(4, starts.Count);
        }

        // Outside a landing nothing is kept: each ask is git's.
        starts.Clear();
        using (Counting(starts))
        {
            await WorkingTree.GitAsync(root, ask, default);
            await WorkingTree.GitAsync(root, ask, default);
        }

        Assert.Equal(2, starts.Count);
    }

    // The ceilings are the git starts of one landing on this fixture, the press on a squash rule and on a merge rule: each branch's tree
    // comes from the refs' one listing, the tree's status, head and branch in one start, and the refs of a repository are listed once
    // whichever of its folders asks. What is left asked again is the line's commit and the head, which another process can move and which
    // a landing therefore reads afresh before each act.
    // A merge rule's press goes by the integration transport, its merge made in the line's integration tree and the checkout fast-forwarded.
    // It starts the tree found (`worktree list`) and made (`worktree add`) and its binding read (`rev-parse --show-toplevel`) with its four
    // puts back at the base; the ancestry read, the merge and the head read; the line read before the move and in its effect and after it,
    // the fence's `ls-tree`, the checkout list and clean check, and the fast-forward: each the transport's own check at its moment, or the
    // move itself.
    // A delete of the landing's branch asks first whether any name the ref transaction holds is a symbolic ref (`for-each-ref
    // --format=%(refname)%09%(symref)`). The branch about to go is a fact the proof's holders listing never read (it leaves the branch
    // itself out), and `update-ref -d` on a symbolic ref would delete its target.
    // Every landing under Current performs its delivery, each effect's outcome read after it and never assumed: the branch's ref made
    // through one ref transaction (its symbolic-ref check and `update-ref --stdin`) and read back, a merge's line read after its press
    // (`rev-parse`, `merge-base --is-ancestor`).
    private const int SquashCeiling = 25;
    private const int MergeCeiling = 39;

    private const string VerifyQuiet = "git rev-parse --verify --quiet ";

    private void Judge(List<string> starts, int ceiling)
    {
        var git = starts.Where(each => each.StartsWith("git ", StringComparison.Ordinal)).ToList();
        // By command and folder kind, so the step that starts the most is seen first (the integration tree's are those under `i-`).
        var breakdown = git.GroupBy(each => each.Split("  @ ")[0].Split(' ')[1] + (each.Contains("/i-") ? " (integration tree)" : ""))
            .OrderByDescending(group => group.Count()).Select(group => $"{group.Count(),3}  {group.Key}");
        var report = $"{git.Count} git starts:\n{string.Join('\n', breakdown)}\n\n{string.Join('\n', git)}";
        // The run's starts in its output, so a ceiling is lowered to what is measured.
        _output.WriteLine(report);
        // A question of commits by their ids is asked once unless a git that may move a ref ran between its two askings. What names a
        // ref or the head is never kept (another process moves it, and a judgement or a delete must see the move), so those are asked
        // as often as the steps ask them. `status` is never kept either, but the landing's judgement hands its count to the dirty check,
        // so it too is asked once until a write. Layout reads are kept only in the planning, so the tidy's own askings,
        // after the table is closed, are not repeats of it.
        var repeated = new List<string>();
        var since = new HashSet<string>(StringComparer.Ordinal);
        foreach (var each in git)
        {
            // The folder is not part of a fact here: the landing is of one repository and one tree, so a command asked in the tree and
            // again in the checkout is asked twice.
            // A `status` is of one working tree's files, so there the folder is part of it.
            var command = each.Split("  @ ")[0];
            var arguments = command["git ".Length..].Split(' ');
            var status = arguments[0] == "status";
            if (RefSnapshot.MayWrite(arguments)) since.Clear();
            else if ((GitFacts.Kept(arguments) || status) && !command.Contains("--show-toplevel")
                     && !command.Contains("--git-common-dir") && !command.Contains("origin/HEAD") && !since.Add(status ? each : command))
            {
                repeated.Add(each);
            }
        }

        Assert.True(repeated.Count == 0, $"repeated without a write between: {string.Join("; ", repeated)}\n{report}");
        // One spelling per ref fact: a ref or the head is never read as itself and again as its commit.
        var read = git.Select(each => each.Split("  @ ")[0]).Where(command => command.StartsWith(VerifyQuiet, StringComparison.Ordinal))
            .Select(command => command[VerifyQuiet.Length..]).ToHashSet(StringComparer.Ordinal);
        var spelledTwice = read.Where(each => !each.Contains('^') && read.Contains(each + "^{commit}")).ToList();
        Assert.True(spelledTwice.Count == 0, $"read under two spellings: {string.Join("; ", spelledTwice)}\n{report}");
        Assert.True(git.Count <= ceiling, report);
    }

    private IDisposable Counting(List<string> starts) => ProcessStarts.Within(info =>
    {
        var program = Path.GetFileNameWithoutExtension(info.FileName).ToLowerInvariant();
        // The working folder is part of the question: the same command in another tree is another fact.
        var arguments = info.ArgumentList.SkipWhile(each => each is "-c" or "core.longpaths=true" or "--no-optional-locks");
        var where = Path.GetRelativePath(_scratch, info.WorkingDirectory).Replace('\\', '/');
        lock (starts) starts.Add(program + " " + string.Join(' ', arguments) + "  @ " + where);
        return Process.Start(info);
    });

    /// <summary>A copy of the run's plain seed (<see cref="GitSeed.PlainAsync"/>): the counts are the landing's own starts.</summary>
    private Task<string> RepositoryAsync(string name) => GitSeed.PlainAsync(Path.Combine(_scratch, name), name);

    private static async Task CommitAsync(string tree, string file, string content, string message)
    {
        await File.WriteAllTextAsync(Path.Combine(tree, file), content + "\n");
        await GitAsync(tree, "add", "-A");
        await GitAsync(tree, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", message);
    }

    private static async Task<string> GitAsync(string cwd, params string[] arguments)
    {
        var info = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(TestGit.Past(info))!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await Task.WhenAll(stdout, stderr);
        await process.WaitForExitAsync();
        return await stdout;
    }

    private static string RepoRoot() => GitSeed.RepoRoot();
}
