using System.Diagnostics;
using System.Text.RegularExpressions;
using Daoris.Driver;
using Xunit.Abstractions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The owed opinions under the watch, judged beside the looks: an owed opinion is judged again only where an input its
/// judgment reads moved, and every judgment of one look shares one read of the quests. Six owed landing opinions, each settled on
/// its own session's tree, among a thousand closed quests: the first look judges all six, a look over nothing moved judges none,
/// starts no git and reads the quest list at most once, and a moved input (a commit in one tree, one quest reopened, one run's
/// opinion node blocked) judges that opinion alone again.
/// </summary>
/// <remarks>
/// Real git for the trees, the local host stood in (<see cref="OpinionStandIn"/>). Each look prints what it took: a bench as much
/// as a test, held to counts, never to times, since a neighbour's load moves those.
/// </remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
public sealed class OpinionJudgmentsTests : IDisposable
{
    private const int Owed = 6;
    private const int Closed = 1000;
    private static readonly DateTimeOffset Moment = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);

    private readonly ITestOutputHelper _output;
    private readonly string _scratch;
    private readonly string _home;

    public OpinionJudgmentsTests(ITestOutputHelper output)
    {
        _output = output;
        _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "opinion-judgments", Guid.NewGuid().ToString("N")[..8]);
        _home = Path.Combine(_scratch, "daoris-home");
        Directory.CreateDirectory(_home);
        File.WriteAllText(Command, "");
    }

    public void Dispose()
    {
        GitSeed.Discard(_scratch);
    }

    [Fact]
    public async Task A_look_over_owed_opinions_nothing_moved_for_judges_none_starts_no_git_and_reads_the_quests_once()
    {
        var (host, _) = await OwedAsync();
        var driver = Driver(host);

        // Every look is taken, and said, before any is held, so the bench's line is whole either way.
        var first = await LookAsync(driver, host, "first look");
        var later = new[] { await LookAsync(driver, host, "second look"), await LookAsync(driver, host, "third look") };

        Assert.Equal(Owed, first.Judged);
        Assert.All(Enumerable.Range(1, Owed), at => Assert.Equal(OpinionGateStates.Settled, Gate(driver, at)?.State));
        Assert.True((first.QuestReads, first.AskReads) is (<= 1, <= 1), $"the first look read the quest list {first.QuestReads} times and the ask {first.AskReads}");
        Assert.All(later, again =>
        {
            Assert.Equal((0, 0, 0, 0), (again.Judged, again.GitStarts, again.OpinionReads, again.AskReads));
            Assert.True(again.QuestReads <= 1, $"a look read the quest list {again.QuestReads} times");
        });
    }

    [Fact]
    public async Task A_moved_input_judges_that_owed_opinion_alone_again()
    {
        var (host, trees) = await OwedAsync();
        var driver = Driver(host);
        await LookAsync(driver, host, "first look");
        await LookAsync(driver, host, "nothing moved");

        // Its session's commit.
        await CommitAsync(trees[3], "later.ts", "more", "a commit nobody read");
        var committed = await LookAsync(driver, host, "a commit in s3's tree");
        Assert.Equal(1, committed.Judged);
        Assert.Equal((OpinionGateStates.CommitsSince, 1), (Gate(driver, 3)?.State, Gate(driver, 3)?.Since));
        Assert.Equal(OpinionGateStates.Settled, Gate(driver, 2)?.State);

        // Its quest's state: reopened, so the opinion owed on its done closes.
        host.Quest("q4", "Open");
        var reopened = await LookAsync(driver, host, "q4 reopened");
        Assert.Equal(1, reopened.Judged);
        Assert.DoesNotContain(new OpinionDues(_home).Open(), due => due.Session == "s4");

        // Its run's node: the opinion node blocked again.
        Node("q5", NodeStates.Blocked);
        var blocked = await LookAsync(driver, host, "q5's opinion node blocked");
        Assert.Equal(1, blocked.Judged);

        var still = await LookAsync(driver, host, "nothing moved again");
        Assert.Equal((0, 0), (still.Judged, still.GitStarts));
    }

    // ——— one look and what it took

    private sealed record Took(int Judged, int GitStarts, int QuestReads, int OpinionReads, int AskReads);

    /// <summary>
    /// One look's owed opinions beside it, waited out: how many it judged (each judgment its own <c>~opinions</c> span), the git it
    /// started, and what it asked of the host, printed.
    /// </summary>
    private async Task<Took> LookAsync(Daoris.Driver.Driver driver, OpinionStandIn host, string name)
    {
        var before = new Dictionary<string, int>(host.Asked, StringComparer.Ordinal);
        // A clock that moves a tenth of a second at every read, so no judgment's span falls under the line's floor.
        long now = 0;
        var stages = new LookStages(clock: () => Interlocked.Add(ref now, 100));
        var watch = Stopwatch.StartNew();
        int git;
        string started;
        using (var starts = GitLaunch.Count())
        {
            driver.Gate.OpinionsBeside(new Snapshot([], [], []), [], stages, CancellationToken.None);
            await driver.Running.SettledAsync();
            git = starts.Starts.Count;
            // Each git command by its first words, with how often it ran and its milliseconds in all.
            started = string.Join(", ", starts.Starts
                .GroupBy(start => string.Join(' ', GitLaunch.Command(start.Arguments).Take(2)))
                .Select(each => $"{each.Key} x{each.Count()} {each.Sum(start => GitLaunched.Ms(start.WholeTicks)):0} ms"));
        }

        var line = stages.Take() ?? "";
        var span = Regex.Match(line, @"~opinions=\d+(?:x(\d+))?");
        var judged = !span.Success ? 0 : span.Groups[1].Success ? int.Parse(span.Groups[1].Value) : 1;
        var asked = host.Asked.Select(each => (each.Key, Count: each.Value - before.GetValueOrDefault(each.Key))).Where(each => each.Count > 0).ToList();
        int Of(Func<string, bool> door) => asked.Where(each => door(each.Key)).Sum(each => each.Count);
        var took = new Took(judged, git, Of(door => door == "GET /api/quests"), Of(door => door.StartsWith("GET /api/opinions/", StringComparison.Ordinal)),
            Of(door => door.StartsWith("GET /api/asks/", StringComparison.Ordinal)));
        _output.WriteLine($"{name}: {watch.ElapsedMilliseconds} ms, {took.Judged} judged, {took.GitStarts} git starts ({started}); asked "
                          + string.Join(", ", asked.OrderBy(each => each.Key, StringComparer.Ordinal).Select(each => $"{each.Key} x{each.Count}")));
        return took;
    }

    /// <summary>The gate the last judgment of <c>s{at}</c>'s owed landing opinion kept.</summary>
    private static OpinionGateState? Gate(Daoris.Driver.Driver driver, int at) =>
        (driver.Running.Facts.Opinion($"s{at}/{OpinionRules.Landing}") as OpinionFact)?.Judged?.Gate;

    // ——— the machine and the fixtures

    private string Command => Path.Combine(_home, "agent-here");

    private Daoris.Driver.Driver Driver(OpinionStandIn host)
    {
        var adapters = new Dictionary<string, Provider>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in new[] { "claude-code", "claude-code-acp", "codex-acp" })
        {
            var real = ProviderRegistry.Built().AgentNamed(name)!;
            adapters[name] = TestDoors.Door(name, new TestAgent(
                Binary: [Command], VersionArguments: [], ProfileVariable: real.Home.Variable, AccountOf: real.AccountOf, ProbeByPresence: true,
                Product: real.Product, Maker: real.Maker, RunsInOwnHome: true));
        }

        var set = TestDoors.Registry(adapters);
        return new Daoris.Driver.Driver(host.Client(), Config(), set, _home, harnesses: new HarnessRoster(set, DaorisHome.Entry(_home, HomeEntry.Harnesses)))
        {
            // Every opinion here is asked and settled already: a pass asked is a failure of the test.
            Passes = (ask, _, _) => throw new InvalidOperationException($"no pass is owed here, yet one was asked for {ask.Working}"),
        };
    }

    private DriverConfig Config() => DriverConfig.Load(DaorisHome.Entry(_home, HomeEntry.DriverConfig));

    /// <summary>
    /// <see cref="Owed"/> sessions, each done on its quest in its own tree of `reports` under a graph whose opinion it has begun,
    /// owed a landing opinion asked and settled with nothing raised, among <see cref="Closed"/> closed quests. Returns the
    /// host standing in and each session's tree by its number.
    /// </summary>
    private async Task<(OpinionStandIn Host, IReadOnlyDictionary<int, string> Trees)> OwedAsync()
    {
        var made = Stopwatch.StartNew();
        (DriverConfig.Empty with
        {
            Actors = StaffedRoles.On("claude-code-acp"),
            Opinions = new Dictionary<string, OpinionRule>(StringComparer.OrdinalIgnoreCase)
            {
                ["reports"] = new OpinionRule(["codex-acp"], [OpinionRules.Landing], false),
            },
        }).Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));

        var root = Path.Combine(_scratch, "reports");
        Directory.CreateDirectory(root);
        await GitAsync(root, "init", "--quiet", "-b", "main");
        await CommitAsync(root, "README.md", "# reports", "first");
        var line = await HeadAsync(root);

        var host = new OpinionStandIn();
        for (var at = 1; at <= Closed; at++) host.Quest($"c{at}");
        var trees = new Dictionary<int, string>();
        for (var at = 1; at <= Owed; at++)
        {
            Govern($"q{at}");
            var tree = (await new SessionTrees(_home).OpenAsync(root, "reports", $"work{at}")).Path;
            await CommitAsync(tree, $"report{at}.ts", $"the work of s{at}", $"the work of s{at}");
            host.Session($"s{at}", "completed", tree: tree).Quest($"q{at}");
            host.Set($"s{at}", "quest", $"q{at}");
            Assert.Equal([OpinionRules.Landing], OpinionLook.Concluded(_home, Config(), $"s{at}",
                new QuestView($"q{at}", "ask #a1", "reports", $"Work q{at}", "", "Done"), "Done", "completed", tree));
            var tip = await HeadAsync(tree);
            new OpinionGates(_home).Asked($"q{at}", "reports", new OpinionAskKept(Moment, OpinionRules.Landing, $"s{at}", tip)
            {
                Opinion = $"op{at}", By = OpinionAskers.Look, Reviewer = "codex-acp",
            });
            host.Opinion($"op{at}", $"s{at}", weights: [], tip: tip, @base: line);
            trees[at] = tree;
        }

        _output.WriteLine($"fixture: {made.ElapsedMilliseconds} ms");
        return (host, trees);
    }

    /// <summary>Run <paramref name="run"/> on `reports` bound to a graph whose <c>opinion</c> reads at the landing, its record past the opinion's start.</summary>
    private void Govern(string run)
    {
        var digest = RunHome.SaveGraph(_home, """
            {"schemaVersion":2,"entries":["claimed"],"end":"landing",
             "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                      {"id":"opinion","kind":"opinion","version":1,"reach":"observed","options":{"required":false,"steps":false}},
                      {"id":"landing","kind":"land","version":1,"reach":"performed","options":{"method":"merge"}}],
             "wires":[{"id":"c>o","kind":"after","from":"claimed","to":"opinion"},{"id":"o>l","kind":"after","from":"opinion","to":"landing"}]}
            """, workflow: "governed");
        Assert.True(new WorkflowRunFiles(_home).Bind(new WorkflowRunBinding(run, "reports", "work", "governed", WorkflowLevels.Repository, Moment)
        {
            Version = 1, Digest = digest,
        }));
        Node(run, NodeStates.Active);
    }

    /// <summary>The record of <paramref name="run"/>'s unit, its <c>opinion</c> node at <paramref name="opinion"/>.</summary>
    private void Node(string run, string opinion)
    {
        var unit = RunUnits.IdOf(run);
        new NodeRecordFiles(_home, () => true).Write(new NodeRecord(unit)
        {
            Statuses = new Dictionary<string, NodeStatus>
            {
                ["claimed"] = new(unit, "claimed", NodeStates.Reached),
                ["opinion"] = new(unit, "opinion", opinion),
            },
        });
    }

    private static async Task<string> HeadAsync(string tree) => (await GitAsync(tree, "rev-parse", "HEAD")).Trim();

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
}
