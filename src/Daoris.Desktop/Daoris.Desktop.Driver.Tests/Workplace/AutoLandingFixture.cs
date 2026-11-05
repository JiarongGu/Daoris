using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The fixture the automatic landing's tests share (PROCSPLIT1): one scratch repository home, a stand-in for the service's quests and
/// records, and a plugin faked on the wire's channel, built per test instance. It was the whole of one class, which xUnit ran one test
/// after another; the classes that derive from it hold the tests by concern, so the git half's runner runs them beside each other.
/// </summary>
/// <remarks>
/// Nothing here is shared between instances: the scratch folder carries a fresh id, and <c>LandingHolds</c>' gates are keyed by the
/// home inside it, so two classes running together never meet at a hold or a folder.
/// </remarks>
public abstract class AutoLandingFixture : IDisposable
{
    protected const string Plugin = "example.lands";

    protected readonly string _scratch;
    protected readonly string _home;
    protected readonly World _world = new();
    protected readonly List<LandingLine> _lines = [];

    protected AutoLandingFixture()
    {
        _scratch = Path.Combine(RepoRoot(), "_fixtures", "auto-landing", Guid.NewGuid().ToString("N")[..8]);
        _home = Path.Combine(_scratch, "daoris-home");
        Directory.CreateDirectory(_home);
    }

    public void Dispose()
    {
        GitSeed.Discard(_scratch);
    }

    /// <summary>
    /// A named workflow `docs-to-pr` saved here with one version and chosen as `engine`'s default (or, with a kind, the workspace's for
    /// it), and run q1 bound to it as its first start binds it (WORKFLOW1e), over the rule <see cref="Rule"/> wrote.
    /// </summary>
    protected void Follow(string steps, string? kind = null)
    {
        StoredSteps.Add(_home, new WorkflowVersionAdded(
            "docs-to-pr", "Documentation to a pull request", "terminal", "2026-10-09T09:00:00Z", System.Text.Json.Nodes.JsonNode.Parse(steps)));
        var use = kind is null
            ? new WorkflowUse("docs-to-pr", "engine", null, null)
            : new WorkflowUse("docs-to-pr", null, "aurora", kind);
        WorkflowSelection.Apply(Config(), use).Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        var task = kind is null ? null : new WorkflowTaskChoice(kind, null);
        Assert.True(WorkflowRunBindings.Bind(_home, WorkflowRunBindings.Plan(Config(), _home, "q1", "engine", "aurora", null, task, [], DateTimeOffset.UtcNow)));
    }

    /// <summary>A done session in its own tree, whose run follows a saved workflow as its conclusion reads it.</summary>
    protected async Task<string> NamedDoneAsync(string root, string session, string quest)
    {
        var opened = await new SessionTrees(_home).OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, $"{session}.txt", $"the work of {session}", $"the work of {session}");
        _world.Quests[quest] = new QuestView(quest, "game", "engine", "Fix the gap", "Fix it.", "Done");
        _world.Records.Add(new SessionRecord(session, "engine", "completed") { Quest = quest, Tree = opened.Path, Created = DateTimeOffset.UtcNow });
        var process = await WorkflowProcesses.ConcludedAsync(_home, Config(), _world, _world.Quests[quest], opened.Path, "aurora");
        Assert.True(process!.Named);
        return opened.Path;
    }

    /// <summary>The quest a session serves, as the service answers it from its records (<c>ServiceClient.SessionQuestAsync</c>).</summary>
    protected Task<string?> SessionQuestAsync(string id, CancellationToken ct) =>
        Task.FromResult(_world.Records.FirstOrDefault(record => string.Equals(record.Id, id, StringComparison.OrdinalIgnoreCase))?.Quest);

    /// <summary>
    /// The person's press as its doors make it (FABRIC-CLOSE1h3): the quest read where the door names its landing, then the landing
    /// judged again under its holds on that read.
    /// </summary>
    protected async Task<TreeLanding> PressAsync(string tree, string? quest = "q1")
    {
        var reads = new PressQuests(_world.QuestAsync);
        if (quest is not null) await reads.FindAsync(quest);
        var pressed = await reads.PressedAsync("s1", quest);
        return await new SessionTrees(_home).LandAsync(tree, new LandingSubject("s1", quest, "Fix the gap"), judged: pressed?.Judged);
    }

    protected const string PushBranch = "feature/q1-fix-the-gap";

    protected static async Task<IReadOnlyList<string>> FeatureBranchesAsync(string root) =>
        (await GitAsync(root, "for-each-ref", "--format=%(refname)", "refs/heads/feature/"))
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private const string Graph = "sha256:closure";

    /// <summary>A submission made on this machine at <paramref name="sequence"/> (FABRIC-CLOSE1a).</summary>
    protected static QuestSubmissionView Made(long sequence) => new("desk", sequence, DateTimeOffset.UtcNow, "ready");

    /// <summary>The agent moves its tree onto a branch of its own and commits its work there (TREEFOLLOW1, D164 point 4).</summary>
    protected static async Task OwnBranchAsync(string tree)
    {
        await GitAsync(tree, "checkout", "--quiet", "-b", "feature/own");
        await CommitAsync(tree, "s1.txt", "the work of s1", "the work of s1");
    }

    // ——— the world, the plugin and the fixtures

    /// <summary>The service's quests and records, standing in.</summary>
    protected sealed class World : IAutoLandingWorld
    {
        public Action? ReadingQuests { get; set; }
        public Dictionary<string, QuestView> Quests { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<SessionRecord> Records { get; } = [];

        public Task<QuestView?> QuestAsync(string id, CancellationToken ct) => Task.FromResult(Quests.GetValueOrDefault(id));

        public Task<IReadOnlyList<SessionRecord>> RecordsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<SessionRecord>>([.. Records]);

        public Task<IReadOnlySet<string>> InUseAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlySet<string>>(Records.Where(r => r.Live && r.Tree is not null).Select(r => r.Tree!).ToHashSet());

        // The review's gate reads the chain and its ask (REVIEWENV1c): no rule names a review here, so these decide nothing.
        public Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct)
        {
            ReadingQuests?.Invoke();
            return Task.FromResult<IReadOnlyList<QuestView>>([.. Quests.Values]);
        }

        public Task<AskView?> AskAsync(string id, CancellationToken ct) => Task.FromResult<AskView?>(null);

        // The second opinion's gate reads the host's opinions (XAGENT1f): none, unless a test settles one (FABRIC-CLOSE1h15).
        public Dictionary<string, OpinionView> Opinions { get; } = new(StringComparer.Ordinal);

        public Task<OpinionView?> OpinionAsync(string id, CancellationToken ct) => Task.FromResult(Opinions.GetValueOrDefault(id));

        /// <summary>What Daoris said to a session through the say door (LANDREBASE1), each kept as the door keeps it.</summary>
        public List<(string Session, string Text, string Key)> Said { get; } = [];

        public Task<SayAnswer?> SayAsync(string session, string text, string key, CancellationToken ct)
        {
            Said.Add((session, text, key));
            return Task.FromResult<SayAnswer?>(new SayAnswer(true, "kept for the session to go on with."));
        }
    }

    protected static JsonElement Json(object frame) => JsonDocument.Parse(JsonSerializer.Serialize(frame)).RootElement.Clone();

    protected DriverConfig Config() => DriverConfig.Load(DaorisHome.Entry(_home, HomeEntry.DriverConfig));

    /// <summary>A session that worked in its own tree and concluded on its quest's done, made due as the driver's conclusion makes it.</summary>
    /// <param name="parent">The quest it follows, where it is a chain's later step.</param>
    /// <param name="from">The session branch its tree grows from, as a chain's step grows (CHAIN2).</param>
    /// <param name="entry">The entry its record names (SESSONE1b), shared by an entry's parts; none is its own line of work.</param>
    /// <param name="submitted">Bound to a closure graph and at its submission, taken, rather than done (FABRIC-CLOSE1h, §E5).</param>
    protected async Task<string> DoneSessionAsync(
        string root, string session, string quest, bool held = false, bool commit = true, string? parent = null, string? from = null,
        string? entry = null, bool submitted = false)
    {
        var opened = await new SessionTrees(_home).OpenAsync(root, "engine", "aurora", from: from);
        if (commit) await CommitAsync(opened.Path, $"{session}.txt", $"the work of {session}", $"the work of {session}");
        var view = new QuestView(quest, "game", "engine", "Fix the gap", "Fix it.", submitted ? "Taken" : "Done") { Held = held, Parent = parent };
        // FABRIC-CLOSE1h: a closure graph's quest stays taken at its agent's `ready`, its submission current (§E5).
        _world.Quests[quest] = submitted ? view with { Closure = Graph, Submission = Made(1) } : view;
        _world.Records.Add(new SessionRecord(session, "engine", "completed")
        {
            Quest = quest, Tree = opened.Path, Created = DateTimeOffset.UtcNow, Entry = entry,
        });
        return opened.Path;
    }

    /// <summary>What a session in this repository's tree would be told of its landing, as a start composes it.</summary>
    protected string Instruction(string root)
    {
        var trees = new SessionTrees(_home);
        var tree = trees.OpenAsync(root, "engine", "aurora").GetAwaiter().GetResult();
        var plan = trees.PlanAsync(tree.Path, new LandingSubject("s9", "q9", "Fix the gap")).GetAwaiter().GetResult();
        var quest = new QuestView("q9", "game", "engine", "Fix the gap", "Fix it.", "Open");
        return TargetPrompt.Compose(SessionTarget.ForQuest(quest, tree.Path, "http://stand-in") with { LandsOn = plan });
    }

    protected IReadOnlyList<SessionEvent> Notes(string session) =>
        [.. new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)).After(session, 0).Events.Where(e => e.Kind == SessionEventKind.Note)];

    protected static object? Value(LandingLine line, string key) => line.Data.Single(pair => pair.Key == key).Value;

    /// <summary>
    /// The repository's rule reads a second opinion before landing from now on (FABRIC-CLOSE1h15, h17), which nobody has asked yet: made
    /// after a done, it is a rule that started reading after it, so the done owed none.
    /// </summary>
    protected void ReadsAnOpinion() =>
        (Config() with
        {
            Opinions = new Dictionary<string, OpinionRule>(StringComparer.OrdinalIgnoreCase)
            {
                ["engine"] = new OpinionRule(["codex-acp"], [OpinionRules.Landing], true),
            },
        }).Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));

    /// <summary>The reviewer read s1's work at <paramref name="tip"/> and raised nothing: the opinion, kept at the gate as asked, settles.</summary>
    protected void Settled(string id, string tip)
    {
        _world.Opinions[id] = new OpinionView(id, OpinionRules.Landing, "first", "s1", "r-" + id, "engine", tip, tip, "codex-acp",
            ReviewerLabels.AnotherMaker, "given")
        {
            Product = "Codex", Maker = "OpenAI", Minutes = 20, Findings = [], Read = "the whole change",
        };
        new OpinionGates(_home).Asked("q1", "engine", new OpinionAskKept(DateTimeOffset.UtcNow, OpinionRules.Landing, "s1", tip)
        {
            Opinion = id, Reviewer = "codex-acp",
        });
    }

    /// <summary>Why the loop's opinion look would close <paramref name="due"/> now, as it reads it (FABRIC-CLOSE1h15), or null where it is still owed.</summary>
    protected Task<string?> ClosesAsync(OpinionDue due) =>
        OpinionLook.ClosesAsync(due, _world.Quests[due.Quest], [.. _world.Quests.Values], Config(), new LandedBranches(_home), CancellationToken.None);

    protected static async Task<string> HeadAsync(string tree) => (await GitAsync(tree, "rev-parse", "HEAD")).Trim();

    protected static async Task<bool> BranchAsync(string root, string branch) =>
        (await GitAsync(root, "branch", "--list", branch)).Trim().Length > 0;

    /// <summary>
    /// A repository on <c>main</c> with one commit, its README: a copy of the run's plain seed (<see cref="GitSeed.PlainAsync"/>,
    /// PROCSPEED1b), five git starts a test fewer. No test here reads the first commit's id or time.
    /// </summary>
    protected Task<string> RepositoryAsync(string name) => GitSeed.PlainAsync(Path.Combine(_scratch, name), name);

    protected static async Task CommitAsync(string tree, string file, string content, string message)
    {
        await File.WriteAllTextAsync(Path.Combine(tree, file), content + "\n");
        await GitAsync(tree, "add", "-A");
        await GitAsync(tree, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", message);
    }

    protected static async Task<string> GitAsync(string cwd, params string[] arguments)
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
        // 🔴 Both streams are read at once, before the wait: read one after the other, a git that writes more than a pipe's
        // worth of warnings to the second blocks on it while the first is still being read (FIX-LOG 2026-10-04).
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await Task.WhenAll(stdout, stderr);
        await process.WaitForExitAsync();
        return await stdout;
    }

    /// <summary>The checkout this build runs from: a linked worktree's <c>.git</c> is a file, so it stops there too.</summary>
    private static string RepoRoot() => GitSeed.RepoRoot();
}
