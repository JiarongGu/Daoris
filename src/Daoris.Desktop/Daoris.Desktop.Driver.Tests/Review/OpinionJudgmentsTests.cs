using System.Diagnostics;
using System.Text.RegularExpressions;
using Daoris.Driver;
using Daoris.Testing;
using Xunit.Abstractions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The owed opinions under the watch, judged beside the looks: an owed opinion is judged again only where an input its
/// judgment reads moved, and every judgment of one look shares one read of the quests. Six owed landing opinions, each settled on
/// its own session's tree, among a thousand closed quests: the first look judges all six, a look over nothing moved judges none,
/// starts no git and reads the quest list at most once, and a moved input judges that opinion alone again: a commit in its tree,
/// its quest reopened, its run's opinion node blocked, its delivery written, its recheck's reviewer ending, its due replaced, an
/// update's drain (every opinion), a reading pass's minutes passing, five minutes on (every opinion), a gate read unread, and a pass
/// begun that ended writing nothing.
/// </summary>
/// <remarks>
/// Real git for the trees, the local host stood in (<see cref="OpinionStandIn"/>), and a driver of its own each look over the
/// watch's one set of running sessions, as the watch builds them. Each look prints what it took: a bench as much as a test, held to
/// counts, never to times, since a neighbour's load moves those.
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

    // The watch's running sessions, which every look's driver is handed, their stamps aging on a clock moved by hand.
    private readonly RunningSessions _running;
    private readonly MovedClock _clock = new() { VirtualTimestamp = true };

    // Where set, a pass asked fails as a reviewer's host that does not open it; otherwise a pass asked is the test's failure.
    private bool _reviewerDown;
    private int _passesAsked;

    public OpinionJudgmentsTests(ITestOutputHelper output)
    {
        _output = output;
        _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "opinion-judgments", Guid.NewGuid().ToString("N")[..8]);
        _home = Path.Combine(_scratch, "daoris-home");
        Directory.CreateDirectory(_home);
        File.WriteAllText(Command, "");
        _running = new RunningSessions(_clock);
    }

    public void Dispose()
    {
        GitSeed.Discard(_scratch);
    }

    [Fact]
    public async Task A_look_over_owed_opinions_nothing_moved_for_judges_none_starts_no_git_and_reads_the_quests_once()
    {
        var (host, _) = await OwedAsync();

        // Every look is taken, and said, before any is held, so the bench's line is whole either way.
        var first = await LookAsync(host, "first look");
        var later = new[] { await LookAsync(host, "second look"), await LookAsync(host, "third look") };

        Assert.Equal(Owed, first.Judged);
        Assert.All(Enumerable.Range(1, Owed), at => Assert.Equal(OpinionGateStates.Settled, Gate(at)?.State));
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
        await LookAsync(host, "first look");
        await LookAsync(host, "nothing moved");

        // Its session's commit.
        await CommitAsync(trees[3], "later.ts", "more", "a commit nobody read");
        var committed = await LookAsync(host, "a commit in s3's tree");
        Assert.Equal(1, committed.Judged);
        Assert.Equal((OpinionGateStates.CommitsSince, 1), (Gate(3)?.State, Gate(3)?.Since));
        Assert.Equal(OpinionGateStates.Settled, Gate(2)?.State);

        // Its quest's state: reopened, so the opinion owed on its done closes.
        host.Quest("q4", "Open");
        var reopened = await LookAsync(host, "q4 reopened");
        Assert.Equal(1, reopened.Judged);
        Assert.DoesNotContain(new OpinionDues(_home).Open(), due => due.Session == "s4");

        // Its run's node: the opinion node blocked again.
        Node("q5", NodeStates.Blocked);
        var blocked = await LookAsync(host, "q5's opinion node blocked");
        Assert.Equal(1, blocked.Judged);

        // Its delivery's record written.
        new OpinionDeliveries(_home).Rechecked("op2", null, "none");
        Assert.Equal(1, (await LookAsync(host, "s2's delivery written")).Judged);

        // An update's drain, which every judgment reads, begun and lifted.
        Assert.Equal(Owed - 1, (await LookAsync(host, "an update drains", draining: true)).Judged);
        Assert.Equal(Owed - 1, (await LookAsync(host, "the drain lifted")).Judged);

        var still = await LookAsync(host, "nothing moved again");
        Assert.Equal((0, 0), (still.Judged, still.GitStarts));
    }

    [Fact]
    public async Task A_recheck_s_reviewer_ending_judges_its_opinion_again()
    {
        var (host, _) = await OwedAsync(2);
        // s1's findings were answered and rechecked: the recheck's reviewer still reads.
        host.Session("r-rc1", "working").Set("r-rc1", "quest", null);
        host.Set("r-rc1", "opinion", "rc1");
        new OpinionDeliveries(_home).Rechecked("op1", "rc1", null);
        await LookAsync(host, "first look");
        Assert.Equal(0, (await LookAsync(host, "nothing moved")).Judged);

        host.Move("r-rc1", "completed");
        Assert.Equal(1, (await LookAsync(host, "the recheck's reviewer ended")).Judged);
    }

    [Fact]
    public async Task A_due_replaced_in_place_judges_its_opinion_again()
    {
        var (host, _) = await OwedAsync(2);
        await LookAsync(host, "first look");
        Assert.Equal(0, (await LookAsync(host, "nothing moved")).Judged);

        // Owed again for the same session and occasion, as a press does: the due is replaced where it stood.
        var dues = new OpinionDues(_home);
        var owed = dues.Open().Single(due => due.Session == "s2");
        dues.Due(owed with { At = owed.At.AddSeconds(1) });
        Assert.Equal(1, (await LookAsync(host, "s2's due replaced")).Judged);
    }

    [Fact]
    public async Task A_reading_pass_is_judged_again_once_its_minutes_pass_and_every_opinion_five_minutes_on()
    {
        var (host, _) = await OwedAsync(2);
        // s1's pass still reads, its minutes running out in two.
        host.Opinion("op1", "s1", state: OpinionViews.Reading, weights: null, due: _clock.GetUtcNow().AddMinutes(2));
        Assert.Equal(2, (await LookAsync(host, "first look")).Judged);
        Assert.Equal(OpinionGateStates.Reading, Gate(1)?.State);

        _clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(0, (await LookAsync(host, "a minute on")).Judged);

        // Out of its minutes, the host reads it out of time though no record moved.
        _clock.Advance(TimeSpan.FromSeconds(90));
        Assert.Equal(1, (await LookAsync(host, "two and a half minutes on: s1's pass out of its minutes")).Judged);

        // s2's judgment is five minutes old; s1's, whose pass's end is past and bounds nothing now, two and a half.
        _clock.Advance(LookFacts.Refresh - TimeSpan.FromSeconds(150));
        Assert.Equal(1, (await LookAsync(host, "five minutes on")).Judged);
    }

    [Fact]
    public async Task A_gate_read_unread_is_judged_again_at_the_next_look()
    {
        var (host, _) = await OwedAsync(2);
        await LookAsync(host, "first look");

        // s1's delivery written while the host does not answer for the opinions.
        host.OpinionFailure = true;
        new OpinionDeliveries(_home).Rechecked("op1", null, "none");
        Assert.Equal(1, (await LookAsync(host, "s1 judged while the opinions do not read")).Judged);
        Assert.Equal(OpinionGateStates.Unread, Gate(1)?.State);

        host.OpinionFailure = false;
        Assert.Equal(1, (await LookAsync(host, "the opinions read again")).Judged);
        Assert.Equal(OpinionGateStates.Settled, Gate(1)?.State);
    }

    [Fact]
    public async Task A_pass_begun_that_ended_writing_nothing_is_judged_again()
    {
        var (host, _) = await OwedAsync(2);
        // No pass on s2's work is kept, so one is owed; its reviewer's host opens none, and nothing is written.
        var gate = new OpinionGates(_home).PathOf("q2")!;
        File.Delete(gate);
        _reviewerDown = true;
        await LookAsync(host, "first look");
        Assert.NotNull(_running.Facts.PeekAct($"s2/{OpinionRules.Landing}"));

        await LookAsync(host, "s2's pass begun, and ended writing nothing");
        Assert.Equal(1, _passesAsked);
        Assert.False(File.Exists(gate));

        await LookAsync(host, "the next look");
        // Owed again: found by this look's judgment, or by the last one's where the pass had ended first, and begun here.
        Assert.True(_running.Facts.PeekAct($"s2/{OpinionRules.Landing}") is not null || _passesAsked == 2,
            $"the pass that ended writing nothing is owed no more: {_passesAsked} asked");
    }

    // ——— one look and what it took

    private sealed record Took(int Judged, int GitStarts, int QuestReads, int OpinionReads, int AskReads);

    /// <summary>
    /// One look's owed opinions beside it, waited out, by a driver of its own over the watch's running sessions, as the watch builds
    /// one per look: how many it judged (each judgment its own <c>~opinions</c> span), the git it started, and what it asked of the
    /// host, printed.
    /// </summary>
    private async Task<Took> LookAsync(OpinionStandIn host, string name, bool draining = false)
    {
        var driver = Driver(host, draining);
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
    private OpinionGateState? Gate(int at) =>
        (_running.Facts.Opinion($"s{at}/{OpinionRules.Landing}") as OpinionFact)?.Judged?.Gate;

    // ——— the machine and the fixtures

    private string Command => Path.Combine(_home, "agent-here");

    private Daoris.Driver.Driver Driver(OpinionStandIn host, bool draining)
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
        return new Daoris.Driver.Driver(
            host.Client(), draining ? InstallUpdate.Drained(Config()) : Config(), set, _home,
            harnesses: new HarnessRoster(set, DaorisHome.Entry(_home, HomeEntry.Harnesses)), running: _running)
        {
            // An opinion here is asked and settled already, or its reviewer's host is down: any other pass asked fails the test.
            Passes = (ask, _, _) =>
            {
                Interlocked.Increment(ref _passesAsked);
                return Task.FromException<OpinionPassRun>(_reviewerDown
                    ? new DriverException("the reviewer's host did not open the pass")
                    : new InvalidOperationException($"no pass is owed here, yet one was asked for {ask.Working}"));
            },
        };
    }

    private DriverConfig Config() => DriverConfig.Load(DaorisHome.Entry(_home, HomeEntry.DriverConfig));

    /// <summary>
    /// <see cref="Owed"/> sessions, each done on its quest in its own tree of `reports` under a graph whose opinion it has begun,
    /// owed a landing opinion asked and settled with nothing raised, among <see cref="Closed"/> closed quests. Returns the
    /// host standing in and each session's tree by its number.
    /// </summary>
    private async Task<(OpinionStandIn Host, IReadOnlyDictionary<int, string> Trees)> OwedAsync(int owed = Owed)
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
        for (var at = 1; at <= owed; at++)
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
