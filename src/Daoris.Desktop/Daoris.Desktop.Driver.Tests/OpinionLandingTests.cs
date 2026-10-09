using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// XAGENT1f (D155 points 9 and 10; the second-agent design §7–§8): the second opinion's gate at every landing door, over real
/// git. The press (<c>LAND_SESSION_TREE</c> and <c>trees land</c> land through <see cref="SessionTrees.LandAsync"/> with the gate
/// the door read once) and the look under <i>Accept automatically</i> each hold a chain's work the level says another agent reads,
/// until an opinion that covers its tip is settled; a commit after it holds again, until the person goes on anyway; a required
/// opinion with no reviewer to be had holds, and one not required rides beside; an earlier step waits for the chain's last step
/// here. What let it go is kept on the landing record.
/// </summary>
/// <remarks>
/// Real git, as the other tree tests, and the service's quests and the local host's opinions stood in: nothing reaches a network
/// or a harness.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class OpinionLandingTests : IDisposable
{
    private readonly string _scratch;
    private readonly string _home;
    private readonly World _world = new();
    private readonly List<LandingLine> _lines = [];

    public OpinionLandingTests()
    {
        _scratch = Path.Combine(RepoRoot(), "_fixtures", "opinion-landing", Guid.NewGuid().ToString("N")[..8]);
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

    /// <summary>
    /// The press, under a branch rule: done and nothing asked, it is refused with the gate's sentence and nothing is made; an
    /// opinion that raised nothing at the tree's tip settles it, and the landing record keeps who read it.
    /// </summary>
    [Fact]
    public async Task The_press_waits_for_its_opinion_and_the_landing_keeps_it_once_settled()
    {
        var root = await RepositoryAsync("web-app");
        Configure(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"), required: false);
        var tree = await DoneSessionAsync(root, "s1", "q1");
        var tip = await HeadAsync(tree);

        var held = await PressAsync(tree);
        Assert.False(held.Landed);
        Assert.Equal(AutoLandingCode.Opinion, held.Refusal);
        Assert.Equal("Waits for a second opinion: it is asked at the driver's next look. `daoris-driver opinion ask s1` asks it now.", held.Message);
        Assert.False(await BranchAsync(root, "feature/q1-fix-the-gap"), "nothing is made while the opinion waits");

        Read("o1", tip, reading: true);
        Assert.StartsWith("Waits for a second opinion: being read by Codex (OpenAI)", (await PressAsync(tree)).Message);

        Read("o1", tip);
        var landed = await PressAsync(tree);
        Assert.True(landed.Landed, landed.Message);
        var record = new LandedBranches(_home).Landing("s1")!.Opinion!;
        Assert.Equal((OpinionGateStates.Settled, "o1", "codex-acp", tip, 1), (record.Said, record.Opinion, record.Reviewer, record.Tip, record.Passes));
    }

    /// <summary>
    /// Bound to its candidate (§8.3): a commit after the opinion's tip is work no other agent read, and the press holds it, saying
    /// how many; the person's <i>Go on anyway…</i> at the new tip lets it land, and the record keeps their words and the commit.
    /// </summary>
    [Fact]
    public async Task A_commit_after_the_opinion_holds_again_until_the_person_goes_on_anyway()
    {
        var root = await RepositoryAsync("web-app");
        Configure(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"), required: false);
        var tree = await DoneSessionAsync(root, "s1", "q1");
        Read("o1", await HeadAsync(tree));

        await CommitAsync(tree, "late.txt", "added after the opinion", "a late change");
        var held = await PressAsync(tree);
        Assert.False(held.Landed);
        Assert.Equal(AutoLandingCode.Opinion, held.Refusal);
        Assert.StartsWith("1 commit since was not read by another agent", held.Message);
        Assert.False(await BranchAsync(root, "feature/q1-fix-the-gap"));

        new OpinionGates(_home).Said("q1", "web-app", new OpinionPersonWord(OpinionPersonSaid.Anyway, await HeadAsync(tree), DateTimeOffset.UtcNow)
        {
            Words = "a comment only", Door = ReviewDoors.Terminal,
        });
        var landed = await PressAsync(tree);
        Assert.True(landed.Landed, landed.Message);
        var record = new LandedBranches(_home).Landing("s1")!.Opinion!;
        Assert.Equal((OpinionGateStates.Anyway, OpinionPersonSaid.Anyway, "a comment only", 1), (record.Said, record.Person, record.Words, record.Unread));
    }

    /// <summary>
    /// Required (§8.4): with no reviewer to be had, the landing holds and says the doors; a merge rule waits as a branch rule does.
    /// Not required, it lands at once and the record says it landed with none, and why.
    /// </summary>
    [Fact]
    public async Task Required_with_no_reviewer_to_be_had_holds_and_not_required_lands_saying_so()
    {
        var root = await RepositoryAsync("web-app");
        Configure(landing: null, required: true);
        var tree = await DoneSessionAsync(root, "s1", "q1");
        var line = await HeadAsync(root);
        Unavailable(await HeadAsync(tree));

        var held = await PressAsync(tree);
        Assert.False(held.Landed);
        Assert.StartsWith("No second opinion: no listed reviewer of another maker is installed. The rule requires one, so the work waits for you:", held.Message);
        Assert.Equal(line, await HeadAsync(root));

        Configure(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"), required: false);
        var landed = await PressAsync(tree);
        Assert.True(landed.Landed, landed.Message);
        Assert.Equal((OpinionGateStates.Unavailable, ReviewerUnavailable.NoReviewer),
            (new LandedBranches(_home).Landing("s1")!.Opinion!.Said, new LandedBranches(_home).Landing("s1")!.Opinion!.Code));
    }

    /// <summary>
    /// <i>Accept automatically</i> (§8.1): the look keeps a due session due as <c>opinion</c>, told once and read again at every look,
    /// logged as <c>opinion.held</c>, and lands it at the first look once the opinion is settled, as the press would.
    /// </summary>
    [Fact]
    public async Task The_look_keeps_it_due_as_opinion_and_lands_it_at_the_first_look_once_settled()
    {
        var root = await RepositoryAsync("web-app");
        Configure(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", AutoAccept: true), required: true);
        var tree = await DoneSessionAsync(root, "s1", "q1", due: true);
        var tip = await HeadAsync(tree);
        Read("o1", tip, reading: true);

        var first = await LookAsync();
        Assert.Contains("Waits for a second opinion: being read by Codex (OpenAI)", Assert.Single(first));
        var entry = new AutoLandings(_home).Of("s1")!;
        Assert.Equal((AutoLandingCode.Opinion, (DateTimeOffset?)null), (entry.Last!.Code, entry.Closed));
        var logged = Assert.Single(_lines, each => each.Event == "opinion.held");
        Assert.Equal((OpinionGateStates.Reading, (object?)true, ReviewDoors.Look),
            (Value(logged, "state"), Value(logged, "required"), Value(logged, "door")));

        // Said once: a look with nothing changed says nothing more, and still lands nothing.
        Assert.Empty(await LookAsync());
        Assert.False(await BranchAsync(root, "feature/q1-fix-the-gap"));

        Read("o1", tip);
        Assert.Single(await LookAsync());
        Assert.True(await BranchAsync(root, "feature/q1-fix-the-gap"));
        Assert.Equal(OpinionGateStates.Settled, new LandedBranches(_home).Landing("s1")!.Opinion!.Said);
    }

    /// <summary>
    /// A chain is read once per repository (§8.1): under <c>landing</c>, a step whose chain has a later step here still to run waits
    /// for it, so one opinion reads the whole work there; once that step is done and its opinion covers both, each lands.
    /// </summary>
    [Fact]
    public async Task An_earlier_step_waits_for_the_chains_last_step_here_whose_opinion_covers_it()
    {
        var root = await RepositoryAsync("web-app");
        Configure(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"), required: false);
        var tree = await DoneSessionAsync(root, "s1", "q1");
        _world.Quests["q2"] = new QuestView("q2", "ask #a1", "web-app", "Fix the rest", "", "Open") { Parent = "q1" };

        var waits = await PressAsync(tree);
        Assert.False(waits.Landed);
        Assert.Contains("quest `#q2` still works there", waits.Message);

        _world.Quests["q2"] = _world.Quests["q2"] with { Status = "Done" };
        await CommitAsync(tree, "rest.txt", "the rest", "the rest of it");
        var opinion = await HeadAsync(tree);
        Read("o1", opinion, working: "s2");
        await ResetAsync(tree, "HEAD~1");

        var landed = await PressAsync(tree);
        Assert.True(landed.Landed, landed.Message);
        Assert.Equal(OpinionGateStates.Settled, new LandedBranches(_home).Landing("s1")!.Opinion!.Said);
    }

    // ——— the world and the fixtures

    /// <summary>The service's quests and records, and the local host's opinions, standing in.</summary>
    private sealed class World : IAutoLandingWorld
    {
        public Dictionary<string, QuestView> Quests { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<SessionRecord> Records { get; } = [];

        public Dictionary<string, OpinionView> Opinions { get; } = new(StringComparer.Ordinal);

        public Task<QuestView?> QuestAsync(string id, CancellationToken ct) => Task.FromResult(Quests.GetValueOrDefault(id));

        public Task<IReadOnlyList<SessionRecord>> RecordsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<SessionRecord>>([.. Records]);

        public Task<IReadOnlySet<string>> InUseAsync(CancellationToken ct) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());

        public Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<QuestView>>([.. Quests.Values]);

        public Task<AskView?> AskAsync(string id, CancellationToken ct) => Task.FromResult<AskView?>(null);

        public Task<OpinionView?> OpinionAsync(string id, CancellationToken ct) => Task.FromResult(Opinions.GetValueOrDefault(id));
    }

    /// <summary>The press, as a door makes it: the whole gate read once for the tree, and handed to the landing.</summary>
    private async Task<TreeLanding> PressAsync(string tree)
    {
        var trees = new SessionTrees(_home);
        var gate = await trees.GateAsync(tree, "q1", _world, "s1");
        return await trees.LandAsync(tree, new LandingSubject("s1", "q1", "Fix the gap"), gate: gate);
    }

    private async Task<IReadOnlyList<string>> LookAsync()
    {
        var lander = new AutoLander(_home, _world, new SessionTrees(_home), new SessionEvents(Path.Combine(_home, "sessions")), _lines.Add);
        var (chosen, said) = await lander.ChooseAsync(Config());
        return [.. said, .. await lander.LandAsync(chosen)];
    }

    /// <summary>A first pass by Codex on the work at <paramref name="tip"/>, kept at the gate as asked, reading or raising nothing.</summary>
    private void Read(string id, string tip, bool reading = false, string working = "s1")
    {
        _world.Opinions[id] = new OpinionView(id, "landing", "first", working, "r-" + id, "web-app", tip, tip, "codex-acp", ReviewerLabels.AnotherMaker,
            reading ? "reading" : "given")
        {
            Product = "Codex", Maker = "OpenAI", Minutes = 20, Findings = reading ? null : [], Read = "the whole change",
        };
        var gates = new OpinionGates(_home);
        if (!gates.Read("q1", "web-app").Asks.Any(ask => ask.Opinion == id))
        {
            gates.Asked("q1", "web-app", new OpinionAskKept(DateTimeOffset.UtcNow, "landing", working, tip) { Opinion = id, Reviewer = "codex-acp" });
        }
    }

    /// <summary>A pass the walk found no reviewer for, kept at the gate as asked.</summary>
    private void Unavailable(string tip) =>
        new OpinionGates(_home).Asked("q1", "web-app", new OpinionAskKept(DateTimeOffset.UtcNow, "landing", "s1", tip) { Code = ReviewerUnavailable.NoReviewer });

    private DriverConfig Config() => DriverConfig.Load(Path.Combine(_home, "driver.json"));

    /// <summary>The landing rule, and an opinion rule for the repository that reads before landing.</summary>
    private void Configure(LandingRule? landing, bool required)
    {
        var config = DriverConfig.Empty with
        {
            Opinions = new Dictionary<string, OpinionRule>(StringComparer.OrdinalIgnoreCase)
            {
                ["web-app"] = new OpinionRule(["codex-acp"], [OpinionRules.Landing], required),
            },
        };
        (landing is null ? config : config.WithLanding("web-app", landing)).Save(Path.Combine(_home, "driver.json"));
    }

    /// <summary>A session that worked in its own tree and concluded on its quest's done; made due as the conclusion makes it, where asked.</summary>
    private async Task<string> DoneSessionAsync(string root, string session, string quest, bool due = false)
    {
        var opened = await new SessionTrees(_home).OpenAsync(root, "web-app", "aurora");
        await CommitAsync(opened.Path, $"{session}.txt", $"the work of {session}", $"the work of {session}");
        _world.Quests[quest] = new QuestView(quest, "ask #a1", "web-app", "Fix the gap", "Fix it.", "Done");
        _world.Records.Add(new SessionRecord(session, "web-app", "completed") { Quest = quest, Tree = opened.Path, Created = DateTimeOffset.UtcNow });
        if (due)
        {
            Assert.Equal(AutoConcluded.Due, AutoLander.Concluded(_home, Config(), new SessionEvents(Path.Combine(_home, "sessions")), session,
                _world.Quests[quest], "Done", "completed", opened.Path, "aurora"));
        }

        return opened.Path;
    }

    private static object? Value(LandingLine line, string key) => line.Data.Single(pair => pair.Key == key).Value;

    private static async Task<bool> BranchAsync(string root, string branch) =>
        (await GitAsync(root, "branch", "--list", branch)).Trim().Length > 0;

    private static async Task<string> HeadAsync(string tree) => (await GitAsync(tree, "rev-parse", "HEAD")).Trim();

    private static Task ResetAsync(string tree, string to) => GitAsync(tree, "reset", "--hard", "--quiet", to);

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
        // Both streams read at once, before the wait, so a git that writes a pipe's worth to one never blocks on it.
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
