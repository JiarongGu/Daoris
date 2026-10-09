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
        try
        {
            Directory.Delete(_scratch, recursive: true);
        }
        catch (IOException) { /* a straggling git handle */ }
        catch (UnauthorizedAccessException) { /* read-only pack files under .git */ }
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
        Assert.StartsWith("set-up step `#q2` waits for a second opinion on the work it shows", sits["q2"]);
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

        var stop = await presses.StopAsync("nothing1", new SessionProcesses(Path.Combine(_home, "sessions")));
        Assert.Equal((false, "there is no second opinion `nothing1` on this machine."), (stop.Done, stop.Message));
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
                Product: real.Product, Maker: real.Maker));
        }

        var set = new AdapterSet(adapters);
        return new Daoris.Driver.Driver(host.Client(), Config(), set, _home, harnesses: new HarnessRoster(set, Path.Combine(_home, "harnesses.json")))
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

    private DriverConfig Config() => DriverConfig.Load(Path.Combine(_home, "driver.json"));

    /// <summary>
    /// A session that worked in its own tree on `reports` and concluded on its quest's done, owed an opinion as the conclusion makes
    /// it, under a rule naming Codex: the host standing in, the tree, and where the work left the line.
    /// </summary>
    private async Task<(OpinionStandIn Host, string Tree, string Line)> DoneAsync(bool required)
    {
        (DriverConfig.Empty with
        {
            Adapter = "claude-code-acp",
            Opinions = new Dictionary<string, OpinionRule>(StringComparer.OrdinalIgnoreCase)
            {
                ["reports"] = new OpinionRule(["codex-acp"], [OpinionRules.Landing], required),
            },
        }).Save(Path.Combine(_home, "driver.json"));

        var root = Path.Combine(_scratch, "reports");
        Directory.CreateDirectory(root);
        await GitAsync(root, "init", "--quiet", "-b", "main");
        await CommitAsync(root, "README.md", "# reports", "first");
        var line = await HeadAsync(root);
        var tree = (await new SessionTrees(_home).OpenAsync(root, "reports", "work")).Path;
        await CommitAsync(tree, "report.ts", "the work", "the work of s1");

        var host = new OpinionStandIn().Session("s1", "completed", tree: tree).Quest("q1");
        Assert.Equal([OpinionRules.Landing], OpinionLook.Concluded(_home, Config(), "s1",
            new QuestView("q1", "ask #a1", "reports", "Work q1", "", "Done"), "Done", "completed", tree));
        return (host, tree, line);
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
        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await Task.WhenAll(stdout, stderr);
        await process.WaitForExitAsync();
        return await stdout;
    }

    /// <summary>The checkout this build runs from: a linked worktree's <c>.git</c> is a file, so it stops there too.</summary>
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
