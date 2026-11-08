using System.Diagnostics;
using Daoris.Driver;
using Xunit.Abstractions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A session's start asks git once per fact: before a session spawns, the tree opener must not repeat git commands one
/// after another (`symbolic-ref origin/HEAD`, `rev-parse --verify refs/heads/main`), which on a machine
/// whose antivirus slows every process start tips <see cref="StopHoldTickTests"/> past its 30 s under load.
/// </summary>
/// <remarks>
/// Every start through the gate is wrapped by <see cref="ProcessStarts.Within"/> while a stub-driven start
/// runs as StopHold's does; the git starts before the harness's own (node) start are the start's, counted and listed.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class StartGitCountTests : IDisposable
{
    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "start-git", Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;
    private readonly ITestOutputHelper _output;

    public StartGitCountTests(ITestOutputHelper output)
    {
        _output = output;
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
        GitSeed.Discard(_home);
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
            Actors = StaffedRoles.On("stub"),
            TestBound = 1,
            PollSeconds = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["stub"] = ["node", script] },
        };
        var adapters = ProviderRegistry.Built();
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
        _output.WriteLine(report);
        Assert.True(before.Count > 0, report);
        Assert.True(before.Count(each => each.Contains("symbolic-ref --short refs/remotes/origin/HEAD", StringComparison.Ordinal)) <= 1, report);
        // The line guessed once, before the tree is made. One spelling per ref fact: the line's commit read after the tree is
        // made is the same command, asked again past the `worktree add` that made a branch.
        // The making holds the repository's hooks (`-c core.hooksPath=…`), so its arguments open with that setting.
        var made = before.FindIndex(each => each.Contains(" worktree add ", StringComparison.Ordinal));
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

    // What the ceiling allows: the line is guessed once, its guess is not checked again, and a fresh tree's `merge-base` and `log`
    // against itself are not asked. The opening asks the checkout and the tree it made each for a working root (`rev-parse
    // --show-toplevel`, two folders, so two facts), makes the tree with its hooks held (`worktree add`), reads the new branch and
    // lets the opening lock go. The session locks its tree in its own name once its record is open, reading the tree's lock
    // (`worktree list --porcelain`) and taking it (`worktree lock`), so no merge tool prunes it. The files stage reads the effective
    // rules once: the commit's tracked names (`ls-tree`), the ignored tops (`ls-files --others`), the linked worktrees (`worktree list`),
    // and the two rule files at the commit (`cat-file`); it writes back what a cut-off start left (`status`) and runs the owed
    // `post-checkout` hook, which the held-hooks making owes. The rest are the start's own reads of the tree's layout and line.
    private const int Ceiling = 21;

    private void Git(params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = _repository, UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(TestGit.Past(info))!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
    }
}
