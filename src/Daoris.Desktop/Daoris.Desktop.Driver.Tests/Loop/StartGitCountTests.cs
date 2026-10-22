using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A session's start asks git once per fact (STARTGIT1): before a session spawns, the tree opener ran about 18 git commands one
/// after another, `symbolic-ref origin/HEAD` three times and `rev-parse --verify refs/heads/main` four or five, which on a machine
/// whose antivirus slows every process start took 8-12 s and tipped <see cref="StopHoldTickTests"/> past its 30 s under load.
/// </summary>
/// <remarks>
/// Every start through the gate is wrapped by <see cref="ProcessStarts.Within"/> (the PROCSTART2 seam) while a stub-driven start
/// runs as StopHold's does; the git starts before the harness's own (node) start are the start's, counted and listed.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class StartGitCountTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-start-git-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    public StartGitCountTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "start@example.com");
        Git("config", "user.name", "STARTGIT1");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public async Task A_start_asks_git_for_each_fact_once_before_the_session_spawns()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        var script = Path.Combine(_home, "agent.mjs");
        File.WriteAllText(script, """
            if (process.argv.includes('--version')) { console.log('stub-harness 1.0.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }
            await fetch(`${process.env.DAORIS_SERVICE_URL}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ action: 'take' }) });
            await fetch(`${process.env.DAORIS_SERVICE_URL}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ action: 'done', reason: 'landed' }) });
            """);
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Trees = ["engine"],
            Adapter = "stub",
            TestBound = 1,
            PollSeconds = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["stub"] = ["node", script] },
        };
        var adapters = AdapterSet.Built();
        var driver = new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home,
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));

        var starts = new List<string>();
        using (ProcessStarts.Within(info =>
        {
            var program = Path.GetFileNameWithoutExtension(info.FileName).ToLowerInvariant();
            var arguments = info.ArgumentList.SkipWhile(each => each is "-c" or "core.longpaths=true");
            lock (starts) starts.Add(program + " " + string.Join(' ', arguments));
            return Process.Start(info);
        }))
        {
            await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        }

        // The git starts before the harness's own run (node on the script): what the start asked before the session spawned.
        var spawn = starts.FindIndex(each => each.Contains("agent.mjs", StringComparison.Ordinal) && !each.Contains("--version", StringComparison.Ordinal));
        Assert.True(spawn > 0, "the session never spawned:\n" + string.Join('\n', starts));
        var before = starts.Take(spawn).Where(each => each.StartsWith("git ", StringComparison.Ordinal)).ToList();
        var report = $"{before.Count} git starts before the spawn:\n{string.Join('\n', starts.Take(spawn))}";
        Assert.True(before.Count > 0, report);
        Assert.True(before.Count(each => each.Contains("symbolic-ref --short refs/remotes/origin/HEAD", StringComparison.Ordinal)) <= 1, report);
        // The line guessed once, before the tree is made. One spelling per ref fact (LANDGIT1c): the line's commit read after the tree is
        // made is the same command, asked again past the `worktree add` that made a branch.
        var made = before.FindIndex(each => each.StartsWith("git worktree add", StringComparison.Ordinal));
        Assert.True(made > 0, report);
        Assert.True(before.Take(made).Count(each => each == "git rev-parse --verify --quiet refs/heads/main^{commit}") <= 1, report);
        // No fact is asked twice: the same command never repeats unless a git that may move a ref ran between its two askings (`rev-parse
        // HEAD` is read once, as the session's baseline).
        var repeated = new List<string>();
        var since = new HashSet<string>(StringComparer.Ordinal);
        foreach (var each in before)
        {
            if (RefSnapshot.MayWrite(each["git ".Length..].Split(' '))) since.Clear();
            else if (!since.Add(each)) repeated.Add(each);
        }

        Assert.True(repeated.Count == 0, $"repeated: {string.Join("; ", repeated)}\n{report}");
        Assert.True(before.Count <= Ceiling, report);
    }

    // 18 git starts before the change (`symbolic-ref origin/HEAD` x3, `rev-parse --verify --quiet refs/heads/main` x4); 11 after
    // (the line is guessed once, its guess is not checked again, and a fresh tree's `merge-base` and `log` against itself are not asked).
    // 12 since TREEDEPS1 (D51): a fresh tree lists the checkout's lockfiles once (`git ls-files … package-lock.json …`) to copy
    // its dependencies, one start that saves the session a whole `npm ci`.
    // 14 since TREELOCK1 (D51's note): the session locks its tree in its own name once its record is open, reading the tree's lock
    // (`git worktree list --porcelain`) and taking it (`git worktree lock --reason daoris session …`), so no merge tool prunes it.
    private const int Ceiling = 14;

    private void Git(params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = _repository, UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(TestGit.Past(info))!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
    }
}
