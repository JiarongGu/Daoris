using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// XAGENT1f (D155 points 2, 9 and 10; the second-agent design §2.1, §7, §8.1, §8.5): the second opinions owed at a look and the
/// person's presses, over real git. A done in a tree of its own under a rule that reads it is owed one; the look asks the pass
/// beside itself, from where the work leaves the line to its tip, keeps it at the gate the moment the host opens it, and sits the
/// chain's set-up step while an agent is still at work on it; once settled, it asks nothing more. The presses keep the person's
/// answer bound to its commit, ask a pass for the next look among the rule's reviewers only, and refuse what nothing waits on.
/// </summary>
/// <remarks>
/// Real git for the tree, the local host stood in (<see cref="OpinionStandIn"/>), and the pass stood in (<see cref="Daoris.Driver.Driver.Passes"/>):
/// no agent runs and nothing reaches a network.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class OpinionLookTests : IDisposable
{
    private readonly string _scratch;
    private readonly string _home;
    private readonly List<(OpinionPassAsk Ask, ReviewerChoice Choice)> _passes = [];

    public OpinionLookTests()
    {
        _scratch = Path.Combine(RepoRoot(), "_fixtures", "opinion-look", Guid.NewGuid().ToString("N")[..8]);
        _home = Path.Combine(_scratch, "daoris-home");
        Directory.CreateDirectory(_home);
        File.WriteAllText(Command, "");
    }

    public void Dispose()
    {
        GitSeed.Discard(_scratch);
    }

    [Fact]
    public async Task The_look_asks_an_owed_pass_beside_itself_keeps_it_at_the_gate_and_sits_the_set_up_step_until_settled()
    {
        var (host, tree, line) = await DoneAsync(required: false);
        host.Quest("q2", "Open", parent: "q1", setUpIn: "local");
        var driver = Driver(host);

        var (sits, passes) = await driver.OpinionsAsync(new Snapshot([], [], []), [], CancellationToken.None);
        await driver.Running.SettledAsync();

        Assert.Equal(1, passes);
        Assert.StartsWith("set-up step `#q2` waits for a second opinion on the work it shows", sits["q2"].Reason);
        Assert.Equal(SittingFact.Landing, sits["q2"].Fact.Code);
        var (ask, choice) = Assert.Single(_passes);
        Assert.Equal((OpinionRules.Landing, "s1", "reports", line, await HeadAsync(tree)), (ask.Occasion, ask.Working, ask.Repository, ask.Base, ask.Tip));
        Assert.Equal(["q1", "q2"], ask.Quests.Select(quest => quest.Id));
        Assert.Equal(("codex-acp", ReviewerLabels.AnotherMaker), (choice.Reviewer, choice.Label));
        var kept = Assert.Single(new OpinionGates(_home).Read("q1", "reports").Asks);
        Assert.Equal(("op1", OpinionAskers.Look, "codex-acp"), (kept.Opinion, kept.By, kept.Reviewer));

        // Settled: nothing more is asked, and the set-up step goes on.
        var (after, again) = await driver.OpinionsAsync(new Snapshot([], [], []), [], CancellationToken.None);
        await driver.Running.SettledAsync();
        Assert.Equal(0, again);
        Assert.Empty(after);
        Assert.Single(_passes);
    }

    /// <summary>
    /// 🔴 LIMITRESUME1j (D206; the limit-resume design §3.6): an opinion asked on a line that ran on two makers refuses a reviewer of
    /// either as another maker's. s1's work went on with Claude Code from s0, cut off by its limit on Codex; the look judges the work's
    /// families from every part of the line, so a listed reviewer of either maker reads it only as the same agent, fresh, and says
    /// that no other maker's agent could.
    /// </summary>
    [Theory]
    [InlineData("codex-acp")]
    [InlineData("claude-code")]
    public async Task An_opinion_asked_on_a_line_that_ran_on_two_makers_refuses_a_reviewer_of_either(string reviewer)
    {
        var (host, _, _) = await DoneAsync(required: false, reviewers: [reviewer]);
        host.Session("s0", "failed", adapter: "codex-acp");
        host.Set("s1", "from", "s0");
        host.Set("s1", "fromWhy", HandOverWhy.Limit);
        var driver = Driver(host);

        await driver.OpinionsAsync(new Snapshot([], [], []), [], CancellationToken.None);
        await driver.Running.SettledAsync();

        var (_, choice) = Assert.Single(_passes);
        Assert.Equal((reviewer, ReviewerLabels.SameAgent, ReviewerUnavailable.NotIndependent), (choice.Reviewer, choice.Label, choice.Code));
        Assert.Equal(["OpenAI", "Anthropic"], choice.Working.Select(family => family.Maker));
        Assert.Equal(ReviewerLabels.SameAgent, Assert.Single(new OpinionGates(_home).Read("q1", "reports").Asks).Label);
    }

    [Fact]
    public async Task The_presses_keep_the_persons_answer_and_ask_a_pass_among_the_rules_reviewers_for_the_next_look()
    {
        var (host, tree, _) = await DoneAsync(required: true);
        var presses = new OpinionPresses(_home, host.Client());

        var named = await presses.AskAsync("s1", "dsh", sameAgent: false, words: null, "asked");
        Assert.False(named.Done);
        Assert.StartsWith("`dsh` is not among `reports`'s reviewers (`codex-acp`)", named.Message);

        var asked = await presses.AskAsync("s1", null, sameAgent: true, words: "fresh eyes", "asked");
        Assert.True(asked.Done, asked.Message);
        Assert.Equal("A second opinion on session s1's work is asked: the same agent, in a fresh conversation, reads it from the driver's next look.", asked.Message);
        var request = Assert.Single(new OpinionGates(_home).Read("q1", "reports").Requests);
        Assert.Equal(("s1", "asked", true, "fresh eyes"), (request.Session, request.Occasion, request.SameAgent, request.Words));

        var anyway = await presses.AnywayAsync("s1", "a comment only", ReviewDoors.Terminal);
        Assert.True(anyway.Done, anyway.Message);
        Assert.Equal("You went on without a settled second opinion before one was asked: \"a comment only\".", anyway.Message);
        var word = Assert.Single(new OpinionGates(_home).Read("q1", "reports").Person);
        Assert.Equal((OpinionPersonSaid.Anyway, await HeadAsync(tree), ReviewDoors.Terminal), (word.Said, word.Tip, word.Door));

        var twice = await presses.MyselfAsync("s1", null, ReviewDoors.Screen);
        Assert.False(twice.Done);
        Assert.StartsWith("nothing waits for a second opinion on session s1's work:", twice.Message);

        var stop = await presses.StopAsync("nothing1", new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)));
        Assert.Equal((false, "there is no second opinion `nothing1` on this machine."), (stop.Done, stop.Message));
    }

    /// <summary>
    /// XAGENT1g3: the look keeps each owed landing opinion's gate for the attention band's route, as that route judged it before
    /// (<see cref="OpinionPresses.GateAsync"/>): a required opinion none could be had for, and commits nobody read where the work
    /// lands by itself, each with its <c>auto</c>. A <c>steps</c> opinion is not kept.
    /// </summary>
    [Fact]
    public async Task The_look_keeps_a_landing_opinions_gate_as_the_route_judged_it_and_not_a_steps_one()
    {
        var (host, tree, _) = await DoneAsync(required: true, on: [OpinionRules.Landing, OpinionRules.Steps]);
        new OpinionGates(_home).Asked("q1", "reports", new OpinionAskKept(DateTimeOffset.UtcNow, OpinionRules.Landing, "s1", await HeadAsync(tree))
        {
            Code = ReviewerUnavailable.NoReviewer, By = OpinionAskers.Look,
        });
        var driver = Driver(host);
        var judged = new List<OpinionJudged>();

        await driver.OpinionsAsync(new Snapshot([], [], []), [], CancellationToken.None, judged);
        await driver.Running.SettledAsync();

        var kept = Assert.Single(judged);
        var route = (await new OpinionPresses(_home, host.Client()).GateAsync("s1")).Gate!;
        Assert.Equal((route.Opinion.State, route.Opinion.Required, route.Opinion.Says), (kept.Gate.State, kept.Gate.Required, kept.Gate.Says));
        Assert.Equal((OpinionGateStates.Unavailable, true), (kept.Gate.State, kept.Gate.Required));
        Assert.Equal((route.Process?.Landing ?? LandingRules.Choose(Config(), "reports", "work")).Rule.AutoAccept, kept.Auto);
        Assert.Equal(("s1", "q1", "reports"), (kept.Session, kept.Quest, kept.Repository));
    }

    /// <summary>
    /// XAGENT1g3: commits made after the opinion read are kept as the route says them, the count and the sentence the same.
    /// </summary>
    [Fact]
    public async Task The_kept_gate_of_commits_since_the_opinion_read_says_what_the_route_says()
    {
        var (host, tree, _) = await DoneAsync(required: false);
        var driver = Driver(host);
        await driver.OpinionsAsync(new Snapshot([], [], []), [], CancellationToken.None);
        await driver.Running.SettledAsync();
        await CommitAsync(tree, "later.ts", "more", "a commit nobody read");
        var judged = new List<OpinionJudged>();

        await driver.OpinionsAsync(new Snapshot([], [], []), [], CancellationToken.None, judged);
        await driver.Running.SettledAsync();

        var kept = Assert.Single(judged);
        var route = (await new OpinionPresses(_home, host.Client()).GateAsync("s1")).Gate!;
        Assert.Equal(OpinionGateStates.CommitsSince, kept.Gate.State);
        Assert.Equal((route.Opinion.State, route.Opinion.Since, route.Opinion.Says), (kept.Gate.State, kept.Gate.Since, kept.Gate.Says));
    }

    /// <summary>
    /// FABRIC-CLOSE1h15 (D173's note): an opinion owed on work whose branch is made but whose push its automatic landing holds back is
    /// asked at the look, as the gate's sentence says, never closed as landed first: the one owed by itself, and the one the person's
    /// own ask made owed after its due had closed. Once no automatic landing holds that push back, the look closes it as landed and
    /// asks nothing more.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_opinion_owed_on_work_whose_push_is_held_back_is_asked_at_the_look_and_not_closed_as_landed(bool person)
    {
        var (host, tree, _) = await DoneAsync(required: false);
        var tip = await HeadAsync(tree);
        var dues = new OpinionDues(_home);
        if (person)
        {
            dues.Close("s1", OpinionRules.Landing, OpinionDueClosed.Off, DateTimeOffset.UtcNow);
            var asked = await new OpinionPresses(_home, host.Client()).AskAsync("s1", null, sameAgent: false, words: null, "asked");
            Assert.True(asked.Done, asked.Message);
        }

        PushHeld(tree, tip);
        var driver = Driver(host);

        var (_, passes) = await driver.OpinionsAsync(new Snapshot([], [], []), [], CancellationToken.None);
        await driver.Running.SettledAsync();

        Assert.Equal(1, passes);
        var (ask, _) = Assert.Single(_passes);
        Assert.Equal(("s1", tip), (ask.Working, ask.Tip));
        Assert.Equal(person ? OpinionAskers.Person : OpinionAskers.Look, Assert.Single(new OpinionGates(_home).Read("q1", "reports").Asks).By);
        Assert.Equal("s1", Assert.Single(dues.Open()).Session);

        // Its push landed and its entry closed: the look closes it as landed, and asks nothing more.
        new AutoLandings(_home).Tried("s1", new AutoTry(DateTimeOffset.UtcNow, AutoLandingCode.Landed) { Tip = tip }, close: true);
        await driver.OpinionsAsync(new Snapshot([], [], []), [], CancellationToken.None);
        await driver.Running.SettledAsync();

        Assert.Empty(dues.Open());
        Assert.Equal(OpinionDueClosed.Landed, dues.All()[^1].Why);
        Assert.Single(_passes);
    }

    /// <summary>
    /// FABRIC-CUTOVER1d (the cutover design §4): a run bound to a graph version is asked its second opinion only once its record has begun its
    /// <c>opinion</c> node; while a check its graph places before the opinion waits, the look asks nothing, and once the pass begins the opinion
    /// the next look asks it.
    /// </summary>
    [Fact]
    public async Task A_graph_runs_opinion_is_not_asked_while_a_check_before_it_waits()
    {
        var (host, _, _) = await DoneAsync(required: false);
        var digest = RunHome.SaveGraph(_home, """
            {"schemaVersion":2,"entries":["claimed"],"end":"handed",
             "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"check","kind":"acme.check/run","version":1,"reach":"performed"},
                      {"id":"opinion","kind":"opinion","version":1,"reach":"observed"},{"id":"handed","kind":"handed","version":1,"reach":"observed"}],
             "wires":[{"id":"c>k","kind":"after","from":"claimed","to":"check"},{"id":"k>o","kind":"after","from":"check","to":"opinion"},
                      {"id":"o>h","kind":"after","from":"opinion","to":"handed"}]}
            """);
        Assert.True(new WorkflowRunFiles(_home).Bind(new WorkflowRunBinding("q1", "reports", "work", "docs", WorkflowLevels.Repository, DateTimeOffset.UtcNow)
        {
            Version = 1, Digest = digest,
        }));
        var unit = RunUnits.IdOf("q1");
        void Record(string opinion) => new NodeRecordFiles(_home, () => true).Write(new NodeRecord(unit)
        {
            Statuses = new Dictionary<string, NodeStatus>
            {
                ["claimed"] = new(unit, "claimed", NodeStates.Reached),
                ["check"] = new(unit, "check", NodeStates.Active),
                ["opinion"] = new(unit, "opinion", opinion),
            },
        });
        var driver = Driver(host);

        Record(NodeStates.Blocked);
        var (_, held) = await driver.OpinionsAsync(new Snapshot([], [], []), [], CancellationToken.None);
        await driver.Running.SettledAsync();
        Assert.Equal(0, held);
        Assert.Empty(_passes);

        Record(NodeStates.Active);
        var (_, asked) = await driver.OpinionsAsync(new Snapshot([], [], []), [], CancellationToken.None);
        await driver.Running.SettledAsync();
        Assert.Equal(1, asked);
        Assert.Single(_passes);
    }

    // ——— the machine and the fixtures

    private string Command => Path.Combine(_home, "agent-here");

    private sealed class Adapter(string name, HarnessToolchain? toolchain) : ISessionAdapter
    {
        public string Name => name;

        public HarnessToolchain? Toolchain => toolchain;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => new("unused");
    }

    /// <summary>The build's own agents by name, each present by a file look, as <c>OpinionRecheckTests</c> has them.</summary>
    private Daoris.Driver.Driver Driver(OpinionStandIn host)
    {
        var adapters = new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in new[] { "claude-code", "claude-code-acp", "codex-acp" })
        {
            var real = AdapterSet.Built().Resolve(name).Toolchain!;
            adapters[name] = new Adapter(name, new HarnessToolchain(
                Binary: [Command], VersionArguments: [], ProfileVariable: real.ProfileVariable, AccountOf: real.AccountOf, ProbeByPresence: true,
                // A test double whose own home is nobody's (OWNSPACE1a): the look's pass is this file's subject, not the account.
                Product: real.Product, Maker: real.Maker, RunsInOwnHome: true));
        }

        var set = new AdapterSet(adapters);
        return new Daoris.Driver.Driver(host.Client(), Config(), set, _home, harnesses: new HarnessRoster(set, DaorisHome.Entry(_home, HomeEntry.Harnesses)))
        {
            Passes = (ask, choice, _) =>
            {
                _passes.Add((ask, choice));
                // The host opens the opinion, and the reviewer raises nothing in what it read.
                ask.Opened?.Invoke("op1", "r-op1");
                host.Opinion("op1", "s1", weights: [], tip: ask.Tip, @base: ask.Base);
                return Task.FromResult(new OpinionPassRun("completed  second opinion op1", true) { Opinion = "op1", Session = "r-op1", State = "completed" });
            },
        };
    }

    private DriverConfig Config() => DriverConfig.Load(DaorisHome.Entry(_home, HomeEntry.DriverConfig));

    /// <summary>
    /// A session that worked in its own tree on `reports` and concluded on its quest's done, owed an opinion as the conclusion makes
    /// it, under a rule naming Codex: the host standing in, the tree, and where the work left the line.
    /// </summary>
    private async Task<(OpinionStandIn Host, string Tree, string Line)> DoneAsync(bool required, string[]? on = null, string[]? reviewers = null)
    {
        (DriverConfig.Empty with
        {
            Adapter = "claude-code-acp",
            Opinions = new Dictionary<string, OpinionRule>(StringComparer.OrdinalIgnoreCase)
            {
                ["reports"] = new OpinionRule(reviewers ?? ["codex-acp"], on ?? [OpinionRules.Landing], required),
            },
        }).Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));

        var root = Path.Combine(_scratch, "reports");
        Directory.CreateDirectory(root);
        await GitAsync(root, "init", "--quiet", "-b", "main");
        await CommitAsync(root, "README.md", "# reports", "first");
        var line = await HeadAsync(root);
        var tree = (await new SessionTrees(_home).OpenAsync(root, "reports", "work")).Path;
        await CommitAsync(tree, "report.ts", "the work", "the work of s1");

        var host = new OpinionStandIn().Session("s1", "completed", tree: tree).Quest("q1");
        Assert.Equal(on ?? [OpinionRules.Landing], OpinionLook.Concluded(_home, Config(), "s1",
            new QuestView("q1", "ask #a1", "reports", "Work q1", "", "Done"), "Done", "completed", tree));
        return (host, tree, line);
    }

    /// <summary>
    /// s1's automatic landing made its branch at <paramref name="tip"/> and held its push back (FABRIC-CLOSE1h2): its landing recorded,
    /// not pushed, and its entry open as <c>push-held</c>.
    /// </summary>
    private void PushHeld(string tree, string tip)
    {
        new LandedBranches(_home).Record(new LandedBranch("reports", "work", "feature/q1-work-q1", "main", tip, "s1", "q1", "Work q1", DateTimeOffset.UtcNow));
        var due = new AutoLandings(_home);
        due.Due(new AutoLanding("s1", "q1", "reports", "work", tree, DateTimeOffset.UtcNow));
        due.Tried("s1", new AutoTry(DateTimeOffset.UtcNow, AutoLandingCode.PushHeld) { Tip = tip, Branch = "feature/q1-work-q1" }, close: false);
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

    /// <summary>The checkout this build runs from: a linked worktree's <c>.git</c> is a file, so it stops there too.</summary>
    private static string RepoRoot() => GitSeed.RepoRoot();
}
