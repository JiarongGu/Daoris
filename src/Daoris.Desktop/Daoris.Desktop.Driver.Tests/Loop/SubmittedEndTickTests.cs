using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-CLOSE1h5 (D173's CLOSELAND1 note; the fabric design §E5), through a REAL tick: a quest in a closure graph is never done
/// before the graph's end, so its session ends at its agent's <c>ready</c> with the quest still taken. The driver's own conclusion of
/// the run reads that submission as the done it stands for: the session ends completed, never parked as though it waited on the
/// person, and is due under <i>Accept automatically</i>; the look then lands its branch, and the quest stays taken at its submission.
/// </summary>
/// <remarks>
/// Over the real driver, client and lander, with the service's doors and the agent standing in: the stand-in marks the session that
/// took its quest (STANDDOWN2's <c>took</c>), as the service's connector does, since that is what parked such a session before.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class SubmittedEndTickTests : IDisposable
{
    private readonly string _scratch;
    private readonly string _home;
    private readonly string _repository;

    public SubmittedEndTickTests()
    {
        _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "submitted-end", Guid.NewGuid().ToString("N")[..8]);
        _home = Path.Combine(_scratch, "daoris-home");
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_scratch, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "ready@example.test");
        Git("config", "user.name", "CLOSE1h5");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");
    }

    public void Dispose() => GitSeed.Discard(_scratch);

    /// <summary>
    /// The row's proof: the first run's conclusion of a bound quest whose agent took it, committed its work and said <c>ready</c>.
    /// </summary>
    [Fact]
    public async Task A_session_ending_at_its_ready_is_due_through_the_drivers_conclusion_and_its_branch_lands()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        service.Took = true;
        var config = Config();

        await Driver(service, config).RunOnceAsync().WaitAsync(Poll.Patience);

        var record = service.Session("s1");
        Assert.Equal("completed", record["state"]!.GetValue<string>());
        Assert.Equal("Taken", service.Status("q1"));
        var entry = new AutoLandings(_home).Of("s1");
        Assert.NotNull(entry);
        Assert.Null(entry.Closed);

        var lander = new AutoLander(
            _home, new ServiceAutoLandingWorld(new ServiceClient(service.Url, null)), new SessionTrees(_home),
            new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)));
        var (chosen, said) = await lander.ChooseAsync(config);
        Assert.Single(chosen);
        var landed = await lander.LandAsync(chosen);

        Assert.Equal(AutoLandingCode.Landed, new AutoLandings(_home).Of("s1")!.Last!.Code);
        var branch = new LandedBranches(_home).Landing("s1");
        Assert.NotNull(branch);
        Assert.Contains(branch.Branch, Git("branch", "--list", branch.Branch), StringComparison.Ordinal);
        Assert.Equal("Taken", service.Status("q1"));
        Assert.Empty(said);
        Assert.Single(landed);
    }

    private DriverConfig Config()
    {
        var config = (DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Trees = ["engine"],
            Adapter = "stub",
            TestBound = 1,
            PollSeconds = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["stub"] = ["node", Agent()] },
        }).WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", AutoAccept: true));
        // The look reads the rule again from the home's file just before each landing (FABRIC-END7's review).
        config.Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        return config;
    }

    private Daoris.Driver.Driver Driver(AskAndWaitTickTests.StandIn service, DriverConfig config)
    {
        var adapters = AdapterSet.Built();
        return new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home, processes: new SessionProcesses(),
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
    }

    /// <summary>
    /// The stand-in harness: as an agent in a closure graph does, it takes its quest, commits its work in its tree, and says
    /// <c>ready</c> with the commit, never <c>done</c>.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_scratch, "agent.mjs");
        File.WriteAllText(script, """
            import { execFileSync } from 'node:child_process';
            import { writeFileSync } from 'node:fs';
            if (process.argv.includes('--version')) { console.log('stub-harness 1.0.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }
            const respond = (body) => fetch(`${process.env.DAORIS_SERVICE_URL}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            const git = (...args) =>
              execFileSync('git', ['-c', 'user.email=ready@example.test', '-c', 'user.name=Stub', ...args], { encoding: 'utf8' });
            await respond({ action: 'take' });
            writeFileSync('note.txt', 'the note field\n');
            git('add', '-A');
            git('commit', '-qm', 'the note field');
            await respond({ action: 'ready', reason: 'the note field is in', commit: git('rev-parse', 'HEAD').trim() });
            """);
        return script;
    }

    private string Git(params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = _repository, UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(TestGit.Past(info))!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }
}
