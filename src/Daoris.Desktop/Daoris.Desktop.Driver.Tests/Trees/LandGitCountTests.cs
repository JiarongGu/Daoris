using System.Diagnostics;
using Daoris.Driver;
using Xunit.Abstractions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A landing asks git once per fact (LANDGIT1, after STARTGIT1): one landing, the press's or the look's, started about 50 git
/// processes and asked some facts four or five times (`rev-parse --show-toplevel`, `symbolic-ref --short -q HEAD`,
/// `rev-parse --verify refs/heads/main^{commit}`, `merge-base`), which on a machine whose antivirus slows every process start
/// costs many seconds of nothing but starts.
/// </summary>
/// <remarks>
/// Every start through the gate is wrapped by <see cref="ProcessStarts.Within"/> (the PROCSTART2 seam) while one landing runs; the
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

    /// <summary>The look's automatic landing of a branch, the rule's tidy behind it.</summary>
    [Fact]
    public async Task An_automatic_landing_asks_git_for_each_fact_once()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Tidy: true, AutoAccept: true));
        var world = new World();
        var tree = await DoneSessionAsync(world, root, "s1", "q1");
        var lander = new AutoLander(_home, world, new SessionTrees(_home),
            new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)), _ => { });
        var (chosen, _) = await lander.ChooseAsync(Config());
        Assert.Single(chosen);

        var starts = new List<string>();
        using (Counting(starts))
        {
            var said = Assert.Single(await lander.LandAsync(chosen));
            Assert.Contains("its work is on `feature/q1-fix-the-gap`", said);
        }

        Assert.False(Directory.Exists(tree), "the rule's tidy took the tree");
        Judge(starts, AutomaticCeiling);
    }

    /// <summary>The press on a squash rule with a tidy: the branch made of one commit, the tree and its branch gone.</summary>
    [Fact]
    public async Task A_squash_landing_asks_git_for_each_fact_once()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Tidy: true, Squash: true));
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
        Judge(starts, SquashCeiling);
    }

    /// <summary>The press on a merge rule with a tidy: the work merged into the line, the tree and its branch gone.</summary>
    [Fact]
    public async Task A_merge_landing_asks_git_for_each_fact_once()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Merge, Tidy: true));
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
    /// 🔴 What another process can move is never kept (the review of LANDGIT1): the session's head, a branch's tip, and the commits
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

    /// <summary>A table that was closed answers nothing, wherever a child task still holds it, and a child never inherits a live one.</summary>
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

        // The first ask, and the child's after the scope closed: neither was answered from the table.
        Assert.Equal(2, starts.Count);
    }

    /// <summary>
    /// 🔴 A fact is kept only until a git of the landing's own may move a ref: the branch made answers the question it was asked
    /// before it was made, and a plugin's push (<see cref="GitFacts.Dropped"/>) drops what was held.
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

    // Git starts of one landing on this fixture, before LANDGIT1 and after: the look's automatic landing 61 and 52, the press on a
    // squash rule 54 and 51, the press on a merge rule 45 and 42. A first version kept every ref and head read and reached 34, 42 and
    // 34; its review found each a stale answer a judgement or a delete could act on, so only what an id answers is kept now. Merged
    // with FABRIC-END4a's judged delete (its holders read and its one ref transaction) and END6c's per-line fetch reservation, the
    // automatic landing measured 55 and the merge press 43; the squash press stayed within 51. PROC1d-iv's questions of commits kept
    // a judgement (and `diff` and `rev-list --count` of ids kept in the planning) moved none of the three: one branch asks no pair twice.
    // LANDGIT1c hands the judgement's `status` count to the dirty check, and the look's to the landing: 53, 50 and 42.
    // LANDGIT1b passed the head and tip reads down instead of keeping them: the look's try takes the rule's plan alone (the press's plan
    // read the line's tips, the head and the named branch's tip again), the landing reads the tree's repository and branch once for
    // its readers, the content proof takes the tree's own branch its judgement read, and the line's two forms are one snapshot. 44, 46
    // and 40, each act's own read (the branch's commits, the pinned tip, the tree's move) still made right before it. The two together,
    // each landing's `status` and its repository and branch handed down: 42, 45 and 39.
    private const int AutomaticCeiling = 42;
    private const int SquashCeiling = 45;
    private const int MergeCeiling = 39;

    private const string VerifyQuiet = "git rev-parse --verify --quiet ";

    private void Judge(List<string> starts, int ceiling)
    {
        var git = starts.Where(each => each.StartsWith("git ", StringComparison.Ordinal)).ToList();
        var report = $"{git.Count} git starts:\n{string.Join('\n', git)}";
        // The run's starts in its output, so a ceiling is lowered to what was measured.
        _output.WriteLine(report);
        // A question of commits by their ids is asked once unless a git that may move a ref ran between its two askings. What names a
        // ref or the head is never kept (another process moves it, and a judgement or a delete must see the move), so those are asked
        // as often as the steps ask them. `status` is never kept either, but the landing's judgement hands its count to the dirty check
        // (LANDGIT1c), so it too is asked once until a write. Layout reads are kept only in the planning, so the tidy's own askings,
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
        // One spelling per ref fact (LANDGIT1c): a ref or the head is never read as itself and again as its commit.
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
        var arguments = info.ArgumentList.SkipWhile(each => each is "-c" or "core.longpaths=true");
        var where = Path.GetRelativePath(_scratch, info.WorkingDirectory).Replace('\\', '/');
        lock (starts) starts.Add(program + " " + string.Join(' ', arguments) + "  @ " + where);
        return Process.Start(info);
    });

    private sealed class World : IAutoLandingWorld
    {
        public Dictionary<string, QuestView> Quests { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<SessionRecord> Records { get; } = [];
        public Task<QuestView?> QuestAsync(string id, CancellationToken ct) => Task.FromResult(Quests.GetValueOrDefault(id));
        public Task<IReadOnlyList<SessionRecord>> RecordsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<SessionRecord>>([.. Records]);
        public Task<IReadOnlySet<string>> InUseAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlySet<string>>(Records.Where(r => r.Live && r.Tree is not null).Select(r => r.Tree!).ToHashSet());
        public Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<QuestView>>([.. Quests.Values]);
        public Task<AskView?> AskAsync(string id, CancellationToken ct) => Task.FromResult<AskView?>(null);
        public Task<OpinionView?> OpinionAsync(string id, CancellationToken ct) => Task.FromResult<OpinionView?>(null);
    }

    private DriverConfig Config() => DriverConfig.Load(DaorisHome.Entry(_home, HomeEntry.DriverConfig));

    private void Rule(LandingRule rule) =>
        DriverConfig.Empty.WithLanding("engine", rule).Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));

    private async Task<string> DoneSessionAsync(World world, string root, string session, string quest)
    {
        var opened = await new SessionTrees(_home).OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, $"{session}.txt", $"the work of {session}", $"the work of {session}");
        world.Quests[quest] = new QuestView(quest, "game", "engine", "Fix the gap", "Fix it.", "Done");
        world.Records.Add(new SessionRecord(session, "engine", "completed")
        {
            Quest = quest, Tree = opened.Path, Created = DateTimeOffset.UtcNow,
        });
        var verdict = AutoLander.Concluded(_home, Config(), new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)), session, world.Quests[quest],
            "Done", "completed", opened.Path, "aurora");
        Assert.Equal(AutoConcluded.Due, verdict);
        return opened.Path;
    }

    private async Task<string> RepositoryAsync(string name)
    {
        var root = Path.Combine(_scratch, name);
        Directory.CreateDirectory(root);
        await GitAsync(root, "init", "--quiet", "-b", "main");
        await GitAsync(root, "config", "user.email", "fixture@example.test");
        await GitAsync(root, "config", "user.name", "Fixture");
        await CommitAsync(root, "README.md", $"# {name}", "first");
        return root;
    }

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
