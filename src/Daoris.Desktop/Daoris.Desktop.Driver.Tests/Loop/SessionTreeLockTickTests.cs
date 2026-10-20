using System.Diagnostics;
using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TREELOCK1 (D51's note) through a REAL tick: a driven session's own tree is locked in its name while it runs, so git refuses it an
/// unforced remove, the move the merge tool's prune made (the FIX-LOG's 2026-10-20 DOGFOOD1 entry), and is let go when it ends. A
/// real process (node), real git and the stand-in service <see cref="AskAndWaitTickTests.StandIn"/>; no model and no account.
/// </summary>
/// <remarks>The lock's git half, a prune included, is <c>SessionLocksTests</c>'.</remarks>
[Trait(Category.Name, Category.Process)]
public sealed class SessionTreeLockTickTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-tree-lock-tick-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    public SessionTreeLockTickTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "treelock@example.com");
        Git("config", "user.name", "TREELOCK1");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");
    }

    public void Dispose()
    {
        try { TreeSetAside.DeleteNeverFollowing(_home); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public async Task A_driven_sessions_tree_is_locked_in_its_name_while_it_runs_and_let_go_when_it_ends()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        var release = Path.Combine(_home, "release");
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Trees = ["engine"],
            Adapter = "stub",
            TimeoutMinutes = 1,
            PollSeconds = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["stub"] = ["node", Agent(), release] },
        };
        var adapters = AdapterSet.Built();
        var driver = new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home,
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));

        // 1. The session takes its quest and works in its own tree, which is locked in its name: an unforced remove is refused.
        var run = driver.RunOnceAsync();
        await UntilAsync(() => service.SessionCount == 1 && service.Status("q1") == "Taken");
        var tree = service.Session("s1")["tree"]!.GetValue<string>();
        Assert.Equal("daoris session s1", await SessionLocksTests.LockOfAsync(_repository, tree));
        var removed = await RunAsync(_repository, "worktree", "remove", tree);
        Assert.NotEqual(0, removed.ExitCode);
        Assert.Contains("locked", removed.Stderr);

        // 2. It closes its quest and ends: its tree stands, as nothing deletes itself (D51), and is no longer locked.
        await File.WriteAllTextAsync(release, "");
        await run.WaitAsync(Poll.Patience);

        Assert.Equal("completed", service.Session("s1")["state"]!.GetValue<string>());
        Assert.True(Directory.Exists(tree));
        Assert.Null(await SessionLocksTests.LockOfAsync(_repository, tree));
    }

    /// <summary>The stand-in harness: it takes the quest, works until the test releases it, then closes the quest done.</summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "agent.mjs");
        File.WriteAllText(script, """
            import { existsSync } from 'node:fs';
            if (process.argv.includes('--version')) { console.log('stub-harness 1.0.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }
            const [release] = process.argv.slice(2);
            const respond = (body) => fetch(`${process.env.DAORIS_SERVICE_URL}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            await respond({ action: 'take' });
            while (!existsSync(release)) await new Promise((done) => setTimeout(done, 50));
            await respond({ action: 'done', reason: 'landed' });
            """);
        return script;
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        var patience = DateTime.UtcNow + Poll.Patience;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < patience, "the condition never held");
            await Task.Delay(50);
        }
    }

    private void Git(params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = _repository, UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(TestGit.Past(info))!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
    }
}
