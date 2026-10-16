using System.Diagnostics;
using Daoris.Driver;

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

    public LandGitCountTests()
    {
        _scratch = Path.Combine(RepoRoot(), "_fixtures", "land-git-count", Guid.NewGuid().ToString("N")[..8]);
        _home = Path.Combine(_scratch, "daoris-home");
        Directory.CreateDirectory(_home);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_scratch, recursive: true);
        }
        catch (IOException) { /* a straggling git handle */ }
        catch (UnauthorizedAccessException) { /* read-only pack files under .git */ }
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

    // Git starts of one landing on this fixture, before LANDGIT1 and after: the look's automatic landing 61 and 34, the press on a
    // squash rule 54 and 42, the press on a merge rule 45 and 34. The tidy's own reads (its proof and its delete) are asked as they
    // were: what they repeat of the head is read after the landing's writes, so it is read again.
    private const int AutomaticCeiling = 34;
    private const int SquashCeiling = 42;
    private const int MergeCeiling = 34;

    private static void Judge(List<string> starts, int ceiling)
    {
        var git = starts.Where(each => each.StartsWith("git ", StringComparison.Ordinal)).ToList();
        var report = $"{git.Count} git starts:\n{string.Join('\n', git)}";
        // A command asked again is a repeat unless a git that may move a ref ran between its two askings: what the landing wrote is
        // read again, as a later step depends on it. `status` is the files', never a ref's: each gate that refuses on uncommitted work
        // reads it as it acts, so two of them are two askings by design.
        var repeated = new List<string>();
        var since = new HashSet<string>(StringComparer.Ordinal);
        foreach (var each in git)
        {
            // The folder is not part of a fact here: the landing is of one repository and one tree, so a command asked in the tree and
            // again in the checkout is asked twice.
            var command = each.Split("  @ ")[0];
            var arguments = command["git ".Length..].Split(' ');
            if (RefSnapshot.MayWrite(arguments)) since.Clear();
            else if (!command.StartsWith("git status ", StringComparison.Ordinal) && !since.Add(command)) repeated.Add(each);
        }

        Assert.True(repeated.Count == 0, $"repeated without a write between: {string.Join("; ", repeated)}\n{report}");
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
        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await Task.WhenAll(stdout, stderr);
        await process.WaitForExitAsync();
        return await stdout;
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git"))
               && !File.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? AppContext.BaseDirectory;
    }
}
